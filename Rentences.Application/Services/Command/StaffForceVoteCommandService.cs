using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Rentences.Application;
using Rentences.Application.Services;
using Rentences.Application.Services.Game;
using Rentences.Domain.Definitions.Game;

public class StaffForceVoteCommandService : ICommandService
{
    private readonly IGameService _gameService;
    private readonly IInterop _interop;

    // Per-channel active vote tracking to prevent concurrent votes in the same channel
    private static readonly ConcurrentDictionary<ulong, ActiveForceVote> ActiveVotes = new();

    // Fixed vote duration (seconds) unless there is a shared constant elsewhere
    private static readonly TimeSpan VoteDuration = TimeSpan.FromSeconds(30);

    // Fixed emoji mappings for options
    private const string CasualEmoji = "1️⃣";
    private const string LettersEmoji = "2️⃣";
    private const string ReverseEmoji = "3️⃣";

    public StaffForceVoteCommandService(IGameService gameService, IInterop interop)
    {
        _gameService = gameService;
        _interop = interop;
    }

    public string CommandName => "-forcevote";

    public async Task ProcessCommandAsync(string[] args, SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage)
        {
            return;
        }

        var socketGuildUser = message.Author as SocketGuildUser;
        if (socketGuildUser == null)
        {
            await message.Channel.SendMessageAsync("This command can only be used in a server.", allowedMentions: AllowedMentions.None);
            return;
        }

        var config = _interop.GetDiscordConfiguration();
        if (!ulong.TryParse(config.StaffRoleId, out var staffRoleId))
        {
            await message.Channel.SendMessageAsync("Staff role ID is not properly configured.", allowedMentions: AllowedMentions.None);
            return;
        }

        var hasStaffRole = socketGuildUser.Roles.Any(role => role.Id == staffRoleId);
        if (!hasStaffRole)
        {
            var accessDeniedEmbed = new EmbedBuilder()
                .WithTitle("❌ Access Denied")
                .WithDescription("This command is restricted to staff members only.")
                .WithColor(Color.Red)
                .Build();

            await message.Channel.SendMessageAsync(embed: accessDeniedEmbed, allowedMentions: AllowedMentions.None);
            return;
        }

        // Parse arguments:
        // - zero or more gamemode identifiers: "casual", "letters", "reverse"
        // - optional "--message" flag; capture the rest of the text after it as the custom message
        var (gamemodes, customMessage) = ParseArguments(args);

        // If no valid gamemodes specified, use default [Casual, Letters, Reverse]
        if (gamemodes.Count == 0)
        {
            gamemodes.Add(Gamemodes.GAMEMODE_CASUAL);
            gamemodes.Add(Gamemodes.GAMEMODE_LETTER_VOTE);
            gamemodes.Add(Gamemodes.GAMEMODE_REVERSE_SENTENCE);
        }

        // If after parsing we somehow have no options, abort
        if (gamemodes.Count == 0)
        {
            var noOptionsEmbed = new EmbedBuilder()
                .WithTitle("❌ Invalid Options")
                .WithDescription("No valid gamemode options were provided for the force vote.")
                .WithColor(Color.Red)
                .Build();

            await message.Channel.SendMessageAsync(embed: noOptionsEmbed, allowedMentions: AllowedMentions.None);
            return;
        }

        var channelId = message.Channel.Id;

        // Check for existing active vote in this channel
        if (!ActiveVotes.TryAdd(channelId, new ActiveForceVote()))
        {
            var activeVoteEmbed = new EmbedBuilder()
                .WithTitle("❌ Force Vote Already Running")
                .WithDescription("A staff force vote is already active in this channel. Please wait for it to complete before starting another.")
                .WithColor(Color.Red)
                .Build();

            await message.Channel.SendMessageAsync(embed: activeVoteEmbed, allowedMentions: AllowedMentions.None);
            return;
        }

