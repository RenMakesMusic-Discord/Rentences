using Discord;
using Discord.WebSocket;
using ErrorOr;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Rentences.Application.Services.Game;

namespace Rentences.Application.Services;

public class GameService : IGameService, IDisposable
{
    private readonly IDictionary<Gamemodes, IGamemodeHandler> games;
    private IGamemodeHandler? currentGame;
    
    private readonly ILogger<GameService> logger;
    private readonly SemaphoreSlim gameLock = new SemaphoreSlim(1, 1);
    private readonly Timer? cleanupTimer;
    private bool disposed = false;

    private GameState CurrentGameState;
    private readonly Random random = new Random();
    private readonly object stateLock = new object();
    private readonly IFeaturedGamemodeSelector _featuredGamemodeSelector;

    // Tracks the last completed game round to prevent duplicate natural-end handling.
    private Guid? _lastCompletedGameId;

    // Next game to start naturally after a completed game; consumed once.
    private Gamemodes? _nextFeaturedGamemodeOverride;

    public GameService(
        IDictionary<Gamemodes, IGamemodeHandler> _games,
        ILogger<GameService> _logger,
        IFeaturedGamemodeSelector featuredGamemodeSelector)
    {
        logger = _logger;
        games = _games;
        _featuredGamemodeSelector = featuredGamemodeSelector;
        
        // Initialize cleanup timer for force termination scenarios
        cleanupTimer = new Timer(async _ => await PerformPeriodicCleanup(), null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        
        _ = Task.Run(() => StartGame(Gamemodes.GAMEMODE_CASUAL));
    }

    public void Dispose()
    {
        if (disposed) return;
        
        logger.LogInformation("[Game Service] Disposing game service...");
        
        // Force terminate any running game
        _ = Task.Run(() => ForceTerminateCurrentGame("Service disposal"));
        
        // Clean up resources
        gameLock?.Dispose();
        cleanupTimer?.Dispose();
        
        disposed = true;
        logger.LogInformation("[Game Service] Game service disposed successfully");
    }

    /// <summary>
    /// Thread-safe method to add a message to the current game
    /// </summary>
    public async Task<ErrorOr<bool>> PerformAddActionAsync(SocketMessage msg)
    {
        await gameLock.WaitAsync();
        try
        {
            if (disposed)
            {
                logger.LogWarning("[Game Service] Attempted to add message but service is disposed");
                return Error.Failure("Game service is not available");
            }

            if (currentGame == null)
            {
                // No active game; ignore without blocking the gateway.
                return Error.Failure("No active game");
            }

            if (currentGame.GameState.CurrentState != GameStatus.IN_PROGRESS)
            {
                // Game exists but not in progress; ignore safely.
                return Error.Failure("Game is not in progress");
            }

            return await currentGame.AddMessage(msg);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Game Service] Error adding message to game");
            return Error.Failure($"Failed to add message: {ex.Message}");
        }
        finally
        {
            gameLock.Release();
        }
    }

