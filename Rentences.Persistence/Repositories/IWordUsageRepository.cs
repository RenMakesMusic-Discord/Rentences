namespace Rentences.Persistence.Repositories;

using Rentences.Domain.Definitions.Game;

public interface IWordUsageRepository
{
    Task TrackWordUsageAsync(string wordValue);
    Task<WordUsage> GetWordUsageAsync(string wordValue);
    IQueryable<WordUsage> GetTopWords(int topCount);
}
