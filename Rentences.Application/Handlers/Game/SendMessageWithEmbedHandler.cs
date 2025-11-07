using Microsoft.Extensions.Options;
using Rentences.Domain.Definitions;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;

namespace Rentences.Application.Handlers;
public class SendMessageWithEmbedHandler : IRequestHandler<SendDiscordMessageWithEmbed, ErrorOr<bool>>
{
    private readonly DiscordConfiguration _discordConfig;
    private readonly DiscordInterop _discord;

    public SendMessageWithEmbedHandler(DiscordConfiguration discordConfig, DiscordInterop discord)
    {
        _discordConfig = discordConfig;
        _discord = discord;
    }

    public async Task<ErrorOr<bool>> Handle(SendDiscordMessageWithEmbed request, CancellationToken cancellationToken)
    {
        // Use the discordConfig to send the start message
        if (request.Embed is not null)
        {
            await _discord.SendMessageAsync(ulong.Parse(_discordConfig.ChannelId), request.Embed);
        }
        else
        {
            await _discord.SendMessageAsync(ulong.Parse(_discordConfig.ChannelId), request.Message);
        }
        return true;
    }
}