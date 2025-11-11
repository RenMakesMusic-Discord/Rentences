

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
    private readonly IOptions<DiscordConfiguration> _discordConfig = discordConfig ?? throw new ArgumentNullException(nameof(discordConfig));
    private readonly DiscordInterop _discord = discord ?? throw new ArgumentNullException(nameof(discord));
    private readonly IGameService _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));

    async Task<GameEndedNotificationResponse> IRequestHandler<GameEndedNotification, GameEndedNotificationResponse>.Handle(GameEndedNotification request, CancellationToken cancellationToken)
    {
        // Always use the ChannelId from appsettings via DiscordConfiguration; this is the single source of truth.
        var configuredChannelId = _discordConfig.Value.ChannelId;

        if (string.IsNullOrWhiteSpace(configuredChannelId))
        {
            throw new InvalidOperationException("Discord ChannelId is not configured. Ensure 'DiscordConfiguration:ChannelId' is set in appsettings.json.");
        }

        if (!ulong.TryParse(configuredChannelId, out var channelId))
        {
            throw new InvalidOperationException($"Discord ChannelId '{configuredChannelId}' is invalid. Ensure 'DiscordConfiguration:ChannelId' is a valid ulong.");
        }

        // Send the end message strictly using the configuration-derived ChannelId.
        await _discord.SendMessageAsync(channelId, request.EndMessage);

        // Delegate lifecycle completion + auto-start to GameService.
        await _gameService.EndGameFromNaturalFlowAsync(request.GameState, request.EndMessage);

        return new();
    }
}
