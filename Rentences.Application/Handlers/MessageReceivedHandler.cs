

using Microsoft.Extensions.Logging;
using Rentences.Application.Services;

namespace Rentences.Application.Handlers;

public class MessageReceivedHandler : IRequestHandler<MessageReceivedCommand, MessageReceivedResponse>
{
   private readonly ILogger<MessageReceivedHandler> _logger;
   private readonly IGameService _gameService;

   public MessageReceivedHandler(ILogger<MessageReceivedHandler> logger, IGameService gameService)
   {
       _logger = logger;
       _gameService = gameService;
   }

   public async Task<MessageReceivedResponse> Handle(MessageReceivedCommand request, CancellationToken cancellationToken)
   {
       // Always ignore bot messages.
       if (request.message.Author.IsBot)
       {
           return new MessageReceivedResponse();
       }

       try
       {
           // Hard bound to avoid ever blocking Discord.NET's gateway thread.
           using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
           cts.CancelAfter(TimeSpan.FromSeconds(2));

           var addTask = _gameService.PerformAddActionAsync(request.message);
           var completed = await Task.WhenAny(addTask, Task.Delay(Timeout.Infinite, cts.Token));

           if (completed == addTask)
           {
               var result = await addTask;
               if (result.IsError)
               {
                   _logger.LogDebug(
                       "GameService rejected message {MessageId}: {Reason}",
                       request.message.Id,
                       result.FirstError.Description);
               }
           }
           else
           {
               _logger.LogWarning(
                   "GameService.PerformAddActionAsync timed out for message {MessageId}; continuing to avoid blocking gateway.",
                   request.message.Id);
           }
       }
       catch (OperationCanceledException)
       {
           // Cancellation from Discord or timeout; do not bubble as a gateway-blocking failure.
           _logger.LogWarning(
               "MessageReceived handling cancelled or timed out for message {MessageId}; avoiding gateway block.",
               request.message.Id);
       }
       catch (Exception ex)
       {
           _logger.LogError(
               ex,
               "Unexpected error while processing MessageReceived for message {MessageId}",
               request.message.Id);
       }

       return new MessageReceivedResponse();
   }
}
