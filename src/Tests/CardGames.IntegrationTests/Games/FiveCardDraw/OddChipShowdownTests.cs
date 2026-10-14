using System.Text.Json;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.PerformShowdown;
using CardGames.Poker.Evaluation;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.IntegrationTests.Games.FiveCardDraw;

public class OddChipShowdownTests : IntegrationTestBase
{
	[Fact]
	public async Task TiedMainAndSidePots_PayOddChipsClockwiseFromTheDealer()
	{
		var gameId = await SeedShowdownAsync();

		var first = await Mediator.Send(new PerformShowdownCommand(gameId));
		first.IsT0.Should().BeTrue(first.IsT1 ? first.AsT1.Message : string.Empty);

		await using (var seen = GetFreshDbContext())
		{
			var stacks = await StacksAsync(seen, gameId);
			stacks["Alice"].Should().Be(103);
			stacks["Bob"].Should().Be(101);
			stacks["Carol"].Should().Be(105);

			var pots = await seen.Pots
				.Where(pot => pot.GameId == gameId)
				.OrderBy(pot => pot.PotOrder)
				.ToListAsync();
			var main = ReadAmounts(pots[0].WinnerPayouts);
			main["Carol"].Should().Be(2);
			main["Alice"].Should().Be(1);
			main["Bob"].Should().Be(1);
			pots[0].WinnerPayouts.Should().NotContain("\"amount\":5");
			pots[0].WinnerPayouts.Should().NotContain("\"amount\":3");

			var side = ReadAmounts(pots[1].WinnerPayouts);
			side["Carol"].Should().Be(3);
			side["Alice"].Should().Be(2);
			side.Should().NotContainKey("Bob");
			pots[1].WinnerPayouts.Should().NotContain("\"amount\":5");
			pots[1].WinnerPayouts.Should().NotContain("Bob");
		}

		var second = await Mediator.Send(new PerformShowdownCommand(gameId));
		second.IsT0.Should().BeTrue(second.IsT1 ? second.AsT1.Message : string.Empty);

		await using (var replayed = GetFreshDbContext())
		{
			var stacks = await StacksAsync(replayed, gameId);
			stacks["Alice"].Should().Be(103);
			stacks["Bob"].Should().Be(101);
			stacks["Carol"].Should().Be(105);
		}
	}

	private async Task<Guid> SeedShowdownAsync()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "FIVECARDDRAW", 3, startingChips: 100, ante: 0);

		var names = new[] { "Alice", "Bob", "Carol" };
		var seats = new[] { 0, 3, 5 };
		for (var index = 0; index < names.Length; index++)
		{
			setup.Players[index].Name = names[index];
			setup.GamePlayers[index].SeatPosition = seats[index];
			setup.GamePlayers[index].ChipStack = 100;
			setup.GamePlayers[index].HasFolded = false;
			setup.GamePlayers[index].Status = GamePlayerStatus.Active;
		}

		var game = setup.Game;
		game.DealerPosition = 3;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.CurrentHandNumber = 1;
		game.CurrentHandGameTypeCode = HandEvaluatorFactory.FiveCardDrawCode;
		game.Status = GameStatus.InProgress;

		var dealtCards = await DbContext.GameCards.Where(card => card.GameId == game.Id).ToListAsync();
		DbContext.GameCards.RemoveRange(dealtCards);

		var now = DateTimeOffset.UtcNow;
		var playersByName = setup.GamePlayers.ToDictionary(gamePlayer => gamePlayer.Player.Name);
		foreach (var gamePlayer in setup.GamePlayers)
		{
			for (var cardIndex = 0; cardIndex < RoyalHearts.Length; cardIndex++)
			{
				DbContext.GameCards.Add(new GameCard
				{
					GameId = game.Id,
					GamePlayerId = gamePlayer.Id,
					HandNumber = 1,
					DealOrder = cardIndex + 1,
					Location = CardLocation.Hole,
					IsDiscarded = false,
					IsVisible = false,
					Suit = RoyalHearts[cardIndex].Suit,
					Symbol = RoyalHearts[cardIndex].Symbol,
					DealtAt = now,
					DealtAtPhase = nameof(Phases.Dealing)
				});
			}
		}

		var mainPot = new Pot
		{
			GameId = game.Id,
			HandNumber = 1,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 4,
			IsAwarded = false,
			CreatedAt = now
		};
		AddContribution(mainPot, playersByName["Alice"], now, true);
		AddContribution(mainPot, playersByName["Bob"], now, true);
		AddContribution(mainPot, playersByName["Carol"], now, true);

		var sidePot = new Pot
		{
			GameId = game.Id,
			HandNumber = 1,
			PotType = PotType.Side,
			PotOrder = 1,
			Amount = 5,
			IsAwarded = false,
			CreatedAt = now
		};
		AddContribution(sidePot, playersByName["Alice"], now, true);
		AddContribution(sidePot, playersByName["Bob"], now, false);
		AddContribution(sidePot, playersByName["Carol"], now, true);

		DbContext.Pots.AddRange(mainPot, sidePot);
		await DbContext.SaveChangesAsync();
		return game.Id;
	}

	private static void AddContribution(Pot pot, GamePlayer gamePlayer, DateTimeOffset now, bool isEligibleToWin)
	{
		pot.Contributions.Add(new PotContribution
		{
			Pot = pot,
			GamePlayerId = gamePlayer.Id,
			GamePlayer = gamePlayer,
			Amount = pot.Amount,
			IsEligibleToWin = isEligibleToWin,
			ContributedAt = now
		});
	}

	private static Dictionary<string, int> ReadAmounts(string? json)
	{
		using var document = JsonDocument.Parse(json!);
		var payouts = new Dictionary<string, int>();
		foreach (var item in document.RootElement.EnumerateArray())
			payouts[item.GetProperty("playerName").GetString()!] = item.GetProperty("amount").GetInt32();

		return payouts;
	}

	private static async Task<Dictionary<string, int>> StacksAsync(CardsDbContext context, Guid gameId)
	{
		return await context.GamePlayers
			.Include(gamePlayer => gamePlayer.Player)
			.Where(gamePlayer => gamePlayer.GameId == gameId)
			.ToDictionaryAsync(gamePlayer => gamePlayer.Player.Name, gamePlayer => gamePlayer.ChipStack);
	}

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] RoyalHearts =
	[
		(CardSuit.Hearts, CardSymbol.Ace),
		(CardSuit.Hearts, CardSymbol.King),
		(CardSuit.Hearts, CardSymbol.Queen),
		(CardSuit.Hearts, CardSymbol.Jack),
		(CardSuit.Hearts, CardSymbol.Ten)
	];
}
