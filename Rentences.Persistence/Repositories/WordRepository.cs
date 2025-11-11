namespace Rentences.Persistence.Repositories;

 using System;
 using System.Threading.Tasks;
 using Microsoft.EntityFrameworkCore;
 using Rentences.Domain.Definitions.Game;
 using Rentences.Persistence;

public class WordRepository : IWordRepository
{
    private readonly AppDbContext _dbContext;

    public WordRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddWordAsync(Word word)
    {
        _dbContext.Words.Add(word);
        await _dbContext.SaveChangesAsync();
    }

    public async Task TrackWordUsageAsync(string wordValue)
    {
        // Normalize for consistent tracking (matches NormalizeWord rules)
        if (string.IsNullOrWhiteSpace(wordValue))
            return;

        // Lowercase and strip leading/trailing punctuation/separators, preserve internal apostrophes
        int start = 0;
        int end = wordValue.Length - 1;

        while (start <= end && !char.IsLetterOrDigit(wordValue[start]) && wordValue[start] != '\'')
            start++;

        while (end >= start && !char.IsLetterOrDigit(wordValue[end]) && wordValue[end] != '\'')
            end--;

        if (start > end)
            return;

        var strippedWord = wordValue.Substring(start, end - start + 1).ToLowerInvariant();

        var wordUsage = await _dbContext.WordUsages.SingleOrDefaultAsync(w => w.WordValue == strippedWord);
        if (wordUsage == null)
        {
            // Add new word usage if it doesn't exist
            wordUsage = new WordUsage { WordValue = strippedWord, Count = 1 };
            _dbContext.WordUsages.Add(wordUsage);
        }
        else
        {
            // Increment the count if the word already exists
            wordUsage.Count++;
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateUserStatisticsAsync(ulong userId)
    {
        var userStats = await _dbContext.UserStatistics.SingleOrDefaultAsync(u => u.UserId == userId);
        if (userStats == null)
        {
            // Create new user statistics if they don't exist
            userStats = new UserWordStatistics { UserId = userId, TotalWordsAdded = 1 };
            _dbContext.UserStatistics.Add(userStats);
        }
        else
        {
            // Increment the count of total words added
            userStats.TotalWordsAdded++;
        }

        await _dbContext.SaveChangesAsync();
    }

    public IQueryable<Word> GetTopWordsByUser(ulong userId, int topCount = 10)
    {
        // EF-translatable filtering by user, then switch to LINQ-to-Objects
        return _dbContext.Words
            .Where(w => w.Author == userId)
            .AsEnumerable()
            // Normalize for aggregation: lowercase + strip leading/trailing punctuation/separators, preserve internal apostrophes
            .Select(w => new
            {
                Original = w,
                Normalized = NormalizeForAggregation(w.Value)
            })
            .Where(x => !string.IsNullOrEmpty(x.Normalized))
            .GroupBy(x => x.Normalized)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Take(topCount)
            .Select(g => new Word
            {
                Value = g.Key,
            })
            .AsQueryable();
    }

    private static string NormalizeForAggregation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim();

        // Filter out Discord custom emoji markup such as <:name:id> or <a:name:id>
        // This is done before further normalization so these tokens are excluded from aggregation.
        if (IsDiscordCustomEmoji(value))
            return string.Empty;

        int start = 0;
        int end = value.Length - 1;

        while (start <= end && !char.IsLetterOrDigit(value[start]) && value[start] != '\'')
            start++;

        while (end >= start && !char.IsLetterOrDigit(value[end]) && value[end] != '\'')
            end--;

        if (start > end)
            return string.Empty;

        return value.Substring(start, end - start + 1).ToLowerInvariant();
    }

    private static bool IsDiscordCustomEmoji(string value)
    {
        // Expected formats:
        //   <:name:id>
        //   <a:name:id>
        // We avoid regex and use simple structural checks to keep this lightweight.

        if (value.Length < 8) // minimal plausible length, e.g. "<:a:1>"
            return false;

        if (value[0] != '<' || value[^1] != '>')
            return false;

        var inner = value.AsSpan(1, value.Length - 2); // strip surrounding < >

        // Animated: a:name:id
        // Static:   :name:id (note that original has leading ':' after '<')
        int firstColon = inner.IndexOf(':');
        if (firstColon <= 0 || firstColon == inner.Length - 1)
            return false;

        bool isAnimated = inner[0] == 'a';

        // For animated, we expect "a:name:id" -> firstColon after 'a'
        // For static, we expect ":name:id"    -> firstColon at 0 is invalid due to check above
        if (isAnimated && firstColon != 1)
            return false;

        // There must be a second colon separating name and id
        int secondColon = inner.Slice(firstColon + 1).IndexOf(':');
        if (secondColon <= 0)
            return false;

        secondColon += firstColon + 1;

        if (secondColon >= inner.Length - 1)
            return false;

        // Ensure id part is all digits
        var idSpan = inner.Slice(secondColon + 1);
        foreach (var ch in idSpan)
        {
            if (!char.IsDigit(ch))
                return false;
        }

        return idSpan.Length > 0;
    }
}