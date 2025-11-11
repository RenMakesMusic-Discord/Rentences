using Discord;
using Discord.WebSocket;
using ErrorOr;


using System;
using System.Threading.Tasks;

namespace Rentences.Application.Services;
public interface IGameService {
    // Existing methods (for backward compatibility)
    public Task StartGame(Gamemodes gamemode);
    public Task StartGame();
    public ErrorOr<bool> EndGame();
    public ErrorOr<bool> PerformAddAction(SocketMessage msg);
    public ErrorOr<bool> PerformRemoveAction(ulong msgid);

    // New thread-safe methods with force termination
    public Task StartGameWithForceTerminationAsync(Gamemodes gameMode, string reason = "User requested new game");
    public Task ForceTerminateCurrentGame(string reason = "Force termination");
    public Task<ErrorOr<bool>> PerformAddActionAsync(SocketMessage msg);
    public Task<ErrorOr<bool>> PerformRemoveActionAsync(ulong msgid);
    public bool IsGameRunning();
    public Gamemodes? GetCurrentGameMode();
}
