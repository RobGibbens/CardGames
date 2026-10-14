using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.StartHand;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.IntegrationTests.Games.FiveCardDraw;

public class FoldedContributionSidePotTests : IntegrationTestBase
{
	[Fact]
	public async Task FoldedAnteBelowTheShortAllIn_PaysTheMainPotToTheShortStack()
	{
		var game = await StartDealtHandAsync([500, 100, 50, 500], ante: 10);

		// A fold is rejected when the street has no bet, so Dave checks and folds to the raise.
		await Act(game.Id, BettingActionType.Check);
		await Act(game.Id, BettingActionType.AllIn);
		await Act(game.Id, BettingActionType.Raise, 140);
		await Act(game.Id, BettingActionType.Call);
		await Act(game.Id, BettingActionType.Fold);

		await using var beforeShowdown = GetFreshDbContext();
		var dealt = await beforeShowdown.Games.SingleAsync(item => item.Id == game.Id);
		dealt.CurrentPhase.Should().Be(nameof(Phases.DrawPhase));
		var pots = await LoadPotsAsync(beforeShowdown, game.Id);
		pots.Select(pot => pot.Amount).Should().Equal(160, 200);
		pots.Sum(pot => pot.Amount).Should().Be(360);
		pots.Should().OnlyContain(pot => pot.Contributions.Sum(contribution => contribution.Amount) == pot.Amount);
		Eligible(pots[0]).Should().BeEquivalentTo("Player 1", "Player 3", "Player 4");
		Ineligible(pots[0]).Should().BeEquivalentTo([("Player 2", 10)]);
		Eligible(pots[1]).Should().BeEquivalentTo("Player 1", "Player 4");
		Ineligible(pots[1]).Should().BeEmpty();

		await ScriptShowdownAsync(game.Id);
		var showdown = await Mediator.Send(new PerformShowdownCommand(game.Id));
		showdown.IsT0.Should().BeTrue(showdown.IsT1 ? showdown.AsT1.Message : string.Empty);

		await using var seen = GetFreshDbContext();
		var stacks = await StacksAsync(seen, game.Id);
		stacks["Player 3"].Should().Be(160);
		stacks["Player 4"].Should().Be(550);
		stacks["Player 1"].Should().Be(350);
		stacks["Player 2"].Should().Be(90);
		stacks.Values.Sum().Should().Be(1_150);
	}

	[Fact]
	public async Task FoldedBetBetweenTwoAllInLevels_PaysTheMiddlePotToTheCoveringAllIn()
	{
		var game = await StartDealtHandAsync([500, 200, 50, 150], ante: 0);

		// Bob's all-in from 80 to 150 is a short raise, so Carol cannot raise to 200 over it.
		// Bob calls 80, Carol raises to 200, Dave folds, and Bob shoves the remaining 70.
		await Act(game.Id, BettingActionType.Bet, 80);
		await Act(game.Id, BettingActionType.AllIn);
		await Act(game.Id, BettingActionType.Call);
		await Act(game.Id, BettingActionType.Raise, 200);
		await Act(game.Id, BettingActionType.Fold);
		await Act(game.Id, BettingActionType.AllIn);

		await using var beforeShowdown = GetFreshDbContext();
		var dealt = await beforeShowdown.Games.SingleAsync(item => item.Id == game.Id);
		dealt.CurrentPhase.Should().Be(nameof(Phases.DrawPhase));
		var pots = await LoadPotsAsync(beforeShowdown, game.Id);
		pots.Select(pot => pot.Amount).Should().Equal(200, 230, 50);
		pots.Sum(pot => pot.Amount).Should().Be(480);
		pots.Should().OnlyContain(pot => pot.Contributions.Sum(contribution => contribution.Amount) == pot.Amount);
		Eligible(pots[0]).Should().BeEquivalentTo("Player 1", "Player 3", "Player 4");
		Ineligible(pots[0]).Should().BeEquivalentTo([("Player 2", 50)]);
		Eligible(pots[1]).Should().BeEquivalentTo("Player 1", "Player 4");
		Ineligible(pots[1]).Should().BeEquivalentTo([("Player 2", 30)]);
		Eligible(pots[2]).Should().BeEquivalentTo("Player 1");
		Ineligible(pots[2]).Should().BeEmpty();

		await ScriptShowdownAsync(game.Id);
		var showdown = await Mediator.Send(new PerformShowdownCommand(game.Id));
		showdown.IsT0.Should().BeTrue(showdown.IsT1 ? showdown.AsT1.Message : string.Empty);

		await using var seen = GetFreshDbContext();
		var stacks = await StacksAsync(seen, game.Id);
		stacks["Player 3"].Should().Be(200);
		stacks["Player 4"].Should().Be(230);
		stacks["Player 1"].Should().Be(350);
		stacks["Player 2"].Should().Be(120);
		stacks.Values.Sum().Should().Be(900);
	}

