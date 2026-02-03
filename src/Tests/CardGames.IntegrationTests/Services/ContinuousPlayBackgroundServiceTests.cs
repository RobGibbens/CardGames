using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Games.GameFlow;
using CardGames.Contracts.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.IntegrationTests.Services;

public class ContinuousPlayBackgroundServiceTests : IDisposable
{
    private readonly CardsDbContext _dbContext;
    private readonly FakeServiceScopeFactory _scopeFactory;
    private readonly ContinuousPlayBackgroundService _service;
    private readonly FakeLogger<ContinuousPlayBackgroundService> _logger;
    private readonly FakeGameFlowHandlerFactory _flowHandlerFactory;
    private readonly FakeGameStateBroadcaster _broadcaster;
    private readonly FakeHandHistoryRecorder _recorder;

    public ContinuousPlayBackgroundServiceTests()
    {
        var options = new DbContextOptionsBuilder<CardsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext = new CardsDbContext(options);
        _logger = new FakeLogger<ContinuousPlayBackgroundService>();
        
        _flowHandlerFactory = new FakeGameFlowHandlerFactory();
        _broadcaster = new FakeGameStateBroadcaster();
        _recorder = new FakeHandHistoryRecorder();

        _scopeFactory = new FakeServiceScopeFactory(_dbContext, _broadcaster, _recorder, _flowHandlerFactory);
        _service = new ContinuousPlayBackgroundService(_scopeFactory, _logger);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _service.Dispose();
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_AbandonedGame_MarksAsComplete()
    {
        // Arrange
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Dealing",
            Status = GameStatus.InProgress,
            GamePlayers = new List<GamePlayer>(),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame.Should().NotBeNull();
        updatedGame!.Status.Should().Be(GameStatus.Completed);
        updatedGame.CurrentPhase.Should().Be("Complete");
        _broadcaster.Broadcasts.Should().ContainSingle(b => b.GameId == game.Id);
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_ActiveAppearsAbandoned_ButHasActivePlayers_DoesNotMarkComplete()
    {
        // Arrange
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Dealing",
            Status = GameStatus.InProgress,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var player = new GamePlayer
        {
            GameId = game.Id,
            PlayerId = Guid.NewGuid(),
            Status = GamePlayerStatus.Active,
            LeftAtHandNumber = -1
        };
        game.GamePlayers.Add(player);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame.Should().NotBeNull();
        updatedGame!.Status.Should().Be(GameStatus.InProgress);
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_DrawComplete_TransitionsToShowdown()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "DrawComplete",
            Status = GameStatus.InProgress,
            DrawCompletedAt = now.AddSeconds(-10), // Expired
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.NextPhase = "Showdown";

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be("Showdown");
        handler.ProcessDrawCompleteCalled.Should().BeTrue();
        _broadcaster.Broadcasts.Should().Contain(b => b.GameId == game.Id);
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_DrawComplete_InlineShowdown_TransitionsToPostShowdown()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "DrawComplete",
            Status = GameStatus.InProgress,
            DrawCompletedAt = now.AddSeconds(-10),
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.NextPhase = "Showdown";
        handler.SupportsInlineShowdown = true;
        handler.PostShowdownPhase = "Complete";

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be("Complete");
        handler.ProcessDrawCompleteCalled.Should().BeTrue();
        handler.PerformShowdownCalled.Should().BeTrue();
        handler.ProcessPostShowdownCalled.Should().BeTrue();
    }
    
    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_ReadyForNextHand_StartsHand()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" },
            DealerPosition = 0
        };

        var p1 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 1000, LeftAtHandNumber = -1 };
        var p2 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 1, ChipStack = 1000, LeftAtHandNumber = -1 };
        game.GamePlayers.Add(p1);
        game.GamePlayers.Add(p2);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.InitialPhase = "Dealing";

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentHandNumber.Should().Be(2);
        updatedGame.CurrentPhase.Should().Be("Dealing");
        handler.DealCardsCalled.Should().BeTrue();
        handler.OnHandStartingCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_ReadyForNextHand_InsufficientPlayers_Pauses()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };

        // Only 1 player
        var p1 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 1000, LeftAtHandNumber = -1 };
        game.GamePlayers.Add(p1);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be("WaitingForPlayers");
        updatedGame.Status.Should().Be(GameStatus.BetweenHands);
        updatedGame.NextHandStartsAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_ReadyForNextHand_AutoSitsOutBrokePlayers()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            Ante = 10,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };

        var p1 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 1000, LeftAtHandNumber = -1 };
        var p2 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 1, ChipStack = 5, LeftAtHandNumber = -1 }; // Insufficient for ante
        var p3 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 2, ChipStack = 1000, LeftAtHandNumber = -1 };
        
        game.GamePlayers.Add(p1);
        game.GamePlayers.Add(p2);
        game.GamePlayers.Add(p3);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedP2 = await _dbContext.GamePlayers.FindAsync(p2.Id);
        updatedP2!.IsSittingOut.Should().BeTrue(); // Should be sat out
    }
    
    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_ReadyForNextHand_CollectsAntes()
    {
         // Arrange
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            Ante = 10,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };

        var p1 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 1000, LeftAtHandNumber = -1 };
        var p2 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 1, ChipStack = 1000, LeftAtHandNumber = -1 };
        game.GamePlayers.Add(p1);
        game.GamePlayers.Add(p2);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.SkipsAnteCollection = false;
        handler.InitialPhase = "Dealing"; // Usually after CollectingAntes, but service sets this based on handler. 
        // NOTE: The service calls CollectAntesAsync which sets phase to Dealing at the end. 
        // But before that, StartNextHandAsync sets CurrentPhase = flowHandler.GetInitialPhase(game).
        // If Ante > 0 and SkipsAnteCollection is false, it runs CollectAntesAsync.

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedP1 = await _dbContext.GamePlayers.FindAsync(p1.Id);
        updatedP1!.ChipStack.Should().Be(990);
        
        var pot = await _dbContext.Pots.FirstOrDefaultAsync(p => p.GameId == game.Id && p.HandNumber == 2);
        pot.Should().NotBeNull();
        pot!.Amount.Should().Be(20);
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_ChipCoverage_PausesIfShort()
    {
         // Arrange
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };

        // Create a pot that is not awarded (e.g. carry over)
        var pot = new Pot { GameId = game.Id, HandNumber = 2, Amount = 200, PotType = PotType.Main, IsAwarded = false };
        _dbContext.Pots.Add(pot);

        var p1 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 1000, LeftAtHandNumber = -1 };
        var p2 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 1, ChipStack = 50, LeftAtHandNumber = -1 }; // Short
        game.GamePlayers.Add(p1);
        game.GamePlayers.Add(p2);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.RequiresChipCoverageCheck = true;
        handler.ChipCheckConfig = new ChipCheckConfiguration { IsEnabled = true, PauseDuration = TimeSpan.FromMinutes(1), ShortageAction = ChipShortageAction.SitOut };

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.IsPausedForChipCheck.Should().BeTrue();
        updatedGame.ChipCheckPauseEndsAt.Should().NotBeNull();
        handler.DealCardsCalled.Should().BeFalse(); // Should NOT start hand
    }
    
    // Fakes
    private class FakeLogger<T> : ILogger<T>
    {
        public List<string> InfoLogs { get; } = new();
        public List<string> ErrorLogs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (logLevel == LogLevel.Error) ErrorLogs.Add(message);
            else InfoLogs.Add(message);
        }
    }

    private class FakeServiceScopeFactory : IServiceScopeFactory
    {
        private readonly CardsDbContext _dbContext;
        private readonly IGameStateBroadcaster _broadcaster;
        private readonly IHandHistoryRecorder _recorder;
        private readonly IGameFlowHandlerFactory _handlerFactory;

        public FakeServiceScopeFactory(
            CardsDbContext dbContext, 
            IGameStateBroadcaster broadcaster,
            IHandHistoryRecorder recorder,
            IGameFlowHandlerFactory handlerFactory)
        {
            _dbContext = dbContext;
            _broadcaster = broadcaster;
            _recorder = recorder;
            _handlerFactory = handlerFactory;
        }

        public IServiceScope CreateScope()
        {
            return new FakeServiceScope(new FakeServiceProvider(_dbContext, _broadcaster, _recorder, _handlerFactory));
        }
    }

    private class FakeServiceScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }

        public FakeServiceScope(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public void Dispose() { }
    }

    private class FakeServiceProvider : IServiceProvider
    {
        private readonly CardsDbContext _dbContext;
        private readonly IGameStateBroadcaster _broadcaster;
        private readonly IHandHistoryRecorder _recorder;
        private readonly IGameFlowHandlerFactory _handlerFactory;

        public FakeServiceProvider(
            CardsDbContext dbContext, 
            IGameStateBroadcaster broadcaster,
            IHandHistoryRecorder recorder,
            IGameFlowHandlerFactory handlerFactory)
        {
            _dbContext = dbContext;
            _broadcaster = broadcaster;
            _recorder = recorder;
            _handlerFactory = handlerFactory;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(CardsDbContext)) return _dbContext;
            if (serviceType == typeof(IGameStateBroadcaster)) return _broadcaster;
            if (serviceType == typeof(IHandHistoryRecorder)) return _recorder;
            if (serviceType == typeof(IGameFlowHandlerFactory)) return _handlerFactory;
            return null;
        }
    }

    private class FakeGameStateBroadcaster : IGameStateBroadcaster
    {
         public List<(Guid GameId, DateTimeOffset Time)> Broadcasts { get; } = new();

         public Task BroadcastGameStateAsync(Guid gameId, CancellationToken cancellationToken = default)
         {
             Broadcasts.Add((gameId, DateTimeOffset.UtcNow));
             return Task.CompletedTask;
         }

         public Task BroadcastGameStateToUserAsync(Guid gameId, string userId, CancellationToken cancellationToken = default)
         {
             return Task.CompletedTask;
         }

         public Task BroadcastPlayerJoinedAsync(Guid gameId, string playerName, int seatPosition, bool isRejoining, CancellationToken cancellationToken = default)
         {
             return Task.CompletedTask;
         }

         public Task BroadcastTableSettingsUpdatedAsync(TableSettingsUpdatedDto settings, CancellationToken cancellationToken = default)
         {
             return Task.CompletedTask;
         }

         public Task BroadcastPlayerActionAsync(Guid gameId, int seatPosition, string? action, string description, CancellationToken cancellationToken = default)
         {
             return Task.CompletedTask;
         }
    }

    private class FakeHandHistoryRecorder : IHandHistoryRecorder
    {
        public Task<bool> RecordHandHistoryAsync(RecordHandHistoryParameters parameters, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private class FakeGameFlowHandlerFactory : IGameFlowHandlerFactory
    {
        private readonly Dictionary<string, FakeGameFlowHandler> _handlers = new();

        public IGameFlowHandler GetHandler(string? gameTypeCode)
        {
            var code = gameTypeCode ?? "DEFAULT";
            if (!_handlers.ContainsKey(code))
            {
                _handlers[code] = new FakeGameFlowHandler(code);
            }
            return _handlers[code];
        }

        public bool TryGetHandler(string? gameTypeCode, out IGameFlowHandler? handler)
        {
             handler = GetHandler(gameTypeCode);
             return true;
        }

        public FakeGameFlowHandler SetHandlerForCode(string code)
        {
            var handler = new FakeGameFlowHandler(code);
            _handlers[code] = handler;
            return handler;
        }
    }

    private class FakeGameFlowHandler : IGameFlowHandler
    {
        public string GameTypeCode { get; }
        public bool SupportsInlineShowdown { get; set; } = false;
        public bool SkipsAnteCollection { get; set; } = false;
        public IReadOnlyList<string> SpecialPhases { get; } = new List<string>();
        public bool RequiresChipCoverageCheck { get; set; } = false;
        public ChipCheckConfiguration ChipCheckConfig { get; set; } = new ChipCheckConfiguration { IsEnabled = false, PauseDuration = TimeSpan.Zero, ShortageAction = ChipShortageAction.SitOut };

        public string InitialPhase { get; set; } = "Dealing";
        public string NextPhase { get; set; } = "NextPhase";
        public string PostShowdownPhase { get; set; } = "Complete";

        public bool ProcessDrawCompleteCalled { get; set; }
        public bool PerformShowdownCalled { get; set; }
        public bool ProcessPostShowdownCalled { get; set; }
        public bool DealCardsCalled { get; set; }
        public bool OnHandStartingCalled { get; set; }
        public bool OnHandCompletedCalled { get; set; }

        public FakeGameFlowHandler(string code)
        {
            GameTypeCode = code;
        }

        public GameRules GetGameRules() => null!;

        public string GetInitialPhase(Game game) => InitialPhase;

        public string? GetNextPhase(Game game, string currentPhase) => NextPhase;

        public DealingConfiguration GetDealingConfiguration() => new DealingConfiguration { PatternType = DealingPatternType.AllAtOnce, InitialCardsPerPlayer = 5 };

        public ChipCheckConfiguration GetChipCheckConfiguration() => ChipCheckConfig;

        public Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default)
        {
            OnHandStartingCalled = true;
            return Task.CompletedTask;
        }

        public Task OnHandCompletedAsync(Game game, CancellationToken cancellationToken = default)
        {
            OnHandCompletedCalled = true;
            return Task.CompletedTask;
        }

        public Task DealCardsAsync(CardsDbContext context, Game game, List<GamePlayer> eligiblePlayers, DateTimeOffset now, CancellationToken cancellationToken)
        {
            DealCardsCalled = true;
            return Task.CompletedTask;
        }

        public Task<ShowdownResult> PerformShowdownAsync(CardsDbContext context, Game game, IHandHistoryRecorder handHistoryRecorder, DateTimeOffset now, CancellationToken cancellationToken)
        {
            PerformShowdownCalled = true;
            return Task.FromResult(new ShowdownResult 
            { 
                IsSuccess = true,
                WinnerPlayerIds = new List<Guid>(),
                LoserPlayerIds = new List<Guid>(),
                TotalPotAwarded = 0
            });
        }

        public Task<string> ProcessDrawCompleteAsync(CardsDbContext context, Game game, IHandHistoryRecorder handHistoryRecorder, DateTimeOffset now, CancellationToken cancellationToken)
        {
            ProcessDrawCompleteCalled = true;
            return Task.FromResult(NextPhase);
        }

        public Task<string> ProcessPostShowdownAsync(CardsDbContext context, Game game, ShowdownResult showdownResult, DateTimeOffset now, CancellationToken cancellationToken)
        {
            ProcessPostShowdownCalled = true;
            return Task.FromResult(PostShowdownPhase);
        }
    }
}
