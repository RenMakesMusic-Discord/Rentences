using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
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
    // Preferred overload: explicitly uses IConfiguration
    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // MediatR and core application services
        services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(IInterop).Assembly); });
        services.AddTransient<IInterop, Interop>();

        services.AddScoped<WordService>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        services.AddSingleton<CommandHandler>();

        // Register all ICommandService implementations
        var commandServiceType = typeof(ICommandService);
        var commandServices = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => commandServiceType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var serviceType in commandServices)
        {
            services.AddSingleton(serviceType);
        }

        // Register game mode handlers
        services.AddTransient<Casual>();
        services.AddTransient<ReverseSentence>();

        // LetterVoting depends on IGameService (lazy) and other services
        services.AddTransient<LetterVoting>(provider =>
        {
            var letterVoting = new LetterVoting(
                provider.GetRequiredService<ILogger<LetterVoting>>(),
                provider.GetRequiredService<IInterop>(),
                provider.GetRequiredService<DiscordConfiguration>(),
                provider.GetRequiredService<WordService>(),
                new Lazy<IGameService>(() => provider.GetRequiredService<IGameService>()),
                provider.GetRequiredService<IMediator>()
            );
            return letterVoting;
        });

        // Featured gamemode configuration and selector
        services.Configure<FeaturedGamemodeOptions>(configuration.GetSection("FeaturedGamemode"));
        services.AddSingleton<IFeaturedGamemodeSelector, FeaturedGamemodeSelector>();

        // IGameService must always be constructed with the featured selector
        services.AddSingleton<IGameService>(provider =>
        {
            var handlers = new Dictionary<Gamemodes, IGamemodeHandler>
            {
                { Gamemodes.GAMEMODE_CASUAL, provider.GetRequiredService<Casual>() },
                { Gamemodes.GAMEMODE_LETTER_VOTE, provider.GetRequiredService<LetterVoting>() },
                { Gamemodes.GAMEMODE_REVERSE_SENTENCE, provider.GetRequiredService<ReverseSentence>() }
            };

            var logger = provider.GetRequiredService<ILogger<GameService>>();
            var featuredSelector = provider.GetRequiredService<IFeaturedGamemodeSelector>();

            return new GameService(handlers, logger, featuredSelector);
        });

        return services;
    }

    // Legacy overload: resolves IConfiguration from the container and forwards
    // Ensures that any caller still gets a GameService wired with IFeaturedGamemodeSelector.
    [Obsolete("Use RegisterApplicationServices(this IServiceCollection services, IConfiguration configuration) instead.")]
    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        var configuration = provider.GetRequiredService<IConfiguration>();
        return services.RegisterApplicationServices(configuration);
    }
}