	private async Task<Game> StartDealtHandAsync(int[] stacks, int ante)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "FIVECARDDRAW", 4, startingChips: stacks.Max(), ante: ante);
		foreach (var player in setup.GamePlayers)
			player.ChipStack = stacks[player.SeatPosition];
		await DbContext.SaveChangesAsync();

		var started = await Mediator.Send(new StartHandCommand(setup.Game.Id));
		started.IsT0.Should().BeTrue(started.IsT1 ? started.AsT1.Message : string.Empty);
		var antes = await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
		antes.IsT0.Should().BeTrue(antes.IsT1 ? antes.AsT1.Message : string.Empty);
		var dealt = await Mediator.Send(new DealHandsCommand(setup.Game.Id));
		dealt.IsT0.Should().BeTrue(dealt.IsT1 ? dealt.AsT1.Message : string.Empty);
		return setup.Game;
	}

	private async Task Act(Guid gameId, BettingActionType action, int amount = 0)
	{
		var result = await Mediator.Send(new ProcessBettingActionCommand(gameId, action, amount)
		{
			BoundSeat = await TryCurrentBettingSeatAsync(gameId)
		});
		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : "action should succeed");
	}

	private static async Task<List<Pot>> LoadPotsAsync(CardsDbContext context, Guid gameId) =>
		await context.Pots.Include(pot => pot.Contributions).ThenInclude(contribution => contribution.GamePlayer)
			.ThenInclude(gamePlayer => gamePlayer.Player)
			.Where(pot => pot.GameId == gameId && pot.HandNumber == 1)
			.OrderBy(pot => pot.PotOrder)
			.ToListAsync();

	private static IEnumerable<string> Eligible(Pot pot) =>
		pot.Contributions.Where(contribution => contribution.IsEligibleToWin)
			.Select(contribution => contribution.GamePlayer.Player.Name);

	private static IEnumerable<(string Name, int Amount)> Ineligible(Pot pot) =>
		pot.Contributions.Where(contribution => !contribution.IsEligibleToWin)
			.Select(contribution => (contribution.GamePlayer.Player.Name, contribution.Amount));

	private async Task ScriptShowdownAsync(Guid gameId)
	{
		var players = await DbContext.GamePlayers.Include(player => player.Player)
			.Where(player => player.GameId == gameId && !player.HasFolded)
			.ToListAsync();
		var cards = await DbContext.GameCards
			.Where(card => card.GameId == gameId &&
			               card.HandNumber == 1 &&
			               card.Location == CardLocation.Hole &&
			               !card.IsDiscarded)
			.ToListAsync();
		var bySeat = players.ToDictionary(player => player.SeatPosition);
		Assign(cards, bySeat[2].Id,
			(CardSuit.Spades, CardSymbol.Ace),
			(CardSuit.Hearts, CardSymbol.Ace),
			(CardSuit.Diamonds, CardSymbol.Ace),
			(CardSuit.Clubs, CardSymbol.Ace),
			(CardSuit.Spades, CardSymbol.King));
		Assign(cards, bySeat[3].Id,
			(CardSuit.Spades, CardSymbol.Nine),
			(CardSuit.Hearts, CardSymbol.Nine),
			(CardSuit.Clubs, CardSymbol.Nine),
			(CardSuit.Diamonds, CardSymbol.Eight),
			(CardSuit.Hearts, CardSymbol.Eight));
		Assign(cards, bySeat[0].Id,
			(CardSuit.Spades, CardSymbol.Six),
			(CardSuit.Hearts, CardSymbol.Six),
			(CardSuit.Spades, CardSymbol.Five),
			(CardSuit.Hearts, CardSymbol.Five),
			(CardSuit.Clubs, CardSymbol.Four));

		var game = await DbContext.Games.SingleAsync(item => item.Id == gameId);
		game.CurrentPhase = nameof(Phases.Showdown);
		await DbContext.SaveChangesAsync();
	}

	private static void Assign(List<GameCard> cards, Guid gamePlayerId, params (CardSuit Suit, CardSymbol Symbol)[] hand)
	{
		var hole = cards.Where(card => card.GamePlayerId == gamePlayerId).OrderBy(card => card.DealOrder).ToList();
		hole.Should().HaveCount(5);
		for (var index = 0; index < hand.Length; index++)
		{
			hole[index].Suit = hand[index].Suit;
			hole[index].Symbol = hand[index].Symbol;
		}
	}

	private static async Task<Dictionary<string, int>> StacksAsync(CardsDbContext context, Guid gameId)
	{
		var players = await context.GamePlayers.Include(player => player.Player)
			.Where(player => player.GameId == gameId)
			.ToListAsync();
		return players.ToDictionary(player => player.Player.Name, player => player.ChipStack);
	}
}
