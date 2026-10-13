using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.StartHand;
using CardGames.Poker.Api.Services;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.IntegrationTests.Games.HoldEm;

public class SidePotSettlementTests : IntegrationTestBase
{
	[Fact]
	public async Task ShortAllIn_Showdown_PaysMainAndSideAndSurvivesASecondSettlement()
	{
		var game = await CreateDealtHoldEmGameAsync([1_000, 50, 200]);
		await Act(game.Id, BettingActionType.AllIn);
		await Act(game.Id, BettingActionType.AllIn);
		await Act(game.Id, BettingActionType.AllIn);
		(await Fresh(game.Id)).CurrentPhase.Should().Be("Showdown");

		await ScriptShowdownAsync(game.Id);
		var history = (FakeHandHistoryRecorder)ServiceProvider.GetRequiredService<IHandHistoryRecorder>();
		var settlement = (FakeHandSettlementService)ServiceProvider.GetRequiredService<IHandSettlementService>();

		var showdown = await Mediator.Send(new PerformShowdownCommand(game.Id));
		showdown.IsT0.Should().BeTrue(showdown.IsT1 ? showdown.AsT1.Message : string.Empty);
		showdown.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int>
		{
			["Player 2"] = 150,
			["Player 1"] = 300
		});

		await using var seen = GetFreshDbContext();
		var players = await seen.GamePlayers.Include(player => player.Player)
			.Where(player => player.GameId == game.Id)
			.ToListAsync();
		players.Single(player => player.Player.Name == "Player 1").ChipStack.Should().Be(1_100);
		players.Single(player => player.Player.Name == "Player 2").ChipStack.Should().Be(150);
		players.Single(player => player.Player.Name == "Player 3").ChipStack.Should().Be(0);
		players.Sum(player => player.ChipStack).Should().Be(1_250);

		var pots = await seen.Pots.Include(pot => pot.Contributions).ThenInclude(contribution => contribution.GamePlayer)
			.ThenInclude(gamePlayer => gamePlayer.Player)
			.Where(pot => pot.GameId == game.Id && pot.HandNumber == 1)
			.OrderBy(pot => pot.PotOrder)
			.ToListAsync();
		pots.Select(pot => pot.Amount).Should().Equal(150, 300);
		pots.Should().OnlyContain(pot => pot.IsAwarded);
		Eligible(pots[0]).Should().BeEquivalentTo("Player 1", "Player 2", "Player 3");
		Eligible(pots[1]).Should().BeEquivalentTo("Player 1", "Player 3");
		pots[0].WinnerPayouts.Should().Contain("Player 2").And.Contain("150");
		pots[1].WinnerPayouts.Should().Contain("Player 1").And.Contain("300");

		history.RecordedHands.Should().ContainSingle();
		history.RecordedHands[0].TotalPot.Should().Be(450);
		history.RecordedHands[0].Winners.Should().Contain(winner => winner.PlayerName == "Player 2" && winner.AmountWon == 150);
		history.RecordedHands[0].Winners.Should().Contain(winner => winner.PlayerName == "Player 1" && winner.AmountWon == 300);
		history.RecordedHands[0].PlayerResults.Sum(result => result.NetChipDelta).Should().Be(0);
		settlement.SettledHands.Should().ContainSingle();
		settlement.SettledHands[0].Payouts.Values.Sum().Should().Be(450);

		var again = await Mediator.Send(new PerformShowdownCommand(game.Id));
		again.IsT0.Should().BeTrue();
		again.AsT0.Payouts.Should().BeEquivalentTo(showdown.AsT0.Payouts);
		history.RecordedHands.Should().ContainSingle();
		await using var reloaded = GetFreshDbContext();
		var reloadedPlayers = await reloaded.GamePlayers.Where(player => player.GameId == game.Id).ToListAsync();
		reloadedPlayers.Sum(player => player.ChipStack).Should().Be(1_250);
		reloadedPlayers.Single(player => player.SeatPosition == 0).ChipStack.Should().Be(1_100);

