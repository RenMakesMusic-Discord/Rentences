using ErrorOr;
using Discord.WebSocket;

namespace Rentences.Application;
public interface IInterop
{
    public Task<ErrorOr<MessageReceivedResponse>> SendMessageReceivedCommand(MessageReceivedCommand command);
    public Task<ErrorOr<MessageDeletedResponse>> SendMessageDeletedCommand(MessageDeletedCommand command);
    public Task SendGameStartedNotification(GameStartedNotification command);
    public Task<ErrorOr<bool>> SendMessage(SendDiscordMessage command);
    public Task<ErrorOr<GameMessageReactionResponse>> SendGameMessageReaction(GameMessageReactionCommand command);
    
    public Task<WakeGameResponse> WakeGame(WakeGameCommand command);
    public Task ExecuteCommand(SocketMessage message);
    public Task<string> GetLeaderboard();
}
