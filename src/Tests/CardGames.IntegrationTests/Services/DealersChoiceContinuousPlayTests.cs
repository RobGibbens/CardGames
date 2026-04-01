using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Services.Cache;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Games.GameFlow;
using CardGames.Contracts.SignalR;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.IntegrationTests.Services;

/// <summary>
/// Tests for ContinuousPlayBackgroundService behavior in Dealer's Choice mode.
/// </summary>
public class DealersChoiceContinuousPlayTests : IDisposable
{
    private readonly CardsDbContext _dbContext;
    private readonly FakeServiceScopeFactory _scopeFactory;
    private readonly ContinuousPlayBackgroundService _service;
    private readonly FakeLogger<ContinuousPlayBackgroundService> _logger;
    private readonly FakeGameFlowHandlerFactory _flowHandlerFactory;
    private readonly FakeGameStateBroadcaster _broadcaster;
    private readonly FakeHandHistoryRecorder _recorder;

    public DealersChoiceContinuousPlayTests()
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
        _service = new ContinuousPlayBackgroundService(
            _scopeFactory,
            new FakeActiveGameCache(),
            Options.Create(new ActiveGameCacheOptions { AdaptivePollingEnabled = false }),
            _logger);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _service.Dispose();
    }

    [Fact]
    public async Task ReadyForNextHand_DCGame_TransitionsToWaitingForDealerChoice()
    {
        // Arrange — DC game that just completed a hand (no carried-forward pot)
        var now = DateTimeOffset.UtcNow;
        var game = CreateDCGame(now, currentPhase: "Complete", dealerPosition: 0, dcDealerPosition: 0);
        AddActivePlayers(game, 3);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be(nameof(Phases.WaitingForDealerChoice));
        updatedGame.Status.Should().Be(GameStatus.BetweenHands);
        updatedGame.GameTypeId.Should().BeNull();
        updatedGame.CurrentHandGameTypeCode.Should().BeNull();
        updatedGame.Ante.Should().BeNull();
        updatedGame.MinBet.Should().BeNull();
        updatedGame.NextHandStartsAt.Should().BeNull();
    }

    [Fact]
    public async Task ReadyForNextHand_DCGame_AdvancesDealerPosition()
    {
        // Arrange — dealer at seat 0, should advance to seat 1
        var now = DateTimeOffset.UtcNow;
        var game = CreateDCGame(now, currentPhase: "Complete", dealerPosition: 0, dcDealerPosition: 0);
        AddActivePlayers(game, 3);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.DealersChoiceDealerPosition.Should().Be(1);
    }

    [Fact]
    public async Task ReadyForNextHand_DCGame_DealerWrapsAround()
    {
        // Arrange — dealer at last seat (2), should wrap to seat 0
        var now = DateTimeOffset.UtcNow;
        var game = CreateDCGame(now, currentPhase: "Complete", dealerPosition: 2, dcDealerPosition: 2);
        AddActivePlayers(game, 3);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.DealersChoiceDealerPosition.Should().Be(0);
    }

    [Fact]
    public async Task ReadyForNextHand_DCGame_SkipsInactivePlayers()
    {
        // Arrange — dealer at seat 0, seat 1 has left, should skip to seat 2
        var now = DateTimeOffset.UtcNow;
        var game = CreateDCGame(now, currentPhase: "Complete", dealerPosition: 0, dcDealerPosition: 0);

        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 1000, LeftAtHandNumber = -1 });
        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 1, ChipStack = 1000, LeftAtHandNumber = 1 }); // left
        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 2, ChipStack = 1000, LeftAtHandNumber = -1 });

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.DealersChoiceDealerPosition.Should().Be(2);
    }

    [Fact]
    public async Task ReadyForNextHand_DCGame_MultiHandContinuation_DoesNotRotateDealer()
    {
        // Arrange — DC game with a carried-forward pot (multi-hand continuation)
        var now = DateTimeOffset.UtcNow;
        var game = CreateDCGame(now, currentPhase: "Complete", dealerPosition: 0, dcDealerPosition: 0);
        AddActivePlayers(game, 3);

        _dbContext.Games.Add(game);

        // Add a pot with amount > 0 for the next hand (signals multi-hand continuation)
        var pot = new Pot
        {
            Id = Guid.CreateVersion7(),
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber + 1,
            PotType = PotType.Main,
            Amount = 50,
            IsAwarded = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Pots.Add(pot);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.InitialPhase = "Dealing";

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert — should continue normally, NOT go to WaitingForDealerChoice
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.DealersChoiceDealerPosition.Should().Be(0, "DC dealer should not rotate during multi-hand continuation");
        updatedGame.CurrentPhase.Should().NotBe(nameof(Phases.WaitingForDealerChoice));
    }

    [Fact]
    public async Task ReadyForNextHand_DCGame_MultiHandVariant_FirstHandEmptyPot_CollectsAntesAndContinues()
    {
        // Arrange — DC game with a multi-hand variant (e.g., Kings and Lows).
        // The first hand had no antes (SkipsAnteCollection = true), so pot was 0.
        // After the showdown, losers match 0 → no next-hand pot with funds.
        // With IsMultiHandVariant = true, the background service detects that the
        // current hand had no funded pot and continues the variant.
        var now = DateTimeOffset.UtcNow;
        var game = CreateDCGame(now, currentPhase: "Complete", dealerPosition: 0, dcDealerPosition: 0);
        game.Ante = 10;
        AddActivePlayers(game, 3);

        _dbContext.Games.Add(game);

        // Current hand's pot was 0 (no antes collected) and already awarded
        var currentHandPot = new Pot
        {
            Id = Guid.CreateVersion7(),
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            PotType = PotType.Main,
            Amount = 0,
            IsAwarded = true,
            AwardedAt = now,
            CreatedAt = now
        };
        _dbContext.Pots.Add(currentHandPot);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.InitialPhase = "Dealing";
        handler.SkipsAnteCollection = true;
        handler.IsMultiHandVariant = true;

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert — multi-hand variant whose first hand had an empty pot:
        // should NOT go to WaitingForDealerChoice, should continue the variant.
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().NotBe(nameof(Phases.WaitingForDealerChoice),
            "multi-hand variant should continue when current hand had no funded pot (first hand)");
        updatedGame.DealersChoiceDealerPosition.Should().Be(0,
            "DC dealer should not rotate during multi-hand variant");

        // Verify that antes were collected on the new hand
        var updatedPot = await _dbContext.Pots
            .FirstOrDefaultAsync(p => p.GameId == game.Id &&
                                      p.HandNumber == updatedGame.CurrentHandNumber &&
                                      p.PotType == PotType.Main);
        updatedPot.Should().NotBeNull();
        updatedPot!.Amount.Should().Be(30, "3 players * 10 ante = 30");
    }

    [Fact]
    public async Task ReadyForNextHand_DCGame_MultiHandVariant_FundedPotNoMatchingPot_EndsVariant()
    {
        // Arrange — DC game with a multi-hand variant where the variant has legitimately ended.
        // The current hand had a funded pot (e.g., 50 from pot matching) that was awarded,
        // but no next-hand pot was created (player beat the deck, or losers couldn't match).
        // The variant should END and transition to WaitingForDealerChoice.
        var now = DateTimeOffset.UtcNow;
        var game = CreateDCGame(now, currentPhase: "Complete", dealerPosition: 0, dcDealerPosition: 0);
        game.Ante = 10;
        AddActivePlayers(game, 3);

        _dbContext.Games.Add(game);

        // Current hand's pot was funded (from pot matching) and awarded — variant ran normally
        var currentHandPot = new Pot
        {
            Id = Guid.CreateVersion7(),
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            PotType = PotType.Main,
            Amount = 50,
            IsAwarded = true,
            AwardedAt = now,
            CreatedAt = now
        };
        _dbContext.Pots.Add(currentHandPot);
        // No pot for next hand — variant has ended (player beat the deck)
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.InitialPhase = "Dealing";
        handler.SkipsAnteCollection = true;
        handler.IsMultiHandVariant = true;

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert — variant has ended: should rotate DC dealer and go to WaitingForDealerChoice
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be(nameof(Phases.WaitingForDealerChoice),
            "variant ended legitimately — should transition to DC choice");
    }

    [Fact]
    public async Task ReadyForNextHand_DCGame_ScrewYourNeighborWinner_WithOneEligiblePlayer_ReturnsToWaitingForDealerChoice()
    {
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = nameof(Phases.Complete),
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "SCREWYOURNEIGHBOR", Name = "Screw Your Neighbor" },
            CurrentHandGameTypeCode = "SCREWYOURNEIGHBOR",
            DealerPosition = 2,
            DealersChoiceDealerPosition = 0,
            OriginalDealersChoiceDealerPosition = 0,
            IsDealersChoice = true,
            Ante = 25,
            MinBet = 25
        };

        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 0, ChipStack = 150, LeftAtHandNumber = -1 });
        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 1, ChipStack = 0, LeftAtHandNumber = -1, IsSittingOut = true });
        game.GamePlayers.Add(new GamePlayer { GameId = game.Id, PlayerId = Guid.NewGuid(), Status = GamePlayerStatus.Active, SeatPosition = 2, ChipStack = 0, LeftAtHandNumber = -1, IsSittingOut = true });

        _dbContext.Games.Add(game);
        _dbContext.Pots.Add(new Pot
        {
            Id = Guid.CreateVersion7(),
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            PotType = PotType.Main,
            Amount = 50,
            IsAwarded = true,
            AwardedAt = now,
            CreatedAt = now
        });
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("SCREWYOURNEIGHBOR");
        handler.InitialPhase = "Dealing";
        handler.SkipsAnteCollection = true;
        handler.IsMultiHandVariant = true;

        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame.Should().NotBeNull();
        updatedGame!.CurrentPhase.Should().Be(nameof(Phases.WaitingForDealerChoice));
        updatedGame.Status.Should().Be(GameStatus.BetweenHands);
        updatedGame.DealersChoiceDealerPosition.Should().Be(1,
            "the Dealer's Choice turn should advance from the original SYN picker, not get stranded in WaitingForPlayers");
        updatedGame.GameTypeId.Should().BeNull();
        updatedGame.CurrentHandGameTypeCode.Should().BeNull();
    }

    [Fact]
    public async Task ReadyForNextHand_DCGame_BroadcastsGameState()
    {
        var now = DateTimeOffset.UtcNow;
        var game = CreateDCGame(now, currentPhase: "Complete", dealerPosition: 0, dcDealerPosition: 0);
        AddActivePlayers(game, 3);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        _broadcaster.Broadcasts.Should().Contain(b => b.GameId == game.Id);
    }

    [Fact]
    public async Task ReadyForNextHand_NonDCGame_StillAutoStartsNormally()
    {
        // Regression: standard game is unaffected by DC logic
        var now = DateTimeOffset.UtcNow;
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" },
            DealerPosition = 0,
            IsDealersChoice = false
        };
        AddActivePlayers(game, 2);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("TESTGAME");
        handler.InitialPhase = "Dealing";

        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be("Dealing");
        updatedGame.CurrentHandNumber.Should().Be(2);
    }

    [Fact]
    public async Task DCGame_WaitingToStart_AfterDealerChoice_DealsHandCorrectly()
    {
        // When a DC game is in WaitingToStart (after dealer chose a game type via ChooseDealerGameCommand),
        // the background service should deal the hand and start the game — NOT rotate the DC dealer.
        // The DC rotation only happens after a hand completes (from Complete phase).
        var now = DateTimeOffset.UtcNow;
        var gameType = new GameType { Code = "FIVECARDDRAW", Name = "Five Card Draw" };
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = nameof(Phases.WaitingToStart),
            Status = GameStatus.BetweenHands,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = gameType,
            GameTypeId = gameType.Id,
            CurrentHandGameTypeCode = "FIVECARDDRAW",
            DealerPosition = 0,
            DealersChoiceDealerPosition = 0,
            IsDealersChoice = true,
            Ante = 10,
            MinBet = 20
        };
        AddActivePlayers(game, 3);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("FIVECARDDRAW");
        handler.InitialPhase = "Dealing";

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert — skips DC rotation (game was in WaitingToStart), deals the hand
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be("Dealing");
        updatedGame.CurrentHandNumber.Should().Be(2);
        // DC dealer position should NOT have changed
        updatedGame.DealersChoiceDealerPosition.Should().Be(0);
    }

    [Fact]
    public async Task DCGame_WaitingToStart_OmahaVariant_DealsHandWithoutRotatingDcDealer()
    {
        // Scenario: dealer chose Omaha in Dealer's Choice mode.
        // Continuous play should start the hand from WaitingToStart and keep DC dealer fixed.
        var now = DateTimeOffset.UtcNow;
        var gameType = new GameType { Code = "OMAHA", Name = "Omaha" };
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = nameof(Phases.WaitingToStart),
            Status = GameStatus.BetweenHands,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = gameType,
            GameTypeId = gameType.Id,
            CurrentHandGameTypeCode = "OMAHA",
            DealerPosition = 0,
            DealersChoiceDealerPosition = 0,
            IsDealersChoice = true,
            Ante = 0,
            MinBet = 10,
            SmallBlind = 5,
            BigBlind = 10
        };
        AddActivePlayers(game, 3);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("OMAHA");
        handler.InitialPhase = "CollectingBlinds";
        handler.SkipsAnteCollection = true;

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be("CollectingBlinds");
        updatedGame.CurrentHandNumber.Should().Be(2);
        updatedGame.DealersChoiceDealerPosition.Should().Be(0);
    }

    [Fact]
    public async Task DCGame_WaitingToStart_IrishHoldEmVariant_DealsHandWithoutRotatingDcDealer()
    {
        // Scenario: dealer chose Irish Hold 'Em in Dealer's Choice mode.
        // Continuous play should start the hand from WaitingToStart and keep DC dealer fixed.
        var now = DateTimeOffset.UtcNow;
        var gameType = new GameType { Code = "IRISHHOLDEM", Name = "Irish Hold 'Em" };
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = nameof(Phases.WaitingToStart),
            Status = GameStatus.BetweenHands,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = gameType,
            GameTypeId = gameType.Id,
            CurrentHandGameTypeCode = "IRISHHOLDEM",
            DealerPosition = 0,
            DealersChoiceDealerPosition = 0,
            IsDealersChoice = true,
            Ante = 0,
            MinBet = 10,
            SmallBlind = 5,
            BigBlind = 10
        };
        AddActivePlayers(game, 3);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("IRISHHOLDEM");
        handler.InitialPhase = "CollectingBlinds";
        handler.SkipsAnteCollection = true;

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be("CollectingBlinds");
        updatedGame.CurrentHandNumber.Should().Be(2);
        updatedGame.DealersChoiceDealerPosition.Should().Be(0);
    }

    [Fact]
    public async Task DCGame_WaitingToStart_KingsAndLowsVariant_DealsHandCorrectly()
    {
        // Scenario: DC game where dealer chose Kings and Lows (multi-hand variant).
        // Kings and Lows has SkipsAnteCollection=true and IsMultiHandVariant=true.
        // The background service should still deal the first hand correctly.
        var now = DateTimeOffset.UtcNow;
        var gameType = new GameType { Code = "KINGSANDLOWS", Name = "Kings and Lows" };
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = nameof(Phases.WaitingToStart),
            Status = GameStatus.BetweenHands,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = gameType,
            GameTypeId = gameType.Id,
            CurrentHandGameTypeCode = "KINGSANDLOWS",
            DealerPosition = 0,
            DealersChoiceDealerPosition = 0,
            OriginalDealersChoiceDealerPosition = 0,
            IsDealersChoice = true,
            Ante = 5,
            MinBet = 10
        };
        AddActivePlayers(game, 3);

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var handler = _flowHandlerFactory.SetHandlerForCode("KINGSANDLOWS");
        handler.InitialPhase = "Dealing";
        handler.SkipsAnteCollection = true;
        handler.IsMultiHandVariant = true;

        // Act
        await _service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        // Assert — should deal the hand, not rotate DC or get stuck
        var updatedGame = await _dbContext.Games.FindAsync(game.Id);
        updatedGame!.CurrentPhase.Should().Be("Dealing",
            "K&L initial hand should deal cards from WaitingToStart");
        updatedGame.CurrentHandNumber.Should().Be(2);
        updatedGame.DealersChoiceDealerPosition.Should().Be(0);
    }

    #region Helpers

    private static Game CreateDCGame(
        DateTimeOffset now,
        string currentPhase,
        int dealerPosition,
        int dcDealerPosition)
    {
        return new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = currentPhase,
            Status = GameStatus.InProgress,
            NextHandStartsAt = now.AddSeconds(-1),
            CurrentHandNumber = 1,
            GameType = new GameType { Code = "TESTGAME", Name = "Test Game" },
            CurrentHandGameTypeCode = "TESTGAME",
            DealerPosition = dealerPosition,
            DealersChoiceDealerPosition = dcDealerPosition,
            IsDealersChoice = true,
            Ante = 10,
            MinBet = 20
        };
    }

    private static void AddActivePlayers(Game game, int count)
    {
        for (var i = 0; i < count; i++)
        {
            game.GamePlayers.Add(new GamePlayer
            {
                GameId = game.Id,
                PlayerId = Guid.NewGuid(),
                Status = GamePlayerStatus.Active,
                SeatPosition = i,
                ChipStack = 1000,
                LeftAtHandNumber = -1
            });
        }
    }

    #endregion

    #region Fakes (same pattern as ContinuousPlayBackgroundServiceTests)

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

        public IServiceScope CreateScope() =>
            new FakeServiceScope(new FakeServiceProvider(_dbContext, _broadcaster, _recorder, _handlerFactory));
    }

    private class FakeServiceScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }
        public FakeServiceScope(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;
        public void Dispose() { }
    }

    private class FakeServiceProvider : IServiceProvider
    {
        private readonly CardsDbContext _dbContext;
        private readonly IGameStateBroadcaster _broadcaster;
        private readonly IHandHistoryRecorder _recorder;
        private readonly IGameFlowHandlerFactory _handlerFactory;
        private readonly IPlayerChipWalletService _playerChipWalletService;

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
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(CardsDbContext)) return _dbContext;
            if (serviceType == typeof(IGameStateBroadcaster)) return _broadcaster;
            if (serviceType == typeof(IHandHistoryRecorder)) return _recorder;
            if (serviceType == typeof(IGameFlowHandlerFactory)) return _handlerFactory;
            if (serviceType == typeof(IPlayerChipWalletService)) return _playerChipWalletService;
            return null;
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

        public Task BroadcastGameStateToUserAsync(Guid gameId, string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BroadcastPlayerJoinedAsync(Guid gameId, string playerName, int seatPosition, bool isRejoining, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BroadcastTableToastAsync(TableToastNotificationDto notification, CancellationToken cancellationToken = default)
        {
            ToastNotifications.Add(notification);
            return Task.CompletedTask;
        }
        public Task BroadcastTableSettingsUpdatedAsync(TableSettingsUpdatedDto settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BroadcastOddsVisibilityUpdatedAsync(OddsVisibilityUpdatedDto notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BroadcastPlayerActionAsync(Guid gameId, int seatPosition, string? action, string description, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeHandHistoryRecorder : IHandHistoryRecorder
    {
        public Task<bool> RecordHandHistoryAsync(RecordHandHistoryParameters parameters, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private class FakeGameFlowHandlerFactory : IGameFlowHandlerFactory
    {
        private readonly Dictionary<string, FakeGameFlowHandler> _handlers = new();

        public IGameFlowHandler GetHandler(string? gameTypeCode)
        {
            var code = gameTypeCode ?? "DEFAULT";
            if (!_handlers.ContainsKey(code))
                _handlers[code] = new FakeGameFlowHandler(code);
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

        public FakeGameFlowHandler(string code) => GameTypeCode = code;

        public GameRules GetGameRules() => null!;
        public string GetInitialPhase(Game game) => InitialPhase;
        public string? GetNextPhase(Game game, string currentPhase) => NextPhase;
        public DealingConfiguration GetDealingConfiguration() => new DealingConfiguration { PatternType = DealingPatternType.AllAtOnce, InitialCardsPerPlayer = 5 };
        public ChipCheckConfiguration GetChipCheckConfiguration() => ChipCheckConfig;
        public Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PrepareForNewHandAsync(
            CardsDbContext context,
            Game game,
            List<GamePlayer> eligiblePlayers,
            int upcomingHandNumber,
            DateTimeOffset now,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task OnHandCompletedAsync(Game game, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DealCardsAsync(CardsDbContext context, Game game, List<GamePlayer> eligiblePlayers, DateTimeOffset now, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<ShowdownResult> PerformShowdownAsync(CardsDbContext context, Game game, IHandHistoryRecorder handHistoryRecorder, DateTimeOffset now, CancellationToken cancellationToken)
            => Task.FromResult(new ShowdownResult { IsSuccess = true, WinnerPlayerIds = new List<Guid>(), LoserPlayerIds = new List<Guid>(), TotalPotAwarded = 0 });

        public Task<string> ProcessDrawCompleteAsync(CardsDbContext context, Game game, IHandHistoryRecorder handHistoryRecorder, DateTimeOffset now, CancellationToken cancellationToken)
            => Task.FromResult(NextPhase);

        public Task<string> ProcessPostShowdownAsync(CardsDbContext context, Game game, ShowdownResult showdownResult, DateTimeOffset now, CancellationToken cancellationToken)
            => Task.FromResult(PostShowdownPhase);

        public Task PerformAutoActionAsync(AutoActionContext context)
            => Task.CompletedTask;
    }

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

    #endregion
}
