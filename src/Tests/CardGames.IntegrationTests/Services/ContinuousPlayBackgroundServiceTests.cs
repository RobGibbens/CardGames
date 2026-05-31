using System.Diagnostics.Metrics;
using System.Reflection;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
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
    private readonly DbContextOptions<CardsDbContext> _dbOptions;
    private readonly FakeServiceScopeFactory _scopeFactory;
    private readonly ContinuousPlayBackgroundService _service;
    private readonly FakeLogger<ContinuousPlayBackgroundService> _logger;
    private readonly FakeGameFlowHandlerFactory _flowHandlerFactory;
    private readonly FakeGameStateBroadcaster _broadcaster;
    private readonly FakeHandHistoryRecorder _recorder;

    public ContinuousPlayBackgroundServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<CardsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext = new CardsDbContext(_dbOptions);
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
    public async Task ProcessGamesReadyForNextHandAsync_AbandonedLinkedOneOffGame_MarksEventCompleted()
    {
        var league = new League
        {
            Id = Guid.NewGuid(),
            Name = "Test League",
            CreatedByUserId = "test-user",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Dealing",
            Status = GameStatus.InProgress,
            GamePlayers = new List<GamePlayer>(),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var oneOffEvent = new LeagueOneOffEvent
        {
            Id = Guid.NewGuid(),
            LeagueId = league.Id,
            Name = "Cash Game",
            CreatedByUserId = "test-user",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Status = LeagueOneOffEventStatus.Planned,
            EventType = LeagueOneOffEventType.CashGame,
            LaunchedGameId = game.Id
        };

        _dbContext.Leagues.Add(league);
        _dbContext.Games.Add(game);
        _dbContext.LeagueOneOffEvents.Add(oneOffEvent);
        await _dbContext.SaveChangesAsync();

        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        var updatedEvent = await _dbContext.LeagueOneOffEvents.FindAsync(oneOffEvent.Id);
        updatedEvent.Should().NotBeNull();
        updatedEvent!.Status.Should().Be(LeagueOneOffEventStatus.Completed);
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
        handler.PrepareForNewHandCalled.Should().BeTrue();
        handler.OnHandStartingCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_ReadyForNextHand_RecordsAdvancedMetric()
    {
        // Arrange
        var measurements = new List<CounterMeasurement>();
        using var listener = StartContinuousPlayMeterListener(measurements);
        using var telemetryServices = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var service = new ContinuousPlayBackgroundService(
            _scopeFactory,
            _logger,
            new ContinuousPlayTelemetry(telemetryServices.GetRequiredService<IMeterFactory>()));
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "METRICGAME", Name = "Metric Game" },
            DealerPosition = 0
        };

        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 1000, LeftAtHandNumber = -1 });
        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 1, ChipStack = 1000, LeftAtHandNumber = -1 });

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _flowHandlerFactory.SetHandlerForCode("METRICGAME");

        // Act
        await service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        measurements.Any(m =>
            m.InstrumentName == "continuous_play_games_processed_total" &&
            m.Value == 1 &&
            m.Tags.TryGetValue("phase", out var phase) &&
            (string?)phase == "next_hand" &&
            m.Tags.TryGetValue("outcome", out var outcome) &&
            (string?)outcome == "advanced")
            .Should().BeTrue();
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_ReadyForNextHand_RecordsFailedMetricAndContinues()
    {
        // Arrange
        var measurements = new List<CounterMeasurement>();
        using var listener = StartContinuousPlayMeterListener(measurements);
        using var telemetryServices = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var service = new ContinuousPlayBackgroundService(
            _scopeFactory,
            _logger,
            new ContinuousPlayTelemetry(telemetryServices.GetRequiredService<IMeterFactory>()));
        var now = DateTimeOffset.UtcNow;
        var failingGame = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "FAILINGMETRICGAME", Name = "Failing Metric Game" },
            DealerPosition = 0
        };
        var succeedingGame = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "CONTINUEMETRICGAME", Name = "Continue Metric Game" },
            DealerPosition = 0
        };

        AddActivePlayers(failingGame);
        AddActivePlayers(succeedingGame);
        _dbContext.Games.AddRange(failingGame, succeedingGame);
        await _dbContext.SaveChangesAsync();

        _flowHandlerFactory.SetHandlerForCode("FAILINGMETRICGAME").ThrowOnPrepareForNewHand = true;
        _flowHandlerFactory.SetHandlerForCode("CONTINUEMETRICGAME");

        // Act
        var act = () => service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        measurements.Any(m =>
            m.InstrumentName == "continuous_play_games_processed_total" &&
            m.Value == 1 &&
            m.Tags.TryGetValue("phase", out var phase) &&
            (string?)phase == "next_hand" &&
            m.Tags.TryGetValue("outcome", out var outcome) &&
            (string?)outcome == "failed")
            .Should().BeTrue();

        var updatedSucceedingGame = await _dbContext.Games.FindAsync(succeedingGame.Id);
        updatedSucceedingGame!.CurrentHandNumber.Should().Be(2);
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
    public async Task ProcessGamesReadyForNextHandAsync_TournamentBustedPlayer_RemainsSeatedAsObserver()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 2,
            Ante = 10,
            TournamentBuyIn = 100,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };

        var fundedPlayer = new GamePlayer
        {
            GameId = game.Id,
            PlayerId = Guid.NewGuid(),
            Status = GamePlayerStatus.Active,
            SeatPosition = 0,
            ChipStack = 100,
            LeftAtHandNumber = -1
        };

        var bustedPlayer = new GamePlayer
        {
            GameId = game.Id,
            PlayerId = Guid.NewGuid(),
            Status = GamePlayerStatus.Active,
            SeatPosition = 1,
            ChipStack = 0,
            LeftAtHandNumber = -1
        };

        game.GamePlayers.Add(fundedPlayer);
        game.GamePlayers.Add(bustedPlayer);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.InitialPhase = "Dealing";

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == game.Id);

        var updatedBustedPlayer = updatedGame.GamePlayers.Single(gp => gp.SeatPosition == 1);
        updatedBustedPlayer.Status.Should().Be(GamePlayerStatus.Eliminated);
        updatedBustedPlayer.IsSittingOut.Should().BeTrue();
        updatedBustedPlayer.FinalChipCount.Should().Be(0);
        updatedBustedPlayer.LeftAt.Should().NotBeNull();

        updatedGame.CurrentPhase.Should().Be("Ended");
        updatedGame.Status.Should().Be(GameStatus.Completed);
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_TournamentWithSingleFundedPlayer_CompletesGame()
    {
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 3,
            Ante = 10,
            TournamentBuyIn = 100,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };

        game.GamePlayers.Add(new GamePlayer
        {
            GameId = game.Id,
            PlayerId = Guid.NewGuid(),
            Status = GamePlayerStatus.Active,
            SeatPosition = 0,
            ChipStack = 120,
            LeftAtHandNumber = -1
        });

        game.GamePlayers.Add(new GamePlayer
        {
            GameId = game.Id,
            PlayerId = Guid.NewGuid(),
            Status = GamePlayerStatus.Active,
            SeatPosition = 1,
            ChipStack = 0,
            LeftAtHandNumber = -1
        });

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.InitialPhase = "Dealing";

        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        var updatedGame = await _dbContext.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == game.Id);

        updatedGame.Status.Should().Be(GameStatus.Completed);
        updatedGame.CurrentPhase.Should().Be("Ended");
        updatedGame.HandCompletedAt.Should().NotBeNull();
        updatedGame.EndedAt.Should().NotBeNull();
        updatedGame.NextHandStartsAt.Should().BeNull();
        _broadcaster.ToastNotifications.Should().Contain(t => t.GameId == game.Id && t.Message.Contains("Tournament complete"));
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
    public async Task ProcessGamesReadyForNextHandAsync_ScrewYourNeighbor_FirstHand_DoesNotCollectAntes()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 0,
            Ante = 25,
            GameType = new GameType { Code = "SCREWYOURNEIGHBOR", Name = "Screw Your Neighbor" }
        };

        var p1 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 100, LeftAtHandNumber = -1 };
        var p2 = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 1, ChipStack = 100, LeftAtHandNumber = -1 };
        game.GamePlayers.Add(p1);
        game.GamePlayers.Add(p2);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("SCREWYOURNEIGHBOR");
        handler.SkipsAnteCollection = true;
        handler.IsMultiHandVariant = true;
        handler.InitialPhase = "Dealing";

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedP1 = await _dbContext.GamePlayers.FindAsync(p1.Id);
        var updatedP2 = await _dbContext.GamePlayers.FindAsync(p2.Id);
        updatedP1!.ChipStack.Should().Be(100);
        updatedP2!.ChipStack.Should().Be(100);

        var pot = await _dbContext.Pots.FirstOrDefaultAsync(p => p.GameId == game.Id && p.HandNumber == 1 && p.PotType == PotType.Main);
        pot.Should().NotBeNull();
        pot!.Amount.Should().Be(0);
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

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_CashGameSingleFundedPlayerAndBustedSitOut_StartsRebuyGracePause()
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

        var funded = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 1000, LeftAtHandNumber = -1 };
        var busted = new GamePlayer
        {
            GameId = game.Id,
            PlayerId = Guid.NewGuid(),
            Status = GamePlayerStatus.SittingOut,
            IsSittingOut = true,
            SeatPosition = 1,
            ChipStack = 0,
            LeftAtHandNumber = -1
        };
        game.GamePlayers.Add(funded);
        game.GamePlayers.Add(busted);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.RequiresChipCoverageCheck = false;

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame.Should().NotBeNull();
        updatedGame!.IsPausedForRebuyGrace.Should().BeTrue();
        updatedGame.IsPausedForChipCheck.Should().BeTrue();
        updatedGame.RebuyGraceEndsAt.Should().NotBeNull();
        updatedGame.CurrentPhase.Should().Be("WaitingForPlayers");
        updatedGame.Status.Should().Be(GameStatus.BetweenHands);
        handler.DealCardsCalled.Should().BeFalse();
        _broadcaster.ToastNotifications.Should().ContainSingle(t => t.GameId == game.Id && t.Message.Contains("Rebuy window started"));
    }

    [Fact]
    public async Task HandleCashGameRebuyGraceTimerExpiredAsync_EndsGameAndStartsLobbyCountdown()
    {
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "WaitingForPlayers",
            Status = GameStatus.BetweenHands,
            CurrentHandNumber = 1,
            IsPausedForChipCheck = true,
            ChipCheckPauseStartedAt = now.AddMinutes(-5),
            ChipCheckPauseEndsAt = now.AddSeconds(-1),
            IsPausedForRebuyGrace = true,
            RebuyGraceStartedAt = now.AddMinutes(-5),
            RebuyGraceEndsAt = now.AddSeconds(-1)
        };

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var method = typeof(ContinuousPlayBackgroundService).GetMethod(
            "HandleCashGameRebuyGraceTimerExpiredAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull("the rebuy grace expiry callback should exist so the server can end paused cash games");

        var task = method!.Invoke(_service, [game.Id]) as Task;
        task.Should().NotBeNull();
        await task!;

        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame.Should().NotBeNull();
        updatedGame!.CurrentPhase.Should().Be("Ended");
        updatedGame.Status.Should().Be(GameStatus.Completed);
        updatedGame.HandCompletedAt.Should().NotBeNull();
        updatedGame.NextHandStartsAt.Should().NotBeNull();
        updatedGame.NextHandStartsAt.Should().BeAfter(updatedGame.HandCompletedAt!.Value);
        updatedGame.NextHandStartsAt.Should().BeOnOrBefore(updatedGame.HandCompletedAt.Value.AddSeconds(ContinuousPlayBackgroundService.CashRebuyGameOverDisplayDurationSeconds + 1));
        updatedGame.IsPausedForChipCheck.Should().BeFalse();
        updatedGame.IsPausedForRebuyGrace.Should().BeFalse();
        _broadcaster.ToastNotifications.Should().Contain(t => t.GameId == game.Id && t.Message.Contains("Game ended"));
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_RebuyGraceActiveWithRemainingBustedPlayers_KeepsPauseActive()
    {
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "WaitingForPlayers",
            Status = GameStatus.BetweenHands,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            Ante = 10,
            IsPausedForChipCheck = true,
            ChipCheckPauseStartedAt = now.AddSeconds(-10),
            ChipCheckPauseEndsAt = now.AddSeconds(10),
            IsPausedForRebuyGrace = true,
            RebuyGraceStartedAt = now.AddSeconds(-10),
            RebuyGraceEndsAt = now.AddSeconds(10),
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };

        var fundedOne = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 100, LeftAtHandNumber = -1 };
        var fundedTwo = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 1, ChipStack = 50, IsSittingOut = false, LeftAtHandNumber = -1 };
        var busted = new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.SittingOut, SeatPosition = 2, ChipStack = 0, IsSittingOut = true, LeftAtHandNumber = -1 };
        game.GamePlayers.Add(fundedOne);
        game.GamePlayers.Add(fundedTwo);
        game.GamePlayers.Add(busted);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.RequiresChipCoverageCheck = false;

        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame.Should().NotBeNull();
        updatedGame!.IsPausedForRebuyGrace.Should().BeTrue();
        updatedGame.IsPausedForChipCheck.Should().BeTrue();
        updatedGame.Status.Should().Be(GameStatus.BetweenHands);
        handler.DealCardsCalled.Should().BeFalse();
    }

    [Fact]
    public async Task HandleCashGameRebuyGraceTimerExpiredAsync_WithTwoFundedPlayers_ResumesPlay()
    {
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "WaitingForPlayers",
            Status = GameStatus.BetweenHands,
            CurrentHandNumber = 1,
            Ante = 10,
            IsPausedForChipCheck = true,
            ChipCheckPauseStartedAt = now.AddMinutes(-5),
            ChipCheckPauseEndsAt = now.AddSeconds(-1),
            IsPausedForRebuyGrace = true,
            RebuyGraceStartedAt = now.AddMinutes(-5),
            RebuyGraceEndsAt = now.AddSeconds(-1)
        };

        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 100, LeftAtHandNumber = -1 });
        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 1, ChipStack = 25, LeftAtHandNumber = -1 });
        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.SittingOut, SeatPosition = 2, ChipStack = 0, IsSittingOut = true, LeftAtHandNumber = -1 });

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var method = typeof(ContinuousPlayBackgroundService).GetMethod(
            "HandleCashGameRebuyGraceTimerExpiredAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var task = method!.Invoke(_service, [game.Id]) as Task;
        task.Should().NotBeNull();
        await task!;

        var updatedGame = await _dbContext.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == game.Id);

        updatedGame.IsPausedForChipCheck.Should().BeFalse();
        updatedGame.IsPausedForRebuyGrace.Should().BeFalse();
        updatedGame.Status.Should().Be(GameStatus.BetweenHands);
        updatedGame.CurrentPhase.Should().Be("WaitingForPlayers");
        updatedGame.NextHandStartsAt.Should().NotBeNull();
        updatedGame.EndedAt.Should().BeNull();
        updatedGame.GamePlayers.Should().ContainSingle(gp => gp.ChipStack == 0 && gp.Status == GamePlayerStatus.SittingOut && gp.IsSittingOut);
        _broadcaster.ToastNotifications.Should().Contain(t => t.GameId == game.Id && t.Message.Contains("Resuming play without busted players"));
    }

    [Fact]
    public async Task HandleCashGameRebuyGraceTimerExpiredAsync_MarksLinkedOneOffEventCompleted()
    {
        var now = DateTimeOffset.UtcNow;
        var league = new League
        {
            Id = Guid.NewGuid(),
            Name = "Cash League",
            CreatedByUserId = "test-user",
            CreatedAtUtc = now.AddDays(-1)
        };
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "WaitingForPlayers",
            Status = GameStatus.BetweenHands,
            CurrentHandNumber = 1,
            IsPausedForChipCheck = true,
            ChipCheckPauseStartedAt = now.AddMinutes(-5),
            ChipCheckPauseEndsAt = now.AddSeconds(-1),
            IsPausedForRebuyGrace = true,
            RebuyGraceStartedAt = now.AddMinutes(-5),
            RebuyGraceEndsAt = now.AddSeconds(-1)
        };
        var oneOffEvent = new LeagueOneOffEvent
        {
            Id = Guid.NewGuid(),
            LeagueId = league.Id,
            Name = "Cash One-Off",
            CreatedByUserId = "test-user",
            CreatedAtUtc = now.AddHours(-1),
            EventType = LeagueOneOffEventType.CashGame,
            Status = LeagueOneOffEventStatus.Planned,
            LaunchedGameId = game.Id
        };

        _dbContext.Leagues.Add(league);
        _dbContext.Games.Add(game);
        _dbContext.LeagueOneOffEvents.Add(oneOffEvent);
        await _dbContext.SaveChangesAsync();

        var method = typeof(ContinuousPlayBackgroundService).GetMethod(
            "HandleCashGameRebuyGraceTimerExpiredAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var task = method!.Invoke(_service, [game.Id]) as Task;
        task.Should().NotBeNull();
        await task!;

        var updatedEvent = await _dbContext.LeagueOneOffEvents.FindAsync(oneOffEvent.Id);
        updatedEvent.Should().NotBeNull();
        updatedEvent!.Status.Should().Be(LeagueOneOffEventStatus.Completed);
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_FinalizesQueuedLeave_CreditsWallet()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var leavingPlayer = new Player { Id = Guid.NewGuid(), Name = "Leaving Player", Email = "leaving@test.com" };
        var stayingPlayer = new Player { Id = Guid.NewGuid(), Name = "Staying Player", Email = "staying@test.com" };

        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };

        var p1 = new GamePlayer
        {
            GameId = game.Id,
            PlayerId = leavingPlayer.Id,
            Player = leavingPlayer,
            Status = GamePlayerStatus.Active,
            SeatPosition = 0,
            ChipStack = 250,
            LeftAtHandNumber = 1
        };

        var p2 = new GamePlayer
        {
            GameId = game.Id,
            PlayerId = stayingPlayer.Id,
            Player = stayingPlayer,
            Status = GamePlayerStatus.Active,
            SeatPosition = 1,
            ChipStack = 1000,
            LeftAtHandNumber = -1
        };

        game.GamePlayers.Add(p1);
        game.GamePlayers.Add(p2);

        _dbContext.Players.AddRange(leavingPlayer, stayingPlayer);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.InitialPhase = "Dealing";

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var account = await _dbContext.PlayerChipAccounts.FirstOrDefaultAsync(x => x.PlayerId == leavingPlayer.Id);
        account.Should().NotBeNull();
        account!.Balance.Should().Be(250);

        var ledgerEntry = await _dbContext.PlayerChipLedgerEntries
            .Where(x => x.PlayerId == leavingPlayer.Id && x.Type == PlayerChipLedgerEntryType.CashOut)
            .FirstOrDefaultAsync();
        ledgerEntry.Should().NotBeNull();
        ledgerEntry!.AmountDelta.Should().Be(250);

        var finalizedPlayer = await _dbContext.GamePlayers.FirstAsync(x => x.Id == p1.Id);
        finalizedPlayer.Status.Should().Be(GamePlayerStatus.Left);
        finalizedPlayer.FinalChipCount.Should().Be(250);
    }

    // ---------------------------------------------------------------------
    // Transition safety tests
    //
    // These tests lock down the continuous-progression behaviour at the
    // transition level: timing boundaries, idempotency, retry safety,
    // stale/concurrent EF state, and one-time side effects (broadcast /
    // league completion sync). They are intended to fail loudly if a future
    // refactor of ContinuousPlayBackgroundService removes a timing guard,
    // drops an idempotency guard, or duplicates a side effect.
    // ---------------------------------------------------------------------

    private static GamePlayer ActivePlayer(Guid gameId, int seat, int chips = 1000) => new()
    {
        GameId = gameId,
        PlayerId = Guid.NewGuid(),
        Status = GamePlayerStatus.Active,
        SeatPosition = seat,
        ChipStack = chips,
        LeftAtHandNumber = -1
    };

    // --- A. Timing boundaries ------------------------------------------------

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_DoesNotStartNextHand_BeforeScheduledTime()
    {
        // Arrange: next hand scheduled in the future — threshold not yet crossed.
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(30),
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };
        game.GamePlayers.Add(ActivePlayer(game.Id, 0));
        game.GamePlayers.Add(ActivePlayer(game.Id, 1));
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.InitialPhase = "Dealing";

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert: nothing advanced before the scheduled time.
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentHandNumber.Should().Be(1);
        updatedGame.CurrentPhase.Should().Be("Complete");
        handler.DealCardsCalled.Should().BeFalse();
        _broadcaster.Broadcasts.Should().NotContain(b => b.GameId == game.Id);
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_StartsNextHand_AtScheduledThreshold()
    {
        // Arrange: NextHandStartsAt exactly at "now" exercises the inclusive `<= now` boundary.
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now,
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };
        game.GamePlayers.Add(ActivePlayer(game.Id, 0));
        game.GamePlayers.Add(ActivePlayer(game.Id, 1));
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
        updatedGame.NextHandStartsAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_DrawComplete_DoesNotTransition_BeforeDisplayWindowExpires()
    {
        // Arrange: draw completed "now" — the display window has not yet elapsed.
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "DrawComplete",
            Status = GameStatus.InProgress,
            DrawCompletedAt = now,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };
        game.GamePlayers.Add(ActivePlayer(game.Id, 0));
        game.GamePlayers.Add(ActivePlayer(game.Id, 1));
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.NextPhase = "Showdown";

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert: still in DrawComplete, no transition work performed.
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be("DrawComplete");
        handler.ProcessDrawCompleteCalled.Should().BeFalse();
        _broadcaster.Broadcasts.Should().NotContain(b => b.GameId == game.Id);
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_DrawComplete_Transitions_JustAfterWindowBoundary()
    {
        // Arrange: draw completed just past the display window boundary.
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "DrawComplete",
            Status = GameStatus.InProgress,
            DrawCompletedAt = now.AddSeconds(-(ContinuousPlayBackgroundService.DrawCompleteDisplayDurationSeconds + 1)),
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };
        game.GamePlayers.Add(ActivePlayer(game.Id, 0));
        game.GamePlayers.Add(ActivePlayer(game.Id, 1));
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
    public async Task ProcessGamesReadyForNextHandAsync_KlondikeReveal_DoesNotTransition_BeforeWindowExpires()
    {
        // Arrange: reveal window is 20s; only 10s have elapsed.
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "KlondikeReveal",
            Status = GameStatus.InProgress,
            DrawCompletedAt = now.AddSeconds(-10),
            GameType = new GameType { Code = "KLONDIKE", Name = "Klondike" }
        };
        game.GamePlayers.Add(ActivePlayer(game.Id, 0));
        game.GamePlayers.Add(ActivePlayer(game.Id, 1));
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert: not advanced and not abandoned (active players present).
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be("KlondikeReveal");
        updatedGame.Status.Should().Be(GameStatus.InProgress);
        _broadcaster.Broadcasts.Should().NotContain(b => b.GameId == game.Id);
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_KlondikeReveal_Transitions_AfterWindowExpires()
    {
        // Arrange: reveal window is 20s; 25s have elapsed.
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "KlondikeReveal",
            Status = GameStatus.InProgress,
            DrawCompletedAt = now.AddSeconds(-(ContinuousPlayBackgroundService.KlondikeRevealDisplayDurationSeconds + 5)),
            GameType = new GameType { Code = "KLONDIKE", Name = "Klondike" }
        };
        game.GamePlayers.Add(ActivePlayer(game.Id, 0));
        game.GamePlayers.Add(ActivePlayer(game.Id, 1));
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be("Showdown");
        _broadcaster.Broadcasts.Should().Contain(b => b.GameId == game.Id);
    }

    // --- B/C. Idempotency & retry safety ------------------------------------

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_RunTwice_DoesNotStartTwoHands()
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
        game.GamePlayers.Add(ActivePlayer(game.Id, 0));
        game.GamePlayers.Add(ActivePlayer(game.Id, 1));
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.InitialPhase = "Dealing";

        // Act: first run advances exactly one hand.
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);
        var afterFirst = await _dbContext.Games.FindAsync(game.Id);
        afterFirst!.CurrentHandNumber.Should().Be(2);
        afterFirst.NextHandStartsAt.Should().BeNull();
        var broadcastsAfterFirst = _broadcaster.Broadcasts.Count(b => b.GameId == game.Id);

        // Act: second run is a safe no-op (NextHandStartsAt guard already cleared).
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert: no second hand, no duplicated broadcast.
        var afterSecond = await _dbContext.Games.FindAsync(game.Id);
        afterSecond!.CurrentHandNumber.Should().Be(2);
        afterSecond.CurrentPhase.Should().Be("Dealing");
        _broadcaster.Broadcasts.Count(b => b.GameId == game.Id).Should().Be(broadcastsAfterFirst);
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_AbandonedGame_RunTwice_DoesNotDuplicateCompletion()
    {
        // Arrange: an abandoned game (no remaining players).
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

        // Act: run the progression routine twice.
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert: completed once; the completed game is excluded on the second pass,
        // so the completion broadcast is not duplicated.
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.Status.Should().Be(GameStatus.Completed);
        updatedGame.CurrentPhase.Should().Be("Complete");
        _broadcaster.Broadcasts.Count(b => b.GameId == game.Id).Should().Be(1);
    }

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_DrawComplete_RunTwice_IsIdempotent()
    {
        // Arrange: a DrawComplete game whose display window has expired.
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "DrawComplete",
            Status = GameStatus.InProgress,
            DrawCompletedAt = now.AddSeconds(-10),
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" }
        };
        game.GamePlayers.Add(ActivePlayer(game.Id, 0));
        game.GamePlayers.Add(ActivePlayer(game.Id, 1));
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.NextPhase = "Showdown";

        // Act: first run transitions to Showdown.
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);
        var afterFirst = await _dbContext.Games.FindAsync(game.Id);
        afterFirst!.CurrentPhase.Should().Be("Showdown");
        handler.ProcessDrawCompleteCalled.Should().BeTrue();
        var broadcastsAfterFirst = _broadcaster.Broadcasts.Count(b => b.GameId == game.Id);

        // Reset the call flag to detect any duplicate transition on the second pass.
        handler.ProcessDrawCompleteCalled = false;

        // Act: second run must not re-process (game is no longer in DrawComplete).
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var afterSecond = await _dbContext.Games.FindAsync(game.Id);
        afterSecond!.CurrentPhase.Should().Be("Showdown");
        handler.ProcessDrawCompleteCalled.Should().BeFalse();
        _broadcaster.Broadcasts.Count(b => b.GameId == game.Id).Should().Be(broadcastsAfterFirst);
    }

    // --- D. Stale / concurrent EF state -------------------------------------

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_StaleConcurrentContext_DoesNotDoubleAdvance()
    {
        // Arrange: a game ready for its next hand.
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
        game.GamePlayers.Add(ActivePlayer(game.Id, 0));
        game.GamePlayers.Add(ActivePlayer(game.Id, 1));
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _flowHandlerFactory.SetHandlerForCode("TESTGAME").InitialPhase = "Dealing";

        // A second service with its own EF context over the SAME in-memory store,
        // simulating a concurrent polling cycle that preloaded the game while it
        // was still "ready" (stale view of the world).
        await using var staleContext = new CardsDbContext(_dbOptions);
        // Preload the game into the stale context's change tracker while it still
        // looks "ready", capturing a stale snapshot that must not cause a re-advance.
        var staleGameSnapshot = await staleContext.Games
            .Include(g => g.GamePlayers)
            .Include(g => g.GameType)
            .FirstAsync(g => g.Id == game.Id);
        staleGameSnapshot.CurrentHandNumber.Should().Be(1);

        var staleBroadcaster = new FakeGameStateBroadcaster();
        var staleFlowFactory = new FakeGameFlowHandlerFactory();
        staleFlowFactory.SetHandlerForCode("TESTGAME").InitialPhase = "Dealing";
        var staleScopeFactory = new FakeServiceScopeFactory(
            staleContext, staleBroadcaster, new FakeHandHistoryRecorder(), staleFlowFactory);
        using var staleService = new ContinuousPlayBackgroundService(
            staleScopeFactory, new FakeLogger<ContinuousPlayBackgroundService>());

        // Act: the primary service advances the hand first.
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Then the stale/concurrent service runs the same progression path.
        await staleService.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert: exactly one advancement survived; no double-advance or duplicate side effect.
        await using var verifyContext = new CardsDbContext(_dbOptions);
        var persisted = await verifyContext.Games.FindAsync(game.Id);
        persisted!.CurrentHandNumber.Should().Be(2);
        persisted.CurrentPhase.Should().Be("Dealing");
        persisted.NextHandStartsAt.Should().BeNull();
        staleBroadcaster.Broadcasts.Should().NotContain(b => b.GameId == game.Id);
    }

    // --- F. One-time side effects -------------------------------------------

    [Fact]
    public async Task ProcessGamesReadyForNextHandAsync_AbandonedLinkedOneOffGame_RunTwice_SyncsCompletionOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var league = new League
        {
            Id = Guid.NewGuid(),
            Name = "Test League",
            CreatedByUserId = "test-user",
            CreatedAtUtc = now
        };
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Dealing",
            Status = GameStatus.InProgress,
            GamePlayers = new List<GamePlayer>(),
            UpdatedAt = now.AddMinutes(-1)
        };
        var oneOffEvent = new LeagueOneOffEvent
        {
            Id = Guid.NewGuid(),
            LeagueId = league.Id,
            Name = "Cash Game",
            CreatedByUserId = "test-user",
            CreatedAtUtc = now,
            Status = LeagueOneOffEventStatus.Planned,
            EventType = LeagueOneOffEventType.CashGame,
            LaunchedGameId = game.Id
        };

        _dbContext.Leagues.Add(league);
        _dbContext.Games.Add(game);
        _dbContext.LeagueOneOffEvents.Add(oneOffEvent);
        await _dbContext.SaveChangesAsync();

        // Act: run the abandoned-game path twice.
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert: the linked event is completed and the completion broadcast happens once.
        var updatedEvent = await _dbContext.LeagueOneOffEvents.FindAsync(oneOffEvent.Id);
        updatedEvent!.Status.Should().Be(LeagueOneOffEventStatus.Completed);
        _broadcaster.Broadcasts.Count(b => b.GameId == game.Id).Should().Be(1);
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

    private sealed record CounterMeasurement(string InstrumentName, long Value, Dictionary<string, object?> Tags);

    private static MeterListener StartContinuousPlayMeterListener(List<CounterMeasurement> measurements)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ContinuousPlayTelemetry.MeterName &&
                instrument.Name == "continuous_play_games_processed_total")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var tagValues = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagValues[tag.Key] = tag.Value;
            }

            measurements.Add(new CounterMeasurement(instrument.Name, measurement, tagValues));
        });
        listener.Start();
        return listener;
    }

    private static void AddActivePlayers(Game game)
    {
        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 1000, LeftAtHandNumber = -1 });
        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 1, ChipStack = 1000, LeftAtHandNumber = -1 });
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
        private readonly IPlayerChipWalletService _playerChipWalletService;
        private readonly ILeagueBroadcaster _leagueBroadcaster;
        private readonly LeagueGameCompletionSyncService _leagueCompletionSyncService;

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
			_playerChipWalletService = new PlayerChipWalletService(_dbContext);
			_leagueBroadcaster = new FakeLeagueBroadcaster();
			_leagueCompletionSyncService = new LeagueGameCompletionSyncService(
				_dbContext,
				_leagueBroadcaster,
				new FakeLogger<LeagueGameCompletionSyncService>());
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(CardsDbContext)) return _dbContext;
            if (serviceType == typeof(IGameStateBroadcaster)) return _broadcaster;
            if (serviceType == typeof(IHandHistoryRecorder)) return _recorder;
            if (serviceType == typeof(IGameFlowHandlerFactory)) return _handlerFactory;
			if (serviceType == typeof(IPlayerChipWalletService)) return _playerChipWalletService;
			if (serviceType == typeof(ILeagueBroadcaster)) return _leagueBroadcaster;
			if (serviceType == typeof(LeagueGameCompletionSyncService)) return _leagueCompletionSyncService;
            return null;
        }
    }

    private class FakeLeagueBroadcaster : ILeagueBroadcaster
    {
        public Task BroadcastLeagueEventChangedAsync(LeagueEventChangedDto eventChanged, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task BroadcastEventSessionLaunchedAsync(LeagueEventSessionLaunchedDto sessionLaunched, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task BroadcastJoinRequestSubmittedAsync(LeagueJoinRequestSubmittedDto joinRequestSubmitted, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task BroadcastJoinRequestUpdatedAsync(LeagueJoinRequestUpdatedDto joinRequestUpdated, string? requesterUserId = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class FakeGameStateBroadcaster : IGameStateBroadcaster
    {
         public List<(Guid GameId, DateTimeOffset Time)> Broadcasts { get; } = new();
            public List<TableToastNotificationDto> ToastNotifications { get; } = new();

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

         public Task BroadcastTableToastAsync(TableToastNotificationDto notification, CancellationToken cancellationToken = default)
         {
             ToastNotifications.Add(notification);
             return Task.CompletedTask;
         }

         public Task BroadcastTableSettingsUpdatedAsync(TableSettingsUpdatedDto settings, CancellationToken cancellationToken = default)
         {
             return Task.CompletedTask;
         }

         public Task BroadcastOddsVisibilityUpdatedAsync(OddsVisibilityUpdatedDto notification, CancellationToken cancellationToken = default)
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
        public bool AutoCollectsAntesOnStart { get; set; } = false;
        public bool IsMultiHandVariant { get; set; } = false;
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
        public bool PrepareForNewHandCalled { get; set; }
        public bool OnHandCompletedCalled { get; set; }
        public bool ThrowOnPrepareForNewHand { get; set; }

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

        public Task PrepareForNewHandAsync(
            CardsDbContext context,
            Game game,
            List<GamePlayer> eligiblePlayers,
            int upcomingHandNumber,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            PrepareForNewHandCalled = true;
            if (ThrowOnPrepareForNewHand)
            {
                throw new InvalidOperationException("Forced prepare failure");
            }

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

        public Task PerformAutoActionAsync(AutoActionContext context)
        {
            return Task.CompletedTask;
        }
    }
}
