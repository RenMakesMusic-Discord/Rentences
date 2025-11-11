using Microsoft.Extensions.Options;
using Rentences.Domain.Definitions;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using System.Reactive;

namespace Rentences.Application.Handlers;
public class SendMessageHandler : IRequestHandler<SendDiscordMessage, ErrorOr<bool>>
{
    private readonly DiscordConfiguration _discordConfig;
    private readonly DiscordInterop _discord;

    public SendMessageHandler(DiscordConfiguration discordConfig, DiscordInterop discord)
    {
        _discordConfig = discordConfig;
        _discord = discord;
    }

    public async Task<ErrorOr<bool>> Handle(SendDiscordMessage request, CancellationToken cancellationToken) {
        // Always resolve ChannelId from configuration as single source of truth.
        var configuredChannelId = _discordConfig.ChannelId;

        if (string.IsNullOrWhiteSpace(configuredChannelId))
        {
            throw new InvalidOperationException("Discord ChannelId is not configured. Ensure 'DiscordConfiguration:ChannelId' is set in appsettings.json.");
        }

        if (!ulong.TryParse(configuredChannelId, out var channelId))
        {
            throw new InvalidOperationException($"Discord ChannelId '{configuredChannelId}' is invalid. Ensure 'DiscordConfiguration:ChannelId' is a valid ulong.");
        }

        await _discord.SendMessageAsync(channelId, request.Message);
        return true;
    }
}

