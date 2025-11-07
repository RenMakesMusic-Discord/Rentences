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
        _discordConfig = discordConfig;
        _discord = discord;
    }

    public Task Handle(GameStartedNotification notification, CancellationToken cancellationToken)
    {
        // Use the discordConfig to send the start message
        _discord.SendMessageAsync(ulong.Parse(_discordConfig.ChannelId), notification.StartMessage)
     .GetAwaiter().GetResult();

        return Task.CompletedTask;
    }
}

