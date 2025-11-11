using Discord;
using Discord.WebSocket;
using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Rentences.Domain.Definitions;
using Rentences.Domain.Definitions.Game;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rentences.Application.Services.Game
{
    public class Casual : IGamemodeHandler
    {
        private readonly ILogger _logger;
        private readonly IInterop _backend;
        private readonly DiscordConfiguration _config;
        private readonly WordService _wordService;
        private ulong LastSenderId;

        public Casual(ILogger<Casual> logger, IInterop Backend, DiscordConfiguration configuration, WordService wordService)
        {
            _logger = logger;
            _backend = Backend;
            _config = configuration;
            _wordService = wordService;
            WordList = new List<Word>();
        }

        public GameState GameState { get; set; }
        public List<Word> WordList { get; set; }

        public async Task<ErrorOr<bool>> AddMessage(SocketMessage message)
        {
            var previousWord = WordList.OrderByDescending(w => w.TimeStamp).LastOrDefault();

            if (GameState.CurrentState != GameStatus.IN_PROGRESS)
            {
                await FailMessage(message);
                return Error.Validation(CustomErrorValues.WordValidationTitle.Title, "Game is not currently in progress.");
            }

            Word w = new Word().CreateWord(message.Id, message.Content, message.Author.Id, message.Timestamp);
            if (w == null || WordList.Count < 3 && Word.ContainsValidTermination(w) || LastSenderId == message.Author.Id || Word.ContainsValidTermination(previousWord))
            {
                await FailMessage(message);
                return Error.Validation(CustomErrorValues.WordValidationTitle.Title, CustomErrorValues.WordValidationTitle.Description);
            }

            await _backend.SendGameMessageReaction(new() { socketMessage = message, emoji = _config.CorrectEmoji });
            await AddWord(w);
            if (Word.ContainsValidTermination(w))
            {
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
            return await Task.FromResult(true); // Add await operator
        }

        public async Task EndGame()
        {
            try
            {
                // Mark terminal state for GameService
                GameState = new GameState
                {
                    GameId = GameState.GameId == Guid.Empty ? Guid.NewGuid() : GameState.GameId,
                    CurrentState = GameStatus.ENDED
                };

                LastSenderId = ulong.MinValue;

                // Build sentence and contributor summary
                var authors = new List<ulong>();
                var userTags = ">>> ";
                var sentenceBuilder = new StringBuilder();

                foreach (var word in WordList.OrderBy(w => w.TimeStamp))
                {
                    if (!string.IsNullOrWhiteSpace(word.Value))
                    {
                        sentenceBuilder.Append(' ').Append(word.Value);
                    }

                    if (!authors.Contains(word.Author))
                    {
                        authors.Add(word.Author);

                        var topWord = _wordService.GetTopWordsByUser(word.Author, 1)?.ToList();
                        var totalContributions = await _wordService.GetTotalWordsAddedByUserAsync(word.Author);

                        string topWordInfo = topWord != null && topWord.Any()
                            ? $"**<@{word.Author}>** [ *Top word: {topWord.First().Value}* | *Total contributions: {totalContributions}* ]"
                            : $"**<@{word.Author}>** [ *Total contributions: {totalContributions}* ]";

                        userTags += topWordInfo + "\n";
                    }
                }

                var rawSentence = sentenceBuilder.ToString();
                var hasWords = !string.IsNullOrWhiteSpace(rawSentence);

                string description;
                if (hasWords)
                {
                    var cleaned = CleanMessage(rawSentence);
                    description = $"**The players have constructed the following sentence:**\n# {cleaned}\n{userTags}";
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

                // Notify via backend; do not start a new game here.
                await _backend.SendGameStartedNotification(new(GameState, embed));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while ending the game."); // Log the exception
            }
        }

        private string CleanMessage(string message)
        {
            // Step 1: Extract and store tags with placeholders
            var tagMatches = System.Text.RegularExpressions.Regex.Matches(message, @"<[^>]+>");
            var tags = new List<string>();
            foreach (System.Text.RegularExpressions.Match match in tagMatches)
            {
                tags.Add(match.Value);
            }

            // Replace each tag with a unique placeholder
            for (int i = 0; i < tags.Count; i++)
            {
                message = message.Replace(tags[i], $"{{TAG{i}}}");
            }

            // Step 2: Clean the non-tagged content

            // Trim leading/trailing whitespace
            message = message.Trim();

            // Capitalize the first letter of the sentence
            if (!string.IsNullOrEmpty(message))
            {
                message = char.ToUpper(message[0]) + message.Substring(1);
            }

            // Remove any space before punctuation (e.g., periods, commas, colons, semicolons, question marks, exclamation marks, parentheses)
            message = System.Text.RegularExpressions.Regex.Replace(message, @"\s+([,.!?;:])", "$1");

            // Ensure a single space after periods, commas, semicolons, colons, question marks, and exclamation marks
            message = System.Text.RegularExpressions.Regex.Replace(message, @"([,.!?;:])\s*", "$1 ");

            // Remove spaces inside parentheses and ensure proper spacing outside of them
            message = System.Text.RegularExpressions.Regex.Replace(message, @"\s*\(\s*", " (");  // Remove spaces before or inside (
            message = System.Text.RegularExpressions.Regex.Replace(message, @"\s*\)\s*", ") ");  // Remove spaces inside or after )

            // Correct spacing around quotation marks
            message = System.Text.RegularExpressions.Regex.Replace(message, @"\s*([“”\""])", "$1"); // Remove spaces before quotes
            message = System.Text.RegularExpressions.Regex.Replace(message, @"([“”\""])\s*", "$1 "); // Ensure space after closing quotes

            // Trim any extra space at the end
            message = message.TrimEnd();

            // Step 3: Reinsert tags in their original positions
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
            // Legacy overload: send provided summary, then start a fresh game state.
            WordList = new List<Word>();
            GameState = new GameState
            {
                GameId = Guid.NewGuid(),
                CurrentState = GameStatus.IN_PROGRESS
            };

            await _backend.SendGameStartedNotification(new(GameState, previousMessage));
            if (!string.IsNullOrWhiteSpace(userTags))
            {
                await _backend.SendMessage(new SendDiscordMessage($"✨ Contributed by ✨\n {userTags}"));
            }
        }

        public async Task StartGame(string previousMessage)
        {
            WordList = new List<Word>();
            GameState = new GameState
            {
                GameId = Guid.NewGuid(),
                CurrentState = GameStatus.IN_PROGRESS
            };

            var embed = new EmbedBuilder()
                .WithDescription($"{previousMessage}\n\n🔄 A new game of **Rentences Casual** is about to begin! Get ready! 🎮")
                .WithColor(Color.Blue)
                .Build();

            await _backend.SendGameStartedNotification(new(GameState, embed));
        }

        public async Task StartGame()
        {
            WordList = new List<Word>();
            GameState = new GameState
            {
                GameId = Guid.NewGuid(),
                CurrentState = GameStatus.IN_PROGRESS
            };

            var embed = new EmbedBuilder()
                .WithDescription("🔄 A new game of **Rentences Casual** is about to begin! Get ready! 🎮")
                .WithColor(Color.Blue)
                .Build();

            await _backend.SendGameStartedNotification(new(GameState, embed));
        }
    }
}
