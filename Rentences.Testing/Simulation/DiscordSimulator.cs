using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Rentences.Testing.Core;
using Spectre.Console;

namespace Rentences.Testing.Simulation;

public class DiscordSimulator
{
    private readonly TestConfiguration _config;
    private readonly ILogger<DiscordSimulator> _logger;
    
    public DiscordSimulator(TestConfiguration config, ILogger<DiscordSimulator> logger)
    {
        _config = config;
        _logger = logger;
    }
    
    public object CreateUser(string username)
    {
        _logger.LogInformation($"Creating simulated user: {username}");
        return new { Username = username, Id = (ulong)new Random().Next(1000000000, 2147483647) };
    }
    
    public object CreateMessage(string content, string userId)
    {
        _logger.LogInformation($"Creating simulated message: {content}");
        return new { Content = content, AuthorId = userId, Id = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
    }
    
    public void SimulateCommand(string command, object user)
    {
        _logger.LogInformation($"Simulating command: {command}");
    }
    
    public void SimulateGameAction(object action, object message)
    {
        _logger.LogInformation($"Simulating game action");
    }
}