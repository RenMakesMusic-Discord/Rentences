using Microsoft.Extensions.Hosting;
using Rentences.Application;
using Rentences.Domain.Definitions;

public class DiscordListener : IHostedService
{
    private readonly DiscordSocketClient client;
    private readonly DiscordConfiguration configuration;
    private readonly IInterop interop;

    private bool isReady = false;

    public DiscordListener(DiscordConfiguration options, DiscordSocketClient _listenerClient, IInterop _interop)
    {
        configuration = options;
        client = _listenerClient;
        interop = _interop;
    }

    public async Task Authenticate()
    {
        await client.LoginAsync(Discord.TokenType.Bot, configuration.Token, true);
    }

    public void RegisterListeners()
    {
        #region REG: Startup
        client.Connected += Client_Connected;
        client.LoggedIn += Client_LoggedIn;
        client.LoggedOut += Client_LoggedOut;
        client.Ready += Client_Ready;
        #endregion

        #region REG: Lifetime Commands
        client.MessageReceived += Client_MessageReceived;
        client.MessageDeleted += Client_MessageDeleted;
        client.MessageUpdated += Client_MessageUpdated;
        #endregion

        #region REG: Server Events
        client.UserBanned += Client_UserBanned;
        #endregion
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        RegisterListeners();
        if (client.LoginState == Discord.LoginState.LoggedOut)
        {
            await Authenticate();
        }
        while (!isReady)
        {
            if (client.LoginState != Discord.LoginState.LoggedIn)
                break;
            isReady = true;
        }
        await interop.WakeGame(new());
        EmojiDetector.InitializeAllowedEmojis(client.GetGuild(ulong.Parse(configuration.ServerId)));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        client?.Dispose();
    }

    private Task Client_Ready()
    {
        isReady = true;
        return Task.CompletedTask;
    }

    private Task Client_UserBanned(SocketUser arg1, SocketGuild arg2)
    {
        Console.WriteLine("Received " + arg1.Id);
        return Task.CompletedTask;
    }

    private Task Client_MessageUpdated(Discord.Cacheable<Discord.IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
    {
        if (isListenerChannel(arg2.Channel.Id.ToString()).IsError)
            return Task.CompletedTask;

        Console.WriteLine("Received " + arg1.Value);
        return Task.CompletedTask;
    }

    private async Task Client_MessageDeleted(Discord.Cacheable<Discord.IMessage, ulong> arg1, Discord.Cacheable<Discord.IMessageChannel, ulong> arg2)
    {
        if (isListenerChannel(arg2.Value.Id.ToString()).IsError)
            return;

        await interop.SendMessageDeletedCommand(new() { messageId = arg1.Id });
    }

    private async Task Client_MessageReceived(SocketMessage arg)
    {
        if (isListenerChannel(arg.Channel.Id.ToString()).IsError || arg.Author.IsBot)
            return;

        if (arg.Content.StartsWith("-"))
        {
            await interop.ExecuteCommand(arg);
            return;
        }

        await interop.SendMessageReceivedCommand(new() { message = arg });
    }

    private Task Client_LoggedOut()
    {
        Console.WriteLine("Received ");
        return Task.CompletedTask;
    }

    private Task Client_LoggedIn()
    {
        Console.WriteLine("Received ");
        return Task.CompletedTask;
    }

    private Task Client_Connected()
    {
        Console.WriteLine("Received ");
        return Task.CompletedTask;
    }

    public ErrorOr<bool> isListenerChannel(string channelid)
    {
        if (channelid == configuration.ChannelId)
            return true;

        return Error.Unauthorized();
    }
}
