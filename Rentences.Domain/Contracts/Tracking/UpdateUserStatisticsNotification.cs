namespace Rentences.Domain.Contracts.Tracking;
public class UpdateUserStatisticsNotification : INotification
{
    public ulong UserId { get; }

    public UpdateUserStatisticsNotification(ulong userId)
    {
        UserId = userId;
    }
}