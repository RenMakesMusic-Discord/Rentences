

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
        // Always use the ChannelId from appsettings (DiscordConfiguration).
        var configuredChannelId = _discordConfig.Value.ChannelId;

        if (string.IsNullOrWhiteSpace(configuredChannelId))
        {
            throw new InvalidOperationException("Discord ChannelId is not configured. Cannot send game ended notification.");
        }

        if (!ulong.TryParse(configuredChannelId, out var channelId))
        {
            throw new InvalidOperationException($"Discord ChannelId '{configuredChannelId}' is invalid. Cannot send game ended notification.");
        }

        await _discord.SendMessageAsync(channelId, request.EndMessage);

        // Delegate lifecycle completion + auto-start to GameService.
        await _gameService.EndGameFromNaturalFlowAsync(request.GameState, request.EndMessage);

        return new();
    }
}
