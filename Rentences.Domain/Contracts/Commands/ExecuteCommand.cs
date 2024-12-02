using Discord.WebSocket;
using MediatR;

public class ExecuteCommand : IRequest<Unit>
{
    public SocketMessage Message { get; set; }

    public ExecuteCommand(SocketMessage message)
    {
        Message = message;
    }
}
