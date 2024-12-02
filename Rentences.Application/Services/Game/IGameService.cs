using Discord;
using Discord.WebSocket;
using ErrorOr;


namespace Rentences.Application.Services; 
public interface IGameService {
    public Task StartGame(Gamemodes gamemode);
    public Task StartGame();
    public ErrorOr<bool> EndGame();
    public ErrorOr<bool> PerformAddAction(SocketMessage msg);
    public ErrorOr<bool> PerformRemoveAction(ulong msgid);

}
