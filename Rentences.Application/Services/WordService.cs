using Microsoft.EntityFrameworkCore;
using Rentences.Domain.Definitions.Game;
using Rentences.Persistence;
using Rentences.Persistence.Repositories;

namespace Rentences.Application.Services;

public class WordService
{
    private readonly AppDbContext _dbContext;
    private readonly IWordRepository _wordRepository;
    public WordService(AppDbContext dbContext, IWordRepository wordRepo)
    {
        _dbContext = dbContext;
        _wordRepository = wordRepo;
    }

    // Track word usage in the database
    public async Task TrackWordUsageAsync(Word word)
    {
        // Get the punctuation-stripped word
        var strippedWord = word.GetStrippedValue().ToLower(); // Case-insensitive tracking

        // Find the existing word usage record or create a new one
        var wordUsage = await _dbContext.WordUsages.SingleOrDefaultAsync(w => w.WordValue == strippedWord);
        if (wordUsage == null)
        {
            // If the word does not exist, add it to the database
            wordUsage = new WordUsage { WordValue = strippedWord, Count = 1 };
            _dbContext.WordUsages.Add(wordUsage);
        }
        else
        {
            // If the word exists, increment the usage count
            wordUsage.Count++;
           
        }
        _dbContext.Words.Add(word);
        // Save changes to the database
        await _dbContext.SaveChangesAsync();
    }

    // Update user statistics in the database
    public async Task UpdateUserStatisticsAsync(ulong userId)
    {
        // Find the existing user statistics record or create a new one
        var userStats = await _dbContext.UserStatistics.SingleOrDefaultAsync(u => u.UserId == userId);
        if (userStats == null)
        {
            // If the user statistics do not exist, create a new record
            userStats = new UserWordStatistics { UserId = userId, TotalWordsAdded = 1 };
            _dbContext.UserStatistics.Add(userStats);
        }
        else
        {
            // If the user statistics exist, increment the total words added
            userStats.TotalWordsAdded++;
        }

        // Save changes to the database
        await _dbContext.SaveChangesAsync();
    }

    // Get the top words for a user based on word usage
    public IQueryable<Word> GetTopWordsByUser(ulong userId, int topCount = 1)
    {
        // Query to get the top used words by a specific user
        return _wordRepository.GetTopWordsByUser(userId, topCount);
    }
    // Get the top word for a user based on word usage
    public string GetTopWordByUser(ulong userId)
    {
        // Query to get the top used word by a specific user
        return _dbContext.Words
            .Where(w => w.Author == userId)
            .GroupBy(w => w.Value) // Group by the word's value
            .OrderByDescending(g => g.Count()) // Order by the count of each group in descending order
            .Select(g => g.Key) // Select the word value (string) from the group
            .FirstOrDefault(); // Get the word with the highest count or null if none found
    }



    // Get the total number of words added by a user
    public async Task<int> GetTotalWordsAddedByUserAsync(ulong userId)
    {
        // Query to get the user's total word contribution count
        var userStats = await _dbContext.UserStatistics.SingleOrDefaultAsync(u => u.UserId == userId);
        return userStats?.TotalWordsAdded ?? 0;
    }
}
