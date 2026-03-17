using CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Services;
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
    }

    [Fact]
    public async Task StartHand_WhenDeckCannotCoverNextHand_ReshufflesFreshDeck()
    {
        var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(3);

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

        game = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.Showdown));

        var showdownResult = await Mediator.Send(new PerformShowdownCommand(game.Id));
        showdownResult.IsT0.Should().BeTrue();

        var refreshedGame = await DbContext.Games
            .AsNoTracking()
            .FirstAsync(g => g.Id == game.Id);

        var refreshedWinner = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == winner.Id);
        var refreshedLoser = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == loser.Id);
        var nextHandPot = await DbContext.Pots
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.GameId == game.Id &&
                                     p.HandNumber == refreshedGame.CurrentHandNumber + 1 &&
                                     p.PotType == PotType.Main);

        refreshedWinner.ChipStack.Should().Be(100);
        refreshedLoser.ChipStack.Should().Be(75);
        nextHandPot.Should().NotBeNull();
        nextHandPot!.Amount.Should().Be(25);
        refreshedGame.CurrentPhase.Should().Be(nameof(Phases.Complete));
        refreshedGame.NextHandStartsAt.Should().NotBeNull();
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

        var showdownGame = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        showdownGame.CurrentPhase.Should().Be(nameof(Phases.Showdown));

        var showdownResult = await Mediator.Send(new PerformShowdownCommand(showdownGame.Id));
        showdownResult.IsT0.Should().BeTrue();

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

        game = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.Showdown));

        var showdownResult = await Mediator.Send(new PerformShowdownCommand(game.Id));
        showdownResult.IsT0.Should().BeTrue();
        var showdown = showdownResult.AsT0;
        showdown.Payouts.Should().ContainSingle();
        showdown.Payouts.Should().ContainKey(winner.Player.Name);
        showdown.Payouts[winner.Player.Name].Should().Be(25);

        var walletService = Scope.ServiceProvider.GetRequiredService<IPlayerChipWalletService>();
        var winnerBalance = await walletService.GetBalanceAsync(winner.PlayerId, CancellationToken.None);
        var loserBalance = await walletService.GetBalanceAsync(loser.PlayerId, CancellationToken.None);

        winnerBalance.Should().Be(25);
        loserBalance.Should().Be(-25);

        var refreshedGame = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == game.Id);
        refreshedGame.CurrentPhase.Should().Be("Ended");
        refreshedGame.Status.Should().Be(GameStatus.Completed);
        refreshedGame.NextHandStartsAt.Should().BeNull();
    }
}

