namespace Rentences.Domain.Contracts;

public record struct GameEndedNotification(GameState _gameState, string _endMessage) : IRequest<GameEndedNotificationResponse>
{
    public readonly GameState GameState = _gameState;
    public readonly string EndMessage = _endMessage;
}
