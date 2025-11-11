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

    /// <summary>
    /// Centralized lifecycle hook for game modes or handlers to signal that
    /// the current game has naturally completed via normal gameplay.
    /// Implementations MUST:
    /// - Validate the signal corresponds to the active game (where applicable),
    /// - Finalize the game via the internal EndGame pipeline,
    /// - Start the next natural game according to orchestration rules,
    /// - Be idempotent / safe against duplicate calls.
    /// </summary>
    public Task<ErrorOr<bool>> EndGameFromNaturalFlowAsync(GameState finalState, string endMessage);
}
