using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class StringExtensions
{
    public static string CleanMessage(this string message)
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
}
