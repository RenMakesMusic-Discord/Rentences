

using Microsoft.Extensions.Options;
using Rentences.Domain.Definitions;
using System.Reactive;

namespace Rentences.Application.Handlers;

internal class GameEndedNotificationHandler(DiscordInterop discord, IOptions<DiscordConfiguration> discordConfig) : IRequestHandler<GameEndedNotification, GameEndedNotificationResponse>
{
    private readonly IOptions<DiscordConfiguration> _discordConfig = discordConfig;
    private readonly DiscordInterop _discord = discord;

    async Task<GameEndedNotificationResponse> IRequestHandler<GameEndedNotification, GameEndedNotificationResponse>.Handle(GameEndedNotification request, CancellationToken cancellationToken)
    {
        await _discord.SendMessageAsync(ulong.Parse(_discordConfig.Value.ChannelId), request.EndMessage);
        return new();
    }
}
