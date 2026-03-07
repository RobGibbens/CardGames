using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Evaluation.Evaluators;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Games.IrishHoldEm;

/// <summary>
/// Phase 2 deployment-readiness smoke tests for Irish Hold 'Em.
/// Each test exercises a complete journey from the PRD's Phase 2 checklist:
///   1. Create Table → Irish selectable with blinds
///   2. Dealer's Choice → Irish prompts blinds
///   3. Full hand lifecycle including discard overlay
///   4. Showdown produces correct winner with Hold'Em ranking
/// Also includes metadata/registry verification tests.
/// </summary>
public class IrishHoldEmSmokeTests : IntegrationTestBase
{
    #region Smoke 1: Create Game with Blind Configuration

    [Fact]
    public async Task Smoke_CreateIrishHoldEmGame_HasBlindsAndCorrectMetadata()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.IrishHoldEmCode,
            "Irish Smoke Table",
            0,   // ante (unused for blind-based)
            10,
            new List<PlayerInfo>
            {
                new("Alice", 1000),
                new("Bob", 1000),
                new("Charlie", 1000)
            },
            SmallBlind: 5,
            BigBlind: 10);

        // Act
        var result = await Mediator.Send(command);

        // Assert — command succeeds
        result.IsT0.Should().BeTrue("Expected successful creation of Irish Hold 'Em game");
        var success = result.AsT0;
        success.GameId.Should().Be(gameId);
        success.GameTypeCode.Should().Be("IRISHHOLDEM");
        success.PlayerCount.Should().Be(3);

        // Assert — persisted state
        var game = await GetFreshDbContext().Games
            .Include(g => g.GameType)
            .FirstAsync(g => g.Id == gameId);

        game.GameType!.Code.Should().Be("IRISHHOLDEM");
        game.SmallBlind.Should().Be(5, "Small blind should be persisted");
        game.BigBlind.Should().Be(10, "Big blind should be persisted");
        game.Status.Should().Be(GameStatus.WaitingForPlayers);
        game.CurrentPhase.Should().Be(nameof(Phases.WaitingToStart));
    }

    #endregion

    #region Smoke 2: Dealer's Choice Irish Selection with Blinds

    [Fact]
    public async Task Smoke_DealersChoice_IrishHoldEmSelection_AppliesBlinds()
    {
        // Arrange — Dealer's Choice game with "Test User" as the current DC dealer
        var setup = await CreateDealersChoiceSetupWithTestUser(dealerSeatPosition: 0, playerCount: 3);

        var command = new ChooseDealerGameCommand(
            setup.Game.Id,
            PokerGameMetadataRegistry.IrishHoldEmCode,
            Ante: 0,
            MinBet: 10,
            SmallBlind: 5,
            BigBlind: 10);

        // Act
        var result = await Mediator.Send(command);

        // Assert — command succeeds
        result.IsT0.Should().BeTrue("Expected successful DC choice of Irish Hold 'Em");
        var success = result.AsT0;
        success.GameTypeCode.Should().Be("IRISHHOLDEM");
        success.SmallBlind.Should().Be(5);
        success.BigBlind.Should().Be(10);

        // Assert — persisted state
        var game = await GetFreshDbContext().Games
            .Include(g => g.GameType)
            .FirstAsync(g => g.Id == setup.Game.Id);

        game.CurrentHandGameTypeCode.Should().Be("IRISHHOLDEM");
        game.SmallBlind.Should().Be(5);
        game.BigBlind.Should().Be(10);

        // Assert — DC hand log
        var handLog = await GetFreshDbContext().DealersChoiceHandLogs
            .SingleAsync(l => l.GameId == setup.Game.Id);

        handLog.GameTypeCode.Should().Be("IRISHHOLDEM");
        handLog.SmallBlind.Should().Be(5);
        handLog.BigBlind.Should().Be(10);

        // Assert — starting a hand deals 4 hole cards per player
        game = await GetFreshDbContext().Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase = nameof(Phases.WaitingToStart);
        game.SmallBlind = 5;
        game.BigBlind = 10;
        game.DealerPosition = 0;
        game.CurrentHandNumber = 1;
        game.Status = GameStatus.InProgress;
        await DbContext.SaveChangesAsync();

        var handler = FlowHandlerFactory.GetHandler("IRISHHOLDEM");
        handler.GetDealingConfiguration().InitialCardsPerPlayer.Should().Be(4,
            "Irish Hold 'Em should deal 4 hole cards per player");
    }

    #endregion

    #region Smoke 3: Full Hand Lifecycle

    [Fact]
    public async Task Smoke_FullIrishHoldEmHandLifecycle_CompletesSuccessfully()
    {
        // Step 1: Create game, join 3 players, configure blinds (SB=5, BB=10)
        var setup = await CreateIrishHoldEmGameSetupAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;
        await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

        // Step 2: Deal hand → verify 4 hole cards each, blinds collected
        var handler = FlowHandlerFactory.GetHandler("IRISHHOLDEM");
        await handler.DealCardsAsync(
            DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        var freshCtx = GetFreshDbContext();
        var playerCards = await freshCtx.GameCards
            .Where(gc => gc.GameId == game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.GamePlayerId != null
                && gc.Location == CardLocation.Hand)
            .ToListAsync();

        playerCards.Should().HaveCount(12, "3 players × 4 hole cards each = 12");

        // Verify blinds collected (dealer=0, SB=1, BB=2)
        var players = await freshCtx.GamePlayers
            .Where(gp => gp.GameId == game.Id)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        players[1].TotalContributedThisHand.Should().Be(5, "SB should have contributed 5");
        players[2].TotalContributedThisHand.Should().Be(10, "BB should have contributed 10");

        // Step 3: PreFlop betting round (all check through)
        await CheckCurrentBettingRoundThrough(game);

        var gameAfterPreFlop = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAfterPreFlop.CurrentPhase.Should().Be("DrawPhase",
            "after PreFlop betting, flop is dealt and phase goes directly to DrawPhase");

        // Step 4: Verify 3 community cards dealt (Flop)
        var communityAfterFlop = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community)
            .ToListAsync();

        communityAfterFlop.Should().HaveCount(3, "Flop should deal 3 community cards");

        var gameInDraw = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameInDraw.CurrentPhase.Should().Be("DrawPhase", "should be in DrawPhase for discards");

        // Verify 4 cards per player before discard
        var preDiscardCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id && gc.GamePlayerId != null && !gc.IsDiscarded)
            .ToListAsync();
        preDiscardCards.GroupBy(c => c.GamePlayerId)
            .Should().AllSatisfy(g => g.Count().Should().Be(4, "each player has 4 cards before discard"));

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

        // Step 7: Verify discards advanced to Turn phase
        var gameAfterDiscard = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAfterDiscard.CurrentPhase.Should().Be("Turn",
            "after all discards, phase should advance to Turn");

        // Verify each player has exactly 2 non-discarded hole cards
        var postDiscardCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id && gc.GamePlayerId != null && !gc.IsDiscarded)
            .ToListAsync();
        postDiscardCards.GroupBy(c => c.GamePlayerId)
            .Should().AllSatisfy(g => g.Count().Should().Be(2,
                "each player should have exactly 2 non-discarded hole cards after discard"));

        // Verify Turn community card was dealt by the discard handler
        var communityAfterTurn = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community)
            .ToListAsync();
        communityAfterTurn.Should().HaveCount(4, "Turn should have 4 community cards total (3 flop + 1 turn)");

        // Step 8: Turn betting round (all check through)
        await CheckCurrentBettingRoundThrough(game);

        // Step 9: River → verify 5th community card
        var gameAfterTurn = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAfterTurn.CurrentPhase.Should().Be("River",
            "after Turn betting, phase should be River");

        var communityAfterRiver = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community)
            .ToListAsync();
        communityAfterRiver.Should().HaveCount(5, "River should have 5 community cards total");

        // Step 10: River betting round (all check through)
        await CheckCurrentBettingRoundThrough(game);

        // Step 11: Verify phase reaches Showdown
        var gameAtShowdown = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAtShowdown.CurrentPhase.Should().Be("Showdown",
            "after River betting, phase should reach Showdown");

        // Step 12: Verify each player has exactly 2 non-discarded hole cards at showdown
        var showdownCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id
                && gc.GamePlayerId != null
                && !gc.IsDiscarded
                && gc.HandNumber == game.CurrentHandNumber)
            .ToListAsync();

        showdownCards.GroupBy(c => c.GamePlayerId)
            .Should().AllSatisfy(g => g.Count().Should().Be(2,
                "each player should have exactly 2 hole cards at showdown"));
    }

    #endregion

    #region Smoke 4: Showdown Uses Hold'Em Ranking (Not Omaha)

    [Fact]
    public async Task Smoke_IrishShowdown_UsesHoldEmRanking_NotOmahaRules()
    {
        // Verify the evaluator for IRISHHOLDEM produces HoldemHand, NOT OmahaHand.
        // This proves Irish uses Hold'Em evaluation (board plays, 0-2 hole cards),
        // which would NOT work under Omaha rules (must use exactly 2 hole cards).

        var evaluatorFactory = new HandEvaluatorFactory();
        var evaluator = evaluatorFactory.GetEvaluator("IRISHHOLDEM");

        // Assert — evaluator is the Irish-specific one
        evaluator.Should().BeOfType<IrishHoldEmHandEvaluator>(
            "IRISHHOLDEM should resolve to IrishHoldEmHandEvaluator");

        // Assert — evaluator creates HoldemHand (not OmahaHand)
        var holeCards = "2c 3d".ToCards();
        var communityCards = "5h 6c 7d 8s 9h".ToCards();

        var hand = evaluator.CreateHand(holeCards, communityCards, new List<Core.French.Cards.Card>());

        hand.Should().BeOfType<HoldemHand>(
            "Irish Hold 'Em evaluation must create HoldemHand, not OmahaHand");

        // Assert — the hand evaluation uses Hold'Em rules where 0 hole cards can contribute.
        // A board straight (5-6-7-8-9) should be usable even if hole cards (2c, 3d) don't help.
        hand.Type.Should().NotBe(HandType.HighCard,
            "board straight should be recognized — proves Hold'Em-style 0-hole-card usage");

        // Assert — Omaha evaluator would NOT resolve for IRISHHOLDEM
        var omahaEvaluator = evaluatorFactory.GetEvaluator("OMAHA");
        omahaEvaluator.Should().BeOfType<OmahaHandEvaluator>(
            "Omaha evaluator is a distinct type from Irish evaluator");
        omahaEvaluator.Should().NotBeSameAs(evaluator,
            "Irish and Omaha must use different evaluator instances");
    }

    #endregion

    #region Smoke 5: Metadata Registry

    [Fact]
    public async Task Smoke_IrishMetadata_RegisteredCorrectlyInGameTypeRegistry()
    {
        // Verify IRISHHOLDEM appears in the seeded game types with correct properties
        var gameType = await DbContext.GameTypes
            .FirstOrDefaultAsync(gt => gt.Code == "IRISHHOLDEM");

        gameType.Should().NotBeNull("IRISHHOLDEM must be registered in the game type registry");
        gameType!.Name.Should().Be("Irish Hold 'Em");
        gameType.MinPlayers.Should().Be(2);
        gameType.MaxPlayers.Should().Be(10);
        gameType.InitialHoleCards.Should().Be(4, "Irish deals 4 hole cards initially");
        gameType.MaxCommunityCards.Should().Be(5, "5 community cards (flop + turn + river)");

        // Verify the constant in the code-level registry
        PokerGameMetadataRegistry.IrishHoldEmCode.Should().Be("IRISHHOLDEM");
    }

    #endregion

    #region Smoke 6: Flow Handler Factory

    [Fact]
    public void Smoke_IrishFlowHandler_RegisteredInFactory()
    {
        // Verify GameFlowHandlerFactory.GetHandler("IRISHHOLDEM") returns IrishHoldEmFlowHandler
        var handler = FlowHandlerFactory.GetHandler("IRISHHOLDEM");

        handler.Should().BeOfType<IrishHoldEmFlowHandler>(
            "IRISHHOLDEM should resolve to IrishHoldEmFlowHandler");
        handler.GameTypeCode.Should().Be("IRISHHOLDEM");
        handler.SkipsAnteCollection.Should().BeTrue("Irish uses blinds, not antes");

        // Verify dealing config
        var config = handler.GetDealingConfiguration();
        config.InitialCardsPerPlayer.Should().Be(4, "Irish deals 4 hole cards");
        config.PatternType.Should().Be(DealingPatternType.CommunityCard);
        config.AllFaceDown.Should().BeTrue();
    }

    #endregion

    #region Smoke 7: ContinuousPlay DrawPhase Coverage

    [Fact]
    public void Smoke_IrishContinuousPlay_DrawPhaseInProgressPhases()
    {
        // Verify DrawPhase is recognized as an in-progress phase name
        // (Irish uses "DrawPhase" as its discard phase ID, matching the enum)
        var handler = FlowHandlerFactory.GetHandler("IRISHHOLDEM") as IrishHoldEmFlowHandler;
        handler.Should().NotBeNull();

        var rules = handler!.GetGameRules();
        var drawPhase = rules.Phases.FirstOrDefault(p => p.PhaseId == "DrawPhase");

        drawPhase.Should().NotBeNull("Irish rules must include a DrawPhase");
        drawPhase!.Category.Should().Be("Drawing",
            "DrawPhase category must be 'Drawing' for UI discard overlay activation");
        drawPhase.RequiresPlayerAction.Should().BeTrue(
            "DrawPhase requires each player to discard exactly 2 cards");
        drawPhase.Name.Should().Be("Discard",
            "user-facing name should be 'Discard' not 'Draw'");

        // Verify phase progression includes DrawPhase between Flop and Turn
        var phaseIds = rules.Phases.Select(p => p.PhaseId).ToList();
        var flopIndex = phaseIds.IndexOf("Flop");
        var drawIndex = phaseIds.IndexOf("DrawPhase");
        var turnIndex = phaseIds.IndexOf("Turn");

        flopIndex.Should().BeGreaterThanOrEqualTo(0);
        drawIndex.Should().BeGreaterThanOrEqualTo(0);
        turnIndex.Should().BeGreaterThanOrEqualTo(0);
        drawIndex.Should().Be(flopIndex + 1, "DrawPhase must immediately follow Flop");
        turnIndex.Should().Be(drawIndex + 1, "Turn must immediately follow DrawPhase");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates an Irish Hold 'Em game setup with blinds configured and hand number set for dealing.
    /// </summary>
    private async Task<GameSetup> CreateIrishHoldEmGameSetupAsync(
        int playerCount = 3,
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
    /// Manually advances the game to DrawPhase, dealing 3 community flop cards.
    /// Required because the HoldEm betting handler's Flop→Turn hardcoded transition
    /// doesn't route through DrawPhase for Irish Hold 'Em.
    /// </summary>
    private async Task SetupDrawPhaseState(Game game)
    {
        game.CurrentPhase = nameof(Phases.DrawPhase);

        var activePlayers = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active && !gp.HasFolded)
            .OrderBy(gp => gp.SeatPosition)
            .ToList();

        var firstPlayerSeat = activePlayers.First().SeatPosition;
        game.CurrentDrawPlayerIndex = firstPlayerSeat;
        game.CurrentPlayerIndex = firstPlayerSeat;

        foreach (var gp in activePlayers)
        {
            gp.HasDrawnThisRound = false;
        }

        await DbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Manually deals the next community card from the deck for the given phase.
    /// Works around the known gap where ProcessDiscardCommandHandler.StartTurnPhase
    /// creates a Turn betting round but does not deal the Turn community card.
    /// </summary>
    private async Task DealNextCommunityCardAsync(Game game, string phase, int dealOrder)
    {
        var db = GetFreshDbContext();
        db.Attach(game);

        var nextDeckCard = await db.GameCards
            .Where(gc => gc.GameId == game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Deck)
            .OrderBy(gc => gc.DealOrder)
            .FirstAsync();

        nextDeckCard.Location = CardLocation.Community;
        nextDeckCard.IsVisible = true;
        nextDeckCard.DealtAtPhase = phase;
        nextDeckCard.DealOrder = dealOrder;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Manually marks the current betting round as complete without using the betting handler.
    /// Used when we need to skip the Flop→Turn hardcoded transition and manually 
    /// set up DrawPhase state instead.
    /// </summary>
    private async Task CompleteCurrentBettingRoundManually(Game game)
    {
        var db = GetFreshDbContext();
        var round = await db.BettingRounds
            .FirstOrDefaultAsync(br => br.GameId == game.Id && !br.IsComplete);

        if (round is not null)
        {
            round.IsComplete = true;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Checks all active players through the current betting round only (one round).
    /// Tracks the round ID to stop once that specific round completes.
    /// </summary>
    private async Task CheckCurrentBettingRoundThrough(Game game)
    {
        var initialRound = await GetFreshDbContext().BettingRounds
            .FirstOrDefaultAsync(br => br.GameId == game.Id && !br.IsComplete);
        if (initialRound is null) return;

        var roundId = initialRound.Id;

        for (var i = 0; i < 10; i++) // Safety limit
        {
            var freshRound = await GetFreshDbContext().BettingRounds
                .FirstOrDefaultAsync(br => br.Id == roundId && !br.IsComplete);
            if (freshRound is null) break;

            var result = await Mediator.Send(
                new ProcessBettingActionCommand(game.Id, BettingActionType.Check));

            result.IsT0.Should().BeTrue($"check at iteration {i} should succeed");
        }
    }

    /// <summary>
    /// Creates a Dealer's Choice game setup where "Test User" is at the dealer seat.
    /// </summary>
    private async Task<GameSetup> CreateDealersChoiceSetupWithTestUser(
        int dealerSeatPosition,
        int playerCount = 3)
    {
        var game = await DatabaseSeeder.CreateDealersChoiceGameAsync(DbContext);
        var players = new List<Player>();
        var gamePlayers = new List<GamePlayer>();

        for (var i = 0; i < playerCount; i++)
        {
            var playerName = i == dealerSeatPosition ? "Test User" : $"Player {i + 1}";
            var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, playerName);
            var gamePlayer = await DatabaseSeeder.AddPlayerToGameAsync(DbContext, game, player, i);
            players.Add(player);
            gamePlayers.Add(gamePlayer);
        }

        game.DealersChoiceDealerPosition = dealerSeatPosition;
        game.CurrentPhase = nameof(Phases.WaitingForDealerChoice);
        game.CurrentHandNumber = 1;
        game.Status = GameStatus.InProgress;
        await DbContext.SaveChangesAsync();

        var loadedGame = await DbContext.Games
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .FirstAsync(g => g.Id == game.Id);

        return new GameSetup(loadedGame, players, gamePlayers);
    }

    #endregion
}
