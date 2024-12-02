using Discord;
using Discord.WebSocket;
using ErrorOr;

namespace Rentences.Application;

public interface IGamemodeHandler
{
    public GameState GameState { get; set; }
    public Task StartGame();
    public Task<ErrorOr<bool>> AddMessage(SocketMessage Message);
    public Task<ErrorOr<bool>> DeleteMessage(ulong msgid);
    public Task EndGame();
}
