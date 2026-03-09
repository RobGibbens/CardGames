using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Games.HoldEm;

[CollectionDefinition("RedRiverSequential", DisableParallelization = true)]
public sealed class RedRiverSequentialCollection;

/// <summary>
/// Integration tests for Red River lifecycle and river bonus-card behavior.
/// </summary>
[Collection("RedRiverSequential")]
public class RedRiverHandLifecycleTests : IntegrationTestBase
{
    private const string GameTypeCode = "REDRIVER";

    protected override async Task SeedBaseDataAsync()
    {
        await base.SeedBaseDataAsync();

        var exists = await DbContext.GameTypes.AnyAsync(gt => gt.Code == GameTypeCode);
        if (exists)
        {
            return;
        }

        await DbContext.GameTypes.AddAsync(new GameType
        {
            Id = Guid.CreateVersion7(),
            Code = GameTypeCode,
            Name = "Red River",
            MinPlayers = 2,
            MaxPlayers = 10,
            InitialHoleCards = 2,
            InitialBoardCards = 0,
            MaxCommunityCards = 6,
            MaxPlayerCards = 8,
            BettingStructure = BettingStructure.Ante
        });

        await DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task FlowHandler_IsConfiguredAsHoldEmStyle()
    {
        var handler = new RedRiverFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, GameTypeCode, 3);

        handler.SkipsAnteCollection.Should().BeTrue();
        handler.GameTypeCode.Should().Be(GameTypeCode);
        handler.GetInitialPhase(setup.Game).Should().Be("CollectingBlinds");
        handler.GetDealingConfiguration().InitialCardsPerPlayer.Should().Be(2);
    }

