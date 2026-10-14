using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.DrawHands;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.PerformShowdown;

public class PerformShowdownCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenNoNaturalSevens_PaysTheHandPoolClockwiseFromTheDealer()
	{
		var aliceHand = new TwosJacksManWithTheAxeDrawHand(RoyalFlush);
		var bobHand = new TwosJacksManWithTheAxeDrawHand(RoyalFlush);
		var carolHand = new TwosJacksManWithTheAxeDrawHand(RoyalFlush);
		aliceHand.HasNaturalPairOfSevens().Should().BeFalse();
		bobHand.HasNaturalPairOfSevens().Should().BeFalse();
		carolHand.HasNaturalPairOfSevens().Should().BeFalse();
		aliceHand.Strength.Should().Be(bobHand.Strength);
		aliceHand.Strength.Should().Be(carolHand.Strength);

		await using var context = CreateContext();
		var game = await SeedShowdownAsync(context, 4, RoyalHearts);
		var settlement = Substitute.For<IHandSettlementService>();
		var sut = CreateSut(context, settlement);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.SevensPoolRolledOver.Should().BeTrue();
		var payouts = result.AsT0.Payouts;
		payouts["Carol"].Should().Be(2);
		payouts["Alice"].Should().Be(1);
		payouts["Bob"].Should().Be(1);
		await AssertStacksAsync(context, ("Carol", 102), ("Alice", 101), ("Bob", 101));
		await AssertSplitPayoutsAsync(context, ("Carol", 0, 2), ("Alice", 0, 1), ("Bob", 0, 1));

		await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		await AssertStacksAsync(context, ("Carol", 102), ("Alice", 101), ("Bob", 101));
		await settlement.Received(1).SettleHandAsync(
			Arg.Any<Game>(),
			Arg.Is<Dictionary<string, int>>(paid =>
				paid.Count == 3 && paid["Carol"] == 2 && paid["Alice"] == 1 && paid["Bob"] == 1),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Handle_WhenEveryPlayerHasNaturalSevensAndTheSameHighHand_SplitsEachPoolClockwise()
	{
		var aliceHand = new TwosJacksManWithTheAxeDrawHand(NaturalSevensHand);
		var bobHand = new TwosJacksManWithTheAxeDrawHand(NaturalSevensHand);
		var carolHand = new TwosJacksManWithTheAxeDrawHand(NaturalSevensHand);
		aliceHand.HasNaturalPairOfSevens().Should().BeTrue();
		bobHand.HasNaturalPairOfSevens().Should().BeTrue();
		carolHand.HasNaturalPairOfSevens().Should().BeTrue();
		aliceHand.Strength.Should().Be(bobHand.Strength);
		aliceHand.Strength.Should().Be(carolHand.Strength);

		await using var context = CreateContext();
		var game = await SeedShowdownAsync(context, 7, NaturalSevens);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.SevensPoolRolledOver.Should().BeFalse();
		var payouts = result.AsT0.Payouts;
		payouts["Carol"].Should().Be(3);
		payouts["Alice"].Should().Be(2);
		payouts["Bob"].Should().Be(2);
		await AssertStacksAsync(context, ("Carol", 103), ("Alice", 102), ("Bob", 102));
		(await StackSumAsync(context)).Should().Be(307);
		await AssertSplitPayoutsAsync(context, ("Carol", 1, 2), ("Alice", 1, 1), ("Bob", 1, 1));
	}

	[Fact]
	public async Task Handle_WhenTheShortAllInLosesTheSidePot_PaysEachPotToThePlayersWhoMatchedIt()
	{
		await using var context = CreateContext();
		var game = await SeedLayeredShowdownAsync(
			context,
			dealerPosition: 0,
			[
				Player("Alice", 0, chipStack: 0, contributed: 100, isAllIn: true, FourAces),
				Player("Bob", 3, chipStack: 0, contributed: 300, isAllIn: true, FullHouse),
				Player("Carol", 5, chipStack: 700, contributed: 300, isAllIn: false, TwoPair)
			],
			[
				Layer(amount: 300, potOrder: 0, PotType.Main, "Alice", "Bob", "Carol"),
				Layer(amount: 400, potOrder: 1, PotType.Side, "Bob", "Carol")
			]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.SevensPoolRolledOver.Should().BeTrue();
		success.SevensPayouts.Should().BeEmpty();
		success.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 300, ["Bob"] = 400 });
		success.HighHandPayouts.Should().BeEquivalentTo(success.Payouts);
		await AssertStacksAsync(context, ("Alice", 300), ("Bob", 400), ("Carol", 700));
		(await StackSumAsync(context)).Should().Be(1400);
		await AssertPotPayoutsAsync(context, 0, ("Alice", 300));
		await AssertPotPayoutsAsync(context, 1, ("Bob", 400));

		var replay = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		replay.IsT0.Should().BeTrue();
		await AssertStacksAsync(context, ("Alice", 300), ("Bob", 400), ("Carol", 700));
	}

	[Fact]
	public async Task Handle_WhenSevensWinOnlyTheMainPot_RollsTheSidePotIntoTheHighHand()
	{
		await using var context = CreateContext();
		var game = await SeedLayeredShowdownAsync(
			context,
			dealerPosition: 0,
			[
				Player("Alice", 0, chipStack: 0, contributed: 100, isAllIn: true, NaturalSevensHighCard),
				Player("Bob", 3, chipStack: 0, contributed: 300, isAllIn: true, FullHouse),
				Player("Carol", 5, chipStack: 700, contributed: 300, isAllIn: false, TwoPair)
			],
			[
				Layer(amount: 300, potOrder: 0, PotType.Main, "Alice", "Bob", "Carol"),
				Layer(amount: 400, potOrder: 1, PotType.Side, "Bob", "Carol")
			]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.SevensPoolRolledOver.Should().BeTrue();
		success.SevensPayouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 150 });
		success.HighHandPayouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 550 });
		success.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 150, ["Bob"] = 550 });
		await AssertStacksAsync(context, ("Alice", 150), ("Bob", 550), ("Carol", 700));
		await AssertPotPayoutsAsync(context, 1, ("Bob", 400));

		var alice = success.PlayerHands.Single(hand => hand.PlayerName == "Alice");
		alice.IsSevensWinner.Should().BeTrue();
		alice.IsHighHandWinner.Should().BeFalse();
		alice.SevensAmountWon.Should().Be(150);
		var bob = success.PlayerHands.Single(hand => hand.PlayerName == "Bob");
		bob.IsHighHandWinner.Should().BeTrue();
		bob.IsSevensWinner.Should().BeFalse();
		bob.HighHandAmountWon.Should().Be(550);
	}

	[Fact]
	public async Task Handle_WhenTheOnlyPotHasNoContributions_PaysTheBestHandTheWholePot()
	{
		await using var context = CreateContext();
		var game = await SeedLayeredShowdownAsync(
			context,
			dealerPosition: 0,
			[
				Player("Alice", 0, chipStack: 500, contributed: 45, isAllIn: false, FourAces),
				Player("Bob", 3, chipStack: 500, contributed: 45, isAllIn: false, FullHouse)
			],
			[Layer(amount: 90, potOrder: 0, PotType.Main)]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 90 });
		await AssertStacksAsync(context, ("Alice", 590), ("Bob", 500));
	}

	[Fact]
	public async Task Handle_WhenAFoldedPlayerIsStillMarkedEligible_PaysOnlyTheCallersWhoShowed()
	{
		await using var context = CreateContext();
		var game = await SeedLayeredShowdownAsync(
			context,
			dealerPosition: 3,
			[
				Player("Alice", 0, chipStack: 200, contributed: 30, isAllIn: false, TiedAceHigh),
				Player("Bob", 3, chipStack: 200, contributed: 30, isAllIn: false, TiedAceHighMatch),
				Player("Dan", 5, chipStack: 80, contributed: 30, isAllIn: false, cards: null, hasFolded: true)
			],
			[Layer(amount: 90, potOrder: 0, PotType.Main, "Dan", "Alice", "Bob")]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 45, ["Bob"] = 45 });
		await AssertStacksAsync(context, ("Alice", 245), ("Bob", 245), ("Dan", 80));
	}

	[Fact]
	public async Task Handle_WhenThreeEligiblePlayersTieTheMainPot_GivesTheOddChipClockwiseFromTheDealer()
	{
		await using var context = CreateContext();
		var game = await SeedLayeredShowdownAsync(
			context,
			dealerPosition: 3,
			[
				Player("Alice", 0, chipStack: 100, contributed: 0, isAllIn: false, RoyalHearts),
				Player("Bob", 3, chipStack: 100, contributed: 0, isAllIn: false, RoyalHearts),
				Player("Carol", 5, chipStack: 100, contributed: 0, isAllIn: false, RoyalHearts)
			],
			[Layer(amount: 5, potOrder: 0, PotType.Main, "Alice", "Bob", "Carol")]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts["Carol"].Should().Be(2);
		result.AsT0.Payouts["Alice"].Should().Be(2);
		result.AsT0.Payouts["Bob"].Should().Be(1);
		await AssertStacksAsync(context, ("Carol", 102), ("Alice", 102), ("Bob", 101));
	}

	private static PerformShowdownCommandHandler CreateSut(
		CardsDbContext context,
		IHandSettlementService? settlement = null)
	{
		return new PerformShowdownCommandHandler(
			context,
			Substitute.For<IHandHistoryRecorder>(),
			settlement ?? Substitute.For<IHandSettlementService>());
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
			.Options;

		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedShowdownAsync(
		CardsDbContext context,
		int potAmount,
		(CardSuit Suit, CardSymbol Symbol)[] holeCards)
	{
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = nameof(Phases.Showdown),
			CurrentHandNumber = 1,
			DealerPosition = 3,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};
		context.Games.Add(game);

		foreach (var (name, seat) in Seats)
		{
			var player = CreatePlayer(name, now);
			var gamePlayer = CreateGamePlayer(game, player, seat, now);
			context.Players.Add(player);
			context.GamePlayers.Add(gamePlayer);

			for (var cardIndex = 0; cardIndex < holeCards.Length; cardIndex++)
			{
				context.GameCards.Add(CreateHoleCard(
					game, gamePlayer, cardIndex + 1, holeCards[cardIndex].Suit, holeCards[cardIndex].Symbol, now));
			}
		}

		context.Pots.Add(new Pot
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = potAmount,
			IsAwarded = false,
			CreatedAt = now
		});

		await context.SaveChangesAsync();
		return game;
	}

	private static Player CreatePlayer(string name, DateTimeOffset now)
	{
		return new Player
		{
			Id = Guid.NewGuid(),
			Name = name,
			CreatedAt = now,
			UpdatedAt = now
		};
	}

	private static GamePlayer CreateGamePlayer(Game game, Player player, int seatPosition, DateTimeOffset now)
	{
		return new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = player.Id,
			Player = player,
			SeatPosition = seatPosition,
			ChipStack = 100,
			StartingChips = 100,
			Status = GamePlayerStatus.Active,
			HasFolded = false,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}

	private static GameCard CreateHoleCard(
		Game game,
		GamePlayer player,
		int dealOrder,
		CardSuit suit,
		CardSymbol symbol,
		DateTimeOffset now)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player.Id,
			GamePlayer = player,
			HandNumber = game.CurrentHandNumber,
			DealOrder = dealOrder,
			Location = CardLocation.Hole,
			IsDiscarded = false,
			IsVisible = false,
			Suit = suit,
			Symbol = symbol,
			DealtAt = now,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}

	private static async Task AssertStacksAsync(CardsDbContext context, params (string Name, int Stack)[] expected)
	{
		var stacks = await context.GamePlayers
			.Include(gamePlayer => gamePlayer.Player)
			.ToDictionaryAsync(gamePlayer => gamePlayer.Player.Name, gamePlayer => gamePlayer.ChipStack);

		foreach (var (name, stack) in expected)
			stacks[name].Should().Be(stack);
	}

	private static readonly (string Name, int Seat)[] Seats = [("Alice", 0), ("Bob", 3), ("Carol", 5)];

	private static readonly Card[] RoyalFlush =
	[
		new(Suit.Hearts, Symbol.Ace),
		new(Suit.Hearts, Symbol.King),
		new(Suit.Hearts, Symbol.Queen),
		new(Suit.Hearts, Symbol.Jack),
		new(Suit.Hearts, Symbol.Ten)
	];

	private static readonly Card[] NaturalSevensHand =
	[
		new(Suit.Clubs, Symbol.Seven),
		new(Suit.Diamonds, Symbol.Seven),
		new(Suit.Hearts, Symbol.Ace),
		new(Suit.Hearts, Symbol.King),
		new(Suit.Hearts, Symbol.Queen)
	];

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

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] FourAces =
	[
		(CardSuit.Spades, CardSymbol.Ace),
		(CardSuit.Hearts, CardSymbol.Ace),
		(CardSuit.Diamonds, CardSymbol.Ace),
		(CardSuit.Clubs, CardSymbol.Ace),
		(CardSuit.Spades, CardSymbol.King)
	];

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] FullHouse =
	[
		(CardSuit.Spades, CardSymbol.Nine),
		(CardSuit.Hearts, CardSymbol.Nine),
		(CardSuit.Clubs, CardSymbol.Nine),
		(CardSuit.Spades, CardSymbol.Eight),
		(CardSuit.Hearts, CardSymbol.Eight)
	];

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] TwoPair =
	[
		(CardSuit.Spades, CardSymbol.Six),
		(CardSuit.Hearts, CardSymbol.Six),
		(CardSuit.Spades, CardSymbol.Five),
		(CardSuit.Hearts, CardSymbol.Five),
		(CardSuit.Spades, CardSymbol.Four)
	];

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] NaturalSevensHighCard =
	[
		(CardSuit.Spades, CardSymbol.Seven),
		(CardSuit.Hearts, CardSymbol.Seven),
		(CardSuit.Clubs, CardSymbol.Three),
		(CardSuit.Diamonds, CardSymbol.Four),
		(CardSuit.Spades, CardSymbol.Five)
	];

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] TiedAceHigh =
	[
		(CardSuit.Spades, CardSymbol.Ace),
		(CardSuit.Hearts, CardSymbol.Ace),
		(CardSuit.Spades, CardSymbol.King),
		(CardSuit.Clubs, CardSymbol.Queen),
		(CardSuit.Hearts, CardSymbol.Jack)
	];

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] TiedAceHighMatch =
	[
		(CardSuit.Diamonds, CardSymbol.Ace),
		(CardSuit.Clubs, CardSymbol.Ace),
		(CardSuit.Hearts, CardSymbol.King),
		(CardSuit.Spades, CardSymbol.Queen),
		(CardSuit.Diamonds, CardSymbol.Jack)
	];

	private sealed record SeedPlayer(
		string Name,
		int Seat,
		int ChipStack,
		int Contributed,
		bool IsAllIn,
		bool HasFolded,
		(CardSuit Suit, CardSymbol Symbol)[]? Cards);

	private sealed record SeedPot(int Amount, int PotOrder, PotType PotType, string[] EligibleNames);

	private static SeedPlayer Player(
		string name,
		int seat,
		int chipStack,
		int contributed,
		bool isAllIn,
		(CardSuit Suit, CardSymbol Symbol)[]? cards,
		bool hasFolded = false)
	{
		return new SeedPlayer(name, seat, chipStack, contributed, isAllIn, hasFolded, cards);
	}

	private static SeedPot Layer(int amount, int potOrder, PotType potType, params string[] eligibleNames)
	{
		return new SeedPot(amount, potOrder, potType, eligibleNames);
	}

	private static async Task<Game> SeedLayeredShowdownAsync(
		CardsDbContext context,
		int dealerPosition,
		IReadOnlyList<SeedPlayer> players,
		IReadOnlyList<SeedPot> pots)
	{
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = nameof(Phases.Showdown),
			CurrentHandNumber = 1,
			DealerPosition = dealerPosition,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};
		context.Games.Add(game);

		var gamePlayers = new Dictionary<string, GamePlayer>(StringComparer.OrdinalIgnoreCase);
		foreach (var seed in players)
		{
			var player = CreatePlayer(seed.Name, now);
			var gamePlayer = CreateGamePlayer(game, player, seed.Seat, now);
			gamePlayer.ChipStack = seed.ChipStack;
			gamePlayer.TotalContributedThisHand = seed.Contributed;
			gamePlayer.IsAllIn = seed.IsAllIn;
			gamePlayer.HasFolded = seed.HasFolded;
			context.Players.Add(player);
			context.GamePlayers.Add(gamePlayer);
			gamePlayers[seed.Name] = gamePlayer;

			if (seed.Cards is null)
				continue;

			for (var cardIndex = 0; cardIndex < seed.Cards.Length; cardIndex++)
			{
				var card = CreateHoleCard(
					game, gamePlayer, cardIndex + 1, seed.Cards[cardIndex].Suit, seed.Cards[cardIndex].Symbol, now);
				card.Location = CardLocation.Hand;
				context.GameCards.Add(card);
			}
		}

		foreach (var seed in pots)
		{
			var pot = new Pot
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				HandNumber = game.CurrentHandNumber,
				PotType = seed.PotType,
				PotOrder = seed.PotOrder,
				Amount = seed.Amount,
				IsAwarded = false,
				CreatedAt = now,
				RowVersion = [1]
			};
			context.Pots.Add(pot);

			foreach (var name in seed.EligibleNames)
			{
				context.PotContributions.Add(new PotContribution
				{
					Id = Guid.NewGuid(),
					PotId = pot.Id,
					Pot = pot,
					GamePlayerId = gamePlayers[name].Id,
					GamePlayer = gamePlayers[name],
					Amount = 1,
					IsEligibleToWin = true,
					ContributedAt = now,
					RowVersion = [1]
				});
			}
		}

		await context.SaveChangesAsync();
		return game;
	}

	private static async Task AssertSplitPayoutsAsync(
		CardsDbContext context,
		params (string Name, int SevensAmount, int HighHandAmount)[] expected)
	{
		var pot = await context.Pots.SingleAsync(candidate => candidate.HandNumber == 1 && candidate.PotOrder == 0);
		using var document = JsonDocument.Parse(pot.WinnerPayouts!);
		var payouts = document.RootElement.EnumerateArray()
			.Select(element => (
				Name: element.GetProperty("playerName").GetString()!,
				SevensAmount: element.GetProperty("sevensAmount").GetInt32(),
				HighHandAmount: element.GetProperty("highHandAmount").GetInt32()))
			.ToList();

		payouts.Should().BeEquivalentTo(expected.Select(entry => (
			entry.Name,
			entry.SevensAmount,
			entry.HighHandAmount)));
	}

	private static async Task AssertPotPayoutsAsync(
		CardsDbContext context,
		int potOrder,
		params (string Name, int Amount)[] expected)
	{
		var pot = await context.Pots.SingleAsync(candidate => candidate.PotOrder == potOrder);
		using var document = JsonDocument.Parse(pot.WinnerPayouts!);
		var payouts = document.RootElement.EnumerateArray()
			.Select(element => (
				Name: element.GetProperty("playerName").GetString(),
				Amount: element.GetProperty("amount").GetInt32()))
			.ToList();

		payouts.Should().BeEquivalentTo(expected.Select(entry => (Name: entry.Name, Amount: entry.Amount)));
	}

	private static async Task<int> StackSumAsync(CardsDbContext context)
	{
		return await context.GamePlayers.SumAsync(gamePlayer => gamePlayer.ChipStack);
	}
}
