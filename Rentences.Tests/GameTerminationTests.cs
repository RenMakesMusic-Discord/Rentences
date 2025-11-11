using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Rentences.Application.Services;
using Rentences.Application.Services.Game;
using Rentences.Domain;
using Rentences.Domain.Definitions.Game;
using Rentences.Application;

namespace Rentences.Tests
{
    public class GameTerminationTests
    {
        private readonly Mock<ILogger<GameService>> _mockLogger = new();
        private readonly Dictionary<Gamemodes, IGamemodeHandler> _mockGames = new();
        private readonly TestGamemodeHandler _mockCasualGame;
        private readonly TestGamemodeHandler _mockReverseGame;
        private readonly TestGamemodeHandler _mockLetterVotingGame;
        private readonly Mock<IGamemodeHandler> _mockLettersHandler = new();
        private readonly Mock<SocketMessage> _mockMessage = new();
        private readonly Mock<IFeaturedGamemodeSelector> _defaultFeaturedSelector = new();
        private GameService? _gameService;

        public GameTerminationTests()
        {
            _mockCasualGame = new TestGamemodeHandler(GameStatus.IN_PROGRESS);
            _mockReverseGame = new TestGamemodeHandler(GameStatus.IN_PROGRESS);
            _mockLetterVotingGame = new TestGamemodeHandler(GameStatus.IN_PROGRESS);

            // Default featured selector: no featured override
            _defaultFeaturedSelector
                .Setup(s => s.TrySelectFeaturedGamemode(It.IsAny<Gamemodes>()))
                .Returns((Gamemodes?)null);

            // Setup games dictionary
            _mockGames[Gamemodes.GAMEMODE_CASUAL] = _mockCasualGame;
            _mockGames[Gamemodes.GAMEMODE_REVERSE_SENTENCE] = _mockReverseGame;
            _mockGames[Gamemodes.GAMEMODE_LETTER_VOTE] = _mockLetterVotingGame;
        }

        private GameService CreateService(IFeaturedGamemodeSelector? selector = null)
        {
            return new GameService(_mockGames, _mockLogger.Object, selector ?? _defaultFeaturedSelector.Object);
        }

        [Fact]
        public async Task StartGameWithForceTerminationAsync_TerminatesExistingGame()
        {
            // Arrange
            _gameService = CreateService();
            await _gameService.StartGame(Gamemodes.GAMEMODE_CASUAL); // Start initial game

            Assert.Equal(GameStatus.IN_PROGRESS, _mockCasualGame.GameState.CurrentState);

            // Act
            await _gameService.StartGameWithForceTerminationAsync(Gamemodes.GAMEMODE_REVERSE_SENTENCE, "Test termination");

            // Assert: previous game ended, new game in progress
            Assert.Equal(GameStatus.ENDED, _mockCasualGame.GameState.CurrentState);
            Assert.Equal(GameStatus.IN_PROGRESS, _mockReverseGame.GameState.CurrentState);
        }

        [Fact]
        public async Task ForceTerminateCurrentGame_ClearsGameReference()
        {
            // Arrange
            _gameService = CreateService();
            await _gameService.StartGame(Gamemodes.GAMEMODE_CASUAL);
            
            Assert.True(_gameService.IsGameRunning());
            Assert.Equal(GameStatus.IN_PROGRESS, _mockCasualGame.GameState.CurrentState);

            // Act
            await _gameService.ForceTerminateCurrentGame("Force termination test");

            // Assert
            Assert.Equal(GameStatus.ENDED, _mockCasualGame.GameState.CurrentState);
            Assert.False(_gameService.IsGameRunning());
        }

        [Fact]
        public async Task StartGameWithForceTerminationAsync_HandlesNullCurrentGame()
        {
            // Arrange
            _gameService = CreateService();

            // Act
            await _gameService.StartGameWithForceTerminationAsync(Gamemodes.GAMEMODE_CASUAL, "No existing game");

            // Assert
            Assert.Equal(GameStatus.IN_PROGRESS, _mockCasualGame.GameState.CurrentState);
        }

