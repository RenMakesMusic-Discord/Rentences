using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rentences.Domain.Definitions;
using Rentences.Persistence;
using Rentences.Persistence.Repositories;

namespace Rentences.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind the configuration settings
        services.AddDbContext<AppDbContext>(options =>
          options.UseSqlite("Data Source=words.db"));

        services.AddScoped<IWordRepository, WordRepository>();
        services.AddScoped<IWordUsageRepository, WordUsageRepository>();
        services.AddScoped<IUserWordStatisticsRepository, UserWordStatisticsRepository>();
        services.AddHostedService<MigrationHostedService>();


        return services;
    }
}
