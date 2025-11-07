using Discord;
using Discord.WebSocket;
using ErrorOr;
using Microsoft.Extensions.Options;
using Rentences.Domain.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
public class DiscordInterop
{
    private readonly DiscordSocketClient client;
    private readonly DiscordConfiguration configuration;

    private bool isReady = false;
    public DiscordInterop(DiscordConfiguration options, DiscordSocketClient _listnerClient)
    {
        configuration = options;
        client = _listnerClient;
        client.Log += LogAsync;

        if (client.LoginState == LoginState.LoggedOut)
        {
            _ = Authenticate();
        }

        client.Ready += OnReadyAsync;
        
    }

    public async Task<ErrorOr<bool>> Authenticate()
    {
        try
        {
            await client.LoginAsync(TokenType.Bearer, configuration.Token, true);
            await client.StartAsync();

            return true;
        }
        catch (Exception ex)
        {
            return Error.Failure(ex.Message);
        }
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    private Task OnReadyAsync()
    {
        Console.WriteLine($"{client.CurrentUser} is connected!");
        isReady = true;
        return Task.CompletedTask;
    }
    public async Task<ErrorOr<bool>> SendMessageAsync(ulong channelId, Embed message)
    {
        while (!isReady)
        {
            await Task.Delay(1000);
            continue;
        }

        try
        {
            var channel = GetMessageChannel(channelId);
            if (channel == null) return Error.Failure("Channel not found");

            await channel.SendMessageAsync(allowedMentions: AllowedMentions.All, embed: message);
            return true;
        }
        catch (Exception ex)
        {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<ErrorOr<ulong>> SendMessageAsync(ulong channelId, string message)
    {
        while (!isReady)
        {
            await Task.Delay(1000);
            continue;
        }

        try
        {
            var channel = GetMessageChannel(channelId);
            if (channel == null) return Error.Failure("Channel not found");

            var sentMessage = await channel.SendMessageAsync(message);
            return sentMessage.Id;
        }
        catch (Exception ex)
        {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<ErrorOr<bool>> DeleteMessageAsync(ulong channelId, ulong messageId)
    {
        try
        {
            var message = await GetMessageAsync(channelId, messageId);
            if (message == null) return Error.Failure("Message not found");

            await message.DeleteAsync();
            return true;
        }
        catch (Exception ex)
        {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<ErrorOr<bool>> DeleteMessagesAsync(ulong channelId, int limit)
    {
        try
        {
            var channel = GetTextChannel(channelId);
            if (channel == null) return Error.Failure("Channel not found");

            var messages = await channel.GetMessagesAsync(limit).FlattenAsync();
            await channel.DeleteMessagesAsync(messages);
            return true;
        }
        catch (Exception ex)
        {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<ErrorOr<bool>> EditMessageAsync(ulong channelId, ulong messageId, string newContent)
    {
        try
        {
            var message = await GetUserMessageAsync(channelId, messageId);
            if (message == null) return Error.Failure("Message not found");

            await message.ModifyAsync(msg => msg.Content = newContent);
            return true;
        }
        catch (Exception ex)
        {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<IMessage> GetMessageAsync(ulong channelId, ulong messageId)
    {
        try
        {
            var channel = GetMessageChannel(channelId);
            if (channel == null) return null;

            var message = await channel.GetMessageAsync(messageId);
            return message != null ? message : null;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public async Task<IEnumerable<IMessage>> GetMessagesAsync(ulong channelId, int limit = 100)
    {
        try
        {
            var channel = GetMessageChannel(channelId);
            if (channel == null) return null;

            var messages = await channel.GetMessagesAsync(limit).FlattenAsync();
            return messages;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public async Task<ErrorOr<bool>> AddReactionAsync(ulong channelId, ulong messageId, Rentences.Domain.Definitions.Emote emoji)
    {
        try
        {
            var message = await GetMessageAsync(channelId, messageId);
            if (message == null) return Error.Failure("Message not found");

            if (emoji.IsEmoji == true)
                await message.AddReactionAsync(Emoji.Parse(emoji.Contents));
            else
                await message.AddReactionAsync(Discord.Emote.Parse(emoji.Contents));
            return true;
        }
        catch (Exception ex)
        {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<ErrorOr<bool>> RemoveReactionAsync(ulong channelId, ulong messageId, IEmote emote, ulong userId)
    {
        try
        {
            var message = await GetMessageAsync(channelId, messageId);
            if (message == null) return Error.Failure("Message not found");

            var user = client.GetUser(userId);
            if (user == null) return Error.Failure("User not found");

            await message.RemoveReactionAsync(emote, user);
            return true;
        }
        catch (Exception ex)
        {
            return Error.Failure(ex.Message);
        }
    }

    public async Task<IEnumerable<IUser>> GetReactionsAsync(ulong channelId, ulong messageId, IEmote emote)
    {
        try
        {
            var message = await GetMessageAsync(channelId, messageId);
            if (message == null) return null;

            var users = await message.GetReactionUsersAsync(emote, int.MaxValue).FlattenAsync();
            return users;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    // Wrapper methods for IInterop interface
    public async Task<ErrorOr<bool>> AddReactionToMessage(ulong channelId, ulong messageId, Rentences.Domain.Definitions.Emote emoji)
    {
        return await AddReactionAsync(channelId, messageId, emoji);
    }

    public async Task<ErrorOr<IEnumerable<IUser>>> GetReactionsForMessage(ulong channelId, ulong messageId, Rentences.Domain.Definitions.Emote emoji)
    {
        try
        {
            IEmote emote = emoji.IsEmoji == true ? Emoji.Parse(emoji.Contents) : Discord.Emote.Parse(emoji.Contents);
            var users = await GetReactionsAsync(channelId, messageId, emote);
            if (users == null)
            {
                return new List<IUser>();
            }
            return users.ToList();
        }
        catch (Exception ex)
        {
            return Error.Failure(ex.Message);
        }
    }

    private IMessageChannel GetMessageChannel(ulong channelId)
    {
        return client.GetChannel(channelId) as IMessageChannel;
    }

    private ITextChannel GetTextChannel(ulong channelId)
    {
        return client.GetChannel(channelId) as ITextChannel;
    }

    private async Task<IUserMessage> GetUserMessageAsync(ulong channelId, ulong messageId)
    {
        var message = await GetMessageAsync(channelId, messageId);
        return message as IUserMessage;
    }
}
