using Discord;
using Discord.WebSocket;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Rentences.Domain.Definitions;
using Rentences.Domain.Definitions.Game;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Rentences.Application.Services.Game
{
    public class ReverseSentence : IGamemodeHandler
    {
        private readonly ILogger _logger;
        private readonly IInterop _backend;
        private readonly DiscordConfiguration _config;
        private readonly WordService _wordService;
        private ulong LastSenderId;
        private GameStatus gameStatus;

        public ReverseSentence(ILogger<ReverseSentence> logger, IInterop Backend, DiscordConfiguration configuration, WordService wordService)
        {
            _logger = logger;
            _backend = Backend;
            _config = configuration;
            _wordService = wordService;
            WordList = new List<Word>(); // Initialize WordList
        }

        public GameState GameState { get; set; }
        public List<Word> WordList { get; set; }

        public async Task<ErrorOr<bool>> AddMessage(SocketMessage message)
        {
            var previousWord = WordList.OrderByDescending(w => w.TimeStamp).LastOrDefault();

            Word w = new Word().CreateWord(message.Id, message.Content, message.Author.Id, message.Timestamp);
            if (w == null || WordList.Count < 3 && Word.ContainsValidTermination(w) || LastSenderId == message.Author.Id || gameStatus != GameStatus.IN_PROGRESS || Word.ContainsValidTermination(previousWord))
            {
                await FailMessage(message);
                return Error.Validation(CustomErrorValues.WordValidationTitle.Title, CustomErrorValues.WordValidationTitle.Description);
            }

            await _backend.SendGameMessageReaction(new() { socketMessage = message, emoji = _config.CorrectEmoji });
            await AddWord(w);
            if (Word.ContainsValidTermination(w))
            {
                gameStatus = GameStatus.ENDED;
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
                gameStatus = GameStatus.ENDED;
                string generatedMessage = "";
                string preMessage = "The players have constructed the following sentence:";
                string userTags = "";
                List<ulong> authors = new List<ulong>();
                LastSenderId = ulong.MinValue;
                
                // Initialize an empty string for the generated message and user tags
                generatedMessage = string.Empty;
                userTags = ">>> ";

                // Collect all words and join into a single sentence
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

                // Reverse the sentence
                string reversedMessage = ReverseSentenceLogic(generatedMessage);

                // Append user tags to the generated message as a summary
                reversedMessage += "\n";

                var embed = new EmbedBuilder()
                    .WithTitle("🔄 Reversed Sentence Complete! 🔄")
                    .WithDescription($"**{preMessage}**\n# {CleanMessage(reversedMessage)}\n")
                    .WithColor(Color.Purple)
                    .Build();

                await StartGame(embed, userTags);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while ending the game."); // Log the exception
            }
        }

        private string ReverseSentenceLogic(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                return sentence;

            // Clean the sentence first
            sentence = sentence.Trim();

            // Extract punctuation and special characters
            var openingPunct = new List<char>();
            var closingPunct = new List<char>();
            
            // Find opening punctuation at the start
            while (sentence.Length > 0 && "([{\"'".Contains(sentence[0]))
            {
                openingPunct.Add(sentence[0]);
                sentence = sentence.Substring(1);
            }

            // Find closing punctuation at the end
            while (sentence.Length > 0 && ")]}\"'!?.,:;".Contains(sentence[sentence.Length - 1]))
            {
                closingPunct.Insert(0, sentence[sentence.Length - 1]); // Insert at beginning to maintain order
                sentence = sentence.Substring(0, sentence.Length - 1);
            }

            // Split into words
            var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            
            if (words.Count == 0)
                return sentence;

            // Reverse the order of words
            words.Reverse();

            // Handle punctuation that was attached to words
            for (int i = 0; i < words.Count; i++)
            {
                string word = words[i];
                
                // Extract trailing punctuation
                var trailingPunct = new List<char>();
                while (word.Length > 0 && "!?.,:;".Contains(word[word.Length - 1]))
                {
                    trailingPunct.Insert(0, word[word.Length - 1]);
                    word = word.Substring(0, word.Length - 1);
                }

                // Extract leading punctuation
                var leadingPunct = new List<char>();
                while (word.Length > 0 && "([{\"'".Contains(word[0]))
                {
                    leadingPunct.Add(word[0]);
                    word = word.Substring(1);
                }

                // If this is the first word, capitalize it
                if (i == 0 && word.Length > 0)
                {
                    word = char.ToUpper(word[0]) + word.Substring(1);
                }

                // Reconstruct word with punctuation
                words[i] = string.Join("", leadingPunct) + word + string.Join("", trailingPunct);
            }

            // Rebuild the sentence
            string result = string.Join(" ", words);
            
            // Add closing punctuation at the end
            result += string.Join("", closingPunct);
            
            // Add opening punctuation at the beginning
            result = string.Join("", openingPunct) + result;

            return result;
        }

        private string CleanMessage(string message)
        {
            // Step 1: Extract and store tags with placeholders
            var tagMatches = Regex.Matches(message, @"<[^>]+>");
            var tags = new List<string>();
            foreach (Match match in tagMatches)
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
            message = Regex.Replace(message, @"\s+([,.!?;:])", "$1");

            // Ensure a single space after periods, commas, semicolons, colons, question marks, and exclamation marks
            message = Regex.Replace(message, @"([,.!?;:])\s*", "$1 ");

            // Remove spaces inside parentheses and ensure proper spacing outside of them
            message = Regex.Replace(message, @"\s*\(\s*", " (");  // Remove spaces before or inside (
            message = Regex.Replace(message, @"\s*\)\s*", ") ");  // Remove spaces inside or after )

            // Correct spacing around quotation marks
            message = Regex.Replace(message, @"\s*([“”\""])", "$1"); // Remove spaces before quotes
            message = Regex.Replace(message, @"([“”\""])\s*", "$1 "); // Ensure space after closing quotes

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
            await _backend.SendGameStartedNotification(new(GameState, previousMessage));
            await _backend.SendMessage(new SendDiscordMessage($"✨ Contributed by ✨\n {userTags}"));
            await StartGame();
        }

        public async Task StartGame(string previousMessage)
        {
            var embed = new EmbedBuilder()
                .WithDescription($"{previousMessage}\n\n🔄 A new game of **Rentences Reverse Sentence** is about to begin! Get ready! 🎮")
                .WithColor(Color.Purple)
                .Build();

            WordList = new List<Word>();
            gameStatus = GameStatus.IN_PROGRESS;
            await _backend.SendGameStartedNotification(new(GameState, embed));
        }

        public async Task StartGame()
        {
            var embed = new EmbedBuilder()
                .WithDescription("🔄 A new game of **Rentences Reverse Sentence** is about to begin! Get ready! 🎮")
                .WithColor(Color.Purple)
                .Build();

            WordList = new List<Word>();
            gameStatus = GameStatus.IN_PROGRESS;
            await _backend.SendGameStartedNotification(new(GameState, embed));
        }
    }
}