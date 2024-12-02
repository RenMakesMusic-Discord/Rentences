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
        var strippedWord = wordValue.ToLower(); // Case insensitive tracking

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
            .GroupBy(w => w.Value)          // Group by word value
            .OrderByDescending(g => g.Count()) // Order by usage count in descending order
            .Take(topCount)                // Take the top N results
            .Select(g => new Word
            {
                Value = g.Key,
            });
    }
}