using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Data.Entities;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Games.IrishHoldEm;

/// <summary>
/// Integration tests for the Irish Hold 'Em hand lifecycle.
/// Covers blind collection, dealing 4 hole cards, phase progression through
/// PreFlop → Flop → Discard → Turn → River → Showdown, community cards,
/// discard mechanics, and fold-to-win scenarios through the command pipeline.
/// </summary>
public class IrishHoldEmHandLifecycleTests : IntegrationTestBase
{
    #region Flow Handler Properties

    [Fact]
    public void SkipsAnteCollection_ReturnsTrue()
    {
        var handler = new IrishHoldEmFlowHandler();
        handler.SkipsAnteCollection.Should().BeTrue();
    }

    [Fact]
    public async Task GetInitialPhase_ReturnsCollectingBlinds()
    {
        var handler = new IrishHoldEmFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "IRISHHOLDEM", 4);

        var initialPhase = handler.GetInitialPhase(setup.Game);

        initialPhase.Should().Be("CollectingBlinds");
    }

    [Fact]
    public async Task GetNextPhase_FollowsIrishHoldEmProgression()
    {
        var handler = new IrishHoldEmFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "IRISHHOLDEM", 4);
        var game = setup.Game;

        // Assert full phase chain — Irish differs from Hold 'Em by adding DrawPhase after Flop
        handler.GetNextPhase(game, "CollectingBlinds").Should().Be("Dealing");
        handler.GetNextPhase(game, "Dealing").Should().Be("PreFlop");
        handler.GetNextPhase(game, "PreFlop").Should().Be("Flop");
        handler.GetNextPhase(game, "Flop").Should().Be("DrawPhase");
        handler.GetNextPhase(game, "DrawPhase").Should().Be("Turn");
        handler.GetNextPhase(game, "Turn").Should().Be("River");
        handler.GetNextPhase(game, "River").Should().Be("Showdown");
        handler.GetNextPhase(game, "Showdown").Should().Be("Complete");
        handler.GetNextPhase(game, "Complete").Should().BeNull();
    }

    [Fact]
    public void GetDealingConfiguration_ReturnsCommunityCardPatternWith4HoleCards()
    {
        var handler = new IrishHoldEmFlowHandler();

        var config = handler.GetDealingConfiguration();

        config.PatternType.Should().Be(DealingPatternType.CommunityCard);
        config.InitialCardsPerPlayer.Should().Be(4);
        config.AllFaceDown.Should().BeTrue();
    }

    #endregion

    #region Game Setup & Blind Collection

    [Fact]
    public async Task DealCardsAsync_Deals4HoleCardsPerPlayer()
    {
        var handler = FlowHandlerFactory.GetHandler("IRISHHOLDEM");
        var setup = await CreateIrishHoldEmGameSetupAsync(playerCount: 4);
        var game = setup.Game;

        await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

        await handler.DealCardsAsync(
            DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        var cards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id && gc.HandNumber == game.CurrentHandNumber)
            .ToListAsync();

        var playerCards = cards.Where(c => c.GamePlayerId != null && c.Location == CardLocation.Hand).ToList();
        playerCards.Should().HaveCount(16, "4 players * 4 hole cards each");

        foreach (var gp in setup.GamePlayers)
        {
            cards.Count(c => c.GamePlayerId == gp.Id && c.Location == CardLocation.Hand)
                .Should().Be(4, $"player at seat {gp.SeatPosition} should have 4 hole cards");
        }
    }

    [Fact]
    public async Task DealCardsAsync_CollectsBlindsFromCorrectPlayers()
    {
        // Arrange — 4 players, dealer at seat 0 → SB = seat 1, BB = seat 2
        var handler = FlowHandlerFactory.GetHandler("IRISHHOLDEM");
        var setup = await CreateIrishHoldEmGameSetupAsync(playerCount: 4, dealerPosition: 0);
        var game = setup.Game;

        await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

        await handler.DealCardsAsync(
            DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

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

        players[1].TotalContributedThisHand.Should().Be(5, "SB should have contributed 5");
        players[2].TotalContributedThisHand.Should().Be(10, "BB should have contributed 10");
    }

    [Fact]
    public async Task DealCardsAsync_HeadsUp_DealerIsSmallBlind()
    {
        var handler = FlowHandlerFactory.GetHandler("IRISHHOLDEM");
        var setup = await CreateIrishHoldEmGameSetupAsync(playerCount: 2, dealerPosition: 0);
        var game = setup.Game;

        await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

        await handler.DealCardsAsync(
            DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        var freshCtx = GetFreshDbContext();
        var players = await freshCtx.GamePlayers
            .Where(gp => gp.GameId == game.Id)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        players[0].ChipStack.Should().Be(995, "dealer should post SB of 5 in heads-up");
        players[0].TotalContributedThisHand.Should().Be(5);

        players[1].ChipStack.Should().Be(990, "non-dealer should post BB of 10 in heads-up");
        players[1].TotalContributedThisHand.Should().Be(10);
    }

    [Fact]
    public async Task DealCardsAsync_AllHoleCardsAreFaceDown()
    {
        var handler = FlowHandlerFactory.GetHandler("IRISHHOLDEM");
        var setup = await CreateIrishHoldEmGameSetupAsync(playerCount: 3);
        await DatabaseSeeder.CreatePotAsync(DbContext, game: setup.Game, amount: 0);

        await handler.DealCardsAsync(
            DbContext, setup.Game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

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
        var setup = await CreateDealtIrishHoldEmGameAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        var activePlayers = await DbContext.GamePlayers
            .Where(gp => gp.GameId == game.Id && !gp.HasFolded)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        for (var i = 0; i < activePlayers.Count; i++)
        {
            var freshRound = await GetFreshDbContext().BettingRounds
                .FirstOrDefaultAsync(br => br.GameId == game.Id && !br.IsComplete);
            if (freshRound is null) break;

            var result = await Mediator.Send(
                new ProcessBettingActionCommand(game.Id, BettingActionType.Check));

            result.IsT0.Should().BeTrue($"check by player at iteration {i} should succeed");
        }

        var freshGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        freshGame.CurrentPhase.Should().Be("DrawPhase",
            "Irish Hold 'Em: after PreFlop betting, flop is dealt and phase goes directly to DrawPhase");

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

    #region Discard Phase

    [Fact]
    public async Task ProcessDiscard_PlayersDiscard2Of4Cards_EachPlayerHas2CardsAfter()
    {
        // Arrange — get to DrawPhase (PreFlop check around ➜ Flop check around ➜ DrawPhase)
        var setup = await CreateGameInDrawPhaseAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        // Verify players have 4 cards before discard
        var preDiscardCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id && gc.GamePlayerId != null && !gc.IsDiscarded)
            .ToListAsync();
        preDiscardCards.GroupBy(c => c.GamePlayerId)
            .Should().AllSatisfy(g => g.Count().Should().Be(4, "each player should have 4 cards before discard"));

        // Each player discards 2 cards (indices 0, 1)
        var activePlayers = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == game.Id && !gp.HasFolded)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        for (var i = 0; i < activePlayers.Count; i++)
        {
            var discardResult = await Mediator.Send(
                new ProcessDiscardCommand(game.Id, new List<int> { 0, 1 }));

            discardResult.IsT0.Should().BeTrue($"discard for player {i} should succeed");
        }

        // Verify each player now has 2 non-discarded cards
        var postDiscardCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id && gc.GamePlayerId != null && !gc.IsDiscarded)
            .ToListAsync();
        postDiscardCards.GroupBy(c => c.GamePlayerId)
            .Should().AllSatisfy(g => g.Count().Should().Be(2, "each player should have 2 cards after discard"));
    }

    [Fact]
    public async Task ProcessDiscard_InvalidCount_ReturnsError()
    {
        var setup = await CreateGameInDrawPhaseAsync(playerCount: 3, dealerPosition: 0);

        // Try discarding only 1 card (must be exactly 2)
        var result = await Mediator.Send(
            new ProcessDiscardCommand(setup.Game.Id, new List<int> { 0 }));

        result.IsT1.Should().BeTrue("discarding 1 card should fail");
        result.AsT1.Message.Should().Contain("exactly 2");
    }

    [Fact]
    public async Task ProcessDiscard_AllDiscardComplete_AdvancesToTurn()
    {
        var setup = await CreateGameInDrawPhaseAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        var activePlayers = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == game.Id && !gp.HasFolded)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        for (var i = 0; i < activePlayers.Count; i++)
        {
            await Mediator.Send(
                new ProcessDiscardCommand(game.Id, new List<int> { 2, 3 }));
        }

        var freshGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        freshGame.CurrentPhase.Should().Be("Turn", "after all players discard, phase should advance to Turn");
    }

    [Fact]
    public async Task ProcessDiscard_AllowsOutOfOrderDiscard_BySeatIndex()
    {
        var setup = await CreateGameInDrawPhaseAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        var freshDb = GetFreshDbContext();
        var freshGame = await freshDb.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == game.Id);

        var nonCurrentSeat = freshGame.GamePlayers
            .Where(gp => !gp.HasFolded && gp.Status == GamePlayerStatus.Active && gp.SeatPosition != freshGame.CurrentDrawPlayerIndex)
            .OrderBy(gp => gp.SeatPosition)
            .Select(gp => gp.SeatPosition)
            .First();

        var discardResult = await Mediator.Send(
            new ProcessDiscardCommand(game.Id, new List<int> { 0, 1 }, nonCurrentSeat));

        discardResult.IsT0.Should().BeTrue("an eligible Irish Hold 'Em player should be able to discard without waiting for seat order");
        discardResult.AsT0.PlayerSeatIndex.Should().Be(nonCurrentSeat);

        var gamePlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.GameId == game.Id && gp.SeatPosition == nonCurrentSeat);
        gamePlayer.HasDrawnThisRound.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessDiscard_PartialCompletion_StaysInDrawPhaseUntilAllPlayersAct()
    {
        var setup = await CreateGameInDrawPhaseAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        var firstResult = await Mediator.Send(new ProcessDiscardCommand(game.Id, new List<int> { 0, 1 }));
        firstResult.IsT0.Should().BeTrue();

        var afterOneDiscard = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        afterOneDiscard.CurrentPhase.Should().Be("DrawPhase", "Irish Hold 'Em should wait for all eligible players to discard or fold");
    }

    #endregion

    #region Fold-to-Win

    [Fact]
    public async Task ProcessBettingAction_AllButOneFold_HandCompletesWithFoldWin()
    {
        var setup = await CreateDealtIrishHoldEmGameAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        // First actor bets
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

        var freshGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        freshGame.CurrentPhase.Should().Be("Showdown");

        var freshPlayers = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == game.Id)
            .ToListAsync();

        freshPlayers.Count(p => p.HasFolded).Should().Be(2);
        freshPlayers.Count(p => !p.HasFolded).Should().Be(1, "exactly one player should remain");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates an Irish Hold 'Em game setup with blinds configured and hand number set for dealing.
    /// </summary>
    private async Task<GameSetup> CreateIrishHoldEmGameSetupAsync(
        int playerCount = 4,
        int dealerPosition = 0,
        int startingChips = 1000)
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext, "IRISHHOLDEM", playerCount, startingChips);

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
    /// Creates an Irish Hold 'Em game that has already been dealt (in PreFlop with a betting round ready).
    /// </summary>
    private async Task<GameSetup> CreateDealtIrishHoldEmGameAsync(
        int playerCount = 3,
        int dealerPosition = 0,
        int startingChips = 1000)
    {
        var setup = await CreateIrishHoldEmGameSetupAsync(playerCount, dealerPosition, startingChips);
        var game = setup.Game;

        await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

        var handler = FlowHandlerFactory.GetHandler("IRISHHOLDEM");
        await handler.DealCardsAsync(
            DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        return setup;
    }

    /// <summary>
    /// Creates a game that has been dealt and is now in the DrawPhase (discard phase).
    /// PreFlop check-around deals the flop and transitions directly to DrawPhase.
    /// </summary>
    private async Task<GameSetup> CreateGameInDrawPhaseAsync(
        int playerCount = 3,
        int dealerPosition = 0)
    {
        var setup = await CreateDealtIrishHoldEmGameAsync(playerCount, dealerPosition);
        var game = setup.Game;

        // Check all players through PreFlop — this deals the flop and advances to DrawPhase
        await CheckAllPlayersThrough(game);

        var freshGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        freshGame.CurrentPhase.Should().Be("DrawPhase",
            "after PreFlop check-around, Irish Hold 'Em should be in DrawPhase");

        // Reload game entity for further use
        var reloadedGame = await DbContext.Games
            .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
            .Include(g => g.GameType)
            .FirstAsync(g => g.Id == game.Id);

        var gamePlayers = reloadedGame.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
        var players = gamePlayers.Select(gp => gp.Player).ToList();

        return new GameSetup(reloadedGame, players, gamePlayers);
    }

    /// <summary>
    /// Checks all active players through the current betting round.
    /// </summary>
    private async Task CheckAllPlayersThrough(Game game)
    {
        for (var i = 0; i < 10; i++) // Safety limit
        {
            var freshRound = await GetFreshDbContext().BettingRounds
                .FirstOrDefaultAsync(br => br.GameId == game.Id && !br.IsComplete);
            if (freshRound is null) break;

            var result = await Mediator.Send(
                new ProcessBettingActionCommand(game.Id, BettingActionType.Check));

            result.IsT0.Should().BeTrue($"check at iteration {i} should succeed");
        }
    }

    #endregion
}
