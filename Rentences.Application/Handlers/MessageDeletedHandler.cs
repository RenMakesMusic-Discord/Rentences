using Microsoft.Extensions.Logging;
using Rentences.Application.Services;

public class MessageDeletedHandler : IRequestHandler<MessageDeletedCommand, MessageDeletedResponse>
{


    private readonly ILogger<MessageDeletedHandler> logger;
    private readonly IGameService gameService;

    public MessageDeletedHandler(ILogger<MessageDeletedHandler> _logger, IGameService gameService)
    {
        logger = _logger;
        this.gameService = gameService;
    }
    public Task<MessageDeletedResponse> Handle(MessageDeletedCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Deleted the following message [" + request.messageId + "] ");
        gameService.PerformRemoveAction(request.messageId);

        MessageDeletedResponse response = new() { };
        return Task.FromResult(response);
    }

}
