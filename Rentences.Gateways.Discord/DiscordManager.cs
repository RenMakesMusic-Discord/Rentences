


using Discord;
using Discord.WebSocket;
using ErrorOr;
using Microsoft.Extensions.Logging;
namespace Rentences.Gateways.Discord; 
public class DiscordManager {
    private readonly DiscordSocketClient client;
    private readonly ILogger<DiscordManager> logger;
    public DiscordManager(DiscordSocketClient _client, ILogger<DiscordManager> _logger) {
        client = _client;
        logger = _logger;
    }


    public async Task<ErrorOr<bool>> SendMessage(ITextChannel channel, string message) {
        var sendMsg = await channel.SendMessageAsync("Test");
        return true;
    }

}
