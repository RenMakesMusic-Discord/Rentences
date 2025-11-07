namespace Rentences.Persistence.Repositories;

using Rentences.Domain.Definitions.Game;

public interface IUserWordStatisticsRepository
{
    Task UpdateUserStatisticsAsync(ulong userId);
    Task<UserWordStatistics> GetUserStatisticsAsync(ulong userId);
}
