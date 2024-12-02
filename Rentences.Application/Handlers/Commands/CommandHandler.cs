using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public class CommandHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, ICommandService> _commandServices;

    public CommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _commandServices = new Dictionary<string, ICommandService>();

        RegisterCommandServices();
    }

    private void RegisterCommandServices()
    {
        var commandServiceType = typeof(ICommandService);
        var commandServices = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => commandServiceType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var serviceType in commandServices)
        {
            var commandService = (ICommandService)_serviceProvider.GetService(serviceType);
            if (commandService != null)
            {
                _commandServices[commandService.CommandName] = commandService;
            }
        }
    }

    public async Task HandleMessageAsync(SocketMessage message)
    {
        if (message.Content.StartsWith("-"))
        {
            var parts = message.Content.Split(' ');
            var commandName = parts[0];
            var args = parts.Skip(1).ToArray();

            if (_commandServices.TryGetValue(commandName, out var commandService))
            {
                await commandService.ProcessCommandAsync(args, message);
            }
            else
            {
                await message.Channel.SendMessageAsync("Command not recognized.");
            }
        }
    }
}