        // Ensure we clear the slot if anything fails before async flow is scheduled
        var voteRegistered = false;
        try
        {
            if (gamemodes.Count == 1)
            {
                // Single-option: immediately apply without reactions or timers
                var selected = gamemodes[0];

                var startingEmbed = new EmbedBuilder()
                    .WithTitle("🗳️ Staff Force Vote Result (Single Option)")
                    .WithDescription($"Staff selected a single gamemode option. Applying **{FormatGamemodeName(selected)}** as the active gamemode.\nAny existing game may be terminated.")
                    .WithColor(Color.Blue)
                    .Build();

                await message.Channel.SendMessageAsync(embed: startingEmbed, allowedMentions: AllowedMentions.None);

                try
                {
                    await _gameService.StartGameWithForceTerminationAsync(selected, "Staff force vote single-option");

                    var successEmbed = new EmbedBuilder()
                        .WithTitle("🎮 Force Vote Applied")
                        .WithDescription($"The game has been started with gamemode **{FormatGamemodeName(selected)}**. Any existing game has been terminated.")
                        .WithColor(Color.Green)
                        .Build();

                    await message.Channel.SendMessageAsync(embed: successEmbed, allowedMentions: AllowedMentions.None);
                }
                catch (Exception ex)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Game Error")
                        .WithDescription($"Failed to start the selected gamemode: {ex.Message}")
                        .WithColor(Color.Red)
                        .Build();

                    await message.Channel.SendMessageAsync(embed: errorEmbed, allowedMentions: AllowedMentions.None);
                }

                // Single-option path: no ongoing vote, clear immediately
                ActiveVotes.TryRemove(channelId, out _);
                return;
            }

            // Multi-option vote
            var optionEmojiMap = BuildEmojiMap(gamemodes);
            var descriptionLines = new List<string>
            {
                "A staff-initiated force vote has started. React below to choose the next gamemode.",
                "",
                string.Join("\n", optionEmojiMap.Select(kvp => $"{kvp.Value.Emoji} - **{FormatGamemodeName(kvp.Key)}**")),
                "",
                $"Vote duration: **{VoteDuration.TotalSeconds} seconds**.",
                "When the vote completes, the winning gamemode will be started and any existing game may be terminated."
            };

            if (!string.IsNullOrWhiteSpace(customMessage))
            {
                descriptionLines.Add("");
                descriptionLines.Add($"Staff note: {customMessage}");
            }

            var startEmbed = new EmbedBuilder()
                .WithTitle("🗳️ Force Gamemode Vote Started")
                .WithDescription(string.Join("\n", descriptionLines))
                .WithColor(Color.Blue)
                .Build();

            var voteMessage = await message.Channel.SendMessageAsync(embed: startEmbed, allowedMentions: AllowedMentions.None);

            // Add reactions for each option
            foreach (var (_, value) in optionEmojiMap)
            {
                await voteMessage.AddReactionAsync(new Emoji(value.Emoji));
            }

            // Track the active vote details
            var activeVote = new ActiveForceVote
            {
                MessageId = voteMessage.Id,
                ChannelId = voteMessage.Channel.Id,
                Options = optionEmojiMap.Keys.ToList(),
                EmojiMap = optionEmojiMap
            };

            ActiveVotes[channelId] = activeVote;
            voteRegistered = true;

            // Fire-and-forget vote resolution
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(VoteDuration);

                    // Re-fetch the message to get updated reactions
                    if (voteMessage.Channel is not SocketTextChannel textChannel)
                    {
                        return;
                    }

                    var fetchedMessage = await textChannel.GetMessageAsync(voteMessage.Id) as IUserMessage;
                    if (fetchedMessage == null)
                    {
                        return;
                    }

                    // Tally votes (exclude bot reactions)
                    var voteCounts = new Dictionary<Gamemodes, int>();
                    foreach (var option in activeVote.Options)
                    {
                        voteCounts[option] = 0;
                    }

                    foreach (var reaction in fetchedMessage.Reactions)
                    {
                        var emoji = reaction.Key as Emoji;
                        if (emoji == null)
                        {
                            continue;
                        }

                        var matchedOption = activeVote.EmojiMap
                            .FirstOrDefault(kvp => kvp.Value.Emoji == emoji.Name);

                        if (matchedOption.Value == null)
                        {
                            continue;
                        }

                        // Subtract 1 to account for the bot's own reaction (it added each one)
                        var adjustedCount = Math.Max(0, reaction.Value.ReactionCount - 1);
                        if (adjustedCount > 0)
                        {
                            voteCounts[matchedOption.Key] += adjustedCount;
                        }
                    }

                    // Determine winner
                    var maxVotes = voteCounts.Values.DefaultIfEmpty(0).Max();
                    if (maxVotes <= 0)
                    {
                        var noVotesEmbed = new EmbedBuilder()
                            .WithTitle("🗳️ No Votes Cast")
                            .WithDescription("The force vote ended with no valid votes. The current game will remain unchanged.")
                            .WithColor(Color.Orange)
                            .Build();

                        await fetchedMessage.Channel.SendMessageAsync(embed: noVotesEmbed, allowedMentions: AllowedMentions.None);
                        return;
                    }

                    // Tie-breaking: first in the options list with the max vote count
                    var winner = activeVote.Options
                        .First(option => voteCounts.TryGetValue(option, out var count) && count == maxVotes);