    [Fact]
    public async Task StartHand_ForRedRiver_DealsTwoHoleCardsPerPlayer_NotFiveCardDrawHands()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, GameTypeCode, 2, 1000);
        setup.Game.CurrentPhase = nameof(Phases.WaitingToStart);
        setup.Game.CurrentHandNumber = 0;
        await DbContext.SaveChangesAsync();

        var startResult = await Mediator.Send(new StartHandCommand(setup.Game.Id));

        startResult.IsT0.Should().BeTrue();

        var context = GetFreshDbContext();
        var game = await context.Games
            .AsNoTracking()
            .FirstAsync(g => g.Id == setup.Game.Id);

        var dealtHoleCards = await context.GameCards
            .AsNoTracking()
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Hand
                && !gc.IsDiscarded)
            .ToListAsync();

        dealtHoleCards.Should().HaveCount(4, "two-player Red River should deal exactly two hole cards per player");

        var cardsPerPlayer = dealtHoleCards
            .Where(gc => gc.GamePlayerId.HasValue)
            .GroupBy(gc => gc.GamePlayerId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        cardsPerPlayer.Should().HaveCount(2);
        cardsPerPlayer.Values.Should().OnlyContain(count => count == 2);
    }

    [Fact]
    public async Task RiverRound_WhenRiverIsRed_DealsBonusCommunityCardBeforeShowdown()
    {
        var setup = await CreateDealtRedRiverGameAsync(playerCount: 3, dealerPosition: 0);

        await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "River");
        await SetRiverSuitAsync(setup.Game.Id, CardSuit.Hearts);

        await CompleteCurrentBettingRoundWithChecksAsync(setup.Game.Id);

        var fresh = GetFreshDbContext();
        var game = await fresh.Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be("Showdown");

        var communityCards = await fresh.GameCards
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .ToListAsync();

        communityCards.Should().HaveCount(6);
        communityCards.Count(c => c.DealtAtPhase == "RedRiverBonus").Should().Be(1);
    }

    [Fact]
    public async Task RiverRound_WhenRiverIsBlack_DoesNotDealBonusCommunityCard()
    {
        var setup = await CreateDealtRedRiverGameAsync(playerCount: 3, dealerPosition: 0);

        await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "River");
        await SetRiverSuitAsync(setup.Game.Id, CardSuit.Spades);

        await CompleteCurrentBettingRoundWithChecksAsync(setup.Game.Id);

        var fresh = GetFreshDbContext();
        var game = await fresh.Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be("Showdown");

        var communityCards = await fresh.GameCards
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && !gc.IsDiscarded)
            .ToListAsync();

        communityCards.Should().HaveCount(5);
        communityCards.Should().OnlyContain(c => c.DealtAtPhase != "RedRiverBonus");
    }

    [Fact]
    public async Task ProcessBettingAction_WhenNotInBettingPhase_ReturnsInvalidGameState()
    {
        var setup = await CreateRedRiverGameSetupAsync(playerCount: 3, dealerPosition: 0);
        setup.Game.CurrentPhase = "CollectingBlinds";
        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.Check));

        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidGameState);
    }

    [Fact]
    public async Task RiverAllInRunout_WithControlledDeck_DealsDeterministicBonusCardWhenRiverIsRed()
    {
        var setup = await CreateDealtRedRiverGameAsync(playerCount: 2, dealerPosition: 0);

        await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "River");
        await SetRiverSuitAsync(setup.Game.Id, CardSuit.Hearts);
        await ConfigureNextDeckCardAsync(setup.Game.Id, bonusSuit: CardSuit.Clubs, bonusSymbol: CardSymbol.King);

        var preAllInContext = GetFreshDbContext();
        var preAllInGame = await preAllInContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        var preAllInRiverCards = await preAllInContext.GameCards
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == preAllInGame.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && gc.DealtAtPhase == "River")
            .ToListAsync();

        preAllInRiverCards.Should().NotBeEmpty();
        preAllInRiverCards.Should().AllSatisfy(c => c.Suit.Should().Be(CardSuit.Hearts));

        var firstAllIn = await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.AllIn));
        firstAllIn.IsT0.Should().BeTrue("first all-in at River should succeed");

        var stateAfterFirstAllIn = await GetFreshDbContext().Games
            .AsNoTracking()
            .FirstAsync(g => g.Id == setup.Game.Id);

        if (string.Equals(stateAfterFirstAllIn.CurrentPhase, "River", StringComparison.OrdinalIgnoreCase))
        {
            var secondAllIn = await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.AllIn));
            secondAllIn.IsT0.Should().BeTrue("second all-in at River should succeed when runout does not occur on first action");
        }

        var context = GetFreshDbContext();
        var game = await context.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be("Showdown");

        var communityCards = await context.GameCards
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .ToListAsync();

        communityCards.Should().HaveCount(6, "red river in all-in runout should still trigger the bonus board card");

        var riverCard = communityCards.LastOrDefault(c => c.DealtAtPhase == "River");
        riverCard.Should().NotBeNull();
        riverCard!.Suit.Should().Be(CardSuit.Hearts);

        var bonusCard = communityCards.SingleOrDefault(c => c.DealtAtPhase == "RedRiverBonus");
        bonusCard.Should().NotBeNull();
        bonusCard!.Suit.Should().Be(CardSuit.Clubs);
        bonusCard.Symbol.Should().Be(CardSymbol.King);
        bonusCard.DealOrder.Should().Be(6);
    }

    private async Task<GameSetup> CreateRedRiverGameSetupAsync(
        int playerCount,
        int dealerPosition,
        int startingChips = 1000)
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext, GameTypeCode, playerCount, startingChips);

        var game = setup.Game;
        game.SmallBlind = 5;
        game.BigBlind = 10;
        game.DealerPosition = dealerPosition;
        game.CurrentHandNumber = 1;
        game.Status = GameStatus.InProgress;

        await DbContext.SaveChangesAsync();

        return setup;
    }

    private async Task<GameSetup> CreateDealtRedRiverGameAsync(int playerCount, int dealerPosition)
    {
        var setup = await CreateRedRiverGameSetupAsync(playerCount, dealerPosition);
        await DatabaseSeeder.CreatePotAsync(DbContext, setup.Game, 0);

        var handler = FlowHandlerFactory.GetHandler(GameTypeCode);
        await handler.DealCardsAsync(
            DbContext, setup.Game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        return setup;
    }

    private async Task AdvanceToPhaseByCheckingAsync(Guid gameId, string targetPhase)
    {
        for (var i = 0; i < 64; i++)
        {
            var game = await GetFreshDbContext().Games.AsNoTracking().FirstAsync(g => g.Id == gameId);
            if (string.Equals(game.CurrentPhase, targetPhase, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var result = await SendPassiveBettingActionAsync(gameId);
            result.IsT0.Should().BeTrue($"passive betting action at iteration {i} should succeed while advancing to {targetPhase}");
        }

        throw new Xunit.Sdk.XunitException($"Failed to advance game {gameId} to phase {targetPhase} within iteration budget.");
    }

    private async Task CompleteCurrentBettingRoundWithChecksAsync(Guid gameId)
    {
        var round = await GetFreshDbContext().BettingRounds
            .Where(br => br.GameId == gameId && !br.IsComplete)
            .OrderByDescending(br => br.RoundNumber)
            .FirstOrDefaultAsync();

        if (round is null)
        {
            return;
        }

        for (var i = 0; i < 16; i++)
        {
            var freshRound = await GetFreshDbContext().BettingRounds
                .AsNoTracking()
                .FirstOrDefaultAsync(br => br.Id == round.Id);

            if (freshRound is null || freshRound.IsComplete)
            {
                return;
            }

            var result = await SendPassiveBettingActionAsync(gameId);
            result.IsT0.Should().BeTrue($"passive betting action at iteration {i} should complete the current round");
        }

        throw new Xunit.Sdk.XunitException($"Failed to complete betting round {round.Id} for game {gameId}.");
    }

    private async Task SetRiverSuitAsync(Guid gameId, CardSuit suit)
    {
        var context = GetFreshDbContext();
        var game = await context.Games.FirstAsync(g => g.Id == gameId);

        var riverCards = await context.GameCards
            .Where(gc => gc.GameId == gameId
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && gc.DealtAtPhase == "River")
            .ToListAsync();

        riverCards.Should().NotBeEmpty("river card should already be dealt before suit adjustment");
        foreach (var riverCard in riverCards)
        {
            riverCard.Suit = suit;
        }

        await context.SaveChangesAsync();
    }

    private async Task ConfigureNextDeckCardAsync(
        Guid gameId,
        CardSuit bonusSuit,
        CardSymbol bonusSymbol,
        int deckOffset = 0)
    {
        var context = GetFreshDbContext();
        var game = await context.Games.AsNoTracking().FirstAsync(g => g.Id == gameId);

        var deckCards = await context.GameCards
            .Where(gc => gc.GameId == gameId
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Deck)
            .OrderBy(gc => gc.DealOrder)
            .ToListAsync();

        deckCards.Count.Should().BeGreaterThan(deckOffset);

        deckCards[deckOffset].Suit = bonusSuit;
        deckCards[deckOffset].Symbol = bonusSymbol;

        await context.SaveChangesAsync();
    }

    private async Task<OneOf.OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>> SendPassiveBettingActionAsync(Guid gameId)
    {
        var context = GetFreshDbContext();
        var game = await context.Games
            .AsNoTracking()
            .FirstAsync(g => g.Id == gameId);

        var bettingRound = await context.BettingRounds
            .AsNoTracking()
            .Where(br => br.GameId == gameId
                && br.HandNumber == game.CurrentHandNumber
                && !br.IsComplete)
            .OrderByDescending(br => br.RoundNumber)
            .FirstAsync();

        var actor = await context.GamePlayers
            .AsNoTracking()
            .FirstAsync(gp => gp.GameId == gameId && gp.SeatPosition == bettingRound.CurrentActorIndex);

        var action = bettingRound.CurrentBet > actor.CurrentBet
            ? BettingActionType.Call
            : BettingActionType.Check;

        return await Mediator.Send(new ProcessBettingActionCommand(gameId, action));
    }
}
