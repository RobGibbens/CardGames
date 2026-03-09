using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;
using CardGames.Poker.Betting;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Games.CrazyPineapple;

/// <summary>
/// Integration coverage for Crazy Pineapple:
/// - 3 hole cards dealt initially
/// - flop is dealt first, then one-card mandatory discard
/// - then standard Hold 'Em streets (Flop, Turn, River, Showdown)
/// </summary>
public class CrazyPineappleHandLifecycleTests : IntegrationTestBase
{
    private const string GameTypeCode = "CRAZYPINEAPPLE";

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
            Name = "Crazy Pineapple",
            MinPlayers = 2,
            MaxPlayers = 10,
            InitialHoleCards = 3,
            InitialBoardCards = 0,
            MaxCommunityCards = 5,
            MaxPlayerCards = 8,
            BettingStructure = BettingStructure.Blinds
        });

        await DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task DealCards_DealsThreeHoleCardsPerPlayer()
    {
        var setup = await CreateCrazyPineappleGameSetupAsync(playerCount: 4, dealerPosition: 0);
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
            .Should().AllSatisfy(g => g.Count().Should().Be(3, "Crazy Pineapple should deal 3 hole cards initially"));
    }

    [Fact]
    public async Task Lifecycle_PreFlopDiscardThenFlopTurnRiverShowdown()
    {
        var setup = await CreateDealtCrazyPineappleGameAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        await CheckAllPlayersThrough(game.Id);

        var gameAtDrawPhase = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAtDrawPhase.CurrentPhase.Should().Be("DrawPhase",
            "after pre-flop betting, Crazy Pineapple should enter draw phase");

        var beforeDiscardCommunity = await GetCommunityCardsAsync(game.Id, game.CurrentHandNumber);
        beforeDiscardCommunity.Should().HaveCount(3, "flop should be dealt before the discard round");

        await DiscardOneCardForAllActivePlayers(game.Id);

        var afterDiscardCards = await GetNonDiscardedHoleCardsAsync(game.Id);
        afterDiscardCards.GroupBy(c => c.GamePlayerId)
            .Should().AllSatisfy(g => g.Count().Should().Be(2, "after discard each player should hold 2 cards"));

        var gameAfterDiscard = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAfterDiscard.CurrentPhase.Should().Be("Flop",
            "after post-flop discard completion, Crazy Pineapple should enter Flop betting");

        var flopCommunity = await GetCommunityCardsAsync(game.Id, game.CurrentHandNumber);
        flopCommunity.Should().HaveCount(3, "flop should be dealt after the discard round");

        var activeFlopRound = await GetFreshDbContext().BettingRounds
            .Where(br => br.GameId == game.Id && br.HandNumber == game.CurrentHandNumber && !br.IsComplete)
            .OrderByDescending(br => br.RoundNumber)
            .FirstAsync();
        activeFlopRound.Street.Should().Be("Flop", "discard completion should create a Flop betting round");

        await CheckAllPlayersThrough(game.Id);
        var gameAtTurn = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAtTurn.CurrentPhase.Should().Be("Turn");

        await CheckAllPlayersThrough(game.Id);
        var gameAtRiver = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAtRiver.CurrentPhase.Should().Be("River");

        await CheckAllPlayersThrough(game.Id);
        var gameAtShowdown = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameAtShowdown.CurrentPhase.Should().Be("Showdown");
    }

    [Fact]
    public async Task ProcessDiscard_DiscardingTwoCards_ShouldFail()
    {
        var setup = await CreateDealtCrazyPineappleGameAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        await CheckAllPlayersThrough(game.Id);

        var result = await Mediator.Send(
            new ProcessDiscardCommand(game.Id, new List<int> { 0, 1 }));

        result.IsT1.Should().BeTrue("Crazy Pineapple should require exactly one discard after the flop");
        result.AsT1.Message.Should().Contain("exactly 1");
    }

    [Fact]
    public async Task ProcessDiscard_OutsideDrawPhase_ShouldFail()
    {
        var setup = await CreateDealtCrazyPineappleGameAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        var gameBeforeDiscard = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        gameBeforeDiscard.CurrentPhase.Should().Be("PreFlop", "discard should not be allowed before draw phase starts");

        var result = await Mediator.Send(
            new ProcessDiscardCommand(game.Id, new List<int> { 0 }));

        result.IsT1.Should().BeTrue("discard must be rejected when the game is not in draw phase");
        result.AsT1.Code.Should().Be(ProcessDiscardErrorCode.NotInDiscardPhase);
        result.AsT1.Message.Should().Contain("only allowed during");
    }

    private async Task<GameSetup> CreateCrazyPineappleGameSetupAsync(
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

    private async Task<GameSetup> CreateDealtCrazyPineappleGameAsync(int playerCount, int dealerPosition)
    {
        var setup = await CreateCrazyPineappleGameSetupAsync(playerCount, dealerPosition);
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
