using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Games.HoldEm;

[CollectionDefinition("KlondikeSequential", DisableParallelization = true)]
public sealed class KlondikeSequentialCollection;

/// <summary>
/// Integration tests for Klondike Hold'em lifecycle, Klondike Card dealing, and wild card behavior.
/// </summary>
[Collection("KlondikeSequential")]
public class KlondikeHandLifecycleTests : IntegrationTestBase
{
    private const string GameTypeCode = "KLONDIKE";

    protected override async Task SeedBaseDataAsync()
    {
        await base.SeedBaseDataAsync();

        var exists = await DbContext.GameTypes.AnyAsync(gt => gt.Code == GameTypeCode);
        if (exists) return;

        await DbContext.GameTypes.AddAsync(new GameType
        {
            Id = Guid.CreateVersion7(),
            Code = GameTypeCode,
            Name = "Klondike",
            MinPlayers = 2,
            MaxPlayers = 10,
            InitialHoleCards = 2,
            InitialBoardCards = 0,
            MaxCommunityCards = 6,
            MaxPlayerCards = 2,
            BettingStructure = BettingStructure.Ante
        });

        await DbContext.SaveChangesAsync();
    }

    #region Flow Handler Configuration

    [Fact]
    public async Task FlowHandler_IsConfiguredAsHoldEmStyle()
    {
        var handler = new KlondikeFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, GameTypeCode, 3);

