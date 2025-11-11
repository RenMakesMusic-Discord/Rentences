

using Microsoft.Extensions.Logging;
using Rentences.Application.Services;
using System;
using System.Threading.Tasks;

namespace Rentences.Application.Handlers;
public class MessageReceivedHandler : IRequestHandler<MessageReceivedCommand, MessageReceivedResponse> {


    private readonly ILogger<MessageReceivedHandler> logger;
    private readonly IGameService gameService;

    public MessageReceivedHandler(ILogger<MessageReceivedHandler> _logger, IGameService gameService)
    {
        logger = _logger;
        this.gameService = gameService; 
    }
    public async Task<MessageReceivedResponse> Handle(MessageReceivedCommand request, CancellationToken cancellationToken) {
       // logger.LogInformation("Received the following message ["+request.message.Id+"] " + request.message.Content);
        try
        {
            await gameService.PerformAddActionAsync(request.message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message in game service");
        }

        MessageReceivedResponse response = new() { };
        return response;
    }

}
