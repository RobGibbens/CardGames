using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.GameFlow;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Games.HoldEm;

/// <summary>
/// Integration tests for the Texas Hold 'Em hand lifecycle.
/// Covers blind collection, dealing, phase progression, community cards,
/// and fold-to-win scenarios through the command pipeline.
/// </summary>
public class HoldEmHandLifecycleTests : IntegrationTestBase
{
    #region Flow Handler Properties

    [Fact]
    public void SkipsAnteCollection_ReturnsTrue()
    {
        // Arrange
        var handler = new HoldEmFlowHandler();

        // Assert
        handler.SkipsAnteCollection.Should().BeTrue();
    }

    [Fact]
    public async Task GetInitialPhase_ReturnsCollectingBlinds()
    {
        // Arrange
        var handler = new HoldEmFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "HOLDEM", 4);

        // Act
        var initialPhase = handler.GetInitialPhase(setup.Game);

        // Assert
        initialPhase.Should().Be("CollectingBlinds");
    }

    [Fact]
    public async Task GetNextPhase_FollowsHoldEmProgression()
    {
        // Arrange
        var handler = new HoldEmFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "HOLDEM", 4);
        var game = setup.Game;

        // Assert full phase chain
        handler.GetNextPhase(game, "CollectingBlinds").Should().Be("Dealing");
        handler.GetNextPhase(game, "Dealing").Should().Be("PreFlop");
        handler.GetNextPhase(game, "PreFlop").Should().Be("Flop");
        handler.GetNextPhase(game, "Flop").Should().Be("Turn");
        handler.GetNextPhase(game, "Turn").Should().Be("River");
        handler.GetNextPhase(game, "River").Should().Be("Showdown");
        handler.GetNextPhase(game, "Showdown").Should().Be("Complete");
        handler.GetNextPhase(game, "Complete").Should().BeNull();
    }

    [Fact]
    public void GetDealingConfiguration_ReturnsCommunityCardPatternWith2HoleCards()
    {
        // Arrange
        var handler = new HoldEmFlowHandler();

        // Act
        var config = handler.GetDealingConfiguration();

        // Assert
        config.PatternType.Should().Be(DealingPatternType.CommunityCard);
        config.InitialCardsPerPlayer.Should().Be(2);
        config.AllFaceDown.Should().BeTrue();
    }

    #endregion

    #region Game Setup & Blind Collection

    [Fact]
    public async Task DealCardsAsync_Deals2HoleCardsPerPlayer()
    {
        // Arrange
        var handler = FlowHandlerFactory.GetHandler("HOLDEM");
        var setup = await CreateHoldEmGameSetupAsync(playerCount: 4);
        var game = setup.Game;

        // Create pot for blind collection
        await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

        // Act
        await handler.DealCardsAsync(
            DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert
        var cards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id && gc.HandNumber == game.CurrentHandNumber)
            .ToListAsync();

        var playerCards = cards.Where(c => c.GamePlayerId != null && c.Location == CardLocation.Hand).ToList();
        playerCards.Should().HaveCount(8, "4 players * 2 hole cards each");

        foreach (var gp in setup.GamePlayers)
        {
            cards.Count(c => c.GamePlayerId == gp.Id && c.Location == CardLocation.Hand)
                .Should().Be(2, $"player at seat {gp.SeatPosition} should have 2 hole cards");
        }
    }

    [Fact]
    public async Task DealCardsAsync_CollectsBlindsFromCorrectPlayers()
    {
        // Arrange — 4 players, dealer at seat 0 → SB = seat 1, BB = seat 2
        var handler = FlowHandlerFactory.GetHandler("HOLDEM");
        var setup = await CreateHoldEmGameSetupAsync(playerCount: 4, dealerPosition: 0);
        var game = setup.Game;

        await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

        // Act
        await handler.DealCardsAsync(
            DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert — re-query to see updated chip stacks
        var freshCtx = GetFreshDbContext();
        var players = await freshCtx.GamePlayers
            .Where(gp => gp.GameId == game.Id)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        // Seat 0 = dealer, unchanged
        players[0].ChipStack.Should().Be(1000, "dealer should not post a blind");

        // Seat 1 = small blind (5)
        players[1].ChipStack.Should().Be(995, "SB should be deducted 5");

        // Seat 2 = big blind (10)
        players[2].ChipStack.Should().Be(990, "BB should be deducted 10");

        // Seat 3 = no blind
        players[3].ChipStack.Should().Be(1000, "UTG should not post a blind");

        // Verify total contributed tracks blind amounts
        players[1].TotalContributedThisHand.Should().Be(5, "SB should have contributed 5");
        players[2].TotalContributedThisHand.Should().Be(10, "BB should have contributed 10");
    }

    [Fact]
    public async Task DealCardsAsync_HeadsUp_DealerIsSmallBlind()
    {
        // Arrange — 2 players, dealer at seat 0 → dealer = SB, other = BB
        var handler = FlowHandlerFactory.GetHandler("HOLDEM");
        var setup = await CreateHoldEmGameSetupAsync(playerCount: 2, dealerPosition: 0);
        var game = setup.Game;

        await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

        // Act
        await handler.DealCardsAsync(
            DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert
        var freshCtx = GetFreshDbContext();
        var players = await freshCtx.GamePlayers
            .Where(gp => gp.GameId == game.Id)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        // Seat 0 = dealer = SB
        players[0].ChipStack.Should().Be(995, "dealer should post SB of 5 in heads-up");
        players[0].TotalContributedThisHand.Should().Be(5);

        // Seat 1 = BB
        players[1].ChipStack.Should().Be(990, "non-dealer should post BB of 10 in heads-up");
        players[1].TotalContributedThisHand.Should().Be(10);
    }

    [Fact]
    public async Task DealCardsAsync_AllHoleCardsAreFaceDown()
    {
        // Arrange
        var handler = FlowHandlerFactory.GetHandler("HOLDEM");
        var setup = await CreateHoldEmGameSetupAsync(playerCount: 3);
        await DatabaseSeeder.CreatePotAsync(DbContext, game: setup.Game, amount: 0);

        // Act
        await handler.DealCardsAsync(
            DbContext, setup.Game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        // Assert
        var playerCards = await DbContext.GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.GamePlayerId != null)
            .ToListAsync();

        playerCards.Should().AllSatisfy(c => c.IsVisible.Should().BeFalse());
    }

    #endregion

    #region Phase Progression & Community Cards

    [Fact]
    public async Task ProcessBettingAction_PreFlopCheckAround_AdvancesToFlopWith3CommunityCards()
    {
        // Arrange — set up a dealt game in PreFlop with a betting round
        var setup = await CreateDealtHoldEmGameAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        // The betting round was created by DealCardsAsync via the base handler.
        // Get the active betting round.
        var bettingRound = await DbContext.BettingRounds
            .FirstAsync(br => br.GameId == game.Id && !br.IsComplete);

        // In PreFlop after blinds, the first actor is typically UTG (seat after BB).
        // For 3 players with dealer at seat 0: SB = seat 1, BB = seat 2, first to act = seat 0.
        // We need all active players to act: the handler requires everyone to match the current bet.
        // Since base DealDrawStyleCardsAsync resets CurrentBet to 0 after dealing,
        // and the betting round CurrentBet = 0, everyone can check.

        var activePlayers = await DbContext.GamePlayers
            .Where(gp => gp.GameId == game.Id && !gp.HasFolded)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        // Have each active player check through the PreFlop.
        // Walk through the betting round actors until it completes.
        for (var i = 0; i < activePlayers.Count; i++)
        {
            var freshRound = await GetFreshDbContext().BettingRounds
                .FirstOrDefaultAsync(br => br.GameId == game.Id && !br.IsComplete);
            if (freshRound is null) break; // Round already completed

            var result = await Mediator.Send(
                new ProcessBettingActionCommand(game.Id, BettingActionType.Check));

            result.IsT0.Should().BeTrue($"check by player at iteration {i} should succeed");
        }

        // Assert — game should have advanced to Flop
        var freshGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        freshGame.CurrentPhase.Should().Be("Flop");

        // 3 community cards dealt for the Flop
        var communityCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community)
            .ToListAsync();

        communityCards.Should().HaveCount(3, "Flop should deal 3 community cards");
        communityCards.Should().AllSatisfy(c =>
        {
            c.DealtAtPhase.Should().Be("Flop");
            c.IsVisible.Should().BeTrue("community cards should be visible");
        });
    }

    #endregion

    #region Fold-to-Win

    [Fact]
    public async Task ProcessBettingAction_AllButOneFold_HandCompletesWithFoldWin()
    {
        // Arrange — 3-player game dealt and in PreFlop
        var setup = await CreateDealtHoldEmGameAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        // First actor bets so others can fold (can't fold when you can check)
        var betResult = await Mediator.Send(
            new ProcessBettingActionCommand(game.Id, BettingActionType.Bet, 20));
        betResult.IsT0.Should().BeTrue("opening bet should succeed");

        // Two remaining actors fold
        var result1 = await Mediator.Send(
            new ProcessBettingActionCommand(game.Id, BettingActionType.Fold));
        result1.IsT0.Should().BeTrue("first fold should succeed");

        var result2 = await Mediator.Send(
            new ProcessBettingActionCommand(game.Id, BettingActionType.Fold));
        result2.IsT0.Should().BeTrue("second fold should succeed");

        // Assert — phase should advance to Showdown (fold-to-win triggers showdown)
        var freshGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        freshGame.CurrentPhase.Should().Be("Showdown");

        // Two players should be marked as folded
        var freshPlayers = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == game.Id)
            .ToListAsync();

        freshPlayers.Count(p => p.HasFolded).Should().Be(2);
        freshPlayers.Count(p => !p.HasFolded).Should().Be(1, "exactly one player should remain");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a Hold 'Em game setup with blinds configured and hand number set for dealing.
    /// </summary>
    private async Task<GameSetup> CreateHoldEmGameSetupAsync(
        int playerCount = 4,
        int dealerPosition = 0,
        int startingChips = 1000)
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext, "HOLDEM", playerCount, startingChips);

        var game = setup.Game;
        game.SmallBlind = 5;
        game.BigBlind = 10;
        game.DealerPosition = dealerPosition;
        game.CurrentHandNumber = 1;
        game.Status = GameStatus.InProgress;
        await DbContext.SaveChangesAsync();

        return setup;
    }

    /// <summary>
    /// Creates a Hold 'Em game that has already been dealt (in PreFlop with a betting round ready).
    /// </summary>
    private async Task<GameSetup> CreateDealtHoldEmGameAsync(
        int playerCount = 3,
        int dealerPosition = 0,
        int startingChips = 1000)
    {
        var setup = await CreateHoldEmGameSetupAsync(playerCount, dealerPosition, startingChips);
        var game = setup.Game;

        // Create the main pot
        await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

        // Deal cards (collects blinds + deals hole cards + creates PreFlop betting round)
        var handler = FlowHandlerFactory.GetHandler("HOLDEM");
        await handler.DealCardsAsync(
            DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        return setup;
    }

    #endregion
}
