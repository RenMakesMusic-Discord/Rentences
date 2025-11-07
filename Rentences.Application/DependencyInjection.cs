using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rentences.Application.Pipelines;
using Rentences.Application.Services;
using Rentences.Application.Services.Game;
using Rentences.Domain.Definitions;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Rentences.Application;

public static class DependencyInjection
{
    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(IInterop).Assembly); });
        services.AddTransient<IInterop, Interop>();
        
        // Register basic services first
        services.AddScoped<WordService>();
        
        // Register the logging behavior
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        services.AddSingleton<CommandHandler>();

        // Register command services
        var commandServiceType = typeof(ICommandService);
        var commandServices = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => commandServiceType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var serviceType in commandServices)
        {
            services.AddSingleton(serviceType);
        }
        
        // Register game mode handlers first
        services.AddTransient<Casual>();
        services.AddTransient<ReverseSentence>();
        
        // Register LetterVoting with proper dependency resolution
        services.AddTransient<LetterVoting>(provider =>
        {
            var letterVoting = new LetterVoting(
                provider.GetRequiredService<ILogger<LetterVoting>>(),
                provider.GetRequiredService<IInterop>(),
                provider.GetRequiredService<DiscordConfiguration>(),
                provider.GetRequiredService<WordService>(),
                new Lazy<IGameService>(() => provider.GetRequiredService<IGameService>())
            );
            return letterVoting;
        });
        
        // Register IGameService as singleton that depends on all handlers
        services.AddSingleton<IGameService>(provider =>
        {
            var handlers = new Dictionary<Gamemodes, IGamemodeHandler>()
            {
                {Gamemodes.GAMEMODE_CASUAL, provider.GetRequiredService<Casual>() },
                {Gamemodes.GAMEMODE_LETTER_VOTE, provider.GetRequiredService<LetterVoting>() },
                {Gamemodes.GAMEMODE_REVERSE_SENTENCE, provider.GetRequiredService<ReverseSentence>() }
            };
            var logger = provider.GetRequiredService<ILogger<GameService>>();
            return new GameService(handlers, logger);
        });

        return services;
    }
}
