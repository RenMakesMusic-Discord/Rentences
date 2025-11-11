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
            if (currentGame == null || disposed)
            {
                logger.LogWarning("[Game Service] Attempted to add message but no current game exists or service is disposed");
                return Error.Failure("Game service is not available");
            }

            if (currentGame.GameState.CurrentState != GameStatus.IN_PROGRESS)
            {
                logger.LogWarning("[Game Service] Attempted to add message but game is not in progress");
                return Error.Failure("Game is not in progress");
            }

            currentGame.AddMessage(msg);
            return true;
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
        // Run synchronously by waiting for the async version
        try
        {
            var task = EndGameInternalAsync();
            return task.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in legacy EndGame method");
            return Error.Failure($"Failed to end game: {ex.Message}");
        }
    }

    /// <summary>
    /// Start a game with force termination of any existing game
    /// </summary>
    public async Task StartGameWithForceTerminationAsync([Required] Gamemodes gameMode, string reason = "User requested new game")
    {
        await gameLock.WaitAsync();
        try
        {
            logger.LogInformation($"[Game Service] Starting game with force termination. Mode: {gameMode}, Reason: {reason}");
            
            // Force terminate any existing game immediately
            if (currentGame != null)
            {
                await ForceTerminateCurrentGameInternal(reason);
            }

            // Staff/admin explicit override cancels any pending featured gamemode
            _nextFeaturedGamemodeOverride = null;

            // Start the new game
            currentGame = games[gameMode];
            if (currentGame != null)
            {
                await currentGame.StartGame();
                logger.LogInformation($"[Game Service] Successfully started new game: {gameMode}");
            }
            else
            {
                logger.LogError($"[Game Service] Failed to get game handler for mode: {gameMode}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"[Game Service] Error starting game {gameMode} with force termination");
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
            
            // Attempt graceful termination first
            try
            {
                currentGame.EndGame();
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
    /// Internal async version of EndGame for legacy wrapper
    /// </summary>
    private async Task<ErrorOr<bool>> EndGameInternalAsync()
    {
        await gameLock.WaitAsync();
        try
        {
            if (currentGame == null)
            {
                logger.LogWarning("[Game Service] Attempted to end game but no current game exists");
                return Error.Failure("No active game");
            }

            // Capture last mode before ending/clearing
            var lastMode = GetCurrentGameMode() ?? Gamemodes.GAMEMODE_CASUAL;

            currentGame.EndGame();

            // Clear current game
            currentGame = null;

            // One-round featured selection via selector (natural lifecycle only)
            var featured = _featuredGamemodeSelector.TrySelectFeaturedGamemode(lastMode);
            if (featured.HasValue)
            {
                _nextFeaturedGamemodeOverride = featured.Value;
                logger.LogInformation("[Game Service] Featured gamemode selected for next round: {FeaturedGamemode}", featured.Value);
            }

            // Start next natural game (featured override if present, else normal behavior)
            await StartNextNaturalGameAsync();

            logger.LogInformation("[Game Service] Game ended and next natural game started");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Game Service] Error ending game");
            return Error.Failure($"Failed to end game: {ex.Message}");
        }
        finally
        {
            gameLock.Release();
        }
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
            await StartGameWithForceTerminationAsync(modeToStart, "Featured gamemode one-round override");
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
