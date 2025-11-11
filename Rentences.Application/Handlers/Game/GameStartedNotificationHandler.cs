using Microsoft.Extensions.Options;
using Rentences.Domain.Definitions;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Rentences.Gateways.DiscordClient;

namespace Rentences.Application.Handlers;

public class GameStartedNotificationHandler : INotificationHandler<GameStartedNotification>
{
    private readonly DiscordConfiguration _discordConfig;
    private readonly DiscordInterop _discord;

    public GameStartedNotificationHandler(DiscordConfiguration discordConfig, DiscordInterop discord)
    {
        _discordConfig = discord ?? throw new ArgumentNullException(nameof(discordConfig));
        _discord = discord ?? throw new ArgumentNullException(nameof(discord));
    }

    public async Task Handle(GameStartedNotification notification, CancellationToken cancellationToken)
    {
        // Always resolve ChannelId exclusively from DiscordConfiguration (appsettings.json binding).
        var configuredChannelId = _discordConfig.ChannelId;

        if (string.IsNullOrWhiteSpace(configuredChannelId))
        {
            throw new InvalidOperationException("Discord ChannelId is not configured. Cannot send game started notification.");
        }

        if (!ulong.TryParse(configuredChannelId, out var channelId))
        {
            throw new InvalidOperationException($"Discord ChannelId '{configuredChannelId}' is invalid. Cannot send game started notification.");
        }

        // Use strongly-typed configuration-based ChannelId; no overrides or fallbacks.
        await _discord.SendMessageAsync(channelId, notification.StartMessage);
    }
}

