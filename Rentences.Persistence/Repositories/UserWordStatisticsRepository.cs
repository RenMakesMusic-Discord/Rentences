namespace Rentences.Persistence.Repositories;

using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Rentences.Domain.Definitions.Game;
using Rentences.Persistence;

public class UserWordStatisticsRepository : IUserWordStatisticsRepository
{
    private readonly AppDbContext _dbContext;

    public UserWordStatisticsRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpdateUserStatisticsAsync(ulong userId)
    {
        var userStats = await _dbContext.UserStatistics
            .SingleOrDefaultAsync(u => u.UserId == userId);
        if (userStats == null)
        {
            userStats = new UserWordStatistics { UserId = userId, TotalWordsAdded = 1 };
            _dbContext.UserStatistics.Add(userStats);
        }
        else
        {
            userStats.TotalWordsAdded++;
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<UserWordStatistics> GetUserStatisticsAsync(ulong userId)
    {
        return await _dbContext.UserStatistics
            .SingleOrDefaultAsync(u => u.UserId == userId);
    }
}
