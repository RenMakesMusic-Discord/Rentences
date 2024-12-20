﻿using Discord;
using Discord.WebSocket;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Rentences.Domain.Definitions;
using Rentences.Domain.Definitions.Game;

namespace Rentences.Application.Services.Game;
public class Casual : IGamemodeHandler {
    private readonly ILogger _logger;
    private readonly IInterop _backend;
    private readonly DiscordConfiguration _config;
    private readonly WordService _wordService;
    private ulong LastSenderId;
    private GameStatus gameStatus;

    public Casual(ILogger<Casual> logger, IInterop Backend, DiscordConfiguration configuration, WordService wordService) {
        _logger = logger;
        _backend = Backend;
        _config = configuration;
        _wordService = wordService;
        WordList = new List<Word>(); // Initialize WordList
    }

    public GameState GameState { get; set; }
    public List<Word> WordList { get; set; }

    public async Task<ErrorOr<bool>> AddMessage(SocketMessage message) {
        var previousWord = WordList.OrderByDescending(w => w.TimeStamp).LastOrDefault();

        Word w = new Word().CreateWord(message.Id, message.Content, message.Author.Id, message.Timestamp);
        if (w == null || WordList.Count < 3 && Word.ContainsValidTermination(w) || LastSenderId == message.Author.Id || gameStatus != GameStatus.IN_PROGRESS || Word.ContainsValidTermination(previousWord)) {
            await FailMessage(message);
            return Error.Validation(CustomErrorValues.WordValidationTitle.Title, CustomErrorValues.WordValidationTitle.Description);
        }

        await _backend.SendGameMessageReaction(new() { socketMessage = message, emoji = _config.CorrectEmoji });
        await AddWord(w);
        if (Word.ContainsValidTermination(w)) {
            gameStatus = GameStatus.END;
            await _backend.SendGameMessageReaction(new() { socketMessage = message, emoji = _config.WinEmoji });

            await EndGame();
        }

        return true;
    }

    private async Task AddWord(Word word) {
        WordList.Add(word);
        LastSenderId = word.Author;
        await _wordService.TrackWordUsageAsync(word);
        await _wordService.UpdateUserStatisticsAsync(word.Author);
    }

    public async Task FailMessage(SocketMessage message) {
        await _backend.SendGameMessageReaction(new() { socketMessage = message, emoji = _config.LoseEmoji });
    }

    public async Task<ErrorOr<bool>> DeleteMessage(ulong messageId) {
        Word removal = WordList.FirstOrDefault(a => a.MessageId == messageId);
        if (removal == null) {
            return Error.Validation(CustomErrorValues.WordValidationTitle.Title, CustomErrorValues.WordValidationTitle.Description);
        }

        WordList.Remove(removal);
        return await Task.FromResult(true); // Add await operator
    }

    public async Task EndGame() {
        try {
            gameStatus = GameStatus.END;
            string generatedMessage = "";
            string preMessage = "The players have constructed the following sentence:";
            string userTags = "";
            List<ulong> authors = new List<ulong>();
            LastSenderId = ulong.MinValue;

            // Initialize an empty string for the generated message and user tags
            generatedMessage = string.Empty;
            userTags = ">>> ";

            foreach (Word word in WordList.OrderBy(w => w.TimeStamp)) {
                // Append the word's value to the generated message
                generatedMessage += (" " + word.Value);

                // Skip if the author is already processed
                if (authors.Contains(word.Author))
                    continue;

                // Add the author to the processed list
                authors.Add(word.Author);

                // Fetch the author's top word and total number of contributions
                List<Word>? topWord = _wordService.GetTopWordsByUser(word.Author, 1)?.ToList();
                var totalContributions = await _wordService.GetTotalWordsAddedByUserAsync(word.Author);

                // Format the user's contribution details in indented and smaller text
                string topWordInfo = topWord != null && topWord.Any()
                    ? $"**<@{word.Author}>** [ *Top word: {topWord.First().Value}* | *Total contributions: {totalContributions}* ]"
                    : $"**<@{word.Author}>** [ *Total contributions: {totalContributions}* ]";

                // Add the formatted author tag to the userTags string
                userTags += topWordInfo + "\n";
            }

            // Append user tags to the generated message as a summary
            generatedMessage += "\n";

            var embed = new EmbedBuilder()
                .WithTitle("📝 Sentence Complete! 📝")
                .WithDescription($"**{preMessage}**\n# {generatedMessage.CleanMessage()}\n")
                .WithColor(Color.Green)
                .Build();

            await StartGame(embed, userTags);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "An error occurred while ending the game."); // Log the exception
        }
    }

    public void PerformUserAction(GameAction action) {
    }

    public async Task StartGame(Embed previousMessage, string userTags) {
        await _backend.SendGameStartedNotification(new(GameState, previousMessage));
        await _backend.SendMessage(new SendDiscordMessage($"✨ Contributed by ✨\n {userTags}"));
        await StartGame();
    }

    public async Task StartGame(string previousMessage) {
        var embed = new EmbedBuilder()
            .WithDescription($"{previousMessage}\n\n🔄 A new game of **Rentences Casual** is about to begin! Get ready! 🎮")
            .WithColor(Color.Blue)
            .Build();

        WordList = new List<Word>();
        gameStatus = GameStatus.IN_PROGRESS;
        await _backend.SendGameStartedNotification(new(GameState, embed));
    }

    public async Task StartGame() {
        var embed = new EmbedBuilder()
            .WithDescription("🔄 A new game of **Rentences Casual** is about to begin! Get ready! 🎮")
            .WithColor(Color.Blue)
            .Build();

        WordList = new List<Word>();
        gameStatus = GameStatus.IN_PROGRESS;
        await _backend.SendGameStartedNotification(new(GameState, embed));
    }
}
