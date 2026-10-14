using System.Text.Json;
using DedicatedShowdown = CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.PerformShowdown;
using GenericShowdown = CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.IntegrationTests.Games.HoldTheBaseball;

public class SecondShowdownDoorTests : IntegrationTestBase
{
	[Fact]
	public async Task DedicatedShowdown_ShortAllIn_MatchesTheGenericAward_AndASecondCallDoesNotMoveChips()
	{
		var gameId = await SeedSidePotAsync();

		var dedicated = await Mediator.Send(new DedicatedShowdown.PerformShowdownCommand(gameId));
		dedicated.IsT0.Should().BeTrue(dedicated.IsT1 ? dedicated.AsT1.Message : string.Empty);

		await AssertSidePotAsync(gameId);

		var generic = await Mediator.Send(new GenericShowdown.PerformShowdownCommand(gameId));
		generic.IsT0.Should().BeTrue(generic.IsT1 ? generic.AsT1.Message : string.Empty);
		await AssertSidePotStacksAsync(gameId);
	}

	[Fact]
	public async Task GenericShowdown_ShortAllIn_PaysTheSameStacksAsTheDedicatedDoor()
	{
		var gameId = await SeedSidePotAsync();

		var generic = await Mediator.Send(new GenericShowdown.PerformShowdownCommand(gameId));
		generic.IsT0.Should().BeTrue(generic.IsT1 ? generic.AsT1.Message : string.Empty);

		await AssertSidePotStacksAsync(gameId);
	}

	[Fact]
	public async Task DedicatedShowdown_LeftoverChip_PaysCarolThenStaysPutForTheGenericCall()
	{
		var gameId = await SeedLeftoverChipAsync();

		var dedicated = await Mediator.Send(new DedicatedShowdown.PerformShowdownCommand(gameId));
		dedicated.IsT0.Should().BeTrue(dedicated.IsT1 ? dedicated.AsT1.Message : string.Empty);
		await AssertLeftoverStacksAsync(gameId);

		var generic = await Mediator.Send(new GenericShowdown.PerformShowdownCommand(gameId));
		generic.IsT0.Should().BeTrue(generic.IsT1 ? generic.AsT1.Message : string.Empty);
		await AssertLeftoverStacksAsync(gameId);
	}

	private async Task AssertSidePotAsync(Guid gameId)
	{
		await AssertSidePotStacksAsync(gameId);

		await using var context = GetFreshDbContext();
		var pots = await context.Pots
			.Where(pot => pot.GameId == gameId && pot.HandNumber == 1)
			.OrderBy(pot => pot.PotOrder)
			.ToListAsync();
		pots.Should().HaveCount(2);
		pots.Should().OnlyContain(pot => pot.IsAwarded);
		pots[0].Amount.Should().Be(150);
		Names(pots[0].WinnerPayouts).Should().Equal("Alice");
		pots[1].Amount.Should().Be(200);
		Amounts(pots[1].WinnerPayouts).Should().BeEquivalentTo(new Dictionary<string, int>
		{
			["Bob"] = 100,
			["Carol"] = 100
		});
		pots.Should().OnlyContain(pot => pot.WinnerPayouts != null && !pot.WinnerPayouts.Contains("350"));
	}

	private async Task AssertSidePotStacksAsync(Guid gameId)
	{
		await using var context = GetFreshDbContext();
		var stacks = await StacksAsync(context, gameId);
		stacks["Alice"].Should().Be(150);
		stacks["Bob"].Should().Be(200);
		stacks["Carol"].Should().Be(200);
	}

	private async Task AssertLeftoverStacksAsync(Guid gameId)
	{
		await using var context = GetFreshDbContext();
		var stacks = await StacksAsync(context, gameId);
		stacks["Alice"].Should().Be(105);
		stacks["Bob"].Should().Be(105);
		stacks["Carol"].Should().Be(106);
		stacks["Dave"].Should().Be(99);
	}

	private async Task<Guid> SeedSidePotAsync()
	{
		var seats = await SeedSeatsAsync(3);
		seats["Alice"].ChipStack = 0;
		seats["Alice"].IsAllIn = true;
		seats["Alice"].TotalContributedThisHand = 50;
		seats["Bob"].ChipStack = 100;
		seats["Bob"].TotalContributedThisHand = 150;
		seats["Carol"].ChipStack = 100;
		seats["Carol"].TotalContributedThisHand = 150;
		AddHoleCards(seats["Alice"], (CardSuit.Hearts, CardSymbol.Ace), (CardSuit.Diamonds, CardSymbol.Ace));
		AddHoleCards(seats["Bob"], (CardSuit.Hearts, CardSymbol.Eight), (CardSuit.Hearts, CardSymbol.Seven));
		AddHoleCards(seats["Carol"], (CardSuit.Diamonds, CardSymbol.Eight), (CardSuit.Diamonds, CardSymbol.Seven));
		AddCommunity(
			seats["Alice"].Game,
			(CardSuit.Spades, CardSymbol.King),
			(CardSuit.Spades, CardSymbol.Queen),
			(CardSuit.Spades, CardSymbol.Jack),
			(CardSuit.Spades, CardSymbol.Ten),
			(CardSuit.Clubs, CardSymbol.Deuce));
		DbContext.Pots.Add(CreatePot(seats["Alice"].Game, 350));
		await DbContext.SaveChangesAsync();
		return seats["Alice"].GameId;
	}

