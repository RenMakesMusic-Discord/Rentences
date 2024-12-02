using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;

public static class EmojiDetector
{
    private static readonly string DiscordEmojiPattern = @"<:[a-zA-Z0-9_]+:[0-9]+>"; // Custom emoji
    private static readonly string AnimatedDiscordEmojiPattern = @"<a:[a-zA-Z0-9_]+:[0-9]+>"; // Animated emoji

    private static HashSet<ulong> AllowedEmojiIds = new HashSet<ulong>();

    public static void InitializeAllowedEmojis(SocketGuild guild)
    {
        // Populate AllowedEmojiIds with the IDs of the guild emojis
        AllowedEmojiIds = new HashSet<ulong>(guild.Emotes.Select(e => e.Id));
    }

    public static (int EmojiCount, bool IsOnlyEmoji) GetEmojiInfo(string input)
    {
        int discordEmojiCount = CountDiscordEmojis(input);
        int unicodeEmojiCount = CountUnicodeEmojis(input);

        // Total emoji count (Discord emojis + Unicode emojis)
        int totalEmojiCount = discordEmojiCount + unicodeEmojiCount;

        // Check if the input contains only emojis
        bool isOnlyEmoji = totalEmojiCount == 1;

        return (totalEmojiCount, isOnlyEmoji);
    }

    private static int CountDiscordEmojis(string input)
    {
        string combinedDiscordPattern = $"({DiscordEmojiPattern}|{AnimatedDiscordEmojiPattern})";
        var matches = Regex.Matches(input, combinedDiscordPattern);

        int count = 0;
        foreach (Match match in matches)
        {
            // Extract the emoji ID from the match
            var idMatch = Regex.Match(match.Value, @"\d+");
            if (idMatch.Success && ulong.TryParse(idMatch.Value, out ulong emojiId))
            {
                // Only count the emoji if it is in the allowed list
                if (AllowedEmojiIds.Contains(emojiId))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int CountUnicodeEmojis(string input)
    {
        byte[] utf32Bytes = Encoding.UTF32.GetBytes(input);
        int[] codePoints = new int[utf32Bytes.Length / 4];

        for (int i = 0; i < codePoints.Length; i++)
        {
            codePoints[i] = BitConverter.ToInt32(utf32Bytes, i * 4);
        }

        return CountEmojiSequences(codePoints);
    }

    private static int CountEmojiSequences(int[] codePoints)
    {
        int emojiCount = 0;
        for (int i = 0; i < codePoints.Length; i++)
        {
            if (IsEmojiCodePoint(codePoints[i]))
            {
                if (i + 1 < codePoints.Length && (codePoints[i + 1] == 0xFE0F || codePoints[i + 1] == 0x200D))
                {
                    i++;
                }
                emojiCount++;
            }
        }
        return emojiCount;
    }

    private static bool IsEmojiCodePoint(int codePoint)
    {
        return
            (codePoint >= 0x1F600 && codePoint <= 0x1F64F) || // Emoticons
            (codePoint >= 0x1F300 && codePoint <= 0x1F5FF) || // Misc Symbols and Pictographs
            (codePoint >= 0x1F680 && codePoint <= 0x1F6FF) || // Transport and Map Symbols
            (codePoint >= 0x1F700 && codePoint <= 0x1F77F) || // Alchemical Symbols
            (codePoint >= 0x1F780 && codePoint <= 0x1F7FF) || // Geometric Shapes Extended
            (codePoint >= 0x1F800 && codePoint <= 0x1F8FF) || // Supplemental Arrows-C
            (codePoint >= 0x1FA00 && codePoint <= 0x1FAFF) || // Supplemental Symbols and Pictographs
            (codePoint >= 0x1FB00 && codePoint <= 0x1FBFF) || // Additional Symbols for Unicode 16.0
            (codePoint >= 0x1F3FB && codePoint <= 0x1F3FF) || // Skin tone modifiers
            (codePoint >= 0x1F1E6 && codePoint <= 0x1F1FF) || // Regional Indicator Symbols
            (codePoint >= 0xFE00 && codePoint <= 0xFE0F) ||   // Variation selectors
            (codePoint == 0x200D) ||                          // Zero-width joiner
            (codePoint >= 0x2600 && codePoint <= 0x26FF) ||   // Misc symbols
            (codePoint >= 0x2700 && codePoint <= 0x27BF) ||   // Dingbats
            (codePoint >= 0x2B50 && codePoint <= 0x2B55) ||   // Stars and circles
            (codePoint == 0x2764);                            // Heart symbol
    }
}
