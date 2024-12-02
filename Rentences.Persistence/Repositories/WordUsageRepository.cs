using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Rentences.Domain.Definitions.Game;
using Rentences.Persistence;

public class WordUsageRepository : IWordUsageRepository
{
    private readonly AppDbContext _dbContext;

    public WordUsageRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task TrackWordUsageAsync(string wordValue)
    {
        var strippedWord = wordValue.ToLower();

        var wordUsage = await _dbContext.WordUsages
            .SingleOrDefaultAsync(w => w.WordValue == strippedWord);
        if (wordUsage == null)
        {
            wordUsage = new WordUsage { WordValue = strippedWord, Count = 1 };
            _dbContext.WordUsages.Add(wordUsage);
        }
        else
        {
            wordUsage.Count++;
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<WordUsage> GetWordUsageAsync(string wordValue)
    {
        return await _dbContext.WordUsages
            .SingleOrDefaultAsync(w => w.WordValue == wordValue.ToLower());
    }

    public IQueryable<WordUsage> GetTopWords(int topCount)
    {
        return _dbContext.WordUsages
            .OrderByDescending(w => w.Count)
            .Take(topCount);
    }
}
