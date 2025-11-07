using ErrorOr;
using Microsoft.Extensions.Logging;
using MediatR;
using Discord.WebSocket;
using Discord;
using System.Threading.Tasks;
using Rentences.Domain.Definitions;

namespace Rentences.Application.Services
{
    public class Interop : IInterop
    {
        private readonly ILogger _logger;
        private readonly IMediator _mediator;
        private readonly DiscordConfiguration _discordConfiguration;
        private readonly DiscordInterop _discordInterop;

        public Interop(ILogger<Interop> logger, IMediator mediator, DiscordConfiguration discordConfiguration, DiscordInterop discordInterop)
        {
            _logger = logger;
            _mediator = mediator;
            _discordConfiguration = discordConfiguration;
            _discordInterop = discordInterop;
        }

        public async Task<ErrorOr<MessageReceivedResponse>> SendMessageReceivedCommand(MessageReceivedCommand command)
            => await _mediator.Send(command);

        public async Task<ErrorOr<ulong>> SendMessage(SendDiscordMessage command)
        {
            var channelId = ulong.Parse(_discordConfiguration.ChannelId);
            var result = await _discordInterop.SendMessageAsync(channelId, command.Message);
            return result;
        }

        public async Task<ErrorOr<bool>> SendMessageWithEmbed(SendDiscordMessageWithEmbed command) => await _mediator.Send(command);
        public async Task<ErrorOr<MessageDeletedResponse>> SendMessageDeletedCommand(MessageDeletedCommand command)
            => await _mediator.Send(command);
        public Task SendGameStartedNotification(GameStartedNotification notification)
            => _mediator.Publish(notification);

        public async Task<ErrorOr<GameMessageReactionResponse>> SendGameMessageReaction(GameMessageReactionCommand command)
            => await _mediator.Send(command);

        public async Task<ErrorOr<bool>> AddReactionToMessage(ulong channelId, ulong messageId, Rentences.Domain.Definitions.Emote emoji)
        {
            return await _discordInterop.AddReactionToMessage(channelId, messageId, emoji);
        }

        public async Task<ErrorOr<IEnumerable<IUser>>> GetReactionsForMessage(ulong channelId, ulong messageId, Rentences.Domain.Definitions.Emote emoji)
        {
            return await _discordInterop.GetReactionsForMessage(channelId, messageId, emoji);
        }

        public async Task<WakeGameResponse> WakeGame(WakeGameCommand command)
            => await _mediator.Send(command);

        public async Task ExecuteCommand(SocketMessage message)
            =>  await _mediator.Send(new ExecuteCommand(message));

        public async Task<string> GetLeaderboard()
            => await _mediator.Send(new GetLeaderboardCommand());

        public DiscordConfiguration GetDiscordConfiguration()
        {
            return _discordConfiguration;
        }
    }
}
