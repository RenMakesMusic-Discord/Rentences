using Discord;
using Discord.WebSocket;
using ErrorOr;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace Rentences.Application.Services;
public class GameService : IGameService
{
    private readonly IDictionary<Gamemodes, IGamemodeHandler> games;
    private IGamemodeHandler currentGame;
    
    private readonly ILogger<GameService> logger;

    private GameState CurrentGameState;

    public GameService(IDictionary<Gamemodes, IGamemodeHandler> _games, ILogger<GameService> _logger)
    {
        logger = _logger;
        games = _games;
        StartGame(Gamemodes.GAMEMODE_CASUAL);
    }
    public ErrorOr<bool> PerformAddAction(SocketMessage msg)
    {
        currentGame.AddMessage(msg);
        return true;
    }
    public ErrorOr<bool> PerformRemoveAction(ulong msgid)
    {
        currentGame.DeleteMessage(msgid);
        return true;
    }
    public async Task StartGame()
    {
        if (currentGame == null || currentGame.GameState.CurrentState.Equals(GameStatus.IN_PROGRESS)) return;
        await currentGame.StartGame();
    }

    public async Task StartGame([Required]Gamemodes Game)
    {
        logger.LogInformation("[Game Management] A new Game has been requested to start");
        if (currentGame != null && 
            currentGame.GameState.CurrentState.Equals(GameStatus.IN_PROGRESS)) return;

        currentGame = games[Game];
        await currentGame.StartGame();
        return;
    }
    public ErrorOr<bool> EndGame()
    {
        currentGame.EndGame();
        currentGame.StartGame();

        return true;
    }

    public ErrorOr<bool> PerformUserAction()
    {
        throw new NotImplementedException();
    }
}

