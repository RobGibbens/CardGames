using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Games.IrishHoldEm;

/// <summary>
/// TDD coverage for Phil's Mom (Irish Hold 'Em variant):
/// - 4 hole cards dealt initially
/// - one-card discard before flop
/// - one-card discard after flop
/// - then continue with Irish-style Hold 'Em streets (Turn/River/Showdown)
/// </summary>
public class PhilsMomHandLifecycleTests : IntegrationTestBase
{
    private const string GameTypeCode = "PHILSMOM";
    private ITableStateBuilder TableStateBuilder => Scope.ServiceProvider.GetRequiredService<ITableStateBuilder>();

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
            Name = "Phil's Mom",
            MinPlayers = 2,
            MaxPlayers = 10,
            InitialHoleCards = 4,
            InitialBoardCards = 0,
            MaxCommunityCards = 5,
            MaxPlayerCards = 9,
            BettingStructure = BettingStructure.Ante
        });

        await DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task DealCards_DealsFourHoleCardsPerPlayer()
    {
        var setup = await CreatePhilsMomGameSetupAsync(playerCount: 4, dealerPosition: 0);
        var game = setup.Game;

        await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

        var handler = FlowHandlerFactory.GetHandler(GameTypeCode);
        await handler.DealCardsAsync(
            DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        var holeCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.GamePlayerId != null
                && gc.Location == CardLocation.Hand
                && !gc.IsDiscarded)
            .ToListAsync();

        holeCards.GroupBy(c => c.GamePlayerId)
            .Should().AllSatisfy(g => g.Count().Should().Be(4, "Phil's Mom should deal 4 hole cards initially"));
    }

    [Fact]
    public async Task Lifecycle_OneDiscardBeforeFlop_OneDiscardAfterFlop_ThenTurnRiverShowdown()
    {
        var setup = await CreateDealtPhilsMomGameAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        // Complete PreFlop betting first.
        await CheckAllPlayersThrough(game.Id);

        var beforeFlopCommunity = await GetCommunityCardsAsync(game.Id, game.CurrentHandNumber);
        beforeFlopCommunity.Should().HaveCount(0, "first discard should happen before flop is dealt");

        // First discard window: one card each, from 4 -> 3.
        await DiscardOneCardForAllActivePlayers(game.Id);

        var afterFirstDiscard = await GetNonDiscardedHoleCardsAsync(game.Id);
        afterFirstDiscard.GroupBy(c => c.GamePlayerId)
            .Should().AllSatisfy(g => g.Count().Should().Be(3, "after first discard each player should hold 3 cards"));

        var flopCommunity = await GetCommunityCardsAsync(game.Id, game.CurrentHandNumber);
        flopCommunity.Should().HaveCount(3, "flop should be dealt after the first discard round");

        // Complete Flop betting.
        await CheckAllPlayersThrough(game.Id);

        // Second discard window: one card each, from 3 -> 2.
        await DiscardOneCardForAllActivePlayers(game.Id);

        var afterSecondDiscard = await GetNonDiscardedHoleCardsAsync(game.Id);
        afterSecondDiscard.GroupBy(c => c.GamePlayerId)
            .Should().AllSatisfy(g => g.Count().Should().Be(2, "after second discard each player should hold 2 cards"));

        var gameAfterSecondDiscard = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAfterSecondDiscard.CurrentPhase.Should().Be("Turn",
            "after second discard, street progression should align with Irish Hold 'Em and move to Turn");

        var turnCommunity = await GetCommunityCardsAsync(game.Id, game.CurrentHandNumber);
        turnCommunity.Should().HaveCount(4, "turn should be present after the second discard round");

        // Turn betting -> River
        await CheckAllPlayersThrough(game.Id);
        var gameAtRiver = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAtRiver.CurrentPhase.Should().Be("River");

        // River betting -> Showdown
        await CheckAllPlayersThrough(game.Id);
        var gameAtShowdown = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAtShowdown.CurrentPhase.Should().Be("Showdown");
    }

    [Fact]
    public async Task ProcessDiscard_DiscardingTwoCardsInOneAction_ShouldFail()
    {
        var setup = await CreateDealtPhilsMomGameAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        await CheckAllPlayersThrough(game.Id);

        var result = await Mediator.Send(
            new ProcessDiscardCommand(game.Id, new List<int> { 0, 1 }));

        result.IsT1.Should().BeTrue("Phil's Mom should require a single-card discard in each discard round");
    }

    [Fact]
    public async Task BuildPrivateState_AfterFlopBeforeSecondDiscard_IncludesCommunityCardsInEvaluation()
    {
        // Arrange
        var setup = await CreateDealtPhilsMomGameAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;
        var hero = setup.GamePlayers[0];
        var heroEmail = setup.Players[0].Email!;

        await CheckAllPlayersThrough(game.Id);
        await DiscardOneCardForAllActivePlayers(game.Id);

        var gameAfterFirstDiscard = await GetFreshDbContext().Games
            .AsNoTracking()
            .FirstAsync(g => g.Id == game.Id);

        var heroHoleCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id
                && gc.GamePlayerId == hero.Id
                && gc.HandNumber == gameAfterFirstDiscard.CurrentHandNumber
                && gc.Location == CardLocation.Hand
                && !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .Select(gc => new CardGames.Core.French.Cards.Card(
                (CardGames.Core.French.Cards.Suit)gc.Suit,
                (CardGames.Core.French.Cards.Symbol)gc.Symbol))
            .ToListAsync();

        var flopCommunityCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id
                && gc.HandNumber == gameAfterFirstDiscard.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && gc.IsVisible
                && !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .Select(gc => new CardGames.Core.French.Cards.Card(
                (CardGames.Core.French.Cards.Suit)gc.Suit,
                (CardGames.Core.French.Cards.Symbol)gc.Symbol))
            .ToListAsync();

        heroHoleCards.Should().HaveCount(3, "after the first discard Phil's Mom keeps 3 hole cards");
        flopCommunityCards.Should().HaveCount(3, "flop should be visible before the second discard");

        var expectedDescription = CardGames.Poker.Evaluation.HandDescriptionFormatter.GetHandDescription(
            new CardGames.Poker.Hands.CommunityCardHands.HoldemHand(heroHoleCards, flopCommunityCards));

        // Act
        var privateState = await TableStateBuilder.BuildPrivateStateAsync(game.Id, heroEmail);

        // Assert
        privateState.Should().NotBeNull();
        privateState!.HandEvaluationDescription.Should().Be(expectedDescription,
            "flop-stage evaluation must consider community cards before the second discard");
    }

    private async Task<GameSetup> CreatePhilsMomGameSetupAsync(
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

    private async Task<GameSetup> CreateDealtPhilsMomGameAsync(int playerCount, int dealerPosition)
    {
        var setup = await CreatePhilsMomGameSetupAsync(playerCount, dealerPosition);
        var game = setup.Game;

        await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

        var handler = FlowHandlerFactory.GetHandler(GameTypeCode);
        await handler.DealCardsAsync(
            DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        return setup;
    }

    private async Task CheckAllPlayersThrough(Guid gameId)
    {
        var targetRound = await GetFreshDbContext().BettingRounds
            .FirstOrDefaultAsync(br => br.GameId == gameId && !br.IsComplete);
        if (targetRound is null)
        {
            return;
        }

        for (var i = 0; i < 12; i++)
        {
            var currentTargetRound = await GetFreshDbContext().BettingRounds
                .FirstOrDefaultAsync(br => br.Id == targetRound.Id);
            if (currentTargetRound is null || currentTargetRound.IsComplete)
            {
                break;
            }

            var result = await Mediator.Send(new ProcessBettingActionCommand(gameId, BettingActionType.Check));
            result.IsT0.Should().BeTrue($"check at iteration {i} should succeed");
        }
    }

    private async Task DiscardOneCardForAllActivePlayers(Guid gameId)
    {
        var activePlayers = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == gameId && !gp.HasFolded)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        for (var i = 0; i < activePlayers.Count; i++)
        {
            var discardResult = await Mediator.Send(
                new ProcessDiscardCommand(gameId, new List<int> { 0 }));

            discardResult.IsT0.Should().BeTrue($"single-card discard for player iteration {i} should succeed");
        }
    }

    private async Task<List<GameCard>> GetNonDiscardedHoleCardsAsync(Guid gameId)
    {
        return await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == gameId
                && gc.GamePlayerId != null
                && gc.Location == CardLocation.Hand
                && !gc.IsDiscarded)
            .ToListAsync();
    }

    private async Task<List<GameCard>> GetCommunityCardsAsync(Guid gameId, int handNumber)
    {
        return await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == gameId
                && gc.HandNumber == handNumber
                && gc.Location == CardLocation.Community)
            .ToListAsync();
    }
}