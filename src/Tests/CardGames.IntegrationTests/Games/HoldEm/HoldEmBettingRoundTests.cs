using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using BettingRoundEntity = CardGames.Poker.Api.Data.Entities.BettingRound;

namespace CardGames.IntegrationTests.Games.HoldEm;

public class HoldEmBettingRoundTests : IntegrationTestBase
{
	[Fact]
	public async Task LimpedPot_SmallBlindCall_DoesNotDealFlopUntilBigBlindActs()
	{
		var game = await CreateDealtHoldEmGameAsync(3, dealerPosition: 0);

		var opening = await FreshRound(game.Id);
		opening.CurrentActorIndex.Should().Be(0);
		opening.CurrentBet.Should().Be(10);

		var button = await Act(game.Id, BettingActionType.Call);
		button.RoundComplete.Should().BeFalse();
		button.NextPlayerIndex.Should().Be(1);

		var smallBlind = await Act(game.Id, BettingActionType.Call);
		smallBlind.RoundComplete.Should().BeFalse();
		smallBlind.NextPlayerIndex.Should().Be(2);
		(await FreshGame(game.Id)).CurrentPhase.Should().Be("PreFlop");

		var bigBlind = await Act(game.Id, BettingActionType.Check);
		bigBlind.RoundComplete.Should().BeTrue();
		(await FreshGame(game.Id)).CurrentPhase.Should().Be("Flop");
	}

	[Fact]
	public async Task FoldThenCalls_BigBlindStillHasOption()
	{
		var game = await CreateDealtHoldEmGameAsync(4, dealerPosition: 0);

		var opening = await FreshRound(game.Id);
		opening.CurrentActorIndex.Should().Be(3);

		await Act(game.Id, BettingActionType.Fold);
		await Act(game.Id, BettingActionType.Call);
		var smallBlind = await Act(game.Id, BettingActionType.Call);

		smallBlind.RoundComplete.Should().BeFalse();
		smallBlind.NextPlayerIndex.Should().Be(2);
		(await FreshGame(game.Id)).CurrentPhase.Should().Be("PreFlop");

		var bigBlind = await Act(game.Id, BettingActionType.Check);
		bigBlind.RoundComplete.Should().BeTrue();
		(await FreshGame(game.Id)).CurrentPhase.Should().Be("Flop");
	}

	[Fact]
	public async Task RaisedPot_ClosesWhenActionReturnsToLastAggressor()
	{
		var game = await CreateDealtHoldEmGameAsync(3, dealerPosition: 0);

		var raise = await Act(game.Id, BettingActionType.Raise, 40);
		raise.RoundComplete.Should().BeFalse();

		var smallBlind = await Act(game.Id, BettingActionType.Call);
		smallBlind.RoundComplete.Should().BeFalse();

		var bigBlind = await Act(game.Id, BettingActionType.Call);
		bigBlind.RoundComplete.Should().BeTrue();
		(await FreshGame(game.Id)).CurrentPhase.Should().Be("Flop");
	}

	[Fact]
	public async Task FlopCheckAround_CompletesOnlyAfterEveryPlayerChecks()
	{
		var game = await CreateDealtHoldEmGameAsync(4, dealerPosition: 0);
		await Act(game.Id, BettingActionType.Call);
		await Act(game.Id, BettingActionType.Call);
		await Act(game.Id, BettingActionType.Call);
		var bigBlind = await Act(game.Id, BettingActionType.Check);
		bigBlind.RoundComplete.Should().BeTrue();
		(await FreshGame(game.Id)).CurrentPhase.Should().Be("Flop");

		var flopRound = await FreshRound(game.Id);
		flopRound.CurrentActorIndex.Should().Be(1);
		flopRound.CurrentBet.Should().Be(0);

		var first = await Act(game.Id, BettingActionType.Check);
		var second = await Act(game.Id, BettingActionType.Check);
		var third = await Act(game.Id, BettingActionType.Check);
		first.RoundComplete.Should().BeFalse();
		second.RoundComplete.Should().BeFalse();
		third.RoundComplete.Should().BeFalse();
		(await FreshGame(game.Id)).CurrentPhase.Should().Be("Flop");

		var fourth = await Act(game.Id, BettingActionType.Check);
		fourth.RoundComplete.Should().BeTrue();
		(await FreshGame(game.Id)).CurrentPhase.Should().Be("Turn");
	}

	private async Task<ProcessBettingActionSuccessful> Act(Guid gameId, BettingActionType action, int amount = 0)
	{
		var result = await Mediator.Send(new ProcessBettingActionCommand(gameId, action, amount));
		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : "action should succeed");
		return result.AsT0;
	}

	private async Task<Game> FreshGame(Guid gameId)
	{
		return await GetFreshDbContext().Games.FirstAsync(game => game.Id == gameId);
	}

	private async Task<BettingRoundEntity> FreshRound(Guid gameId)
	{
		return await GetFreshDbContext().BettingRounds
			.FirstAsync(round => round.GameId == gameId && !round.IsComplete);
	}

	private async Task<Game> CreateDealtHoldEmGameAsync(int playerCount, int dealerPosition)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "HOLDEM", playerCount, startingChips: 1000);
		var game = setup.Game;
		game.SmallBlind = 5;
		game.BigBlind = 10;
		game.DealerPosition = dealerPosition;
		game.CurrentHandNumber = 1;
		game.Status = GameStatus.InProgress;
		await DbContext.SaveChangesAsync();
		await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);

		var handler = FlowHandlerFactory.GetHandler("HOLDEM");
		await handler.DealCardsAsync(
			DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

		return game;
	}
}
