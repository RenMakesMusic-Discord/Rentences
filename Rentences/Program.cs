using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Rentences.Application;
namespace Rentences;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        // Start the application
        host.Services.CreateScope();

        await host.RunAsync();

    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            })
            .ConfigureLogging(logging =>
            {
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;  // Example option, you can configure other options as well.
                });
            
                // Set console encoding to UTF-8 for Unicode support
                Console.OutputEncoding = System.Text.Encoding.UTF8;
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddPersistence(hostContext.Configuration);
                services.AddDiscordServices(hostContext.Configuration);
                services.RegisterApplicationServices(hostContext.Configuration);
                services.AddSingleton<DiscordListener>();
                services.AddHostedService<DiscordListener>();
            }).UseDefaultServiceProvider(options => options.ValidateScopes = false);
}