

using Microsoft.Extensions.Options;
using Rentences.Domain.Definitions;
using System.Reactive;
using Rentences.Application.Services;

namespace Rentences.Application.Handlers;

internal class GameEndedNotificationHandler(
    DiscordInterop discord,
    IOptions<DiscordConfiguration> discordConfig,
    IGameService gameService) : IRequestHandler<GameEndedNotification, GameEndedNotificationResponse>
{
    private readonly IOptions<DiscordConfiguration> _discordConfig = discordConfig;
    private readonly DiscordInterop _discord = discord;
    private readonly IGameService _gameService = gameService;

    async Task<GameEndedNotificationResponse> IRequestHandler<GameEndedNotification, GameEndedNotificationResponse>.Handle(GameEndedNotification request, CancellationToken cancellationToken)
    {
        // 1) Announce logical end of the game to players.
        await _discord.SendMessageAsync(ulong.Parse(_discordConfig.Value.ChannelId), request.EndMessage);

        // 2) Delegate lifecycle completion + auto-start to GameService.
        //    This is the single orchestrated path for natural game completion.
        await _gameService.EndGameFromNaturalFlowAsync(request.GameState, request.EndMessage);

        return new();
    }
}
