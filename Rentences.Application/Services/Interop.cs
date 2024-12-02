using ErrorOr;
using Microsoft.Extensions.Logging;
using MediatR;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace Rentences.Application.Services
{
    public class Interop : IInterop
    {
        private readonly ILogger _logger;
        private readonly IMediator _mediator;

        public Interop(ILogger<Interop> logger, IMediator mediator)
        {
            _logger = logger;
            _mediator = mediator;
        }

        public async Task<ErrorOr<MessageReceivedResponse>> SendMessageReceivedCommand(MessageReceivedCommand command)
            => await _mediator.Send(command);

        public async Task<ErrorOr<bool>> SendMessage(SendDiscordMessage command) => await _mediator.Send(command);
        public async Task<ErrorOr<MessageDeletedResponse>> SendMessageDeletedCommand(MessageDeletedCommand command)
            => await _mediator.Send(command);
        public Task SendGameStartedNotification(GameStartedNotification notification)
            => _mediator.Publish(notification);

        public async Task<ErrorOr<GameMessageReactionResponse>> SendGameMessageReaction(GameMessageReactionCommand command)
            => await _mediator.Send(command);

        public async Task<WakeGameResponse> WakeGame(WakeGameCommand command)
            => await _mediator.Send(command);

        public async Task ExecuteCommand(SocketMessage message) 
            =>  await _mediator.Send(new ExecuteCommand(message));

        public async Task<string> GetLeaderboard()
            => await _mediator.Send(new GetLeaderboardCommand());

    }
}
