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
        return _dbContext.Words
            .Where(w => w.Author == userId) // Filter by the specific user
            // Normalize for aggregation: lowercase + strip leading/trailing punctuation/separators, preserve internal apostrophes
            .Select(w => new
            {
                Original = w,
                Normalized = NormalizeForAggregation(w.Value)
            })
            .Where(x => !string.IsNullOrEmpty(x.Normalized))
            .GroupBy(x => x.Normalized)          // Group by normalized word value
            .OrderByDescending(g => g.Count()) // Order by usage count in descending order
            .ThenBy(g => g.Key)               // Stable ordering for ties
            .Take(topCount)                   // Take the top N results
            .Select(g => new Word
            {
                Value = g.Key,                // Expose normalized value as the word
            });
    }

    private static string NormalizeForAggregation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim();

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
}