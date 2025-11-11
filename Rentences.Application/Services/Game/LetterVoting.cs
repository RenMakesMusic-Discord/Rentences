using System;
using Discord;
using Discord.WebSocket;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Rentences.Domain.Definitions;
using Rentences.Domain.Definitions.Game;
using Rentences.Domain.Contracts;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Rentences.Application.Services.Game
{
    public class LetterVoting : IGamemodeHandler
    {
        private readonly ILogger _logger;
        private readonly IInterop _backend;
        private readonly DiscordConfiguration _config;
        private readonly WordService _wordService;
        private readonly Lazy<IGameService> _gameService;
        private readonly IMediator _mediator;
        private ulong LastSenderId;
        private GameStatus gameStatus;
        
        // Letter voting state
        private char? selectedLetter = null;
        private bool mustContainLetter = true; // true = must contain, false = cannot contain
        private HashSet<char> availableLetters = new HashSet<char>();
        
        // Voting phase state
        private bool isVotingTimeoutCancelled = false;
        private Dictionary<char, int> voteCounts = new Dictionary<char, int>();
        private List<char> votingLetters = new List<char>();
        private CancellationTokenSource votingCancellationTokenSource;
        private ulong votingMessageId;
        private ulong gameChannelId;
        
        // Letter to emoji mapping for A-Z
        private static readonly Dictionary<char, string> LetterEmojis = new Dictionary<char, string>
        {
            {'A', "🇦"}, {'B', "🇧"}, {'C', "🇨"}, {'D', "🇩"}, {'E', "🇪"}, {'F', "🇫"}, {'G', "🇬"}, {'H', "🇭"}, {'I', "🇮"}, {'J', "🇯"},
            {'K', "🇰"}, {'L', "🇱"}, {'M', "🇲"}, {'N', "🇳"}, {'O', "🇴"}, {'P', "🇵"}, {'Q', "🇶"}, {'R', "🇷"}, {'S', "🇸"}, {'T', "🇹"},
            {'U', "🇺"}, {'V', "🇻"}, {'W', "🇼"}, {'X', "🇽"}, {'Y', "🇾"}, {'Z', "🇿"}
        };

        public LetterVoting(
            ILogger<LetterVoting> logger,
            IInterop Backend,
            DiscordConfiguration configuration,
            WordService wordService,
            Lazy<IGameService> gameService,
            IMediator mediator)
        {
            _logger = logger;
            _backend = Backend;
            _config = configuration;
            _wordService = wordService;
            _gameService = gameService;
            _mediator = mediator;
            WordList = new List<Word>();
            votingCancellationTokenSource = new CancellationTokenSource();

            // Always resolve the gameChannelId from the strongly-typed DiscordConfiguration.
            // This uses the same single source of truth as all other Discord message flows.
            if (string.IsNullOrWhiteSpace(configuration.ChannelId))
            {
                throw new InvalidOperationException("Discord ChannelId is not configured. Ensure 'DiscordConfiguration:ChannelId' is set in appsettings.json.");
            }

            if (!ulong.TryParse(configuration.ChannelId, out gameChannelId))
            {
                throw new InvalidOperationException($"Discord ChannelId '{configuration.ChannelId}' is invalid. Ensure 'DiscordConfiguration:ChannelId' is a valid ulong.");
            }

            // Initialize shared game state
            GameState = new GameState
            {
                GameId = Guid.NewGuid(),
                CurrentState = GameStatus.VOTING
            };

            // Initialize available letters (A-Z, excluding Q, X, Z for simplicity)
            for (char c = 'A'; c <= 'Z'; c++)
            {
                if (!"QZX".Contains(c))
                {
                    availableLetters.Add(c);
                }
            }
        }

        public GameState GameState { get; set; }
        public List<Word> WordList { get; set; }

        public async Task<ErrorOr<bool>> AddMessage(SocketMessage message)
        {
            // Only allow messages during the main game phase, not during voting
            if (gameStatus != GameStatus.IN_PROGRESS || GameState.CurrentState != GameStatus.IN_PROGRESS)
            {
                await FailMessage(message);
                return Error.Validation("Game Status", "Game is not currently accepting words.");
            }

            var previousWord = WordList.OrderByDescending(w => w.TimeStamp).LastOrDefault();

            Word w = new Word().CreateWord(message.Id, message.Content, message.Author.Id, message.Timestamp);
            if (w == null || WordList.Count < 3 && Word.ContainsValidTermination(w) || LastSenderId == message.Author.Id || Word.ContainsValidTermination(previousWord))
            {
                await FailMessage(message);
                return Error.Validation(CustomErrorValues.WordValidationTitle.Title, CustomErrorValues.WordValidationTitle.Description);
            }

            // Check letter constraint if selected
            if (selectedLetter.HasValue)
            {
                char letter = char.ToUpperInvariant(selectedLetter.Value);
                bool wordContainsLetter = w.Value.ToUpperInvariant().Contains(letter);
                
                if (mustContainLetter && !wordContainsLetter)
                {
                    await _backend.SendGameMessageReaction(new() { socketMessage = message, emoji = _config.LoseEmoji });
                    return Error.Validation("Letter Constraint", $"Word must contain the letter '{letter}'");
                }
                
                if (!mustContainLetter && wordContainsLetter)
                {
                    await _backend.SendGameMessageReaction(new() { socketMessage = message, emoji = _config.LoseEmoji });
                    return Error.Validation("Letter Constraint", $"Word cannot contain the letter '{letter}'");
                }
            }

            await _backend.SendGameMessageReaction(new() { socketMessage = message, emoji = _config.CorrectEmoji });
            await AddWord(w);
            if (Word.ContainsValidTermination(w))
            {
                gameStatus = GameStatus.ENDED;
                GameState = new GameState
                {
                    GameId = GameState.GameId,
                    CurrentState = GameStatus.ENDED
                };
                await _backend.SendGameMessageReaction(new() { socketMessage = message, emoji = _config.WinEmoji });
                await EndGame();
            }

            return true;
        }

        private async Task AddWord(Word word)
        {
            WordList.Add(word);
            LastSenderId = word.Author;
            await _wordService.TrackWordUsageAsync(word);
            await _wordService.UpdateUserStatisticsAsync(word.Author);
        }

        public async Task FailMessage(SocketMessage message)
        {
            await _backend.SendGameMessageReaction(new() { socketMessage = message, emoji = _config.LoseEmoji });
        }

        public async Task<ErrorOr<bool>> DeleteMessage(ulong messageId)
        {
            Word removal = WordList.FirstOrDefault(a => a.MessageId == messageId);
            if (removal == null)
            {
                return Error.Validation(CustomErrorValues.WordValidationTitle.Title, CustomErrorValues.WordValidationTitle.Description);
            }

            WordList.Remove(removal);
            return await Task.FromResult(true);
        }

        public async Task EndGame()
        {
            try
            {
                // Ensure terminal state exactly once; GameService drives what happens next.
                if (gameStatus == GameStatus.ENDED && GameState.CurrentState == GameStatus.ENDED)
                {
                    _logger.LogDebug("LetterVoting.EndGame called but game already marked ENDED; skipping duplicate processing.");
                    return;
                }

                gameStatus = GameStatus.ENDED;
                GameState = new GameState
                {
                    GameId = GameState.GameId == Guid.Empty ? Guid.NewGuid() : GameState.GameId,
                    CurrentState = GameStatus.ENDED
                };

                string generatedMessage = "";
                const string preMessage = "The players have constructed the following sentence:";
                string userTags = "";
                var authors = new List<ulong>();
                LastSenderId = ulong.MinValue;

                generatedMessage = string.Empty;
                userTags = ">>> ";

                foreach (var word in WordList.OrderBy(w => w.TimeStamp))
                {
                    generatedMessage += " " + word.Value;

                    if (authors.Contains(word.Author))
                        continue;

                    authors.Add(word.Author);

                    var topWord = _wordService.GetTopWordsByUser(word.Author, 1)?.ToList();
                    var totalContributions = await _wordService.GetTotalWordsAddedByUserAsync(word.Author);

                    string topWordInfo = topWord != null && topWord.Any()
                        ? $"**<@{word.Author}>** [ *Top word: {topWord.First().Value}* | *Total contributions: {totalContributions}* ]"
                        : $"**<@{word.Author}>** [ *Total contributions: {totalContributions}* ]";

                    userTags += topWordInfo + "\n";
                }

                generatedMessage += "\n";

                var hasWords = !string.IsNullOrWhiteSpace(generatedMessage);

                string description;
                if (hasWords)
                {
                    var cleaned = CleanMessage(generatedMessage);
                    description = $"**{preMessage}**\n# {cleaned}\n{userTags}";
                }
                else
                {
                    description = "No valid sentence was constructed this round.";
                }

                var embed = new EmbedBuilder()
                    .WithTitle("📝 Sentence Complete! 📝")
                    .WithDescription(description)
                    .WithColor(Color.Green)
                    .Build();

                // Emit standardized game ended notification with the final embed.
                // GameService.EndGameFromNaturalFlowAsync will:
                // - Validate GameId
                // - Ensure idempotency using _lastCompletedGameId
                // - End the game under gameLock and start the next one after release.
                var endMessage = embed.Description ?? "Letters game finished.";
                await _mediator.Send(new GameEndedNotification(GameState, endMessage));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while ending the game.");
            }
        }

        private string CleanMessage(string message)
        {
            var tagMatches = System.Text.RegularExpressions.Regex.Matches(message, @"<[^>]+>");
            var tags = new List<string>();
            foreach (System.Text.RegularExpressions.Match match in tagMatches)
            {
                tags.Add(match.Value);
            }

            for (int i = 0; i < tags.Count; i++)
            {
                message = message.Replace(tags[i], $"{{TAG{i}}}");
            }

            message = message.Trim();

            if (!string.IsNullOrEmpty(message))
            {
                message = char.ToUpper(message[0]) + message.Substring(1);
            }

            message = System.Text.RegularExpressions.Regex.Replace(message, @"\s+([,.!?;:])", "$1");
            message = System.Text.RegularExpressions.Regex.Replace(message, @"([,.!?;:])\s*", "$1 ");
            message = System.Text.RegularExpressions.Regex.Replace(message, @"\s*\(\s*", " (");
            message = System.Text.RegularExpressions.Regex.Replace(message, @"\s*\)\s*", ") ");
            message = System.Text.RegularExpressions.Regex.Replace(message, @"\s*([“”\""])", "$1");
            message = System.Text.RegularExpressions.Regex.Replace(message, @"([“”\""])\s*", "$1 ");
            message = message.TrimEnd();

            for (int i = 0; i < tags.Count; i++)
            {
                message = message.Replace($"{{TAG{i}}}", tags[i]);
            }

            return message;
        }

        public void PerformUserAction(GameAction action)
        {
        }

        public async Task StartGame(Embed previousMessage, string userTags)
        {
            await StartVotingPhase();
        }

        public async Task StartGame(string previousMessage)
        {
            await StartVotingPhase();
        }

        public async Task StartGame()
        {
            await StartVotingPhase();
        }

        private async Task StartVotingPhase()
        {
            WordList = new List<Word>();
            gameStatus = GameStatus.VOTING;
            GameState = new GameState
            {
                GameId = GameState.GameId,
                CurrentState = GameStatus.VOTING
            };
            selectedLetter = null;
            votingCancellationTokenSource = new CancellationTokenSource();
            voteCounts.Clear();
            votingLetters.Clear();
            isVotingTimeoutCancelled = false;

            // Select 3 random letters for voting
            var random = new Random();
            var allLetters = availableLetters.ToList();
            while (votingLetters.Count < 3 && allLetters.Count > 0)
            {
                int index = random.Next(allLetters.Count);
                var letter = allLetters[index];
                votingLetters.Add(letter);
                allLetters.RemoveAt(index);
            }

            // Create voting embed with reaction options
            var embed = new EmbedBuilder()
                .WithTitle("🗳️ Letter Voting Phase 🗳️")
                .WithDescription("**Vote for your preferred letter constraint!**\n\nReact with the corresponding emoji to vote:\n\n")
                .WithColor(Color.Blue)
                .WithFooter("Voting ends in 30 seconds!")
                .Build();

            var embedBuilder = new EmbedBuilder();
            embedBuilder.WithTitle("🗳️ Letter Voting Phase 🗳️");
            embedBuilder.WithDescription("**Vote for your preferred letter constraint!**\n\n");
            
            // Add voting options
            var votingOptions = new List<string>();
            for (int i = 0; i < votingLetters.Count; i++)
            {
                char letter = votingLetters[i];
                string emoji = i switch
                {
                    0 => "🔴",
                    1 => "🟡",
                    2 => "🟢",
                    _ => "⚪"
                };
                votingOptions.Add($"{emoji} **Letter {letter}** (must contain or cannot contain)");
                voteCounts[letter] = 0;
            }

            embedBuilder.WithDescription(string.Join("\n", votingOptions));
            embedBuilder.WithColor(Color.Blue);
            embedBuilder.WithFooter("Voting ends in 30 seconds!");

            var finalEmbed = embedBuilder.Build();

            // Send the voting message using GameStartedNotification
            try
            {
                // Create a simple text message for voting
                string votingMessage = "🗳️ **Letter Voting Phase** 🗳️\n\n";
                for (int i = 0; i < votingLetters.Count; i++)
                {
                    char letter = votingLetters[i];
                    string emoji = i switch
                    {
                        0 => "🔴",
                        1 => "🟡",
                        2 => "🟢",
                        _ => "⚪"
                    };
                    votingMessage += $"{emoji} **Letter {letter}**\n";
                }
                votingMessage += "\n⏰ Voting ends in 30 seconds!";
                
                // Send message and get the message ID
                var messageResult = await _backend.SendMessage(new SendDiscordMessage(votingMessage));
                if (messageResult.IsError)
                {
                    _logger.LogError("Failed to send voting message: {Error}", messageResult.FirstError);
                    await FallbackToCasual();
                    return;
                }
                
                votingMessageId = messageResult.Value;
                
                // Add emoji reactions for each voting letter
                foreach (char letter in votingLetters)
                {
                    if (LetterEmojis.TryGetValue(letter, out string letterEmoji))
                    {
                        var emoji = new Rentences.Domain.Definitions.Emote
                        {
                            Contents = letterEmoji,
                            IsEmoji = true
                        };
                        
                        await _backend.AddReactionToMessage(gameChannelId, votingMessageId, emoji);
                    }
                }
                
                // Add a "thumbs up" emoji for a general "vote" option
                var thumbsUpEmoji = new Rentences.Domain.Definitions.Emote
                {
                    Contents = "👍",
                    IsEmoji = true
                };
                await _backend.AddReactionToMessage(gameChannelId, votingMessageId, thumbsUpEmoji);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send voting message or add reactions");
                await FallbackToCasual();
                return;
            }

            StartVotingTimer();
        }

        private void StartVotingTimer()
        {
            var timer = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(30000, votingCancellationTokenSource.Token); // 30 seconds
                    
                    if (!isVotingTimeoutCancelled)
                    {
                        await ProcessVoteResults();
                    }
                }
                catch (TaskCanceledException)
                {
                    // Voting was cancelled, do nothing
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in voting timer");
                    await FallbackToCasual();
                }
            });
        }

        private async Task ProcessVoteResults()
        {
            gameStatus = GameStatus.PROCESSING_VOTES;
            GameState = new GameState
            {
                GameId = GameState.GameId,
                CurrentState = GameStatus.PROCESSING_VOTES
            };

            try
            {
                // Count actual votes from Discord reactions
                var actualVoteCounts = new Dictionary<char, int>();
                
                foreach (char letter in votingLetters)
                {
                    if (LetterEmojis.TryGetValue(letter, out string letterEmoji))
                    {
                        var emoji = new Rentences.Domain.Definitions.Emote
                        {
                            Contents = letterEmoji,
                            IsEmoji = true
                        };
                        
                        var reactionsResult = await _backend.GetReactionsForMessage(gameChannelId, votingMessageId, emoji);
                        if (!reactionsResult.IsError && reactionsResult.Value != null)
                        {
                            // Exclude bot reactions (if any) - we'll assume bot user ID is known
                            var userReactions = reactionsResult.Value.Where(user => user.IsBot == false);
                            actualVoteCounts[letter] = userReactions.Count();
                        }
                    }
                }
                
                var hasVotes = actualVoteCounts.Values.Any(count => count > 0);
                
                if (!hasVotes)
                {
                    // No votes received, treat as proper end of Letters and let GameService decide next game
                    GameState = new GameState
                    {
                        GameId = GameState.GameId,
                        CurrentState = GameStatus.ENDED
                    };
                    gameStatus = GameStatus.ENDED;

                    await _mediator.Send(new GameEndedNotification(
                        GameState,
                        "📊 No votes received for Letters mode. Ending Letters game."));

                    return;
                }

                // Determine the winning letter
                char winningLetter = actualVoteCounts.OrderByDescending(x => x.Value).First().Key;
                int winningVotes = actualVoteCounts[winningLetter];
                
                // Randomly determine if must contain or cannot contain
                mustContainLetter = new Random().Next(2) == 0;
                selectedLetter = winningLetter;

                // Send results and start the game
                var resultEmbed = new EmbedBuilder()
                    .WithTitle("🗳️ Voting Results! 🗳️")
                    .WithDescription($"**Winning Letter:** {winningLetter} with {winningVotes} vote(s)\n**Rule:** {(mustContainLetter ? "✅ Must contain" : "❌ Cannot contain")}\n\n🎮 Starting Letter Voting Game!")
                    .WithColor(Color.Green)
                    .Build();

                await _backend.SendGameStartedNotification(new(GameState, resultEmbed));
                gameStatus = GameStatus.IN_PROGRESS;
                GameState = new GameState
                {
                    GameId = GameState.GameId,
                    CurrentState = GameStatus.IN_PROGRESS
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing vote results");
                await FallbackToCasual();
            }
        }

        private async Task FallbackToCasual()
        {
            // Preserve legacy helper but align with lifecycle:
            // mark this Letters instance as ended; GameService should control what runs next.
            gameStatus = GameStatus.ENDED;
            GameState = new GameState
            {
                GameId = GameState.GameId,
                CurrentState = GameStatus.ENDED
            };

            try
            {
                // No automatic game switch here to avoid violating lifecycle assumptions.
                // If called, just emit a generic end notification once.
                await _mediator.Send(new GameEndedNotification(
                    GameState,
                    "Letters game ended; please start a new mode manually or via GameService."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Letters fallback handling");
            }
        }

        // Method to handle Discord reaction events (would be called from a reaction handler)
        public async Task HandleVoteReaction(char letter, IUser user)
        {
            if (gameStatus != GameStatus.VOTING || !votingLetters.Contains(letter))
                return;

            if (voteCounts.ContainsKey(letter))
            {
                voteCounts[letter]++;
                _logger.LogInformation($"Vote recorded for letter {letter} by user {user.Username}");
            }
        }
    }
}