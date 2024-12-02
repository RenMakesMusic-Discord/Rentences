

using Microsoft.Extensions.Logging;
using Rentences.Application.Services;


namespace Rentences.Application.Handlers;
public class MessageReceivedHandler : IRequestHandler<MessageReceivedCommand, MessageReceivedResponse> {


    private readonly ILogger<MessageReceivedHandler> logger;
    private readonly IGameService gameService;

    public MessageReceivedHandler(ILogger<MessageReceivedHandler> _logger, IGameService gameService)
    {
        logger = _logger;
        this.gameService = gameService; 
    }
    public Task<MessageReceivedResponse> Handle(MessageReceivedCommand request, CancellationToken cancellationToken) {
       // logger.LogInformation("Received the following message ["+request.message.Id+"] " + request.message.Content);
        gameService.PerformAddAction(request.message);

        MessageReceivedResponse response = new() { };
        return Task.FromResult(response);
    }

}
