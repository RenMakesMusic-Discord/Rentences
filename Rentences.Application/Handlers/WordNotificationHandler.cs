using Rentences.Domain.Contracts.Tracking;
using Rentences.Persistence.Repositories;

namespace Rentences.Application.Handlers;


public class WordNotificationHandler :
    INotificationHandler<AddWordNotification>,
    INotificationHandler<TrackWordUsageNotification>,
    INotificationHandler<UpdateUserStatisticsNotification>
{
    private readonly IWordRepository _wordRepository;

    public WordNotificationHandler(WordRepository wordRepository)
    {
        _wordRepository = wordRepository;
    }

    public async Task Handle(AddWordNotification notification, CancellationToken cancellationToken)
    {
        await _wordRepository.AddWordAsync(notification.Word);
    }

    public async Task Handle(TrackWordUsageNotification notification, CancellationToken cancellationToken)
    {
        await _wordRepository.TrackWordUsageAsync(notification.WordValue);
    }

    public async Task Handle(UpdateUserStatisticsNotification notification, CancellationToken cancellationToken)
    {
        await _wordRepository.UpdateUserStatisticsAsync(notification.UserId);
    }
}
