using Microsoft.Extensions.Logging;
using Rentences.Application.Services;

public class WakeGameHandler : IRequestHandler<WakeGameCommand, WakeGameResponse>
{


    private readonly ILogger<WakeGameHandler> logger;
    private readonly IGameService gameService;

    public WakeGameHandler(ILogger<WakeGameHandler> _logger, IGameService gameService)
    {
        logger = _logger;
        this.gameService = gameService;
    }
    public Task<WakeGameResponse> Handle(WakeGameCommand request, CancellationToken cancellationToken)
    {
        
        WakeGameResponse response = new() { };
        return Task.FromResult(response);
    }

}
