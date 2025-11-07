using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediatR;
using Rentences.Application;
using Rentences.Application.Handlers;
using System.IO;

namespace Rentences.Testing.Core;

public class TestConfiguration
{
    public string DatabaseConnectionString { get; set; } = "Data Source=test_rentences.db";
    public ulong DefaultUserId { get; set; } = 123456789012345678;
    public ulong DefaultChannelId { get; set; } = 987654321098765432;
    public ulong DefaultGuildId { get; set; } = 112233445566778899;
    public string DefaultGamemode { get; set; } = "GAMEMODE_CASUAL";
    public bool AutoCleanup { get; set; } = true;
    public bool VerboseOutput { get; set; } = true;
    public string? OutputDirectory { get; set; } = "test-reports";
    public List<string> TestWordList { get; set; } = new();
}

public class TestResult
{
    public string TestName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class TestResultSummary
{
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<TestResult> Results { get; set; } = new();
    
    public double SuccessRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
}

public class TestingEnvironment
{
    private readonly TestConfiguration _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TestingEnvironment> _logger;
    
    public TestConfiguration Configuration => _config;
    public IServiceProvider Services => _serviceProvider;
    public ILogger<TestingEnvironment> Logger => _logger;
    
    public TestingEnvironment(TestConfiguration config)
    {
        _config = config;
        _serviceProvider = CreateServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<TestingEnvironment>>();
    }
    
    public async Task InitializeAsync()
    {
        Logger.LogInformation("Initializing testing environment...");
        Logger.LogInformation("Testing environment initialized successfully");
    }
    
    public async Task CleanupAsync()
    {
        Logger.LogInformation("Cleaning up testing environment...");
    }
    
    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        
        // Add configuration
        services.AddSingleton(_config);
        
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        });
        
        // Add Discord configuration for testing
        services.AddSingleton(CreateTestDiscordConfiguration());
        
        // Add persistence services for testing
        services.AddPersistence(CreateTestConfiguration());
        
        // Add Discord gateway services for testing
        services.AddDiscordServices(CreateTestConfiguration());
        
        // Register application services
        services.RegisterApplicationServices();
        
        // Explicitly register handlers that need DiscordInterop
        services.AddTransient<GameStartedNotificationHandler>();
        services.AddTransient<GameMessageReactionHandler>();
        services.AddTransient<WordNotificationHandler>();
        
        // Add testing services
        services.AddSingleton<Rentences.Testing.Services.WordValidationTester>();
        services.AddSingleton<Rentences.Testing.Services.GameMechanicsTester>();
        services.AddSingleton<Rentences.Testing.Services.CommandTester>();
        services.AddSingleton<Rentences.Testing.Services.UserStatisticsTester>();
        services.AddSingleton<Rentences.Testing.Services.LeaderboardTester>();
        services.AddSingleton<Rentences.Testing.Services.DatabaseTester>();
        services.AddSingleton<Rentences.Testing.Services.TestRunner>();
        services.AddSingleton<Rentences.Testing.Services.BatchTestRunner>();
        services.AddSingleton<Rentences.Testing.Services.InteractiveTestingSession>();
        services.AddSingleton<Rentences.Testing.Services.TestSetup>();
        services.AddSingleton<Rentences.Testing.Services.TestReporting>();
        
        return services.BuildServiceProvider();
    }
    
    private Microsoft.Extensions.Configuration.IConfiguration CreateTestConfiguration()
    {
        var configBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string>
        {
            { "DatabaseConnectionString", "Data Source=test_rentences.db" },
            { "DiscordConfiguration:Token", "test-token" },
            { "DiscordConfiguration:ServerId", "123456789" },
            { "DiscordConfiguration:ChannelId", "987654321" },
            { "DiscordConfiguration:Status", "Test Mode" },
            { "DiscordConfiguration:WinEmoji:Contents", "🎉" },
            { "DiscordConfiguration:WinEmoji:IsEmoji", "true" },
            { "DiscordConfiguration:LoseEmoji:Contents", "😢" },
            { "DiscordConfiguration:LoseEmoji:IsEmoji", "true" },
            { "DiscordConfiguration:CorrectEmoji:Contents", "✅" },
            { "DiscordConfiguration:CorrectEmoji:IsEmoji", "true" }
        });
        return configBuilder.Build();
    }
    
    private Rentences.Domain.Definitions.DiscordConfiguration CreateTestDiscordConfiguration()
    {
        return new Rentences.Domain.Definitions.DiscordConfiguration
        {
            Token = "test-token",
            ServerId = "123456789",
            ChannelId = "987654321",
            Status = "Test Mode",
            WinEmoji = new Rentences.Domain.Definitions.Emote { Contents = "🎉", IsEmoji = true },
            LoseEmoji = new Rentences.Domain.Definitions.Emote { Contents = "😢", IsEmoji = true },
            CorrectEmoji = new Rentences.Domain.Definitions.Emote { Contents = "✅", IsEmoji = true }
        };
    }
}