

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
        if (string.IsNullOrWhiteSpace(_discordConfig.Value.ChannelId))
        {
            throw new InvalidOperationException("Discord ChannelId is not configured. Cannot send game ended notification.");
        }

        if (!ulong.TryParse(_discordConfig.Value.ChannelId, out var channelId))
        {
            throw new InvalidOperationException($"Discord ChannelId '{_discordConfig.Value.ChannelId}' is invalid. Cannot send game ended notification.");
        }

        await _discord.SendMessageAsync(channelId, request.EndMessage);

        // 2) Delegate lifecycle completion + auto-start to GameService.
        //    This is the single orchestrated path for natural game completion.
        await _gameService.EndGameFromNaturalFlowAsync(request.GameState, request.EndMessage);

        return new();
    }
}
