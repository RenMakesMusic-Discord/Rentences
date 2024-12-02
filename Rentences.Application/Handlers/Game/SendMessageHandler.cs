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
        // Use the discordConfig to send the start message
        await _discord.SendMessageAsync(ulong.Parse(_discordConfig.ChannelId), request.Message);
        return true;
    }
}

