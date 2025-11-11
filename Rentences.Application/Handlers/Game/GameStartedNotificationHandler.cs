using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Rentences.Domain.Definitions;
using Rentences.Gateways.DiscordClient;

namespace Rentences.Application.Handlers;

public class GameStartedNotificationHandler(
    DiscordInterop discord,
    DiscordConfiguration discordConfig) : INotificationHandler<GameStartedNotification>
{
    private readonly DiscordInterop _discord = discord ?? throw new ArgumentNullException(nameof(discord));
    private readonly DiscordConfiguration _discordConfig = discordConfig ?? throw new ArgumentNullException(nameof(discordConfig));

    public async Task Handle(GameStartedNotification notification, CancellationToken cancellationToken)
    {
        // Always resolve ChannelId exclusively from DiscordConfiguration (appsettings.json binding).
        var configuredChannelId = _discordConfig.ChannelId;

        if (string.IsNullOrWhiteSpace(configuredChannelId))
        {
            throw new InvalidOperationException("Discord ChannelId is not configured. Ensure 'DiscordConfiguration:ChannelId' is set in appsettings.json.");
        }

        if (!ulong.TryParse(configuredChannelId, out var channelId))
        {
            throw new InvalidOperationException($"Discord ChannelId '{configuredChannelId}' is invalid. Ensure 'DiscordConfiguration:ChannelId' is a valid ulong.");
        }

        // Use strongly-typed configuration-based ChannelId; no overrides or fallbacks.
        await _discord.SendMessageAsync(channelId, notification.StartMessage);
    }
}

