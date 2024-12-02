

using Discord;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Rentences.Application.Services;


namespace Rentences.Application.Handlers;
public class GameMessageReactionHandler : IRequestHandler<GameMessageReactionCommand, ErrorOr<GameMessageReactionResponse>> {


    private readonly ILogger<GameMessageReactionHandler> logger;
    private readonly DiscordInterop interop;

    public GameMessageReactionHandler(ILogger<GameMessageReactionHandler> _logger, DiscordInterop clientService)
    {
        logger = _logger;
        interop = clientService;
    }
    public async Task<ErrorOr<GameMessageReactionResponse>> Handle(GameMessageReactionCommand request, CancellationToken cancellationToken) {
        // logger.LogInformation("Received the following message ["+request.message.Id+"] " + request.message.Content);

        await interop.AddReactionAsync(request.socketMessage.Channel.Id, request.socketMessage.Id, request.emoji);
        return new GameMessageReactionResponse();
    }

}
