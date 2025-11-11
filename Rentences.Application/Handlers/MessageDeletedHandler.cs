using Microsoft.Extensions.Logging;
using Rentences.Application.Services;
using System;
using System.Threading.Tasks;

public class MessageDeletedHandler : IRequestHandler<MessageDeletedCommand, MessageDeletedResponse>
{


    private readonly ILogger<MessageDeletedHandler> logger;
    private readonly IGameService gameService;

    public MessageDeletedHandler(ILogger<MessageDeletedHandler> _logger, IGameService gameService)
    {
        logger = _logger;
        this.gameService = gameService;
    }
    public async Task<MessageDeletedResponse> Handle(MessageDeletedCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Deleted the following message [" + request.messageId + "] ");
        try
        {
            await gameService.PerformRemoveActionAsync(request.messageId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing message in game service");
        }

        MessageDeletedResponse response = new() { };
        return response;
    }

}