        [Fact]
        public async Task MultipleRapidGameCommands_NoRaceConditions()
        {
            // Arrange
            _gameService = CreateService();
            var tasks = new List<Task>();
            const int numberOfTasks = 10;

            // Act - launch multiple game commands simultaneously
            for (int i = 0; i < numberOfTasks; i++)
            {
                int taskId = i;
                var gameMode = i % 2 == 0 ? Gamemodes.GAMEMODE_CASUAL : Gamemodes.GAMEMODE_REVERSE_SENTENCE;
                tasks.Add(Task.Run(async () => 
                {
                    await _gameService.StartGameWithForceTerminationAsync(gameMode, $"Task {taskId}");
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - should complete without exceptions and final state should be consistent
            Assert.NotNull(_gameService.GetCurrentGameMode());
        }

        [Fact]
        public async Task ErrorHandling_GracefulTerminationOnException()
        {
            // Arrange
            _gameService = CreateService();
            await _gameService.StartGame(Gamemodes.GAMEMODE_CASUAL);
            
            // Make the game throw an exception on EndGame
            _mockCasualGame.ShouldThrowOnEndGame();

            // Act & Assert - should not throw and still complete
            var exception = await Record.ExceptionAsync(async () => 
                await _gameService.ForceTerminateCurrentGame("Error handling test"));

            Assert.Null(exception);
            Assert.False(_gameService.IsGameRunning());
        }

        [Fact]
        public async Task Disposal_DisposesResourcesCorrectly()
        {
            // Arrange
            _gameService = CreateService();
            await _gameService.StartGame(Gamemodes.GAMEMODE_CASUAL);
            Assert.Equal(GameStatus.IN_PROGRESS, _mockCasualGame.GameState.CurrentState);

            // Act
            _gameService.Dispose();

            // Assert
            Assert.Equal(GameStatus.ENDED, _mockCasualGame.GameState.CurrentState);
            
            // Verify service is disposed - subsequent operations should fail gracefully
            var result = await _gameService.PerformAddActionAsync(_mockMessage.Object);
            Assert.True(result.IsError);
        }

        [Fact]
        public async Task IntegrationTest_CommandServiceGameSwapping()
        {
            // Arrange - Simulate command service behavior
            _gameService = CreateService();
            
            // Start with casual game
            await _gameService.StartGameWithForceTerminationAsync(Gamemodes.GAMEMODE_CASUAL, "Casual command");
            Assert.Equal(Gamemodes.GAMEMODE_CASUAL, _gameService.GetCurrentGameMode());
            Assert.Equal(GameStatus.IN_PROGRESS, _mockCasualGame.GameState.CurrentState);

            // Act - Simulate staff reverse command (should force terminate and start new game)
            await _gameService.StartGameWithForceTerminationAsync(Gamemodes.GAMEMODE_REVERSE_SENTENCE, "Staff reverse command");
            
            // Assert
            Assert.Equal(Gamemodes.GAMEMODE_REVERSE_SENTENCE, _gameService.GetCurrentGameMode());
            Assert.Equal(GameStatus.ENDED, _mockCasualGame.GameState.CurrentState);
            Assert.Equal(GameStatus.IN_PROGRESS, _mockReverseGame.GameState.CurrentState);
        }

        [Fact]
        public async Task EndGame_ReplacesWithRandomGame()
        {
            // Arrange
            _gameService = CreateService();
            await _gameService.StartGame(Gamemodes.GAMEMODE_CASUAL);
            var originalMode = _gameService.GetCurrentGameMode();

            // Act
            var result = _gameService.EndGame();

            // Assert
            Assert.False(result.IsError);
            Assert.NotEqual(originalMode, _gameService.GetCurrentGameMode());
            Assert.NotNull(_gameService.GetCurrentGameMode());
        }

        [Fact]
        public void IsGameRunning_ReflectsCurrentState()
        {
            // Arrange
            _gameService = CreateService();

            // Act & Assert
            Assert.False(_gameService.IsGameRunning());
        }

        [Fact]
        public void GetCurrentGameMode_ReturnsNullWhenNoGame()
        {
            // Arrange
            _gameService = CreateService();

            // Act & Assert
            Assert.Null(_gameService.GetCurrentGameMode());
        }

        [Fact]
        public async Task LetterVoting_StartsWithCorrectStateAndEndsTerminally()
        {
            // Arrange: use a dedicated mapping so we can control Letters handler behavior
            var lettersState = new GameState { GameId = Guid.NewGuid(), CurrentState = GameStatus.VOTING };
            _mockLettersHandler.SetupProperty(g => g.GameState, lettersState);
            var games = new Dictionary<Gamemodes, IGamemodeHandler>
            {
                [Gamemodes.GAMEMODE_LETTER_VOTE] = _mockLettersHandler.Object
            };

            var service = new GameService(games, _mockLogger.Object, _defaultFeaturedSelector.Object);

            // Act: start Letters via standard API
            await service.StartGameWithForceTerminationAsync(Gamemodes.GAMEMODE_LETTER_VOTE, "Letters start");

            // Assert: StartGame was invoked and state is visible as non-ended
            _mockLettersHandler.Verify(g => g.StartGame(), Times.Once);
            Assert.NotEqual(GameStatus.ENDED, _mockLettersHandler.Object.GameState.CurrentState);

            // Simulate natural completion: GameService calls EndGame exactly once
            _mockLettersHandler.Object.GameState = new GameState
            {
                GameId = _mockLettersHandler.Object.GameState.GameId,
                CurrentState = GameStatus.ENDED
            };
            _mockLettersHandler.Setup(g => g.EndGame()).Returns(Task.CompletedTask).Verifiable();

            var endResult = await InvokeEndGameInternalAsync(service);
            Assert.False(endResult.IsError);

            _mockLettersHandler.Verify(g => g.EndGame(), Times.Once);
        }

        [Fact]
        public async Task LetterVoting_ForceTerminate_CallsEndOnceAndClearsCurrentGame()
        {
            // Arrange
            var lettersState = new GameState { GameId = Guid.NewGuid(), CurrentState = GameStatus.IN_PROGRESS };
            _mockLettersHandler.SetupProperty(g => g.GameState, lettersState);
            var games = new Dictionary<Gamemodes, IGamemodeHandler>
            {
                [Gamemodes.GAMEMODE_LETTER_VOTE] = _mockLettersHandler.Object
            };

            var service = new GameService(games, _mockLogger.Object, _defaultFeaturedSelector.Object);
            await service.StartGameWithForceTerminationAsync(Gamemodes.GAMEMODE_LETTER_VOTE, "Letters start");
            _mockLettersHandler.Verify(g => g.StartGame(), Times.Once);

            _mockLettersHandler.Setup(g => g.EndGame()).Returns(Task.CompletedTask).Verifiable();

            // Act
            await service.ForceTerminateCurrentGame("Force terminate letters");

            // Assert
            _mockLettersHandler.Verify(g => g.EndGame(), Times.Once);
            Assert.False(service.IsGameRunning());
        }

        /// <summary>
        /// Helper to invoke the private EndGameInternalAsync via reflection to assert behavior
        /// without altering public API.
        /// </summary>
        private static Task<ErrorOr<bool>> InvokeEndGameInternalAsync(GameService service)
        {
            var method = typeof(GameService).GetMethod("EndGameInternalAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (Task<ErrorOr<bool>>)method!.Invoke(service, null)!;
        }

        /// <summary>
        /// Helper to start a specific game mode using the public StartGameWithForceTerminationAsync API.
        /// </summary>
        private static Task InvokeStartGameWithForceTerminationAsync(GameService service, Gamemodes mode)
        {
            return service.StartGameWithForceTerminationAsync(mode, "test");
        }

        /// <summary>
        /// Helper to force terminate current game using the public API.
        /// </summary>
        private static Task InvokeForceTerminateCurrentGameInternalAsync(GameService service)
        {
            return service.ForceTerminateCurrentGame("test");
        }

        [Fact]
        public async Task PerformAddActionAsync_ThreadSafeOperation()
        {
            // Arrange
            _gameService = CreateService();
            await _gameService.StartGame(Gamemodes.GAMEMODE_CASUAL);

            // Act & Assert - Multiple concurrent add actions
            var tasks = new List<Task<ErrorOr<bool>>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_gameService.PerformAddActionAsync(_mockMessage.Object));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - all operations should complete without throwing exceptions
            Assert.Equal(10, results.Length);
        }

        [Fact]
        public async Task NaturalEnd_WithFeaturedSelection_UsesFeaturedOnce()
        {
            // Arrange
            var featuredSelector = new Mock<IFeaturedGamemodeSelector>();
            featuredSelector
                .SetupSequence(s => s.TrySelectFeaturedGamemode(It.IsAny<Gamemodes>()))
                .Returns(Gamemodes.GAMEMODE_REVERSE_SENTENCE)
                .Returns((Gamemodes?)null);

            var service = new GameService(_mockGames, _mockLogger.Object, featuredSelector.Object);

            await InvokeStartGameWithForceTerminationAsync(service, Gamemodes.GAMEMODE_CASUAL);
            Assert.Equal(Gamemodes.GAMEMODE_CASUAL, service.GetCurrentGameMode());

            // Act: first natural end -> should start featured mode
            var firstEnd = await InvokeEndGameInternalAsync(service);
            Assert.False(firstEnd.IsError);
            Assert.Equal(Gamemodes.GAMEMODE_REVERSE_SENTENCE, service.GetCurrentGameMode());

            // Act: second natural end -> selector returns null, so normal behavior (must not reuse previous featured)
            var secondEnd = await InvokeEndGameInternalAsync(service);
            Assert.False(secondEnd.IsError);
            Assert.NotNull(service.GetCurrentGameMode());
            Assert.NotEqual(Gamemodes.GAMEMODE_REVERSE_SENTENCE, service.GetCurrentGameMode());

            featuredSelector.Verify(s => s.TrySelectFeaturedGamemode(It.IsAny<Gamemodes>()), Times.Exactly(2));
        }

        [Fact]
        public async Task NaturalEnd_WithNoFeatured_UsesNormalBehavior()
        {
            // Arrange: selector always returns null
            var selector = new Mock<IFeaturedGamemodeSelector>();
            selector
                .Setup(s => s.TrySelectFeaturedGamemode(It.IsAny<Gamemodes>()))
                .Returns((Gamemodes?)null);

            var service = new GameService(_mockGames, _mockLogger.Object, selector.Object);
            await InvokeStartGameWithForceTerminationAsync(service, Gamemodes.GAMEMODE_CASUAL);
            var originalMode = service.GetCurrentGameMode();

            // Act
            var result = await InvokeEndGameInternalAsync(service);

            // Assert
            Assert.False(result.IsError);
            Assert.NotNull(service.GetCurrentGameMode());
            Assert.NotEqual(originalMode, service.GetCurrentGameMode());
        }

        [Fact]
        public async Task StaffOverride_ClearsPendingFeatured()
        {
            // Arrange
            var selector = new Mock<IFeaturedGamemodeSelector>();
            selector
                .Setup(s => s.TrySelectFeaturedGamemode(It.IsAny<Gamemodes>()))
                .Returns(Gamemodes.GAMEMODE_LETTER_VOTE);

            var service = new GameService(_mockGames, _mockLogger.Object, selector.Object);
            await InvokeStartGameWithForceTerminationAsync(service, Gamemodes.GAMEMODE_CASUAL);

            // Natural end schedules a featured override
            var endResult = await InvokeEndGameInternalAsync(service);
            Assert.False(endResult.IsError);

            // Act: staff/admin forced start should clear pending featured
            await InvokeStartGameWithForceTerminationAsync(service, Gamemodes.GAMEMODE_REVERSE_SENTENCE);

            // Next natural end should not use the old featured override
            selector.Reset();
            selector
                .Setup(s => s.TrySelectFeaturedGamemode(It.IsAny<Gamemodes>()))
                .Returns((Gamemodes?)null);

            var secondEnd = await InvokeEndGameInternalAsync(service);
            Assert.False(secondEnd.IsError);
            Assert.NotEqual(Gamemodes.GAMEMODE_LETTER_VOTE, service.GetCurrentGameMode());
        }

        [Fact]
        public async Task ForceTerminate_DoesNotTriggerFeatured()
        {
            // Arrange
            var featuredMock = new Mock<IFeaturedGamemodeSelector>();
            var service = new GameService(_mockGames, _mockLogger.Object, featuredMock.Object);

            await InvokeStartGameWithForceTerminationAsync(service, Gamemodes.GAMEMODE_CASUAL);

            // Act
            await InvokeForceTerminateCurrentGameInternalAsync(service);

            // Assert
            featuredMock.Verify(s => s.TrySelectFeaturedGamemode(It.IsAny<Gamemodes>()), Times.Never);
        }

        [Fact]
        public async Task Selector_CalledOnce_PerNaturalEnd()
        {
            // Arrange
            var featuredMock = new Mock<IFeaturedGamemodeSelector>();
            featuredMock
                .Setup(s => s.TrySelectFeaturedGamemode(It.IsAny<Gamemodes>()))
                .Returns((Gamemodes?)null);

            var service = new GameService(_mockGames, _mockLogger.Object, featuredMock.Object);
            await InvokeStartGameWithForceTerminationAsync(service, Gamemodes.GAMEMODE_CASUAL);

            // Act
            var endResult1 = await InvokeEndGameInternalAsync(service);
            var endResult2 = await InvokeEndGameInternalAsync(service);

            // Assert
            Assert.False(endResult1.IsError);
            Assert.False(endResult2.IsError);
            featuredMock.Verify(s => s.TrySelectFeaturedGamemode(It.IsAny<Gamemodes>()), Times.Exactly(2));
        }
    }

    // Test implementation of IGamemodeHandler
    public class TestGamemodeHandler : IGamemodeHandler
    {
        private GameState _gameState;
        private bool _shouldThrowOnEndGame = false;

        public TestGamemodeHandler(GameStatus initialStatus)
        {
            _gameState = new GameState 
            { 
                GameId = Guid.NewGuid(), 
                CurrentState = initialStatus 
            };
        }

        public GameState GameState 
        { 
            get => _gameState; 
            set => _gameState = value; 
        }

        public Task StartGame()
        {
            _gameState.CurrentState = GameStatus.IN_PROGRESS;
            return Task.CompletedTask;
        }

        public Task<ErrorOr<bool>> AddMessage(SocketMessage Message)
        {
            return Task.FromResult<ErrorOr<bool>>(true);
        }

        public Task<ErrorOr<bool>> DeleteMessage(ulong msgid)
        {
            return Task.FromResult<ErrorOr<bool>>(true);
        }

        public Task EndGame()
        {
            if (_shouldThrowOnEndGame)
            {
                throw new InvalidOperationException("Test exception");
            }
            _gameState.CurrentState = GameStatus.ENDED;
            return Task.CompletedTask;
        }

        public void ShouldThrowOnEndGame()
        {
            _shouldThrowOnEndGame = true;
        }
    }
}