        handler.SkipsAnteCollection.Should().BeTrue();
        handler.GameTypeCode.Should().Be(GameTypeCode);
        handler.GetInitialPhase(setup.Game).Should().Be("CollectingBlinds");
        handler.GetDealingConfiguration().InitialCardsPerPlayer.Should().Be(2);
        handler.GetDealingConfiguration().PatternType.Should().Be(DealingPatternType.CommunityCard);
    }

    #endregion

    #region Start Hand & Dealing

    [Fact]
    public async Task StartHand_DealsTwoHoleCardsPerPlayer()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, GameTypeCode, 3, 1000);
        setup.Game.CurrentPhase = nameof(Phases.WaitingToStart);
        setup.Game.CurrentHandNumber = 0;
        await DbContext.SaveChangesAsync();

        var startResult = await Mediator.Send(new StartHandCommand(setup.Game.Id));
        startResult.IsT0.Should().BeTrue();

        var context = GetFreshDbContext();
        var game = await context.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);

        var dealtHoleCards = await context.GameCards
            .AsNoTracking()
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Hand
                && !gc.IsDiscarded)
            .ToListAsync();

        dealtHoleCards.Should().HaveCount(6, "three-player Klondike should deal exactly two hole cards per player");

        var cardsPerPlayer = dealtHoleCards
            .Where(gc => gc.GamePlayerId.HasValue)
            .GroupBy(gc => gc.GamePlayerId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        cardsPerPlayer.Should().HaveCount(3);
        cardsPerPlayer.Values.Should().OnlyContain(count => count == 2);
    }

    #endregion

    #region Klondike Card Dealing

    [Fact]
    public async Task InitialDeal_DealsKlondikeCardFaceDown()
    {
        var setup = await CreateDealtKlondikeGameAsync(playerCount: 3, dealerPosition: 0);

        // Klondike card should already be dealt face-down during initial deal (before any betting)
        var fresh = GetFreshDbContext();
        var game = await fresh.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);

        var klondikeCards = await fresh.GameCards
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && gc.DealtAtPhase == "KlondikeCard")
            .ToListAsync();

        klondikeCards.Should().HaveCount(1, "exactly one Klondike Card should be dealt during initial deal");
        klondikeCards[0].IsVisible.Should().BeFalse("Klondike Card should be dealt face-down");
        klondikeCards[0].DealOrder.Should().Be(0, "Klondike Card DealOrder should be 0 (dealt first)");
    }

    [Fact]
    public async Task FlopPhase_DealsThreeCommunityCards()
    {
        var setup = await CreateDealtKlondikeGameAsync(playerCount: 3, dealerPosition: 0);

        // Complete PreFlop to deal Flop
        await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "Flop");

        var fresh = GetFreshDbContext();
        var game = await fresh.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);

        var flopCards = await fresh.GameCards
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && gc.DealtAtPhase == "Flop")
            .ToListAsync();

        flopCards.Should().HaveCount(3, "Flop should deal 3 community cards");
        flopCards.Should().AllSatisfy(c => c.IsVisible.Should().BeTrue("Flop cards should be visible"));
    }

    [Fact]
    public async Task FullDealSequence_ProducesSixCommunityCards_WithCorrectDealOrders()
    {
        var setup = await CreateDealtKlondikeGameAsync(playerCount: 3, dealerPosition: 0);

        // Advance all the way to Showdown
        await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "Showdown");

        var fresh = GetFreshDbContext();
        var game = await fresh.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);

        var communityCards = await fresh.GameCards
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .ToListAsync();

        communityCards.Should().HaveCount(6, "Klondike should have 6 community cards: KlondikeCard + 3 Flop + Turn + River");

        // Verify deal order and phases (Klondike card dealt first at DealOrder 0, then Flop/Turn/River)
        communityCards[0].DealtAtPhase.Should().Be("KlondikeCard");
        communityCards[0].DealOrder.Should().Be(0);
        communityCards[1].DealtAtPhase.Should().Be("Flop");
        communityCards[1].DealOrder.Should().Be(1);
        communityCards[2].DealtAtPhase.Should().Be("Flop");
        communityCards[2].DealOrder.Should().Be(2);
        communityCards[3].DealtAtPhase.Should().Be("Flop");
        communityCards[3].DealOrder.Should().Be(3);
        communityCards[4].DealtAtPhase.Should().Be("Turn");
        communityCards[4].DealOrder.Should().Be(4);
        communityCards[5].DealtAtPhase.Should().Be("River");
        communityCards[5].DealOrder.Should().Be(5);

        // All visible except the Klondike Card (which gets revealed at Showdown)
        communityCards.Where(c => c.DealtAtPhase != "KlondikeCard")
            .Should().AllSatisfy(c => c.IsVisible.Should().BeTrue());
    }

    #endregion

    #region Negative Path

    [Fact]
    public async Task ProcessBettingAction_WhenNotInBettingPhase_ReturnsInvalidGameState()
    {
        var setup = await CreateKlondikeGameSetupAsync(playerCount: 3, dealerPosition: 0);
        setup.Game.CurrentPhase = "CollectingBlinds";
        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.Check));

        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidGameState);
    }

    #endregion

    #region All-In Runout

    [Fact]
    public async Task AllInRunout_DealsAllCommunityCardsIncludingKlondikeCard()
    {
        var setup = await CreateDealtKlondikeGameAsync(playerCount: 2, dealerPosition: 0);

        // Advance to Flop
        await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "Flop");

        // Both players go all-in at the Flop to trigger runout
        var firstAllIn = await Mediator.Send(
            new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.AllIn));
        firstAllIn.IsT0.Should().BeTrue("first all-in should succeed");

        // Check if we need a second all-in (depends on whether first action completed the round)
        var stateAfterFirst = await GetFreshDbContext().Games
            .AsNoTracking()
            .FirstAsync(g => g.Id == setup.Game.Id);

        if (!string.Equals(stateAfterFirst.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase))
        {
            var secondAllIn = await Mediator.Send(
                new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.AllIn));
            secondAllIn.IsT0.Should().BeTrue("second all-in should succeed");
        }

        var fresh = GetFreshDbContext();
        var game = await fresh.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be("Showdown");

        var communityCards = await fresh.GameCards
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .ToListAsync();

        communityCards.Should().HaveCount(6, "all-in runout should deal all 6 community cards");

        // Verify Klondike Card is present (dealt at initial deal time, not during runout)
        communityCards.Count(c => c.DealtAtPhase == "KlondikeCard").Should().Be(1,
            "Klondike Card should be present (dealt during initial deal)");

        var klondikeCard = communityCards.Single(c => c.DealtAtPhase == "KlondikeCard");
        klondikeCard.DealOrder.Should().Be(0);
        klondikeCard.IsVisible.Should().BeFalse("Klondike Card should remain face-down even after runout");
    }

    #endregion

    #region Helpers

    private async Task<GameSetup> CreateKlondikeGameSetupAsync(
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

    private async Task<GameSetup> CreateDealtKlondikeGameAsync(int playerCount, int dealerPosition)
    {
        var setup = await CreateKlondikeGameSetupAsync(playerCount, dealerPosition);
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
                return;

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

        if (round is null) return;

        for (var i = 0; i < 16; i++)
        {
            var freshRound = await GetFreshDbContext().BettingRounds
                .AsNoTracking()
                .FirstOrDefaultAsync(br => br.Id == round.Id);

            if (freshRound is null || freshRound.IsComplete) return;

            var result = await SendPassiveBettingActionAsync(gameId);
            result.IsT0.Should().BeTrue($"passive betting action at iteration {i} should complete the current round");
        }

        throw new Xunit.Sdk.XunitException($"Failed to complete betting round {round.Id} for game {gameId}.");
    }

    private async Task<OneOf.OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>> SendPassiveBettingActionAsync(Guid gameId)
    {
        var context = GetFreshDbContext();
        var game = await context.Games.AsNoTracking().FirstAsync(g => g.Id == gameId);

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

    #endregion
}
