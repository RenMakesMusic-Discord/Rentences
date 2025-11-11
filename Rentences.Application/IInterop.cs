using ErrorOr;
using Discord.WebSocket;
using Discord;
using Rentences.Domain.Definitions;

namespace Rentences.Application;
public interface IInterop
{
    public Task<ErrorOr<MessageReceivedResponse>> SendMessageReceivedCommand(MessageReceivedCommand command);
    public Task<ErrorOr<MessageDeletedResponse>> SendMessageDeletedCommand(MessageDeletedCommand command);
    public Task SendGameStartedNotification(GameStartedNotification command);
    public Task<GameEndedNotificationResponse> SendGameEndedNotification(GameEndedNotification notification);
    public Task<ErrorOr<ulong>> SendMessage(SendDiscordMessage command);
    public Task<ErrorOr<bool>> SendMessageWithEmbed(SendDiscordMessageWithEmbed command);
    public Task<ErrorOr<GameMessageReactionResponse>> SendGameMessageReaction(GameMessageReactionCommand command);
    
    public Task<WakeGameResponse> WakeGame(WakeGameCommand command);
    public Task ExecuteCommand(SocketMessage message);
    public Task<string> GetLeaderboard();
    public DiscordConfiguration GetDiscordConfiguration();
    public Task<ErrorOr<bool>> AddReactionToMessage(ulong channelId, ulong messageId, Rentences.Domain.Definitions.Emote emoji);
    public Task<ErrorOr<IEnumerable<IUser>>> GetReactionsForMessage(ulong channelId, ulong messageId, Rentences.Domain.Definitions.Emote emoji);
}
