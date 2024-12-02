using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rentences.Domain.Definitions;
using Rentences.Gateways.DiscordClient;

public static class DependencyInjection
{
    public static IServiceCollection AddDiscordServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind the configuration settings
        var discordConfig = configuration.GetSection("DiscordConfiguration").Get<DiscordConfiguration>();
        if (discordConfig == null)
        {
            throw new Exception("DiscordConfiguration section is missing from appsettings.json");
        }
        services.AddSingleton(discordConfig);
        services.AddSingleton(provider => DiscordClientFactory.CreateDiscordClient(discordConfig.Token));
        services.AddSingleton<DiscordInterop>();

        return services;
    }
}
