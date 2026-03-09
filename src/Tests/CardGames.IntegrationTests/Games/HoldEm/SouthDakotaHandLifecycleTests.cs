using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.StartHand;
using CardGames.Poker.Betting;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Games.HoldEm;

/// <summary>
/// Integration tests for South Dakota lifecycle.
/// Rules: 5 hole cards, Flop(2), Turn(1), no River.
/// </summary>
public class SouthDakotaHandLifecycleTests : IntegrationTestBase
{
    private const string GameTypeCode = "SOUTHDAKOTA";

    [Fact]
    public async Task StartHand_ForSouthDakota_DealsFiveHoleCardsPerPlayer()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, GameTypeCode, 3, 1000);
        setup.Game.CurrentPhase = nameof(Phases.WaitingToStart);
        setup.Game.CurrentHandNumber = 0;
        await DbContext.SaveChangesAsync();

        var startResult = await Mediator.Send(new StartHandCommand(setup.Game.Id));

        startResult.IsT0.Should().BeTrue();
        startResult.AsT0.CurrentPhase.Should().Be("PreFlop");

        var context = GetFreshDbContext();
        var game = await context.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        var holeCards = await context.GameCards
            .AsNoTracking()
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Hand
                && !gc.IsDiscarded)
            .ToListAsync();

        holeCards.Should().HaveCount(15);
        holeCards
            .Where(gc => gc.GamePlayerId.HasValue)
            .GroupBy(gc => gc.GamePlayerId!.Value)
            .Should()
            .OnlyContain(g => g.Count() == 5);
    }

    [Fact]
    public async Task Lifecycle_DealsTwoCardFlopAndNoRiver_ThenShowdown()
    {
        var setup = await CreateDealtSouthDakotaGameAsync(playerCount: 3, dealerPosition: 0);

        await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "Flop");

        var contextAfterFlop = GetFreshDbContext();
        var gameAfterFlop = await contextAfterFlop.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        var flopCards = await contextAfterFlop.GameCards
            .AsNoTracking()
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == gameAfterFlop.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .ToListAsync();

        flopCards.Should().HaveCount(2);
        flopCards.Should().OnlyContain(c => c.DealtAtPhase == "Flop");
        flopCards.Select(c => c.DealOrder).Should().Equal(1, 2);

        await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "Turn");
        await CompleteCurrentBettingRoundWithChecksAsync(setup.Game.Id);

        var finalContext = GetFreshDbContext();
        var finalGame = await finalContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
        finalGame.CurrentPhase.Should().Be("Showdown");

        var allCommunity = await finalContext.GameCards
            .AsNoTracking()
            .Where(gc => gc.GameId == setup.Game.Id
                && gc.HandNumber == finalGame.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .ToListAsync();

        allCommunity.Should().HaveCount(3);
        allCommunity.Count(c => c.DealtAtPhase == "Flop").Should().Be(2);
        allCommunity.Count(c => c.DealtAtPhase == "Turn").Should().Be(1);
        allCommunity.Should().OnlyContain(c => c.DealtAtPhase != "River");
    }

    [Fact]
    public async Task ProcessBettingAction_WhenNotInBettingPhase_ReturnsInvalidGameState()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, GameTypeCode, 3);
        setup.Game.CurrentPhase = "CollectingBlinds";
        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.Check));

        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidGameState);
    }

    private async Task<GameSetup> CreateDealtSouthDakotaGameAsync(int playerCount, int dealerPosition)
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, GameTypeCode, playerCount, 1000);

        setup.Game.SmallBlind = 5;
        setup.Game.BigBlind = 10;
        setup.Game.DealerPosition = dealerPosition;
        setup.Game.CurrentHandNumber = 1;
        setup.Game.Status = GameStatus.InProgress;

        await DbContext.SaveChangesAsync();
        await DatabaseSeeder.CreatePotAsync(DbContext, setup.Game, 0);

        var handler = FlowHandlerFactory.GetHandler(GameTypeCode);
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

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

        var amountToCall = Math.Max(0, bettingRound.CurrentBet - actor.CurrentBet);
        var action = amountToCall > 0 ? BettingActionType.Call : BettingActionType.Check;

        return await Mediator.Send(new ProcessBettingActionCommand(gameId, action, amountToCall));
    }
}
