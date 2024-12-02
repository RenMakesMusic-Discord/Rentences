using Rentences.Domain.Definitions.Game;

public interface IWordRepository
{
    Task AddWordAsync(Word word);
    Task TrackWordUsageAsync(string wordValue);
    Task UpdateUserStatisticsAsync(ulong userId);
    IQueryable<Word> GetTopWordsByUser(ulong userId, int topCount = 10);
}
