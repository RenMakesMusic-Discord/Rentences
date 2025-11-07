global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Configuration;
global using Spectre.Console;
global using System.CommandLine;
global using Rentences.Testing.Services;
global using Rentences.Testing.Core;

namespace Rentences.Testing;

public class Program
{
    private static TestingEnvironment? _testingEnvironment;
    
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        if (args.Length == 0)
        {
            await RunInteractiveMode();
            return;
        }

        // Simple command line handling
        if (args[0] == "test")
        {
            var testType = args.Length > 1 ? args[1] : "all";
            var config = new TestConfiguration();
            var environment = new TestingEnvironment(config);
            await environment.InitializeAsync();
            
            var testRunner = new TestRunner(environment);
            await testRunner.RunTestsAsync(testType);
        }
        else if (args[0] == "setup")
        {
            await SetupConfiguration();
        }
    }

    private static async Task RunInteractiveMode()
    {
        AnsiConsole.Write(new FigletText("Rentences Testing").LeftJustified().Color(Color.Green));
        AnsiConsole.Write(new Rule("[bold green]Console Testing Interface[/]").RuleStyle("green"));
        
        var testingConfig = new TestConfiguration();
        _testingEnvironment = new TestingEnvironment(testingConfig);
        
        await _testingEnvironment.InitializeAsync();

        while (true)
        {
            try
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("\n[bold yellow]Select testing option:[/]")
                        .AddChoices(new[]
                        {
                            "🧪 Run All Tests",
                            "📝 Test Word Validation",
                            "🎮 Test Game Mechanics",
                            "💬 Test Command Processing",
                            "🔐 Test Staff Commands",
                            "👥 Test User Statistics",
                            "🏆 Test Leaderboards",
                            "💾 Test Database Operations",
                            "🔧 Interactive Session",
                            "⚙️ Setup & Configuration",
                            "📊 View Test Reports",
                            "❌ Exit"
                        }));

                switch (choice)
                {
                    case "🧪 Run All Tests":
                        await RunAllTests();
                        break;
                    case "📝 Test Word Validation":
                        await TestWordValidation();
                        break;
                    case "🎮 Test Game Mechanics":
                        await TestGameMechanics();
                        break;
                    case "💬 Test Command Processing":
                        await TestCommandProcessing();
                        break;
                    case "🔐 Test Staff Commands":
                        await TestStaffCommands();
                        break;
                    case "👥 Test User Statistics":
                        await TestUserStatistics();
                        break;
                    case "🏆 Test Leaderboards":
                        await TestLeaderboards();
                        break;
                    case "💾 Test Database Operations":
                        await TestDatabaseOperations();
                        break;
                    case "🔧 Interactive Session":
                        await RunInteractiveSession();
                        break;
                    case "⚙️ Setup & Configuration":
                        await SetupConfiguration();
                        break;
                    case "📊 View Test Reports":
                        await ViewTestReports();
                        break;
                    case "❌ Exit":
                        AnsiConsole.WriteLine("👋 Goodbye!");
                        return;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteLine($"[red]Error: {ex.Message}[/]");
            }
        }
    }

    private static async Task RunAllTests()
    {
        AnsiConsole.WriteLine("🧪 Running all tests...");
        var testRunner = new TestRunner(_testingEnvironment!);
        await testRunner.RunTestsAsync("all");
    }

    private static async Task TestWordValidation()
    {
        AnsiConsole.WriteLine("📝 Testing word validation...");
        var tester = new WordValidationTester(_testingEnvironment!);
        await tester.RunValidationTestsAsync();
    }

    private static async Task TestGameMechanics()
    {
        AnsiConsole.WriteLine("🎮 Testing game mechanics...");
        var tester = new GameMechanicsTester(_testingEnvironment!);
        await tester.RunGameTestsAsync();
    }

    private static async Task TestCommandProcessing()
    {
        AnsiConsole.WriteLine("💬 Testing command processing...");
        var tester = new CommandTester(_testingEnvironment!);
        await tester.RunCommandTestsAsync();
    }

    private static async Task TestUserStatistics()
    {
        AnsiConsole.WriteLine("👥 Testing user statistics...");
        var tester = new UserStatisticsTester(_testingEnvironment!);
        await tester.RunStatisticsTestsAsync();
    }

    private static async Task TestLeaderboards()
    {
        AnsiConsole.WriteLine("🏆 Testing leaderboards...");
        var tester = new LeaderboardTester(_testingEnvironment!);
        await tester.RunLeaderboardTestsAsync();
    }

    private static async Task TestStaffCommands()
    {
        AnsiConsole.WriteLine("🔐 Testing staff commands...");
        var testRunner = new TestRunner(_testingEnvironment!);
        await testRunner.RunTestsAsync("staff");
    }

    private static async Task TestDatabaseOperations()
    {
        AnsiConsole.WriteLine("💾 Testing database operations...");
        var tester = new DatabaseTester(_testingEnvironment!);
        await tester.RunDatabaseTestsAsync();
    }

    private static async Task RunInteractiveSession()
    {
        var session = new InteractiveTestingSession(_testingEnvironment!);
        await session.StartAsync();
    }

    private static async Task SetupConfiguration()
    {
        var setup = new TestSetup(_testingEnvironment!);
        await setup.RunSetupAsync();
    }

    private static async Task ViewTestReports()
    {
        var reports = new TestReporting(_testingEnvironment!);
        await reports.DisplayReportsAsync();
    }
}