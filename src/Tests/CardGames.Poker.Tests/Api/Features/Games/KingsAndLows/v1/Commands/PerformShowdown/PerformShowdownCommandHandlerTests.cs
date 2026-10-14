using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.DrawHands;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.KingsAndLows.v1.Commands.PerformShowdown;

public class PerformShowdownCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenThreePlayersTie_PaysOddChipsClockwiseFromTheDealer()
	{
		await using var context = CreateContext();
		var game = await SeedShowdownAsync(context, 5, RoyalHearts);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var payouts = result.AsT0.Payouts;
		payouts["Carol"].Should().Be(2);
		payouts["Alice"].Should().Be(2);
		payouts["Bob"].Should().Be(1);
		payouts.Values.Sum().Should().Be(5);
		await AssertStacksAsync(context, ("Carol", 102), ("Alice", 102), ("Bob", 101));
	}

	[Fact]
	public async Task Handle_WhenOnlyOnePlayerHasAHand_PaysTheWholePot()
	{
		await using var context = CreateContext();
		var game = await SeedShowdownAsync(context, 5, RoyalHearts, aliceOnly: true);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var payouts = result.AsT0.Payouts;
		payouts["Alice"].Should().Be(5);
		payouts.Values.Sum().Should().Be(5);
		await AssertStacksAsync(context, ("Alice", 105), ("Bob", 100), ("Carol", 100));
	}

	[Fact]
	public async Task Handle_WhenThreeStraightsTie_PaysOddChipsClockwiseAndLeavesStacksUnchangedOnReplay()
	{
		var aliceHand = new KingsAndLowsDrawHand(QueenHighStraightSpades.ToCards());
		var bobHand = new KingsAndLowsDrawHand(QueenHighStraightHearts.ToCards());
		var carolHand = new KingsAndLowsDrawHand(QueenHighStraightDiamonds.ToCards());
		aliceHand.Strength.Should().Be(bobHand.Strength);
		aliceHand.Strength.Should().Be(carolHand.Strength);

		await using var context = CreateContext();
		var game = await SeedContestedShowdownAsync(context, new Dictionary<string, string?>
		{
			["Alice"] = QueenHighStraightSpades,
			["Bob"] = QueenHighStraightHearts,
			["Carol"] = QueenHighStraightDiamonds
		});
		var settlement = Substitute.For<IHandSettlementService>();
		var sut = CreateSut(context, settlement);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Winners.Should().Equal("Carol", "Alice", "Bob");
		var payouts = result.AsT0.Payouts;
		payouts["Carol"].Should().Be(2);
		payouts["Alice"].Should().Be(2);
		payouts["Bob"].Should().Be(1);
		await AssertStacksAsync(context, ("Carol", 102), ("Alice", 102), ("Bob", 101));
		(await StackSumAsync(context)).Should().Be(305);

		var pots = await context.Pots.Where(pot => pot.GameId == game.Id).ToListAsync();
		pots.Should().ContainSingle();
		pots[0].HandNumber.Should().Be(1);
		pots[0].IsAwarded.Should().BeTrue();
		pots[0].WinnerPayouts.Should().Contain("\"playerName\":\"Carol\"");
		pots[0].WinnerPayouts.Should().Contain("\"amount\":2");
		pots[0].WinnerPayouts.Should().Contain("\"playerName\":\"Alice\"");
		pots[0].WinnerPayouts.Should().Contain("\"playerName\":\"Bob\"");
		pots[0].WinnerPayouts.Should().Contain("\"amount\":1");
		pots[0].WinnerPayouts.Should().NotContain("\"amount\":3");
		WinnerAmounts(pots[0].WinnerPayouts!).Should().Equal(("Carol", 2), ("Alice", 2), ("Bob", 1));

		await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		await AssertStacksAsync(context, ("Carol", 102), ("Alice", 102), ("Bob", 101));
		await settlement.Received(1).SettleHandAsync(
			Arg.Any<Game>(),
			Arg.Is<Dictionary<string, int>>(paid =>
				paid.Count == 3 && paid["Carol"] == 2 && paid["Alice"] == 2 && paid["Bob"] == 1),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Handle_WhenTwoStraightsTie_MatchesTheLoserForTheWholePot()
	{
		var aliceHand = new KingsAndLowsDrawHand(QueenHighStraightSpades.ToCards());
		var carolHand = new KingsAndLowsDrawHand(QueenHighStraightDiamonds.ToCards());
		var bobHand = new KingsAndLowsDrawHand(BobLosingHand.ToCards());
		bobHand.Strength.Should().BeLessThan(aliceHand.Strength);
		aliceHand.Strength.Should().Be(carolHand.Strength);

		await using var context = CreateContext();
		var game = await SeedContestedShowdownAsync(context, new Dictionary<string, string?>
		{
			["Alice"] = QueenHighStraightSpades,
			["Bob"] = BobLosingHand,
			["Carol"] = QueenHighStraightDiamonds
		});
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var payouts = result.AsT0.Payouts;
		payouts["Carol"].Should().Be(3);
		payouts["Alice"].Should().Be(2);
		payouts.Should().NotContainKey("Bob");
		await AssertStacksAsync(context, ("Carol", 103), ("Alice", 102), ("Bob", 95));

		var pots = await context.Pots.Where(pot => pot.GameId == game.Id).ToListAsync();
		var handOne = pots.Single(pot => pot.HandNumber == 1);
		handOne.IsAwarded.Should().BeTrue();
		handOne.Amount.Should().Be(5);
		var handTwo = pots.Single(pot => pot.HandNumber == 2);
		handTwo.PotType.Should().Be(PotType.Main);
		handTwo.Amount.Should().Be(5);
		handTwo.IsAwarded.Should().BeFalse();
		((await StackSumAsync(context)) + handTwo.Amount).Should().Be(305);
	}

	[Fact]
	public async Task Handle_WhenOnePlayerWinsAndAnotherLoses_PaysTheWholePotAndMatchesTheLoser()
	{
		await using var context = CreateContext();
		var game = await SeedContestedShowdownAsync(
			context,
			new Dictionary<string, string?>
			{
				["Alice"] = QueenHighStraightSpades,
				["Bob"] = BobLosingHand,
				["Carol"] = null
			},
			"Carol");
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts["Alice"].Should().Be(5);
		result.AsT0.Payouts.Should().NotContainKey("Bob");
		result.AsT0.Payouts.Should().NotContainKey("Carol");
		await AssertStacksAsync(context, ("Alice", 105), ("Bob", 95), ("Carol", 100));

		var handTwo = await context.Pots.SingleAsync(pot => pot.GameId == game.Id && pot.HandNumber == 2);
		handTwo.PotType.Should().Be(PotType.Main);
		handTwo.Amount.Should().Be(5);
		handTwo.IsAwarded.Should().BeFalse();
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
		(CardSuit Suit, CardSymbol Symbol)[] holeCards,
		bool aliceOnly = false)
	{
		var now = DateTimeOffset.UtcNow;
		var game = CreateGame(now);
		context.Games.Add(game);

		foreach (var (name, seat) in Seats)
		{
			var player = CreatePlayer(name, now);
			var gamePlayer = CreateGamePlayer(game, player, seat, now);
			context.Players.Add(player);
			context.GamePlayers.Add(gamePlayer);

			if (aliceOnly && name != "Alice")
				continue;

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

	private static Game CreateGame(DateTimeOffset now)
	{
		return new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = nameof(Phases.Showdown),
			CurrentHandNumber = 1,
			DealerPosition = 3,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};
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
			IsVisible = false,
			Suit = suit,
			Symbol = symbol,
			DealtAt = now,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}

	private static async Task<Game> SeedContestedShowdownAsync(
		CardsDbContext context,
		IReadOnlyDictionary<string, string?> hands,
		params string[] foldedPlayers)
	{
		var now = DateTimeOffset.UtcNow;
		var game = CreateGame(now);
		context.Games.Add(game);
		var folded = foldedPlayers.ToHashSet(StringComparer.Ordinal);

		foreach (var (name, seat) in Seats)
		{
			var player = CreatePlayer(name, now);
			var gamePlayer = CreateGamePlayer(game, player, seat, now);
			gamePlayer.HasFolded = folded.Contains(name);
			context.Players.Add(player);
			context.GamePlayers.Add(gamePlayer);

			if (!hands.TryGetValue(name, out var hand) || hand is null)
				continue;

			var dealOrder = 1;
			foreach (var token in hand.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				var (suit, symbol) = ParseCard(token);
				context.GameCards.Add(new GameCard
				{
					Id = Guid.NewGuid(),
					GameId = game.Id,
					Game = game,
					GamePlayerId = gamePlayer.Id,
					GamePlayer = gamePlayer,
					HandNumber = game.CurrentHandNumber,
					DealOrder = dealOrder++,
					Location = CardLocation.Hand,
					IsDiscarded = false,
					IsVisible = false,
					Suit = suit,
					Symbol = symbol,
					DealtAt = now,
					DealtAtPhase = nameof(Phases.Dealing)
				});
			}
		}

		context.Pots.Add(new Pot
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 5,
			IsAwarded = false,
			CreatedAt = now
		});

		await context.SaveChangesAsync();
		return game;
	}

	private static (CardSuit Suit, CardSymbol Symbol) ParseCard(string token)
	{
		var suit = token[^1] switch
		{
			's' => CardSuit.Spades,
			'h' => CardSuit.Hearts,
			'd' => CardSuit.Diamonds,
			'c' => CardSuit.Clubs,
			_ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown suit")
		};
		var symbol = token[..^1] switch
		{
			"2" => CardSymbol.Deuce,
			"3" => CardSymbol.Three,
			"4" => CardSymbol.Four,
			"5" => CardSymbol.Five,
			"7" => CardSymbol.Seven,
			"8" => CardSymbol.Eight,
			"9" => CardSymbol.Nine,
			"J" => CardSymbol.Jack,
			"Q" => CardSymbol.Queen,
			_ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown symbol")
		};
		return (suit, symbol);
	}

	private static (string Name, int Amount)[] WinnerAmounts(string winnerPayouts)
	{
		return winnerPayouts
			.Split("playerName\":\"", StringSplitOptions.RemoveEmptyEntries)
			.Skip(1)
			.Select(part =>
			{
				var name = part[..part.IndexOf('"')];
				var amountToken = part[(part.IndexOf("\"amount\":", StringComparison.Ordinal) + "\"amount\":".Length)..];
				var amountEnd = amountToken.IndexOfAny([',', '}']);
				return (name, int.Parse(amountToken[..amountEnd]));
			})
			.ToArray();
	}

	private static async Task<int> StackSumAsync(CardsDbContext context)
	{
		return await context.GamePlayers.SumAsync(gamePlayer => gamePlayer.ChipStack);
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

	private const string QueenHighStraightSpades = "Qs Jd 9h 8c 7s";
	private const string QueenHighStraightHearts = "Qh Jc 9d 8s 7h";
	private const string QueenHighStraightDiamonds = "Qd Js 9c 8h 7d";
	private const string BobLosingHand = "2s 3h 4d 5c 9h";

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] RoyalHearts =
	[
		(CardSuit.Hearts, CardSymbol.Ace),
		(CardSuit.Hearts, CardSymbol.King),
		(CardSuit.Hearts, CardSymbol.Queen),
		(CardSuit.Hearts, CardSymbol.Jack),
		(CardSuit.Hearts, CardSymbol.Ten)
	];
}
