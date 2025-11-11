using Discord.WebSocket;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Rentences.Application.Services.Game;

namespace Rentences.Application.Services;

// Simplified single-channel GameService.
//
// Design:
// - Single Discord server / single channel.
// - Exactly one active game handler (or none) at any time.
// - No SemaphoreSlim, no timers, no Task.Run orchestration.
// - Game modes NEVER start the next game; they only:
//     * manage their own internal state
//     * emit GameEndedNotification via mediator/backend when they end.
// - GameService is the sole orchestrator for:
//     * starting a game
//     * force-ending a game
//     * reacting to GameEndedNotification (natural end)
// - API remains async-friendly but logic is straightforward.
// - We accept that MediatR/Discord.NET events for this single instance
//   provide sufficient ordering; we do not attempt heavy concurrency control.

public class GameService : IGameService
{
    private readonly IDictionary<Gamemodes, IGamemodeHandler> _games;
    private readonly ILogger<GameService> _logger;
    private readonly IFeaturedGamemodeSelector _featuredGamemodeSelector;

    // The only mutable game state: one active handler or null.
    private IGamemodeHandler? _currentGame;

    // Tracks last completed game id to make natural-end handling idempotent.
    private Guid? _lastCompletedGameId;

    // Optional one-round featured override for natural flow.
    private Gamemodes? _nextFeaturedGamemodeOverride;

    public GameService(
        IDictionary<Gamemodes, IGamemodeHandler> games,
        ILogger<GameService> logger,
        IFeaturedGamemodeSelector featuredGamemodeSelector)
    {
        _games = games;
        _logger = logger;
        _featuredGamemodeSelector = featuredGamemodeSelector;

        // Start with a Casual game on startup.
        _ = StartGameWithForceTerminationAsync(Gamemodes.GAMEMODE_CASUAL, "Initial startup");
    }

    // MESSAGE HANDLING

    // Called from MessageReceivedHandler.
    public async Task<ErrorOr<bool>> PerformAddActionAsync(SocketMessage msg)
    {
        var game = _currentGame;

        if (game is null)
        {
            return Error.Failure("No active game");
        }

        if (game.GameState.CurrentState != GameStatus.IN_PROGRESS)
        {
            return Error.Failure("Game is not in progress");
        }

        try
        {
            return await game.AddMessage(msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Game Service] Error adding message");
            return Error.Failure($"Failed to add message: {ex.Message}");
        }
    }

    public async Task<ErrorOr<bool>> PerformRemoveActionAsync(ulong msgid)
    {
        var game = _currentGame;

        if (game is null)
        {
            return Error.Failure("No active game");
        }

        try
        {
            return await game.DeleteMessage(msgid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Game Service] Error removing message");
            return Error.Failure($"Failed to remove message: {ex.Message}");
        }
    }

    // Legacy sync wrappers.
    public ErrorOr<bool> PerformAddAction(SocketMessage msg)
        => PerformAddActionAsync(msg).GetAwaiter().GetResult();

    public ErrorOr<bool> PerformRemoveAction(ulong msgid)
        => PerformRemoveActionAsync(msgid).GetAwaiter().GetResult();

    public ErrorOr<bool> PerformUserAction()
        => Error.Failure("Not implemented");

    // START GAME APIs

    // Legacy IGameService.StartGame()
    public async Task StartGame()
    {
        if (_currentGame is null || _currentGame.GameState.CurrentState != GameStatus.IN_PROGRESS)
        {
            await StartGameWithForceTerminationAsync(Gamemodes.GAMEMODE_CASUAL, "Legacy start game call");
        }
    }

    public async Task StartGame(Gamemodes gameMode)
        => await StartGameWithForceTerminationAsync(gameMode, "Manual game start request");

    public async Task StartRandomGame()
    {
        var modes = new[]
        {
            Gamemodes.GAMEMODE_CASUAL,
            Gamemodes.GAMEMODE_LETTER_VOTE,
            Gamemodes.GAMEMODE_REVERSE_SENTENCE
        };

        var selected = modes[new Random().Next(modes.Length)];
        await StartGameWithForceTerminationAsync(selected, "Random game selection");
    }

    // Canonical entrypoint: start game, forcibly ending any existing game first.
    public async Task StartGameWithForceTerminationAsync(Gamemodes gameMode, string reason = "User requested new game")
    {
        _logger.LogInformation("[Game Service] Starting game with force termination. Mode={Mode}, Reason={Reason}", gameMode, reason);

        // End any existing game.
        if (_currentGame is not null)
        {
            await ForceTerminateCurrentGameInternal("Start new game: " + reason);
        }

        // Explicit start overrides any pending featured plan.
        _nextFeaturedGamemodeOverride = null;
        _lastCompletedGameId = null;

        if (!_games.TryGetValue(gameMode, out var handler) || handler is null)
        {
            _logger.LogError("[Game Service] No handler registered for mode {Mode}", gameMode);
            _currentGame = null;
            return;
        }

        _currentGame = handler;

        // Let the handler initialize and send GameStartedNotification.
        await _currentGame.StartGame();

        // Ensure a valid GameId.
        if (_currentGame.GameState.GameId == Guid.Empty)
        {
            _currentGame.GameState = new GameState
            {
                GameId = Guid.NewGuid(),
                CurrentState = GameStatus.IN_PROGRESS
            };
        }

        _logger.LogInformation("[Game Service] Started game {Mode} with GameId={GameId}",
            gameMode, _currentGame.GameState.GameId);
    }

