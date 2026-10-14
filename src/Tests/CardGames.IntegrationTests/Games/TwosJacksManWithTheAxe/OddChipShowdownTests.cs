using System.Text.Json;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.PerformShowdown;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.IntegrationTests.Games.TwosJacksManWithTheAxe;

public class OddChipShowdownTests : IntegrationTestBase
{
	[Fact]
	public async Task RoyalFlushes_PayTheRolledHandPoolClockwiseFromTheDealer()
	{
		var gameId = await SeedShowdownAsync(4, RoyalHearts);

		var result = await Mediator.Send(new PerformShowdownCommand(gameId));
		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);

		await using var seen = GetFreshDbContext();
		var stacks = await StacksAsync(seen, gameId);
		stacks["Alice"].Should().Be(101);
		stacks["Bob"].Should().Be(101);
		stacks["Carol"].Should().Be(102);
	}

	[Fact]
	public async Task NaturalSevens_PayEachHalfClockwiseFromTheDealer()
	{
		var gameId = await SeedShowdownAsync(7, NaturalSevens);

		var result = await Mediator.Send(new PerformShowdownCommand(gameId));
		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);

		await using var seen = GetFreshDbContext();
		var stacks = await StacksAsync(seen, gameId);
		stacks["Alice"].Should().Be(102);
		stacks["Bob"].Should().Be(102);
		stacks["Carol"].Should().Be(103);

		var pot = await seen.Pots.SingleAsync(candidate => candidate.GameId == gameId);
		var payouts = ReadSplit(pot.WinnerPayouts);
		payouts["Carol"].HighHandAmount.Should().Be(2);
		payouts["Bob"].HighHandAmount.Should().Be(1);
	}

	private async Task<Guid> SeedShowdownAsync(
		int potAmount,
		(CardSuit Suit, CardSymbol Symbol)[] holeCards)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "TWOSJACKSMANWITHTHEAXE", 3, startingChips: 100, ante: 0);

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
		game.CurrentHandGameTypeCode = "TWOSJACKSMANWITHTHEAXE";
		game.Status = GameStatus.InProgress;

		var dealtCards = await DbContext.GameCards.Where(card => card.GameId == game.Id).ToListAsync();
		DbContext.GameCards.RemoveRange(dealtCards);

		var now = DateTimeOffset.UtcNow;
		foreach (var gamePlayer in setup.GamePlayers)
		{
			for (var cardIndex = 0; cardIndex < holeCards.Length; cardIndex++)
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
					Suit = holeCards[cardIndex].Suit,
					Symbol = holeCards[cardIndex].Symbol,
					DealtAt = now,
					DealtAtPhase = nameof(Phases.Dealing)
				});
			}
		}

		DbContext.Pots.Add(new Pot
		{
			GameId = game.Id,
			HandNumber = 1,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = potAmount,
			IsAwarded = false,
			CreatedAt = now
		});

		await DbContext.SaveChangesAsync();
		return game.Id;
	}

	private static Dictionary<string, (int SevensAmount, int HighHandAmount)> ReadSplit(string? json)
	{
		using var document = JsonDocument.Parse(json!);
		var payouts = new Dictionary<string, (int SevensAmount, int HighHandAmount)>();
		foreach (var item in document.RootElement.EnumerateArray())
		{
			payouts[item.GetProperty("playerName").GetString()!] = (
				item.GetProperty("sevensAmount").GetInt32(),
				item.GetProperty("highHandAmount").GetInt32());
		}

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

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] NaturalSevens =
	[
		(CardSuit.Clubs, CardSymbol.Seven),
		(CardSuit.Diamonds, CardSymbol.Seven),
		(CardSuit.Hearts, CardSymbol.Ace),
		(CardSuit.Hearts, CardSymbol.King),
		(CardSuit.Hearts, CardSymbol.Queen)
	];
}
