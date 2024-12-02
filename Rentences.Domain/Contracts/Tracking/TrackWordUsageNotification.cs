namespace Rentences.Domain.Contracts.Tracking;
public class TrackWordUsageNotification : INotification
{
    public string WordValue { get; }

    public TrackWordUsageNotification(string wordValue)
    {
        WordValue = wordValue;
    }
}