    /// <summary>
    /// Thread-safe method to remove a message from the current game
    /// </summary>
    public async Task<ErrorOr<bool>> PerformRemoveActionAsync(ulong msgid)
    {
        await gameLock.WaitAsync();
        try
        {
            if (currentGame == null || disposed)
            {
                logger.LogWarning("[Game Service] Attempted to remove message but no current game exists or service is disposed");
                return Error.Failure("Game service is not available");
            }

            currentGame.DeleteMessage(msgid);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Game Service] Error removing message from game");
            return Error.Failure($"Failed to remove message: {ex.Message}");
        }
        finally
        {
            gameLock.Release();
        }
    }

    /// <summary>
    /// Legacy start game method for backward compatibility (not thread-safe)
    /// </summary>
    public async Task StartGame()
    {
        if (currentGame == null || currentGame.GameState.CurrentState != GameStatus.IN_PROGRESS)
        {
            await StartGameWithForceTerminationAsync(Gamemodes.GAMEMODE_CASUAL, "Legacy start game call");
        }
    }

    /// <summary>
    /// Start a new game with force termination of any existing game
    /// </summary>
    public async Task StartGame([Required] Gamemodes Game)
    {
        await StartGameWithForceTerminationAsync(Game, "Manual game start request");
    }

    /// <summary>
    /// Start a random game with force termination (public API, used for manual/legacy calls).
    /// Staff/admin or explicit calls should not be influenced by featured overrides.
    /// </summary>
    public async Task StartRandomGame()
    {
        var gameModes = new[]
        {
            Gamemodes.GAMEMODE_CASUAL,
            Gamemodes.GAMEMODE_LETTER_VOTE,
            Gamemodes.GAMEMODE_REVERSE_SENTENCE
        };
        var selectedGame = gameModes[random.Next(gameModes.Length)];

        logger.LogInformation("[Game Management] Random game selected: {SelectedGame} (explicit random request)", selectedGame);
        await StartGameWithForceTerminationAsync(selectedGame, "Random game selection");
    }

    /// <summary>
    /// End the current game and start a new random game
    /// </summary>
    public ErrorOr<bool> EndGame()
    {
        // Legacy synchronous wrapper.
        // WARNING: This blocks synchronously on async code. Prefer using async flows from gateway/event contexts.
        try
        {
            ErrorOr<bool> result;

            gameLock.Wait();
            try
            {
                var task = EndGameInternalAsync();
                var internalResult = task.GetAwaiter().GetResult();
                result = internalResult.Result;
            }
            finally
            {
                gameLock.Release();
            }

            // NOTE: We intentionally do NOT start the next game here to avoid re-entrancy from sync contexts.
            // Callers that rely on natural chaining should use async APIs instead.
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in legacy EndGame method");
            return Error.Failure($"Failed to end game: {ex.Message}");
        }
    }

    /// <summary>
    /// Start a game with force termination of any existing game.
    /// This is the ONLY entry point for starting a new round.
    /// Always:
    /// - Ends any active game (once).
    /// - Clears currentGame.
    /// - Starts the requested mode.
    /// </summary>
    public async Task StartGameWithForceTerminationAsync([Required] Gamemodes gameMode, string reason = "User requested new game")
    {
        await gameLock.WaitAsync();
        try
        {
            logger.LogInformation("[Game Service] Starting game with force termination. Mode: {Mode}, Reason: {Reason}", gameMode, reason);

            // Always ensure previous round is fully torn down.
            if (currentGame != null)
            {
                await ForceTerminateCurrentGameInternal(reason);
            }

            // Any explicit start (including staff) cancels pending featured override.
            _nextFeaturedGamemodeOverride = null;

            // Reset last-completed tracking when a brand new game is explicitly started.
            // This keeps semantics simple: each StartGameWithForceTerminationAsync creates a new logical round.
            _lastCompletedGameId = null;

            if (!games.TryGetValue(gameMode, out var nextHandler) || nextHandler is null)
            {
                logger.LogError("[Game Service] Failed to get game handler for mode: {Mode}", gameMode);
                currentGame = null;
                return;
            }

            currentGame = nextHandler;

            await currentGame.StartGame();

            if (currentGame.GameState.GameId == Guid.Empty)
            {
                // Guarantee a valid GameId for single-server/single-channel orchestration.
                currentGame.GameState = new GameState
                {
                    GameId = Guid.NewGuid(),
                    CurrentState = GameStatus.IN_PROGRESS
                };
            }

            logger.LogInformation("[Game Service] Successfully started new game: {Mode} (GameId={GameId})",
                gameMode, currentGame.GameState.GameId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Game Service] Error starting game {Mode} with force termination", gameMode);
            currentGame = null;
            throw;
        }
        finally
        {
            gameLock.Release();
        }
    }

    /// <summary>
    /// Force terminate the current game, bypassing normal game logic
    /// </summary>
    public async Task ForceTerminateCurrentGame(string reason = "Force termination")
    {
        await gameLock.WaitAsync();
        try
        {
            await ForceTerminateCurrentGameInternal(reason);
        }
        finally
        {
            gameLock.Release();
        }
    }

    /// <summary>
    /// Internal force termination implementation
    /// </summary>
    private async Task ForceTerminateCurrentGameInternal(string reason)
    {
        if (currentGame == null)
        {
            logger.LogInformation("[Game Service] No current game to terminate");
            return;
        }

        try
        {
            logger.LogInformation($"[Game Service] Force terminating current game. Reason: {reason}");
            
            // Attempt graceful termination first: handler should set GameState to ENDED and not start another game.
            try
            {
                await currentGame.EndGame();
                logger.LogInformation("[Game Service] Graceful termination completed");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Game Service] Graceful termination failed, proceeding with force termination");
            }

            // Clear references to ensure complete cleanup
            currentGame = null;
            
            logger.LogInformation("[Game Service] Force termination completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"[Game Service] Error during force termination: {reason}");
            
            // Force clear reference even if termination failed
            currentGame = null;
            logger.LogWarning("[Game Service] Cleared current game reference despite termination error");
        }
    }

    /// <summary>
    /// Periodic cleanup to handle stuck or orphaned games
    /// </summary>
    private async Task PerformPeriodicCleanup()
    {
        if (disposed) return;
        
        await gameLock.WaitAsync();
        try
        {
            if (currentGame != null &&
                currentGame.GameState.CurrentState == GameStatus.ENDED)
            {
                logger.LogWarning("[Game Service] Detected orphaned game state, performing cleanup");
                currentGame = null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Game Service] Error during periodic cleanup");
        }
        finally
        {
            gameLock.Release();
        }
    }

    /// <summary>
    /// Check if a game is currently running
    /// </summary>
    public bool IsGameRunning()
    {
        lock (stateLock)
        {
            return currentGame != null &&
                   currentGame.GameState.CurrentState == GameStatus.IN_PROGRESS;
        }
    }

    /// <summary>
    /// Get the current game mode (if any)
    /// </summary>
    public Gamemodes? GetCurrentGameMode()
    {
        lock (stateLock)
        {
            if (currentGame == null) return null;
            
            // Determine game mode by matching handler
            foreach (var game in games)
            {
                if (game.Value == currentGame)
                {
                    return game.Key;
                }
            }
            
            return null;
        }
    }

    /// <summary>
    /// Internal async version of EndGame for legacy wrapper.
    /// IMPORTANT:
    /// - Caller is responsible for holding gameLock for the entire duration.
    /// - This method MUST NOT start a new game or re-enter gameLock.
    /// - It only ends the current game and computes what should be started next.
    /// </summary>
    private async Task<(ErrorOr<bool> Result, Gamemodes? NextGamemode)> EndGameInternalAsync()
    {
        if (currentGame == null)
        {
            logger.LogWarning("[Game Service] Attempted to end game but no current game exists");
            return (Error.Failure("No active game"), null);
        }

        try
        {
            // Capture last mode before ending/clearing
            // NOTE: Do not call GetCurrentGameMode() here; it uses stateLock and iterates games.
            // Instead, infer the mode directly from the handler map under the existing gameLock.
            Gamemodes lastMode = Gamemodes.GAMEMODE_CASUAL;
            foreach (var pair in games)
            {
                if (pair.Value == currentGame)
                {
                    lastMode = pair.Key;
                    break;
                }
            }

            // Ask handler to finalize only; it must set GameState.CurrentState = ENDED
            // and MUST NOT start a new game.
            await currentGame.EndGame();

            // Capture completed GameId for idempotency tracking.
            var completedGameId = currentGame.GameState.GameId;
            if (completedGameId != Guid.Empty)
            {
                _lastCompletedGameId = completedGameId;
            }

            // Clear current game; GameState is authoritative and now ENDED.
            currentGame = null;

            // One-round featured selection via selector (natural lifecycle only)
            var featured = _featuredGamemodeSelector.TrySelectFeaturedGamemode(lastMode);
            if (featured.HasValue)
            {
                _nextFeaturedGamemodeOverride = featured.Value;
                logger.LogInformation("[Game Service] Featured gamemode selected for next round: {FeaturedGamemode}", featured.Value);
            }

            // Decide next natural game (featured override if present, else random)
            Gamemodes? nextGamemode = null;

            if (_nextFeaturedGamemodeOverride.HasValue)
            {
                nextGamemode = _nextFeaturedGamemodeOverride.Value;
            }
            else
            {
                var gameModes = new[]
                {
                    Gamemodes.GAMEMODE_CASUAL,
                    Gamemodes.GAMEMODE_LETTER_VOTE,
                    Gamemodes.GAMEMODE_REVERSE_SENTENCE
                };

                nextGamemode = gameModes[random.Next(gameModes.Length)];
            }

            return (true, nextGamemode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Game Service] Error ending game");
            return (Error.Failure($"Failed to end game: {ex.Message}"), null);
        }
    }

    /// <summary>
    /// Centralized lifecycle hook for natural game completion.
    /// Called by notification handlers once a game has logically ended and
    /// been announced (e.g., via GameEndedNotification).
    /// </summary>
    public async Task<ErrorOr<bool>> EndGameFromNaturalFlowAsync(GameState finalState, string endMessage)
    {
        logger.LogTrace("[Game Service] Starting natural end-of-game transition");

        Gamemodes? nextGamemode = null;
        ErrorOr<bool> result;

        await gameLock.WaitAsync();
        try
        {
            // Reject obviously invalid signals early
            if (finalState.GameId == Guid.Empty)
            {
                logger.LogWarning("[Game Service] Natural end signal missing GameId; ignoring.");
                return Error.Failure("Invalid game id");
            }

            // Idempotency: if we have already completed this GameId, ignore duplicates.
            if (_lastCompletedGameId.HasValue && _lastCompletedGameId.Value == finalState.GameId)
            {
                logger.LogInformation(
                    "[Game Service] Natural end called for already completed game {GameId}; no action taken.",
                    finalState.GameId);
                return true;
            }

            if (currentGame == null)
            {
                logger.LogWarning(
                    "[Game Service] Natural end called for GameId={GameId} but no current game exists; treating as already finalized.",
                    finalState.GameId);
                _lastCompletedGameId = finalState.GameId;
                return true;
            }

            var activeState = currentGame.GameState;

            // Ensure the signal corresponds to the active game round where possible.
            if (activeState.GameId != Guid.Empty &&
                finalState.GameId != Guid.Empty &&
                activeState.GameId != finalState.GameId)
            {
                logger.LogWarning(
                    "[Game Service] Natural end signal ignored: GameId mismatch. Active={ActiveId}, Final={FinalId}",
                    activeState.GameId, finalState.GameId);
                return Error.Failure("Mismatched game id");
            }

            // If the handler already reports ENDED and we don't have a currentGame anymore,
            // treat it as already finalized.
            if (activeState.CurrentState == GameStatus.ENDED && currentGame == null)
            {
                logger.LogInformation("[Game Service] Natural end called but already finalized; no action taken.");
                _lastCompletedGameId = finalState.GameId;
                return true;
            }

            // Ensure the handler is marked ENDED before running the internal pipeline.
            if (currentGame.GameState.CurrentState != GameStatus.ENDED)
            {
                currentGame.GameState = new GameState
                {
                    GameId = activeState.GameId == Guid.Empty
                        ? finalState.GameId
                        : activeState.GameId,
                    CurrentState = GameStatus.ENDED
                };
            }

            logger.LogInformation("[Game Service] Processing natural game end for GameId={GameId}: {Message}", finalState.GameId, endMessage);

            // Run the shared pipeline under the lock to end and decide next, but do NOT start it yet.
            var internalResult = await EndGameInternalAsync();
            result = internalResult.Result;
            nextGamemode = internalResult.NextGamemode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Game Service] Error in EndGameFromNaturalFlowAsync");
            return Error.Failure($"Failed to end game from natural flow: {ex.Message}");
        }
        finally
        {
            gameLock.Release();
        }

        // After releasing the lock, start the next game if one was selected.
        if (result.IsError)
        {
            return result;
        }

        if (nextGamemode.HasValue)
        {
            logger.LogTrace("[Game Service] Starting next natural game after lock release: {Gamemode}", nextGamemode.Value);

            // Always start via the single entry point; this will:
            // - respect that previous game is fully ended
            // - start the next game for our single server/channel
            if (_nextFeaturedGamemodeOverride.HasValue &&
                _nextFeaturedGamemodeOverride.Value == nextGamemode.Value)
            {
                _nextFeaturedGamemodeOverride = null;
                await StartGameWithForceTerminationAsync(nextGamemode.Value, "Featured gamemode one-round override (natural flow)");
            }
            else
            {
                await StartGameWithForceTerminationAsync(nextGamemode.Value, "Natural random game selection");
            }
        }

        return result;
    }

    /// <summary>
    /// Legacy PerformAddAction method for backward compatibility
    /// </summary>
    public ErrorOr<bool> PerformAddAction(SocketMessage msg)
    {
        try
        {
            var task = PerformAddActionAsync(msg);
            return task.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in legacy PerformAddAction method");
            return Error.Failure($"Failed to add action: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy PerformRemoveAction method for backward compatibility
    /// </summary>
    public ErrorOr<bool> PerformRemoveAction(ulong msgid)
    {
        try
        {
            var task = PerformRemoveActionAsync(msgid);
            return task.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in legacy PerformRemoveAction method");
            return Error.Failure($"Failed to remove action: {ex.Message}");
        }
    }

    public ErrorOr<bool> PerformUserAction()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Start the next "natural" game after a completed round.
    /// Respects a one-round featured override if set; otherwise uses prior random behavior.
    /// </summary>
    private async Task StartNextNaturalGameAsync()
    {
        // Use featured override exactly once if present
        if (_nextFeaturedGamemodeOverride.HasValue)
        {
            var modeToStart = _nextFeaturedGamemodeOverride.Value;
            _nextFeaturedGamemodeOverride = null; // consume
            logger.LogInformation("[Game Service] Starting next game using featured override: {Gamemode}", modeToStart);
            await StartGameWithForceTerminationAsync(modeToStart, "Featured gamemode one-round override (natural flow)");
            return;
        }

        // Fallback: replicate previous StartRandomGame behavior
        var gameModes = new[]
        {
            Gamemodes.GAMEMODE_CASUAL,
            Gamemodes.GAMEMODE_LETTER_VOTE,
            Gamemodes.GAMEMODE_REVERSE_SENTENCE
        };

        var selectedGame = gameModes[random.Next(gameModes.Length)];
        logger.LogInformation("[Game Management] Random game selected (natural flow): {SelectedGame}", selectedGame);

        await StartGameWithForceTerminationAsync(selectedGame, "Natural random game selection");
    }
}
