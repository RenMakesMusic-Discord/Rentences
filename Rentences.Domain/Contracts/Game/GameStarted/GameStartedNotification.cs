using Discord;
using ErrorOr;

namespace Rentences.Domain.Contracts
{
    public record struct GameStartedNotification(GameState GameState, Embed StartMessage) : INotification
    {
        public readonly GameState GameState = GameState;
        public readonly Embed StartMessage = StartMessage;
    }
    public record struct SendDiscordMessage (string Message) :IRequest<ErrorOr<bool>> {
        public readonly string Message = Message;
    }
}