		var next = await Mediator.Send(new StartHandCommand(game.Id));
		next.IsT0.Should().BeTrue(next.IsT1 ? next.AsT1.Message : string.Empty);
		next.AsT0.HandNumber.Should().Be(2);
		await using var handTwo = GetFreshDbContext();
		var live = await handTwo.GamePlayers.Where(player => player.GameId == game.Id && !player.IsSittingOut).ToListAsync();
		live.Should().OnlyContain(player => player.TotalContributedThisHand <= 10);
		live.Should().OnlyContain(player => player.CurrentBet <= 10);
		var currentPots = await handTwo.Pots.Where(pot => pot.GameId == game.Id && pot.HandNumber == 2).ToListAsync();
		currentPots.Should().ContainSingle();
		currentPots[0].PotType.Should().Be(PotType.Main);
		currentPots[0].IsAwarded.Should().BeFalse();
		currentPots[0].Amount.Should().Be(live.Sum(player => player.TotalContributedThisHand));
		var settled = await handTwo.Pots.Where(pot => pot.GameId == game.Id && pot.HandNumber == 1)
			.OrderBy(pot => pot.PotOrder).ToListAsync();
		settled.Select(pot => pot.Amount).Should().Equal(150, 300);
		(live.Sum(player => player.ChipStack) + currentPots[0].Amount +
		 (await handTwo.GamePlayers.Where(player => player.GameId == game.Id && player.IsSittingOut)
			 .SumAsync(player => player.ChipStack))).Should().Be(1_250);
	}

	[Fact]
	public async Task FoldAfterCalling_LeavesTheChipsAndRemovesEligibility()
	{
		var game = await CreateDealtHoldEmGameAsync([1_000, 1_000, 1_000]);
		await Act(game.Id, BettingActionType.Raise, 40);
		await Act(game.Id, BettingActionType.Fold);
		await Act(game.Id, BettingActionType.Fold);

		var showdown = await Mediator.Send(new PerformShowdownCommand(game.Id));
		showdown.IsT0.Should().BeTrue(showdown.IsT1 ? showdown.AsT1.Message : string.Empty);
		showdown.AsT0.WonByFold.Should().BeTrue();
		showdown.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Player 1"] = 55 });

		await using var seen = GetFreshDbContext();
		var players = await seen.GamePlayers.Include(player => player.Player)
			.Where(player => player.GameId == game.Id).ToListAsync();
		players.Single(player => player.SeatPosition == 0).ChipStack.Should().Be(1_015);
		players.Single(player => player.SeatPosition == 1).ChipStack.Should().Be(995);
		players.Single(player => player.SeatPosition == 1).TotalContributedThisHand.Should().Be(5);
		players.Single(player => player.SeatPosition == 2).ChipStack.Should().Be(990);
		players.Sum(player => player.ChipStack).Should().Be(3_000);
	}

	[Fact]
	public async Task Showdown_BeforeTheHandIsReady_ReturnsInvalidGameState()
	{
		var game = await CreateDealtHoldEmGameAsync([1_000, 1_000, 1_000]);

		var showdown = await Mediator.Send(new PerformShowdownCommand(game.Id));

		showdown.IsT1.Should().BeTrue();
		showdown.AsT1.Code.Should().Be(PerformShowdownErrorCode.InvalidGameState);
	}

	[Fact]
	public async Task Showdown_WhenTheStoredPotDisagreesWithContributions_ReturnsInvalidGameState()
	{
		var game = await CreateDealtHoldEmGameAsync([1_000, 50, 200]);
		await Act(game.Id, BettingActionType.AllIn);
		await Act(game.Id, BettingActionType.AllIn);
		await Act(game.Id, BettingActionType.AllIn);
		await ScriptShowdownAsync(game.Id);

		var tracked = await DbContext.Games.Include(item => item.Pots).SingleAsync(item => item.Id == game.Id);
		tracked.Pots.Single(pot => pot.HandNumber == tracked.CurrentHandNumber).Amount += 7;
		await DbContext.SaveChangesAsync();

		var showdown = await Mediator.Send(new PerformShowdownCommand(game.Id));

		showdown.IsT1.Should().BeTrue();
		showdown.AsT1.Code.Should().Be(PerformShowdownErrorCode.InvalidGameState);
		showdown.AsT1.Message.Should().Contain("does not equal contributed chips");
	}

	private static IEnumerable<string> Eligible(Pot pot) =>
		pot.Contributions.Where(contribution => contribution.IsEligibleToWin)
			.Select(contribution => contribution.GamePlayer.Player.Name);

	private async Task ScriptShowdownAsync(Guid gameId)
	{
		var cards = await DbContext.GameCards.Include(card => card.GamePlayer)
			.Where(card => card.GameId == gameId && !card.IsDiscarded)
			.ToListAsync();
		var holes = cards.Where(card => card.Location == CardLocation.Hand)
			.GroupBy(card => card.GamePlayer!.SeatPosition)
			.ToDictionary(group => group.Key, group => group.OrderBy(card => card.DealOrder).ToList());
		var board = cards.Where(card => card.Location == CardLocation.Community)
			.OrderBy(card => card.DealOrder)
			.ToList();
		board.Should().HaveCount(5);

		Assign(holes[0], CardSuit.Hearts, CardSymbol.King, CardSuit.Diamonds, CardSymbol.King);
		Assign(holes[1], CardSuit.Hearts, CardSymbol.Ace, CardSuit.Diamonds, CardSymbol.Ace);
		Assign(holes[2], CardSuit.Hearts, CardSymbol.Queen, CardSuit.Diamonds, CardSymbol.Queen);
		var boardCards = new[]
		{
			(CardSuit.Clubs, CardSymbol.Deuce),
			(CardSuit.Diamonds, CardSymbol.Three),
			(CardSuit.Hearts, CardSymbol.Four),
			(CardSuit.Spades, CardSymbol.Five),
			(CardSuit.Clubs, CardSymbol.Nine)
		};
		for (var index = 0; index < board.Count; index++)
		{
			board[index].Suit = boardCards[index].Item1;
			board[index].Symbol = boardCards[index].Item2;
		}

		await DbContext.SaveChangesAsync();
	}

	private static void Assign(
		IReadOnlyList<GameCard> cards,
		CardSuit firstSuit,
		CardSymbol firstSymbol,
		CardSuit secondSuit,
		CardSymbol secondSymbol)
	{
		cards[0].Suit = firstSuit;
		cards[0].Symbol = firstSymbol;
		cards[1].Suit = secondSuit;
		cards[1].Symbol = secondSymbol;
	}

	private async Task Act(Guid gameId, BettingActionType action, int amount = 0)
	{
		var result = await Mediator.Send(new ProcessBettingActionCommand(gameId, action, amount)
		{
			BoundSeat = await TryCurrentBettingSeatAsync(gameId)
		});
		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : "action should succeed");
	}

	private async Task<Game> Fresh(Guid gameId) =>
		await GetFreshDbContext().Games.SingleAsync(game => game.Id == gameId);

	private async Task<Game> CreateDealtHoldEmGameAsync(int[] stacks)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "HOLDEM", stacks.Length, startingChips: 1000);
		var game = setup.Game;
		game.SmallBlind = 5;
		game.BigBlind = 10;
		game.DealerPosition = 0;
		game.CurrentHandNumber = 1;
		game.Status = GameStatus.InProgress;
		foreach (var player in setup.GamePlayers)
		{
			player.ChipStack = stacks[player.SeatPosition];
			player.StartingChips = stacks[player.SeatPosition];
		}

		await DbContext.SaveChangesAsync();
		await DatabaseSeeder.CreatePotAsync(DbContext, game, 0);
		var handler = FlowHandlerFactory.GetHandler("HOLDEM");
		await handler.DealCardsAsync(
			DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);
		return game;
	}
}
