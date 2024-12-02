namespace Rentences.Domain.Definitions.Game;
public class UserWordStatistics
{
    public int Id { get; set; }
    public ulong UserId { get; set; }
    public int TotalWordsAdded { get; set; }
}