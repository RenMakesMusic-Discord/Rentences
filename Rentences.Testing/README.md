# Rentences Testing Framework

A comprehensive console-based testing interface for the Rentences Discord bot, providing complete testing capabilities without requiring Discord connectivity.

## Overview

This testing framework allows developers and QA teams to thoroughly test all Rentences bot functionality in a controlled console environment. It simulates Discord interactions, game mechanics, and database operations to ensure comprehensive test coverage.

## Features

### 🧪 **Complete Test Coverage**
- Word validation testing
- Game mechanics testing
- Command processing testing
- User statistics testing
- Leaderboard testing
- Database operations testing

### 🔧 **Discord Simulation**
- Simulated Discord users, channels, and messages
- Command processing simulation
- Game action simulation
- Bot response simulation

### 💬 **Interactive Testing Interface**
- Menu-driven testing interface
- Real-time test execution
- Visual test results with Spectre.Console
- Test result reporting and analytics

### ⚙️ **Configuration System**
- Flexible test configuration
- Custom test scenarios
- Batch test execution
- Test result export

## Quick Start

### Prerequisites
- .NET 8.0 or later
- Visual Studio 2022 or VS Code

### Building the Project
```bash
cd Rentences.Testing
dotnet build
```

### Running the Tests

#### Interactive Mode (Recommended)
```bash
cd Rentences.Testing
dotnet run
```

#### Command Line Mode
```bash
# Run all tests
cd Rentences.Testing
dotnet run test

# Run specific test type
dotnet run test word
dotnet run test game
dotnet run test command
dotnet run test stats
dotnet run test leaderboard
dotnet run test database

# Setup testing environment
dotnet run setup
```

## Test Categories

### 📝 Word Validation Testing
- Word existence validation against dictionary
- Word length validation (3-50 characters)
- Character validation (letters only)
- Duplicate word detection
- Case sensitivity testing
- Punctuation stripping

### 🎮 Game Mechanics Testing
- Word addition mechanics
- Word removal mechanics
- Game state management
- Game mode handling
- Game start/end operations
- Scoring system
- Complete game flow testing

### 💬 Command Processing Testing
- Command parsing and execution
- Invalid command handling
- Command permission testing
- Response generation
- Error handling

### 👥 User Statistics Testing
- User word tracking
- Statistics calculation
- User ranking
- Performance analytics
- Bulk user operations

### 🏆 Leaderboard Testing
- Leaderboard generation
- Top user identification
- Ranking calculations
- User comparison

### 💾 Database Operations Testing
- Database connection testing
- CRUD operations
- Data integrity
- Performance testing
- Error handling

## Configuration

The testing framework uses `testconfig.json` for configuration:

```json
{
  "TestDatabase": {
    "ConnectionString": "Data Source=test_rentences.db"
  },
  "DiscordSimulation": {
    "DefaultUserId": "123456789012345678",
    "DefaultChannelId": "987654321098765432",
    "DefaultGuildId": "112233445566778899"
  },
  "GameSettings": {
    "DefaultGamemode": "GAMEMODE_CASUAL",
    "TestWordList": [
      "test", "word", "game", "discord", "bot", "validation"
    ]
  },
  "Testing": {
    "AutoCleanup": true,
    "VerboseOutput": true,
    "BatchTimeout": 300
  }
}
```

## Architecture

### Core Components

#### TestingEnvironment
- Manages test configuration and setup
- Provides access to services and dependencies
- Handles database initialization and cleanup

#### DiscordSimulator
- Simulates Discord entities (users, channels, messages)
- Provides methods for creating test scenarios
- Handles command and game action simulation

#### Test Services
- WordValidationTester: Tests word validation logic
- GameMechanicsTester: Tests game mechanics
- CommandTester: Tests command processing
- UserStatisticsTester: Tests user statistics
- LeaderboardTester: Tests leaderboard functionality
- DatabaseTester: Tests database operations

### Test Results
Each test returns a `TestResult` containing:
- Test name and status (pass/fail)
- Execution duration
- Detailed message
- Additional metadata

## Usage Examples

### Basic Testing
```csharp
// Initialize testing environment
var config = new TestConfiguration();
var environment = new TestingEnvironment(config);
await environment.InitializeAsync();

// Run word validation tests
var wordTester = new WordValidationTester(environment);
await wordTester.RunValidationTestsAsync();
```

### Discord Simulation
```csharp
// Create simulated user and message
var simulator = new DiscordSimulator(config, logger);
var user = simulator.CreateUser("TestUser");
var message = simulator.CreateMessage("hello world", "12345");

// Simulate command
simulator.SimulateCommand("-leaderboard", user);
```

## Development

### Adding New Tests
1. Create a new test class in the `Services` folder
2. Inherit from the base pattern used by other testers
3. Add your test methods that return `TestResult` objects
4. Register the service in `TestingEnvironment.cs`
5. Add menu options in `Program.cs` if needed

### Extending Discord Simulation
1. Add new simulation methods to `DiscordSimulator`
2. Create additional test entity classes as needed
3. Update the simulation logic for new Discord features

## Best Practices

### Test Design
- Write small, focused test methods
- Use descriptive test names
- Include comprehensive error handling
- Provide detailed test results and metadata

### Performance
- Use async/await patterns
- Implement proper cleanup in test teardown
- Monitor test execution time
- Use appropriate timeout values

### Maintainability
- Keep test code separate from production code
- Use configuration for test parameters
- Implement proper logging
- Document complex test scenarios

## Troubleshooting

### Common Issues

**Build Errors**
- Ensure all dependencies are properly referenced
- Check for missing using directives
- Verify .NET version compatibility

**Runtime Errors**
- Check database connection strings
- Verify configuration file exists
- Ensure proper async/await usage

**Test Failures**
- Review test output for specific failure details
- Check database state and test data
- Verify Discord simulation setup

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add comprehensive tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## License

This testing framework is part of the Rentences project and follows the same license terms.

---

**Note**: This testing framework is designed for development and QA purposes. For production testing with actual Discord integration, refer to the main Rentences bot documentation.