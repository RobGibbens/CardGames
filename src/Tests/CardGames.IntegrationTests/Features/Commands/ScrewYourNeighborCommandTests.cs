using CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Services;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for Screw Your Neighbor KeepOrTrade command.
/// </summary>
public class ScrewYourNeighborCommandTests : IntegrationTestBase
{
    /// <inheritdoc />
    protected override void ConfigureServices(IServiceCollection services)
    {
        // SYN settlement tests need the real service so wallet balances are updated.
        services.AddScoped<IHandSettlementService, HandSettlementService>();
    }

    private async Task<(GameSetup Setup, Game Game)> CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(int playerCount = 4)
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext, "SCREWYOURNEIGHBOR", playerCount, startingChips: 100, ante: 25);

        // Start hand — this triggers dealing and transitions to KeepOrTrade
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        var game = await DbContext.Games
            .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
            .Include(g => g.GameCards)
            .FirstAsync(g => g.Id == setup.Game.Id);

        // Should now be in KeepOrTrade phase
        game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));

        return (setup, game);
    }

    private async Task PrimeRemainingDeckAsync(
        Guid gameId,
        int handNumber,
        params (CardSuit Suit, CardSymbol Symbol)[] orderedDeckCards)
    {
        var deckCards = await DbContext.GameCards
            .Where(gc => gc.GameId == gameId &&
                         gc.HandNumber == handNumber &&
                         gc.Location == CardLocation.Deck &&
                         gc.GamePlayerId == null)
            .OrderBy(gc => gc.DealOrder)
            .ToListAsync();

        deckCards.Count.Should().BeGreaterThanOrEqualTo(orderedDeckCards.Length);

        var cardsToKeep = deckCards.Take(orderedDeckCards.Length).ToList();
        var cardsToRemove = deckCards.Skip(orderedDeckCards.Length).ToList();

        if (cardsToRemove.Count > 0)
        {
            DbContext.GameCards.RemoveRange(cardsToRemove);
        }

        for (var i = 0; i < orderedDeckCards.Length; i++)
        {
            cardsToKeep[i].Suit = orderedDeckCards[i].Suit;
            cardsToKeep[i].Symbol = orderedDeckCards[i].Symbol;
        }

        await DbContext.SaveChangesAsync();
    }

    private async Task<Game> CreateDealersChoiceScrewYourNeighborSetupAsync(int playerCount = 3, int startingChips = 25, int ante = 25)
    {
        var game = await DatabaseSeeder.CreateDealersChoiceGameAsync(DbContext);

        for (var i = 0; i < playerCount; i++)
        {
            var playerName = i == 0 ? "Test User" : $"Player {i + 1}";
            var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, playerName);
            await DatabaseSeeder.AddPlayerToGameAsync(DbContext, game, player, i, startingChips);
        }

        game.DealersChoiceDealerPosition = 0;
        game.CurrentPhase = nameof(Phases.WaitingForDealerChoice);
        game.CurrentHandNumber = 1;
        game.Status = GameStatus.InProgress;
        await DbContext.SaveChangesAsync();

        var choice = await Mediator.Send(new ChooseDealerGameCommand(
            game.Id,
            "SCREWYOURNEIGHBOR",
            Ante: ante,
            MinBet: Math.Max(ante, 1)));

        choice.IsT0.Should().BeTrue();

        var gameToStart = await DbContext.Games.FirstAsync(g => g.Id == game.Id);
        gameToStart.NextHandStartsAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await DbContext.SaveChangesAsync();

        var serviceScopeFactory = Scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = new TestLogger<ContinuousPlayBackgroundService>();
        var service = new ContinuousPlayBackgroundService(
            serviceScopeFactory,
            logger);

        await service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);
        logger.ErrorLogs.Should().BeEmpty();
        DbContext.ChangeTracker.Clear();

        return await DbContext.Games
            .AsNoTracking()
            .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
            .Include(g => g.GameCards)
            .FirstAsync(g => g.Id == game.Id);
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> ErrorLogs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                ErrorLogs.Add(formatter(state, exception));
            }
        }
    }

    [Fact]
    public async Task StartHand_TransitionsToKeepOrTrade()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext, "SCREWYOURNEIGHBOR", 4, startingChips: 100, ante: 25);

        var result = await Mediator.Send(new StartHandCommand(setup.Game.Id));
        result.IsT0.Should().BeTrue();

        var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));
    }

    [Fact]
    public async Task KeepOrTrade_KeepDecision_AdvancesToNextPlayer()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync();

        // Find the first actor (current player whose turn it is)
        var firstActorIndex = game.CurrentPlayerIndex;
        var firstActor = game.GamePlayers
            .FirstOrDefault(gp => gp.SeatPosition == firstActorIndex);
        firstActor.Should().NotBeNull();

        var command = new KeepOrTradeCommand(game.Id, firstActor!.PlayerId, "Keep");
        var result = await Mediator.Send(command);

        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.Decision.Should().Be("Keep");
        success.DidTrade.Should().BeFalse();
    }

    [Fact]
    public async Task KeepOrTrade_TradeDecision_ReturnsSuccess()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync();

        var firstActorIndex = game.CurrentPlayerIndex;
        var firstActor = game.GamePlayers
            .FirstOrDefault(gp => gp.SeatPosition == firstActorIndex);
        firstActor.Should().NotBeNull();

        var command = new KeepOrTradeCommand(game.Id, firstActor!.PlayerId, "Trade");
        var result = await Mediator.Send(command);

        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.Decision.Should().Be("Trade");
        // Trade may succeed or be blocked if neighbor has King
        (success.DidTrade || success.WasBlocked).Should().BeTrue();
    }

    [Fact]
    public async Task KeepOrTrade_DealerTradesWithDeck_KingBecomesVisible()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync();

        var dealer = game.GamePlayers.First(gp => gp.SeatPosition == game.DealerPosition);
        game.CurrentPlayerIndex = dealer.SeatPosition;

        var topDeckCard = game.GameCards
            .Where(gc => gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Deck &&
                         gc.GamePlayerId == null)
            .OrderBy(gc => gc.DealOrder)
            .First();

        topDeckCard.Symbol = CardSymbol.King;
        topDeckCard.IsVisible = false;

        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new KeepOrTradeCommand(game.Id, dealer.PlayerId, "Trade"));

        result.IsT0.Should().BeTrue();
        result.AsT0.DidTrade.Should().BeTrue();

        var updatedTopDeckCard = await DbContext.GameCards
            .AsNoTracking()
            .FirstAsync(gc => gc.Id == topDeckCard.Id);

        updatedTopDeckCard.GamePlayerId.Should().Be(dealer.Id);
        updatedTopDeckCard.Location.Should().Be(CardLocation.Hand);
        updatedTopDeckCard.IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task KeepOrTrade_InvalidDecision_ReturnsError()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync();

        var firstActorIndex = game.CurrentPlayerIndex;
        var firstActor = game.GamePlayers
            .FirstOrDefault(gp => gp.SeatPosition == firstActorIndex);
        firstActor.Should().NotBeNull();

        var command = new KeepOrTradeCommand(game.Id, firstActor!.PlayerId, "InvalidDecision");
        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(KeepOrTradeErrorCode.InvalidDecision);
    }

    [Fact]
    public async Task KeepOrTrade_WrongPlayer_ReturnsError()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync();

        // Get a player who is NOT the current actor
        var firstActorIndex = game.CurrentPlayerIndex;
        var wrongPlayer = game.GamePlayers
            .FirstOrDefault(gp => gp.SeatPosition != firstActorIndex);
        wrongPlayer.Should().NotBeNull();

        var command = new KeepOrTradeCommand(game.Id, wrongPlayer!.PlayerId, "Keep");
        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(KeepOrTradeErrorCode.NotPlayersTurn);
    }

    [Fact]
    public async Task KeepOrTrade_GameNotFound_ReturnsError()
    {
        var command = new KeepOrTradeCommand(Guid.NewGuid(), Guid.NewGuid(), "Keep");
        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(KeepOrTradeErrorCode.GameNotFound);
    }

    [Fact]
    public async Task KeepOrTrade_AllPlayersAct_TransitionsToReveal()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(3);

        // All 3 players take their turn (Keep decision for simplicity)
        for (var i = 0; i < 3; i++)
        {
            // Reload game to get updated CurrentPlayerIndex
            game = await DbContext.Games
                .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
                .Include(g => g.GameCards)
                .AsNoTracking()
                .FirstAsync(g => g.Id == setup.Game.Id);

            // If phase has already transitioned (e.g., due to auto-skip), stop
            if (game.CurrentPhase != nameof(Phases.KeepOrTrade))
                break;

            var currentActor = game.GamePlayers
                .FirstOrDefault(gp => gp.SeatPosition == game.CurrentPlayerIndex);

            if (currentActor is null) break;

            var command = new KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep");
            await Mediator.Send(command);
        }

        // Reload final state
        game = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);

        // Should have transitioned past KeepOrTrade (to Reveal or beyond)
        game.CurrentPhase.Should().NotBe(nameof(Phases.KeepOrTrade));
    }

    [Fact]
    public async Task StartHand_WhenEnoughDeckCardsRemain_ReusesDeckInsteadOfReshuffling()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(3);
        var broadcaster = Scope.ServiceProvider.GetRequiredService<IGameStateBroadcaster>().Should().BeOfType<FakeGameStateBroadcaster>().Subject;

        await PrimeRemainingDeckAsync(
            game.Id,
            game.CurrentHandNumber,
            (CardSuit.Spades, CardSymbol.Ace),
            (CardSuit.Hearts, CardSymbol.Deuce),
            (CardSuit.Clubs, CardSymbol.Three),
            (CardSuit.Diamonds, CardSymbol.Four),
            (CardSuit.Spades, CardSymbol.Five));

        var gameToRestart = await DbContext.Games.FirstAsync(g => g.Id == game.Id);
        gameToRestart.CurrentPhase = nameof(Phases.Complete);
        gameToRestart.HandCompletedAt = DateTimeOffset.UtcNow;
        gameToRestart.NextHandStartsAt = null;
        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new StartHandCommand(game.Id));
        result.IsT0.Should().BeTrue();

        var secondHandCards = await DbContext.GameCards
            .AsNoTracking()
            .Where(gc => gc.GameId == game.Id && gc.HandNumber == 2)
            .OrderBy(gc => gc.Location)
            .ThenBy(gc => gc.DealOrder)
            .ToListAsync();

        secondHandCards.Should().HaveCount(5);
        secondHandCards.Should().OnlyContain(gc => gc.HandNumber == 2);
        (await DbContext.GameCards.AnyAsync(gc => gc.GameId == game.Id && gc.HandNumber == 1)).Should().BeFalse();

        var dealtCards = secondHandCards
            .Where(gc => gc.Location == CardLocation.Hand)
            .OrderBy(gc => gc.DealOrder)
            .ToList();
        dealtCards.Should().HaveCount(3);
        dealtCards.Select(gc => gc.Symbol).Should().Equal(
            CardSymbol.Ace,
            CardSymbol.Deuce,
            CardSymbol.Three);

        var remainingDeckCards = secondHandCards
            .Where(gc => gc.Location == CardLocation.Deck)
            .OrderBy(gc => gc.DealOrder)
            .ToList();
        remainingDeckCards.Should().HaveCount(2);
        remainingDeckCards.Select(gc => gc.Symbol).Should().Equal(
            CardSymbol.Four,
            CardSymbol.Five);

        broadcaster.ToastNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task StartHand_WhenDeckCannotCoverNextHand_ReshufflesFreshDeck()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(3);
        var broadcaster = Scope.ServiceProvider.GetRequiredService<IGameStateBroadcaster>().Should().BeOfType<FakeGameStateBroadcaster>().Subject;

        await PrimeRemainingDeckAsync(
            game.Id,
            game.CurrentHandNumber,
            (CardSuit.Spades, CardSymbol.Ace),
            (CardSuit.Hearts, CardSymbol.Deuce),
            (CardSuit.Clubs, CardSymbol.Three));

        var gameToRestart = await DbContext.Games.FirstAsync(g => g.Id == game.Id);
        gameToRestart.CurrentPhase = nameof(Phases.Complete);
        gameToRestart.HandCompletedAt = DateTimeOffset.UtcNow;
        gameToRestart.NextHandStartsAt = null;
        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new StartHandCommand(game.Id));
        result.IsT0.Should().BeTrue();

        var secondHandCards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id && gc.HandNumber == 2)
            .ToListAsync();

        secondHandCards.Should().HaveCount(52);
        secondHandCards.Count(gc => gc.Location == CardLocation.Hand).Should().Be(3);
        secondHandCards.Count(gc => gc.Location == CardLocation.Deck).Should().Be(49);
        (await DbContext.GameCards.AnyAsync(gc => gc.GameId == game.Id && gc.HandNumber == 1)).Should().BeFalse();
        broadcaster.ToastNotifications.Should().ContainSingle();
        broadcaster.ToastNotifications[0].GameId.Should().Be(game.Id);
        broadcaster.ToastNotifications[0].Message.Should().Be("Starting new deck");
    }

    [Fact]
    public async Task PerformShowdown_RuntimeCommandPath_LoserLosesStack_AndPotCarriesToNextHand()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(2);

        var now = DateTimeOffset.UtcNow;
        var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
        var winner = players[0];
        var loser = players[1];

        // Force deterministic cards for this hand so loser is unambiguous.
        var handCards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Hand &&
                         gc.GamePlayerId != null)
            .ToListAsync();

        var winnerCard = handCards.First(c => c.GamePlayerId == winner.Id);
        var loserCard = handCards.First(c => c.GamePlayerId == loser.Id);

        winnerCard.Symbol = CardSymbol.Four;
        winnerCard.Suit = CardSuit.Hearts;
        loserCard.Symbol = CardSymbol.Ace;
        loserCard.Suit = CardSuit.Spades;

        await DbContext.SaveChangesAsync();

        // Complete Keep/Trade turn order using keep decisions.
        for (var i = 0; i < 2; i++)
        {
            game = await DbContext.Games
                .Include(g => g.GamePlayers)
                .AsNoTracking()
                .FirstAsync(g => g.Id == setup.Game.Id);

            if (game.CurrentPhase != nameof(Phases.KeepOrTrade))
            {
                break;
            }

            var currentActor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
            var keepResult = await Mediator.Send(new KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep"));
            keepResult.IsT0.Should().BeTrue();
        }

        // Inline showdown: last KeepOrTrade triggers showdown automatically
        game = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.Complete));

        var refreshedWinner = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == winner.Id);
        var refreshedLoser = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == loser.Id);
        var nextHandPot = await DbContext.Pots
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.GameId == game.Id &&
                                     p.HandNumber == game.CurrentHandNumber + 1 &&
                                     p.PotType == PotType.Main);

        refreshedWinner.ChipStack.Should().Be(100);
        refreshedLoser.ChipStack.Should().Be(75);
        nextHandPot.Should().NotBeNull();
        nextHandPot!.Amount.Should().Be(25);
        game.NextHandStartsAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PerformShowdown_ThenBackgroundStartsNextHand_LoserStackRemainsDecremented()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(2);

        var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
        var winner = players[0];
        var loser = players[1];

        var handCards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Hand &&
                         gc.GamePlayerId != null)
            .ToListAsync();

        var winnerCard = handCards.First(c => c.GamePlayerId == winner.Id);
        var loserCard = handCards.First(c => c.GamePlayerId == loser.Id);
        winnerCard.Symbol = CardSymbol.Four;
        loserCard.Symbol = CardSymbol.Ace;
        await DbContext.SaveChangesAsync();

        for (var i = 0; i < 2; i++)
        {
            game = await DbContext.Games
                .Include(g => g.GamePlayers)
                .AsNoTracking()
                .FirstAsync(g => g.Id == setup.Game.Id);

            if (game.CurrentPhase != nameof(Phases.KeepOrTrade))
            {
                break;
            }

            var currentActor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
            var keepResult = await Mediator.Send(new KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep"));
            keepResult.IsT0.Should().BeTrue();
        }

        // Inline showdown: last KeepOrTrade triggers showdown automatically
        var showdownGame = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        showdownGame.CurrentPhase.Should().Be(nameof(Phases.Complete));

        var afterShowdownLoser = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == loser.Id);
        afterShowdownLoser.ChipStack.Should().Be(75);

        var gameToSchedule = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
        gameToSchedule.NextHandStartsAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await DbContext.SaveChangesAsync();

        var loggerFactory = Scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var serviceScopeFactory = Scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var service = new ContinuousPlayBackgroundService(
            serviceScopeFactory,
            loggerFactory.CreateLogger<ContinuousPlayBackgroundService>());

        await service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        var postBackgroundLoser = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == loser.Id);
        var postBackgroundWinner = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == winner.Id);
        var postBackgroundGame = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);

        postBackgroundLoser.ChipStack.Should().Be(75);
        postBackgroundWinner.ChipStack.Should().Be(100);
        postBackgroundGame.CurrentHandNumber.Should().Be(2);
    }

    [Fact]
    public async Task BackgroundService_WhenEnoughDeckCardsRemain_ReusesDeckForNextHand()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(3);
        var broadcaster = Scope.ServiceProvider.GetRequiredService<IGameStateBroadcaster>().Should().BeOfType<FakeGameStateBroadcaster>().Subject;

        await PrimeRemainingDeckAsync(
            game.Id,
            game.CurrentHandNumber,
            (CardSuit.Spades, CardSymbol.Ace),
            (CardSuit.Hearts, CardSymbol.Deuce),
            (CardSuit.Clubs, CardSymbol.Three),
            (CardSuit.Diamonds, CardSymbol.Four),
            (CardSuit.Spades, CardSymbol.Five));

        var gameToSchedule = await DbContext.Games.FirstAsync(g => g.Id == game.Id);
        gameToSchedule.CurrentPhase = nameof(Phases.Complete);
        gameToSchedule.NextHandStartsAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        gameToSchedule.HandCompletedAt = DateTimeOffset.UtcNow;
        await DbContext.SaveChangesAsync();

        var serviceScopeFactory = Scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = new TestLogger<ContinuousPlayBackgroundService>();
        var service = new ContinuousPlayBackgroundService(
            serviceScopeFactory,
            logger);

        await service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        logger.ErrorLogs.Should().BeEmpty();

        var secondHandCards = await DbContext.GameCards
            .AsNoTracking()
            .Where(gc => gc.GameId == game.Id && gc.HandNumber == 2)
            .OrderBy(gc => gc.Location)
            .ThenBy(gc => gc.DealOrder)
            .ToListAsync();

        secondHandCards.Should().HaveCount(5);
        secondHandCards.Count(gc => gc.Location == CardLocation.Hand).Should().Be(3);
        secondHandCards.Count(gc => gc.Location == CardLocation.Deck).Should().Be(2);

        var dealtCards = secondHandCards
            .Where(gc => gc.Location == CardLocation.Hand)
            .OrderBy(gc => gc.DealOrder)
            .Select(gc => gc.Symbol)
            .ToList();
        dealtCards.Should().Equal(CardSymbol.Ace, CardSymbol.Deuce, CardSymbol.Three);
        broadcaster.ToastNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task BackgroundService_WhenDeckCannotCoverNextHand_ReshufflesFreshDeckAndBroadcastsToast()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(3);
        var broadcaster = Scope.ServiceProvider.GetRequiredService<IGameStateBroadcaster>().Should().BeOfType<FakeGameStateBroadcaster>().Subject;

        await PrimeRemainingDeckAsync(
            game.Id,
            game.CurrentHandNumber,
            (CardSuit.Spades, CardSymbol.Ace),
            (CardSuit.Hearts, CardSymbol.Deuce),
            (CardSuit.Clubs, CardSymbol.Three));

        var gameToSchedule = await DbContext.Games.FirstAsync(g => g.Id == game.Id);
        gameToSchedule.CurrentPhase = nameof(Phases.Complete);
        gameToSchedule.NextHandStartsAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        gameToSchedule.HandCompletedAt = DateTimeOffset.UtcNow;
        await DbContext.SaveChangesAsync();

        var serviceScopeFactory = Scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = new TestLogger<ContinuousPlayBackgroundService>();
        var service = new ContinuousPlayBackgroundService(
            serviceScopeFactory,
            logger);

        await service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);

        logger.ErrorLogs.Should().BeEmpty();

        var secondHandCards = await DbContext.GameCards
            .AsNoTracking()
            .Where(gc => gc.GameId == game.Id && gc.HandNumber == 2)
            .ToListAsync();

        secondHandCards.Should().HaveCount(52);
        secondHandCards.Count(gc => gc.Location == CardLocation.Hand).Should().Be(3);
        secondHandCards.Count(gc => gc.Location == CardLocation.Deck).Should().Be(49);
        broadcaster.ToastNotifications.Should().ContainSingle();
        broadcaster.ToastNotifications[0].GameId.Should().Be(game.Id);
        broadcaster.ToastNotifications[0].Message.Should().Be("Starting new deck");
    }

    [Fact]
    public async Task PerformShowdown_WhenGameEnds_SettlesWinnerToCashierAndReturnsPayout()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
        DbContext,
        "SCREWYOURNEIGHBOR",
        2,
        startingChips: 25,
        ante: 25);

        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        var game = await DbContext.Games
        .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
        .FirstAsync(g => g.Id == setup.Game.Id);

        var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
        var winner = players[0];
        var loser = players[1];

        var handCards = await DbContext.GameCards
        .Where(gc => gc.GameId == game.Id &&
                     gc.HandNumber == game.CurrentHandNumber &&
                     gc.Location == CardLocation.Hand &&
                     gc.GamePlayerId != null)
        .ToListAsync();

        var winnerCard = handCards.First(c => c.GamePlayerId == winner.Id);
        var loserCard = handCards.First(c => c.GamePlayerId == loser.Id);
        winnerCard.Symbol = CardSymbol.Four;
        winnerCard.Suit = CardSuit.Hearts;
        loserCard.Symbol = CardSymbol.Ace;
        loserCard.Suit = CardSuit.Spades;
        await DbContext.SaveChangesAsync();

        for (var i = 0; i < 2; i++)
        {
            game = await DbContext.Games
            .Include(g => g.GamePlayers)
            .AsNoTracking()
            .FirstAsync(g => g.Id == setup.Game.Id);

            if (game.CurrentPhase != nameof(Phases.KeepOrTrade))
            {
                break;
            }

            var currentActor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
            var keepResult = await Mediator.Send(new KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep"));
            keepResult.IsT0.Should().BeTrue();
        }

        // Inline showdown: last KeepOrTrade triggers showdown and settlement automatically
        game = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be("Ended");
        game.Status.Should().Be(GameStatus.Completed);
        game.NextHandStartsAt.Should().BeNull();

        var walletService = Scope.ServiceProvider.GetRequiredService<IPlayerChipWalletService>();
        var winnerBalance = await walletService.GetBalanceAsync(winner.PlayerId, CancellationToken.None);
        var loserBalance = await walletService.GetBalanceAsync(loser.PlayerId, CancellationToken.None);

        winnerBalance.Should().Be(25);
        loserBalance.Should().Be(-25);
    }

    [Fact]
    public async Task DealersChoiceScrewYourNeighbor_StartsWithThreeStacks()
    {
        // In DC, when SYN is chosen with ante=25, each player should get 3 stacks (75 chips)
        var game = await CreateDealersChoiceScrewYourNeighborSetupAsync(playerCount: 3, startingChips: 500, ante: 25);

        game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));

        var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
        foreach (var player in players)
        {
            player.ChipStack.Should().Be(75, $"player at seat {player.SeatPosition} should have 3 stacks (ante × 3 = 75)");
        }
    }

    [Fact]
    public async Task DealersChoiceScrewYourNeighbor_WhenVariantEnds_ReturnsToWaitingForDealerChoice()
    {
        var game = await CreateDealersChoiceScrewYourNeighborSetupAsync();
        game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));

        var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
        var winner = players[0];
        var loserOne = players[1];
        var loserTwo = players[2];

        // Players now have 3 stacks (ante × 3 = 75 chips) — play 3 SYN hands to eliminate losers
        for (var hand = 0; hand < 3; hand++)
        {
            game = await DbContext.Games
                .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
                .Include(g => g.GameCards)
                .AsNoTracking()
                .FirstAsync(g => g.Id == game.Id);

            game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));

            var handCards = await DbContext.GameCards
                .Where(gc => gc.GameId == game.Id &&
                             gc.HandNumber == game.CurrentHandNumber &&
                             gc.Location == CardLocation.Hand &&
                             gc.GamePlayerId != null)
                .ToListAsync();

            handCards.First(c => c.GamePlayerId == winner.Id).Symbol = CardSymbol.Four;
            handCards.First(c => c.GamePlayerId == winner.Id).Suit = CardSuit.Hearts;
            handCards.First(c => c.GamePlayerId == loserOne.Id).Symbol = CardSymbol.Ace;
            handCards.First(c => c.GamePlayerId == loserOne.Id).Suit = CardSuit.Spades;
            handCards.First(c => c.GamePlayerId == loserTwo.Id).Symbol = CardSymbol.Ace;
            handCards.First(c => c.GamePlayerId == loserTwo.Id).Suit = CardSuit.Clubs;
            await DbContext.SaveChangesAsync();

            for (var i = 0; i < 3; i++)
            {
                game = await DbContext.Games
                    .Include(g => g.GamePlayers)
                    .AsNoTracking()
                    .FirstAsync(g => g.Id == game.Id);

                if (game.CurrentPhase != nameof(Phases.KeepOrTrade))
                    break;

                var currentActor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
                var keepResult = await Mediator.Send(new KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep"));
                if (keepResult.IsT1)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"KeepOrTrade H{hand + 1} failed for seat {currentActor.SeatPosition}: {keepResult.AsT1.Code} - {keepResult.AsT1.Message}");
                }
            }

            game = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == game.Id);
            game.CurrentPhase.Should().Be(nameof(Phases.Complete));
            game.NextHandStartsAt.Should().NotBeNull();

            // Run background service to either start next SYN hand or transition back to DC
            var gameToSchedule = await DbContext.Games.FirstAsync(g => g.Id == game.Id);
            gameToSchedule.NextHandStartsAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await DbContext.SaveChangesAsync();

            var service = new ContinuousPlayBackgroundService(
                Scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                new TestLogger<ContinuousPlayBackgroundService>());
            await service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);
            DbContext.ChangeTracker.Clear();
        }

        var updatedGame = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == game.Id);
        updatedGame.CurrentPhase.Should().Be(nameof(Phases.WaitingForDealerChoice));
        updatedGame.Status.Should().Be(GameStatus.BetweenHands);
        updatedGame.CurrentHandGameTypeCode.Should().BeNull();
        updatedGame.DealersChoiceDealerPosition.Should().Be(1);

        var tableStateBuilder = Scope.ServiceProvider.GetRequiredService<ITableStateBuilder>();
        var publicState = await tableStateBuilder.BuildPublicStateAsync(game.Id, CancellationToken.None);
        publicState.Should().NotBeNull();
        publicState!.CurrentPhase.Should().Be(nameof(Phases.WaitingForDealerChoice));
        publicState.IsDealersChoice.Should().BeTrue();
    }

    [Fact]
    public async Task DealersChoiceScrewYourNeighbor_MultiHand_WhenVariantEnds_ReturnsToWaitingForDealerChoice()
    {
        // 3 players, ante=5, SYN chips = ante × 3 = 15 per player (3 stacks)
        // Takes 3 SYN hands to get a winner (player 0 always wins, players 1&2 lose 5 each hand)
        var game = await CreateDealersChoiceScrewYourNeighborSetupAsync(playerCount: 3, startingChips: 15, ante: 5);
        game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));

        var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

        // === Hand 1: Player 0 wins (Four), players 1 & 2 lose (Aces) ===
        var handCards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Hand &&
                         gc.GamePlayerId != null)
            .ToListAsync();

        handCards.First(c => c.GamePlayerId == players[0].Id).Symbol = CardSymbol.Four;
        handCards.First(c => c.GamePlayerId == players[0].Id).Suit = CardSuit.Hearts;
        handCards.First(c => c.GamePlayerId == players[1].Id).Symbol = CardSymbol.Ace;
        handCards.First(c => c.GamePlayerId == players[1].Id).Suit = CardSuit.Spades;
        handCards.First(c => c.GamePlayerId == players[2].Id).Symbol = CardSymbol.Ace;
        handCards.First(c => c.GamePlayerId == players[2].Id).Suit = CardSuit.Clubs;
        await DbContext.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            game = await DbContext.Games.Include(g => g.GamePlayers).AsNoTracking().FirstAsync(g => g.Id == game.Id);
            if (game.CurrentPhase != nameof(Phases.KeepOrTrade)) break;
            var actor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
            var keepResult = await Mediator.Send(new KeepOrTradeCommand(game.Id, actor.PlayerId, "Keep"));
            if (keepResult.IsT1)
                throw new Xunit.Sdk.XunitException($"KeepOrTrade H1 failed: {keepResult.AsT1.Code} - {keepResult.AsT1.Message}");
        }

        // After hand 1: players 1&2 should have 10 chips each (lost 1 stack of 5), game should continue
        game = await DbContext.Games.Include(g => g.GamePlayers).AsNoTracking().FirstAsync(g => g.Id == game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.Complete),
            "SYN should be in Complete phase after non-terminal hand");

        var chipStacks = game.GamePlayers.OrderBy(gp => gp.SeatPosition)
            .Select(gp => new { gp.SeatPosition, gp.ChipStack }).ToList();
        chipStacks[0].ChipStack.Should().Be(15, "winner kept all chips");
        chipStacks[1].ChipStack.Should().Be(10, "loser lost one stack (5 chips)");
        chipStacks[2].ChipStack.Should().Be(10, "loser lost one stack (5 chips)");

        // Run background service to start next SYN hand
        var gameToSchedule = await DbContext.Games.FirstAsync(g => g.Id == game.Id);
        gameToSchedule.NextHandStartsAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await DbContext.SaveChangesAsync();

        var serviceScopeFactory = Scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = new TestLogger<ContinuousPlayBackgroundService>();
        var service = new ContinuousPlayBackgroundService(serviceScopeFactory, logger);
        await service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);
        logger.ErrorLogs.Should().BeEmpty("background service should succeed starting hand 2");
        DbContext.ChangeTracker.Clear();

        // === Hand 2: Same rigging - players 1 & 2 lose again ===
        game = await DbContext.Games.Include(g => g.GamePlayers).Include(g => g.GameCards)
            .AsNoTracking().FirstAsync(g => g.Id == game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade),
            "SYN hand 2 should be in KeepOrTrade phase");

        players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

        handCards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Hand &&
                         gc.GamePlayerId != null)
            .ToListAsync();

        handCards.First(c => c.GamePlayerId == players[0].Id).Symbol = CardSymbol.Four;
        handCards.First(c => c.GamePlayerId == players[0].Id).Suit = CardSuit.Hearts;
        handCards.First(c => c.GamePlayerId == players[1].Id).Symbol = CardSymbol.Ace;
        handCards.First(c => c.GamePlayerId == players[1].Id).Suit = CardSuit.Spades;
        handCards.First(c => c.GamePlayerId == players[2].Id).Symbol = CardSymbol.Ace;
        handCards.First(c => c.GamePlayerId == players[2].Id).Suit = CardSuit.Clubs;
        await DbContext.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            game = await DbContext.Games.Include(g => g.GamePlayers).AsNoTracking().FirstAsync(g => g.Id == game.Id);
            if (game.CurrentPhase != nameof(Phases.KeepOrTrade)) break;
            var actor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
            var keepResult = await Mediator.Send(new KeepOrTradeCommand(game.Id, actor.PlayerId, "Keep"));
            if (keepResult.IsT1)
                throw new Xunit.Sdk.XunitException($"KeepOrTrade H2 failed: {keepResult.AsT1.Code} - {keepResult.AsT1.Message}");
        }

        // After hand 2: players 1&2 should have 5 chips each (1 stack left)
        game = await DbContext.Games.Include(g => g.GamePlayers).AsNoTracking().FirstAsync(g => g.Id == game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.Complete));

        chipStacks = game.GamePlayers.OrderBy(gp => gp.SeatPosition)
            .Select(gp => new { gp.SeatPosition, gp.ChipStack }).ToList();
        chipStacks[0].ChipStack.Should().Be(15, "winner kept all chips");
        chipStacks[1].ChipStack.Should().Be(5, "loser lost two stacks");
        chipStacks[2].ChipStack.Should().Be(5, "loser lost two stacks");

        // Run background service to start hand 3
        gameToSchedule = await DbContext.Games.FirstAsync(g => g.Id == game.Id);
        gameToSchedule.NextHandStartsAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await DbContext.SaveChangesAsync();

        logger = new TestLogger<ContinuousPlayBackgroundService>();
        service = new ContinuousPlayBackgroundService(serviceScopeFactory, logger);
        await service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);
        logger.ErrorLogs.Should().BeEmpty("background service should succeed starting hand 3");
        DbContext.ChangeTracker.Clear();

        // === Hand 3: Same rigging - players 1 & 2 lose final stack, going to 0 ===
        game = await DbContext.Games.Include(g => g.GamePlayers).Include(g => g.GameCards)
            .AsNoTracking().FirstAsync(g => g.Id == game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade),
            "SYN hand 3 should be in KeepOrTrade phase");

        players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

        handCards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Hand &&
                         gc.GamePlayerId != null)
            .ToListAsync();

        handCards.First(c => c.GamePlayerId == players[0].Id).Symbol = CardSymbol.Four;
        handCards.First(c => c.GamePlayerId == players[0].Id).Suit = CardSuit.Hearts;
        handCards.First(c => c.GamePlayerId == players[1].Id).Symbol = CardSymbol.Ace;
        handCards.First(c => c.GamePlayerId == players[1].Id).Suit = CardSuit.Spades;
        handCards.First(c => c.GamePlayerId == players[2].Id).Symbol = CardSymbol.Ace;
        handCards.First(c => c.GamePlayerId == players[2].Id).Suit = CardSuit.Clubs;
        await DbContext.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            game = await DbContext.Games.Include(g => g.GamePlayers).AsNoTracking().FirstAsync(g => g.Id == game.Id);
            if (game.CurrentPhase != nameof(Phases.KeepOrTrade)) break;
            var actor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
            var keepResult = await Mediator.Send(new KeepOrTradeCommand(game.Id, actor.PlayerId, "Keep"));
            if (keepResult.IsT1)
                throw new Xunit.Sdk.XunitException($"KeepOrTrade H3 failed: {keepResult.AsT1.Code} - {keepResult.AsT1.Message}");
        }

        // After hand 3: players 1&2 should have 0 chips, game should be terminal
        game = await DbContext.Games.Include(g => g.GamePlayers).AsNoTracking().FirstAsync(g => g.Id == game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.Complete));
        game.NextHandStartsAt.Should().NotBeNull("DC+SYN terminal should set NextHandStartsAt");

        chipStacks = game.GamePlayers.OrderBy(gp => gp.SeatPosition)
            .Select(gp => new { gp.SeatPosition, gp.ChipStack }).ToList();
        chipStacks[0].ChipStack.Should().BeGreaterThan(0, "winner has chips");
        chipStacks[1].ChipStack.Should().Be(0, "eliminated");
        chipStacks[2].ChipStack.Should().Be(0, "eliminated");

        // Run background service to transition to WaitingForDealerChoice
        gameToSchedule = await DbContext.Games.FirstAsync(g => g.Id == game.Id);
        gameToSchedule.NextHandStartsAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await DbContext.SaveChangesAsync();

        logger = new TestLogger<ContinuousPlayBackgroundService>();
        service = new ContinuousPlayBackgroundService(serviceScopeFactory, logger);
        await service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);
        logger.ErrorLogs.Should().BeEmpty("background service should succeed transitioning to DC");
        DbContext.ChangeTracker.Clear();

        var updatedGame = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == game.Id);
        updatedGame.CurrentPhase.Should().Be(nameof(Phases.WaitingForDealerChoice),
            $"DC+SYN terminal should go to WaitingForDealerChoice, not {updatedGame.CurrentPhase}. " +
            $"Status={updatedGame.Status}, IsDealersChoice={updatedGame.IsDealersChoice}");
    }

    [Fact]
    public async Task DealerDeckTrade_DealerGetsLowestCard_DealerLosesStack()
    {
        // Regression: when the dealer traded with the deck, the showdown used a DB query
        // that missed the unsaved deck card, excluding the dealer from evaluation entirely.
        // This caused the wrong player to lose a chip stack.
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(3);

        var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
        var dealer = players.First(gp => gp.SeatPosition == game.DealerPosition);
        var nonDealerPlayers = players.Where(gp => gp.SeatPosition != game.DealerPosition)
            .OrderBy(gp => gp.SeatPosition)
            .ToList();

        // Rig cards: non-dealers get high cards; dealer gets a medium card they'll want to trade.
        var handCards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Hand &&
                         gc.GamePlayerId != null)
            .ToListAsync();

        handCards.First(c => c.GamePlayerId == nonDealerPlayers[0].Id).Symbol = CardSymbol.Queen;
        handCards.First(c => c.GamePlayerId == nonDealerPlayers[0].Id).Suit = CardSuit.Hearts;
        handCards.First(c => c.GamePlayerId == nonDealerPlayers[1].Id).Symbol = CardSymbol.Jack;
        handCards.First(c => c.GamePlayerId == nonDealerPlayers[1].Id).Suit = CardSuit.Spades;
        handCards.First(c => c.GamePlayerId == dealer.Id).Symbol = CardSymbol.Five;
        handCards.First(c => c.GamePlayerId == dealer.Id).Suit = CardSuit.Diamonds;

        // Rig the deck so the top card is an Ace (lowest possible) — the dealer will draw it.
        await PrimeRemainingDeckAsync(
            game.Id,
            game.CurrentHandNumber,
            (CardSuit.Clubs, CardSymbol.Ace));

        await DbContext.SaveChangesAsync();

        // Non-dealer players keep their cards.
        for (var i = 0; i < 3; i++)
        {
            game = await DbContext.Games
                .Include(g => g.GamePlayers)
                .AsNoTracking()
                .FirstAsync(g => g.Id == setup.Game.Id);

            if (game.CurrentPhase != nameof(Phases.KeepOrTrade)) break;

            var actor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
            var isDealer = actor.SeatPosition == game.DealerPosition;

            // Non-dealers keep; dealer trades with deck to pick up the rigged Ace.
            var decision = isDealer ? "Trade" : "Keep";
            var result = await Mediator.Send(new KeepOrTradeCommand(game.Id, actor.PlayerId, decision));
            result.IsT0.Should().BeTrue();

            if (isDealer)
            {
                result.AsT0.DidTrade.Should().BeTrue("Dealer should have traded with the deck");
            }
        }

        // After all turns, showdown should have fired inline.
        game = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.Complete));

        // The dealer drew an Ace (value 1) — the lowest card. They should lose a stack.
        var refreshedDealer = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == dealer.Id);
        var refreshedNonDealer0 = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == nonDealerPlayers[0].Id);
        var refreshedNonDealer1 = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == nonDealerPlayers[1].Id);

        refreshedDealer.ChipStack.Should().Be(75, "dealer drew an Ace and should lose one stack (25 chips)");
        refreshedNonDealer0.ChipStack.Should().Be(100, "non-dealer with Queen should keep all chips");
        refreshedNonDealer1.ChipStack.Should().Be(100, "non-dealer with Jack should keep all chips");
    }

    [Fact]
    public async Task DealersChoiceScrewYourNeighbor_AnteZero_WhenVariantEnds_ReturnsToWaitingForDealerChoice()
    {
        // Edge-case: ante=0 means ChipStack >= 0 passes the eligible filter for 0-chip losers.
        // After auto-sit-out, the recomputed eligible count must drop below 2 so the DC fallback triggers.
        var game = await CreateDealersChoiceScrewYourNeighborSetupAsync(playerCount: 3, startingChips: 25, ante: 0);
        game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));

        var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

        var handCards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Hand &&
                         gc.GamePlayerId != null)
            .ToListAsync();

        // Rig: player 0 wins (Four), players 1 & 2 lose (Aces) — losers go to 0 chips in one hand
        handCards.First(c => c.GamePlayerId == players[0].Id).Symbol = CardSymbol.Four;
        handCards.First(c => c.GamePlayerId == players[0].Id).Suit = CardSuit.Hearts;
        handCards.First(c => c.GamePlayerId == players[1].Id).Symbol = CardSymbol.Ace;
        handCards.First(c => c.GamePlayerId == players[1].Id).Suit = CardSuit.Spades;
        handCards.First(c => c.GamePlayerId == players[2].Id).Symbol = CardSymbol.Ace;
        handCards.First(c => c.GamePlayerId == players[2].Id).Suit = CardSuit.Clubs;
        await DbContext.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            game = await DbContext.Games.Include(g => g.GamePlayers).AsNoTracking().FirstAsync(g => g.Id == game.Id);
            if (game.CurrentPhase != nameof(Phases.KeepOrTrade)) break;
            var actor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
            var keepResult = await Mediator.Send(new KeepOrTradeCommand(game.Id, actor.PlayerId, "Keep"));
            if (keepResult.IsT1)
                throw new Xunit.Sdk.XunitException($"KeepOrTrade failed: {keepResult.AsT1.Code} - {keepResult.AsT1.Message}");
        }

        var completedGame = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == game.Id);
        completedGame.CurrentPhase.Should().Be(nameof(Phases.Complete));

        var gameToSchedule = await DbContext.Games.FirstAsync(g => g.Id == game.Id);
        gameToSchedule.NextHandStartsAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await DbContext.SaveChangesAsync();

        var serviceScopeFactory = Scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = new TestLogger<ContinuousPlayBackgroundService>();
        var service = new ContinuousPlayBackgroundService(serviceScopeFactory, logger);
        await service.ProcessGamesReadyForNextHandAsync(CancellationToken.None);
        logger.ErrorLogs.Should().BeEmpty();
        DbContext.ChangeTracker.Clear();

        var updatedGame = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == game.Id);
        updatedGame.CurrentPhase.Should().Be(nameof(Phases.WaitingForDealerChoice),
            $"DC+SYN with ante=0 should go to WaitingForDealerChoice, not {updatedGame.CurrentPhase}");
    }
}