    // FORCE TERMINATION

    public async Task ForceTerminateCurrentGame(string reason = "Force termination")
    {
        await ForceTerminateCurrentGameInternal(reason);
    }

    // Internal force terminate: no recursion, no starting next game automatically.
    private async Task ForceTerminateCurrentGameInternal(string reason)
    {
        var game = _currentGame;
        if (game is null)
        {
            _logger.LogInformation("[Game Service] No current game to terminate");
            return;
        }

        try
        {
            _logger.LogInformation("[Game Service] Force terminating current game. Reason={Reason}", reason);

            try
            {
                // Let handler mark itself ENDED and emit GameEndedNotification once.
                await game.EndGame();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Game Service] Game EndGame() threw during force termination; continuing");
            }

            if (game.GameState.GameId != Guid.Empty)
            {
                _lastCompletedGameId = game.GameState.GameId;
            }

            _currentGame = null;

            _logger.LogInformation("[Game Service] Force termination complete; no active game.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Game Service] Unexpected error during force termination; clearing current game.");
            _currentGame = null;
        }
    }

    // NATURAL END FROM GameEndedNotificationHandler

    // Called when a game mode has ended itself and emitted GameEndedNotification.
    public async Task<ErrorOr<bool>> EndGameFromNaturalFlowAsync(GameState finalState, string endMessage)
    {
        if (finalState.GameId == Guid.Empty)
        {
            _logger.LogWarning("[Game Service] Natural end received with empty GameId; ignoring.");
            return Error.Failure("Invalid game id");
        }

        // Idempotency: ignore if we've already handled this GameId.
        if (_lastCompletedGameId.HasValue && _lastCompletedGameId.Value == finalState.GameId)
        {
            _logger.LogInformation("[Game Service] Natural end for GameId={GameId} already handled; ignoring.", finalState.GameId);
            return true;
        }

        var active = _currentGame;

        // If no active game, treat as stale/already processed.
        if (active is null)
        {
            _logger.LogInformation("[Game Service] Natural end for GameId={GameId} but no active game; marking as completed.", finalState.GameId);
            _lastCompletedGameId = finalState.GameId;
            return true;
        }

        var activeState = active.GameState;

        // If active gameId is set and does not match, this notification is stale/unrelated.
        if (activeState.GameId != Guid.Empty &&
            activeState.GameId != finalState.GameId)
        {
            _logger.LogWarning(
                "[Game Service] Natural end GameId mismatch. Active={ActiveId}, Final={FinalId}. Ignoring.",
                activeState.GameId, finalState.GameId);
            return Error.Failure("Mismatched game id");
        }

        // Consider this the authoritative end.
        _logger.LogInformation("[Game Service] Finalizing natural end for GameId={GameId}: {Message}",
            finalState.GameId, endMessage);

        active.GameState = new GameState
        {
            GameId = finalState.GameId,
            CurrentState = GameStatus.ENDED
        };

        _lastCompletedGameId = finalState.GameId;
        _currentGame = null;

        // Decide next mode and start it.
        var next = SelectNextGamemode(active);
        if (next.HasValue)
        {
            await StartGameWithForceTerminationAsync(next.Value, "Natural flow auto-next");
        }

        return true;
    }

    // SIMPLE NEXT-GAME SELECTION

    private Gamemodes? SelectNextGamemode(IGamemodeHandler previous)
    {
        var previousMode = Gamemodes.GAMEMODE_CASUAL;

        foreach (var kvp in _games)
        {
            if (ReferenceEquals(kvp.Value, previous))
            {
                previousMode = kvp.Key;
                break;
            }
        }

        // Check for a featured override.
        var featured = _featuredGamemodeSelector.TrySelectFeaturedGamemode(previousMode);
        if (featured.HasValue)
        {
            _nextFeaturedGamemodeOverride = featured.Value;
        }

        if (_nextFeaturedGamemodeOverride.HasValue)
        {
            var mode = _nextFeaturedGamemodeOverride.Value;
            _nextFeaturedGamemodeOverride = null;
            return mode;
        }

        // Fallback: random from registered modes.
        var modes = _games.Keys.ToArray();
        if (modes.Length == 0)
        {
            _logger.LogError("[Game Service] No registered game modes; cannot select next game.");
            return null;
        }

        return modes[new Random().Next(modes.Length)];
    }

    // HELPERS / DIAGNOSTICS

    public bool IsGameRunning()
        => _currentGame is not null && _currentGame.GameState.CurrentState == GameStatus.IN_PROGRESS;

    public Gamemodes? GetCurrentGameMode()
    {
        var game = _currentGame;
        if (game is null) return null;

        foreach (var kvp in _games)
        {
            if (ReferenceEquals(kvp.Value, game))
            {
                return kvp.Key;
            }
        }

        return null;
    }

    // Legacy synchronous EndGame; uses natural flow.
    public ErrorOr<bool> EndGame()
    {
        var game = _currentGame;
        if (game is null)
        {
            return Error.Failure("No active game");
        }

        var state = game.GameState;
        if (state.GameId == Guid.Empty)
        {
            state.GameId = Guid.NewGuid();
        }

        return EndGameFromNaturalFlowAsync(state, "Legacy EndGame() call")
            .GetAwaiter()
            .GetResult();
    }
}