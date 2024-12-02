using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rentences.Application.Pipelines;
using Rentences.Application.Services;
using Rentences.Application.Services.Game;
using System.Reflection;

namespace Rentences.Application;

public static class DependencyInjection
{
    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(IInterop).Assembly); });
        services.AddTransient<IInterop, Interop>();
        services.AddTransient<Casual>();
        services.AddScoped<WordService>();
        services.AddSingleton<IGameService, GameService>(provider =>
        {
            var handlers = new Dictionary<Gamemodes, IGamemodeHandler>()
            {
                {Gamemodes.GAMEMODE_CASUAL, provider.GetRequiredService<Casual>() }
            };
            var logger = provider.GetRequiredService<ILogger<GameService>>();
            return new GameService(handlers, logger);
        });
        // Register the logging behavior
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        services.AddSingleton<CommandHandler>();

        // Automatically register all ICommandService implementations
        var commandServiceType = typeof(ICommandService);
        var commandServices = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => commandServiceType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var serviceType in commandServices)
        {
            services.AddSingleton(serviceType);
        }

        return services;
    }
}
