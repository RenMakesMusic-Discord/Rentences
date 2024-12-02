using MediatR;
using System.Threading;
using System.Threading.Tasks;

public class ExecuteCommandHandler : IRequestHandler<ExecuteCommand, Unit>
{
    private readonly CommandHandler _commandHandler;

    public ExecuteCommandHandler(CommandHandler commandHandler)
    {
        _commandHandler = commandHandler;
    }

    public async Task<Unit> Handle(ExecuteCommand request, CancellationToken cancellationToken)
    {
        await _commandHandler.HandleMessageAsync(request.Message);
        return Unit.Value;
    }
}
