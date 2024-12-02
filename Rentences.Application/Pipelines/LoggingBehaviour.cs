namespace Rentences.Application.Pipelines;

using MediatR;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;


public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            // Log the request type and its properties
            _logger.LogInformation("Handling {RequestName}: {@Request}", typeof(TRequest).Name, request);

            var response = await next(); // Call the next handler in the pipeline

            // Log the response
            _logger.LogInformation("Handled {RequestName}: {@Response}", typeof(TRequest).Name, response);

            return response;
        }
        catch (Exception ex)
        {
            // Log the exception
            _logger.LogError(ex, "Error handling {RequestName}: {@Request}", typeof(TRequest).Name, request);

            // Re-throw the exception to ensure the application can handle it as needed
            throw;
        }
    }
}
