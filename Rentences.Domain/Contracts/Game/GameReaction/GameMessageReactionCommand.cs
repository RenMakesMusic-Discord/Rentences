
using ErrorOr;
using Rentences.Domain.Definitions;
namespace Rentences.Domain.Contracts; 
public record struct GameMessageReactionCommand : IRequest<ErrorOr<GameMessageReactionResponse>> {
    public SocketMessage socketMessage { get; set; }
    public Emote emoji;
}
