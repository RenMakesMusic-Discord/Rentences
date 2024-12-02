using Discord.WebSocket;

public interface ICommandService
{
    Task ProcessCommandAsync(string[] args, SocketMessage message);
    string CommandName { get; }
}