	private async Task<Guid> SeedLeftoverChipAsync()
	{
		var seats = await SeedSeatsAsync(4);
		foreach (var name in new[] { "Alice", "Bob", "Carol" })
		{
			seats[name].ChipStack = 100;
			seats[name].TotalContributedThisHand = 5;
		}

		seats["Dave"].ChipStack = 99;
		seats["Dave"].TotalContributedThisHand = 1;
		seats["Dave"].HasFolded = true;
		AddHoleCards(seats["Alice"], (CardSuit.Hearts, CardSymbol.Ace), (CardSuit.Diamonds, CardSymbol.King));
		AddHoleCards(seats["Bob"], (CardSuit.Diamonds, CardSymbol.Ace), (CardSuit.Clubs, CardSymbol.King));
		AddHoleCards(seats["Carol"], (CardSuit.Spades, CardSymbol.Ace), (CardSuit.Hearts, CardSymbol.King));
		AddCommunity(
			seats["Alice"].Game,
			(CardSuit.Clubs, CardSymbol.Deuce),
			(CardSuit.Diamonds, CardSymbol.Four),
			(CardSuit.Hearts, CardSymbol.Five),
			(CardSuit.Spades, CardSymbol.Six),
			(CardSuit.Clubs, CardSymbol.Eight));
		DbContext.Pots.Add(CreatePot(seats["Alice"].Game, 16));
		await DbContext.SaveChangesAsync();
		return seats["Alice"].GameId;
	}

	private async Task<Dictionary<string, GamePlayer>> SeedSeatsAsync(int playerCount)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "HOLDTHEBASEBALL", playerCount, startingChips: 100, ante: 0);
		var names = new[] { "Alice", "Bob", "Carol", "Dave" };
		var seatPositions = new[] { 0, 3, 5, 6 };
		for (var index = 0; index < playerCount; index++)
		{
			setup.Players[index].Name = names[index];
			setup.GamePlayers[index].SeatPosition = seatPositions[index];
		}

		var game = setup.Game;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.CurrentHandNumber = 1;
		game.CurrentHandGameTypeCode = "HOLDTHEBASEBALL";
		game.DealerPosition = 3;
		game.Status = GameStatus.InProgress;

		var existingPots = await DbContext.Pots.Where(pot => pot.GameId == game.Id).ToListAsync();
		DbContext.Pots.RemoveRange(existingPots);
		var existingCards = await DbContext.GameCards.Where(card => card.GameId == game.Id).ToListAsync();
		DbContext.GameCards.RemoveRange(existingCards);
		await DbContext.SaveChangesAsync();

		return setup.GamePlayers.ToDictionary(gamePlayer => gamePlayer.Player.Name);
	}

	private void AddHoleCards(GamePlayer player, params (CardSuit Suit, CardSymbol Symbol)[] cards)
	{
		var now = DateTimeOffset.UtcNow;
		for (var index = 0; index < cards.Length; index++)
		{
			DbContext.GameCards.Add(new GameCard
			{
				GameId = player.GameId,
				Game = player.Game,
				GamePlayerId = player.Id,
				GamePlayer = player,
				HandNumber = 1,
				DealOrder = index + 1,
				Location = CardLocation.Hole,
				IsVisible = false,
				Suit = cards[index].Suit,
				Symbol = cards[index].Symbol,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing)
			});
		}
	}

	private void AddCommunity(Game game, params (CardSuit Suit, CardSymbol Symbol)[] cards)
	{
		var now = DateTimeOffset.UtcNow;
		for (var index = 0; index < cards.Length; index++)
		{
			DbContext.GameCards.Add(new GameCard
			{
				GameId = game.Id,
				Game = game,
				HandNumber = 1,
				DealOrder = index + 1,
				Location = CardLocation.Community,
				IsVisible = true,
				Suit = cards[index].Suit,
				Symbol = cards[index].Symbol,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing)
			});
		}
	}

	private static PotEntity CreatePot(Game game, int amount)
	{
		return new PotEntity
		{
			GameId = game.Id,
			Game = game,
			HandNumber = 1,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = amount,
			IsAwarded = false,
			CreatedAt = DateTimeOffset.UtcNow
		};
	}

	private static async Task<Dictionary<string, int>> StacksAsync(CardsDbContext context, Guid gameId)
	{
		return await context.GamePlayers
			.Include(gamePlayer => gamePlayer.Player)
			.Where(gamePlayer => gamePlayer.GameId == gameId)
			.ToDictionaryAsync(gamePlayer => gamePlayer.Player.Name, gamePlayer => gamePlayer.ChipStack);
	}

	private static Dictionary<string, int> Amounts(string? winnerPayouts)
	{
		return ReadPayouts(winnerPayouts).ToDictionary(payout => payout.PlayerName, payout => payout.Amount);
	}

	private static List<string> Names(string? winnerPayouts)
	{
		return ReadPayouts(winnerPayouts).Select(payout => payout.PlayerName).ToList();
	}

	private static List<WinnerPayout> ReadPayouts(string? winnerPayouts)
	{
		return JsonSerializer.Deserialize<List<WinnerPayout>>(
			winnerPayouts!,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
	}

	private sealed record WinnerPayout(string PlayerName, int Amount);
}
