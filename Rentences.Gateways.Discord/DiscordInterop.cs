using Discord;
using Discord.WebSocket;
using ErrorOr;
using Microsoft.Extensions.Options;
using Rentences.Domain.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class DiscordInterop {
    private readonly DiscordSocketClient client;
    private readonly DiscordConfiguration configuration;

    private bool isReady = false;

    public DiscordInterop(DiscordConfiguration options, DiscordSocketClient _listnerClient) {
        configuration = options;
        client = _listnerClient;
        client.Log += LogAsync;

        if (client.LoginState == LoginState.LoggedOut) {
            _ = Authenticate();
        }

        client.Ready += OnReadyAsync;
    }

    public async Task<ErrorOr<bool>> Authenticate() {
        try {
            await client.LoginAsync(TokenType.Bearer, configuration.Token, true);
            await client.StartAsync();

            return true;
        }
        catch (Exception ex) {
            return Error.Failure(ex.Message);
        }
    }

    private Task LogAsync(LogMessage log) {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    private Task OnReadyAsync() {
        Console.WriteLine($"{client.CurrentUser} is connected!");
        isReady = true;
        return Task.CompletedTask;
    }

    public async Task<ErrorOr<bool>> SendMessageAsync(ulong threadId, Embed message) {
        while (!isReady) {
            await Task.Delay(1000);
            continue;
        }

        try {
            var thread = GetThreadChannel(threadId);
            if (thread == null) return Error.Failure("Thread not found");

            await thread.SendMessageAsync(allowedMentions: AllowedMentions.All, embed: message);
            return true;
        }
        catch (Exception ex) {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<ErrorOr<bool>> SendMessageAsync(ulong threadId, string message) {
        while (!isReady) {
            await Task.Delay(1000);
            continue;
        }

        try {
            var thread = GetThreadChannel(threadId);
            if (thread == null) return Error.Failure("Thread not found");

            await thread.SendMessageAsync(message);
            return true;
        }
        catch (Exception ex) {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<ErrorOr<bool>> DeleteMessageAsync(ulong threadId, ulong messageId) {
        try {
            var message = await GetMessageAsync(threadId, messageId);
            if (message == null) return Error.Failure("Message not found");

            await message.DeleteAsync();
            return true;
        }
        catch (Exception ex) {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<ErrorOr<bool>> DeleteMessagesAsync(ulong threadId, int limit) {
        try {
            var thread = GetThreadChannel(threadId);
            if (thread == null) return Error.Failure("Thread not found");

            var messages = await thread.GetMessagesAsync(limit).FlattenAsync();
            await thread.DeleteMessagesAsync(messages);
            return true;
        }
        catch (Exception ex) {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<ErrorOr<bool>> EditMessageAsync(ulong threadId, ulong messageId, string newContent) {
        try {
            var message = await GetUserMessageAsync(threadId, messageId);
            if (message == null) return Error.Failure("Message not found");

            await message.ModifyAsync(msg => msg.Content = newContent);
            return true;
        }
        catch (Exception ex) {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<IMessage> GetMessageAsync(ulong threadId, ulong messageId) {
        try {
            var thread = GetThreadChannel(threadId);
            if (thread == null) return null;

            var message = await thread.GetMessageAsync(messageId);
            return message != null ? message : null;
        }
        catch (Exception ex) {
            return null;
        }
    }

    public async Task<IEnumerable<IMessage>> GetMessagesAsync(ulong threadId, int limit = 100) {
        try {
            var thread = GetThreadChannel(threadId);
            if (thread == null) return null;

            var messages = await thread.GetMessagesAsync(limit).FlattenAsync();
            return messages;
        }
        catch (Exception ex) {
            return null;
        }
    }

    public async Task<ErrorOr<bool>> AddReactionAsync(ulong threadId, ulong messageId, Rentences.Domain.Definitions.Emote emoji) {
        try {
            var message = await GetMessageAsync(threadId, messageId);
            if (message == null) return Error.Failure("Message not found");

            if (emoji.IsEmoji == true)
                await message.AddReactionAsync(Emoji.Parse(emoji.Contents));
            else
                await message.AddReactionAsync(Discord.Emote.Parse(emoji.Contents));
            return true;
        }
        catch (Exception ex) {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<ErrorOr<bool>> RemoveReactionAsync(ulong threadId, ulong messageId, IEmote emote, ulong userId) {
        try {
            var message = await GetMessageAsync(threadId, messageId);
            if (message == null) return Error.Failure("Message not found");

            var user = client.GetUser(userId);
            if (user == null) return Error.Failure("User not found");

            await message.RemoveReactionAsync(emote, user);
            return true;
        }
        catch (Exception ex) {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<IEnumerable<IUser>> GetReactionsAsync(ulong threadId, ulong messageId, IEmote emote) {
        try {
            var message = await GetMessageAsync(threadId, messageId);
            if (message == null) return null;

            var users = await message.GetReactionUsersAsync(emote, int.MaxValue).FlattenAsync();
            return users;
        }
        catch (Exception ex) {
            return null;
        }
    }

    private IThreadChannel GetThreadChannel(ulong threadId) {
        return client.GetChannel(threadId) as IThreadChannel;
    }

    private async Task<IUserMessage> GetUserMessageAsync(ulong threadId, ulong messageId) {
        var message = await GetMessageAsync(threadId, messageId);
        return message as IUserMessage;
    }
}
