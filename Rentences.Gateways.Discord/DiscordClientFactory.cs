using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Rentences.Domain.Definitions;

namespace Rentences.Gateways.DiscordClient;

public static class DiscordClientFactory
{
    public static DiscordSocketClient CreateDiscordClient(string token)
    {
        var config = new DiscordSocketConfig
        {
            // Enable necessary intents: MessageContent, GuildMessages, and GuildMessageReactions
            GatewayIntents = GatewayIntents.GuildMessages |
                             GatewayIntents.GuildMessageReactions |
                             GatewayIntents.MessageContent |
                             GatewayIntents.AllUnprivileged
        };
        var client = new DiscordSocketClient(config);

        client.Log += LogAsync;

        if (client.LoginState == LoginState.LoggedOut)
        {
            _ = Authenticate(client, token);
        }

        client.Ready += OnReadyAsync;

        return client;
    }

    private static async Task Authenticate(DiscordSocketClient client, string token)
    {
        await client.LoginAsync(TokenType.Bot, token, true);
        await client.StartAsync();
    }

    private static Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    private static Task OnReadyAsync()
    {
        Console.WriteLine("Discord client is ready!");
        return Task.CompletedTask;
    }
}
