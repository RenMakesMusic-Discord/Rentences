using Rentences.Domain.Definitions.Game;
using FluentAssertions;
using Xunit;

namespace Rentences.Tests
{
    public class WordTests
    {
        [Theory]
        [InlineData("Hello", "hello")]
        [InlineData("hello", "hello")]
        [InlineData("HELLO!", "hello")]
        [InlineData("hello!!!", "hello")]
        [InlineData("  HeLLo,", "hello")]
        [InlineData("it's", "it's")]
        [InlineData("it's!", "it's")]
        public void NormalizeWord_ShouldNormalizeCaseAndPunctuation(string input, string expected)
        {
            var normalized = input.NormalizeWord();
            normalized.Should().Be(expected);
        }
        [Theory]
        [InlineData((ulong)1, "", (ulong)1)] // Empty value
        [InlineData((ulong)1, "   ", (ulong)1)] // Whitespace value
        [InlineData((ulong)1, "hello world", (ulong)1)] // Contains space
        [InlineData((ulong)1, "hello1", (ulong)1)] // Invalid letter-number mix
        [InlineData((ulong)1, "he'llowhwhwhw", (ulong)1)] // Invalid apostrophe usage
        [InlineData((ulong)1, "hello-world", (ulong)1)] // Hyphen connecting two words
        [InlineData((ulong)1, "😊😊", (ulong)1)] // Multiple emojis
        [InlineData((ulong)1, "<:custom_emoji:123456789><:another_emoji:987654321>", (ulong)1)] // Multiple custom emojis
        [InlineData((ulong)1, "<a:custom_animated:123456789><a:another_animated:987654321>", (ulong)1)] // Multiple animated emojis
        public void WordCreation_ShouldFail_WhenInvalidData(ulong id, string value, ulong author)
        {
            // Act
            Word a = new Word().CreateWord(id, value, author);

            // Assert
            a.Should().BeNull();
        }

        [Theory]
        [InlineData((ulong)1, "12345", (ulong)1)] // Numbers only
        [InlineData((ulong)1, "1990s", (ulong)1)] // Plural form with numbers
        [InlineData((ulong)1, "1990's", (ulong)1)] // Possessive form with numbers
        [InlineData((ulong)1, "£69", (ulong)1)] // Currency symbol with numbers
        [InlineData((ulong)1, "hello,", (ulong)1)] // Word with connecting punctuation
        [InlineData((ulong)1, "hello.", (ulong)1)] // Word with terminating punctuation
        [InlineData((ulong)1, "😊", (ulong)1)] // Single unicode emoji
        [InlineData((ulong)1, "👩‍👩‍👦", (ulong)1)] // Compound unicode emoji (family emoji with ZWJ)
        [InlineData((ulong)1, "<:custom_emoji:123456789>", (ulong)1)] // Single custom Discord emoji
        [InlineData((ulong)1, "<a:custom_animated:123456789>", (ulong)1)] // Single animated Discord emoji
        public void WordCreation_ShouldNotFail_WhenValidData(ulong id, string value, ulong author)
        {
            // Act
            Word a = new Word().CreateWord(id, value, author);

            // Assert
            a.Should().NotBeNull();
        }
    }
}