                    try
                    {
                        await _gameService.StartGameWithForceTerminationAsync(winner, "Staff force vote result");

                        var successEmbed = new EmbedBuilder()
                            .WithTitle("🎮 Force Vote Complete")
                            .WithDescription($"The winning gamemode is **{FormatGamemodeName(winner)}**.\nThe game has been started with this mode. Any existing game has been terminated.")
                            .WithColor(Color.Green)
                            .Build();

                        await fetchedMessage.Channel.SendMessageAsync(embed: successEmbed, allowedMentions: AllowedMentions.None);
                    }
                    catch (Exception ex)
                    {
                        var errorEmbed = new EmbedBuilder()
                            .WithTitle("❌ Game Error")
                            .WithDescription($"The vote completed, but the game could not be started: {ex.Message}")
                            .WithColor(Color.Red)
                            .Build();

                        await fetchedMessage.Channel.SendMessageAsync(embed: errorEmbed, allowedMentions: AllowedMentions.None);
                    }
                }
                finally
                {
                    // Always clear active vote entry
                    ActiveVotes.TryRemove(channelId, out _);
                }
            });
        }
        catch
        {
            if (!voteRegistered)
            {
                ActiveVotes.TryRemove(channelId, out _);
            }

            throw;
        }
    }

    private static (List<Gamemodes> Gamemodes, string CustomMessage) ParseArguments(string[] args)
    {
        var gamemodes = new List<Gamemodes>();
        string? customMessage = null;

        if (args == null || args.Length == 0)
        {
            return (gamemodes, customMessage ?? string.Empty);
        }

        var tokens = args.ToList();
        var messageIndex = tokens.FindIndex(t => string.Equals(t, "--message", StringComparison.OrdinalIgnoreCase));

        if (messageIndex >= 0)
        {
            if (messageIndex + 1 < tokens.Count)
            {
                customMessage = string.Join(" ", tokens.Skip(messageIndex + 1));
            }

            tokens = tokens.Take(messageIndex).ToList();
        }

        foreach (var token in tokens)
        {
            var lower = token.ToLowerInvariant();
            switch (lower)
            {
                case "casual":
                    if (!gamemodes.Contains(Gamemodes.GAMEMODE_CASUAL))
                    {
                        gamemodes.Add(Gamemodes.GAMEMODE_CASUAL);
                    }
                    break;
                case "letters":
                    if (!gamemodes.Contains(Gamemodes.GAMEMODE_LETTER_VOTE))
                    {
                        gamemodes.Add(Gamemodes.GAMEMODE_LETTER_VOTE);
                    }
                    break;
                case "reverse":
                    if (!gamemodes.Contains(Gamemodes.GAMEMODE_REVERSE_SENTENCE))
                    {
                        gamemodes.Add(Gamemodes.GAMEMODE_REVERSE_SENTENCE);
                    }
                    break;
                default:
                    // Ignore invalid tokens
                    break;
            }
        }

        return (gamemodes, customMessage ?? string.Empty);
    }

    private static string FormatGamemodeName(Gamemodes gamemode)
    {
        return gamemode switch
        {
            Gamemodes.GAMEMODE_CASUAL => "Casual",
            Gamemodes.GAMEMODE_LETTER_VOTE => "Letters",
            Gamemodes.GAMEMODE_REVERSE_SENTENCE => "Reverse Sentence",
            _ => gamemode.ToString()
        };
    }

    private static Dictionary<Gamemodes, EmojiOption> BuildEmojiMap(List<Gamemodes> options)
    {
        var map = new Dictionary<Gamemodes, EmojiOption>();

        foreach (var option in options)
        {
            if (option == Gamemodes.GAMEMODE_CASUAL)
            {
                map[option] = new EmojiOption(CasualEmoji);
            }
            else if (option == Gamemodes.GAMEMODE_LETTER_VOTE)
            {
                map[option] = new EmojiOption(LettersEmoji);
            }
            else if (option == Gamemodes.GAMEMODE_REVERSE_SENTENCE)
            {
                map[option] = new EmojiOption(ReverseEmoji);
            }
        }

        return map;
    }

    private class ActiveForceVote
    {
        public ulong MessageId { get; set; }
        public ulong ChannelId { get; set; }
        public List<Gamemodes> Options { get; set; } = new();
        public Dictionary<Gamemodes, EmojiOption> EmojiMap { get; set; } = new();
    }

    private class EmojiOption
    {
        public EmojiOption(string emoji)
        {
            Emoji = emoji;
        }

        public string Emoji { get; }
    }
}