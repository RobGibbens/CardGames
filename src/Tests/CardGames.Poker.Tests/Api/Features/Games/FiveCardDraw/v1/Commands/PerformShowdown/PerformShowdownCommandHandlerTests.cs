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
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.DrawHands;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.FiveCardDraw.v1.Commands.PerformShowdown;

public class PerformShowdownCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenMainAndSidePotsTie_PaysOddChipsPerPotClockwiseFromTheDealer()
	{
		var aliceHand = new DrawHand(RoyalFlush);
		var bobHand = new DrawHand(RoyalFlush);
		var carolHand = new DrawHand(RoyalFlush);
		aliceHand.Strength.Should().Be(bobHand.Strength);
		aliceHand.Strength.Should().Be(carolHand.Strength);

		await using var context = CreateContext();
		var game = await SeedShowdownAsync(context);
		var settlement = Substitute.For<IHandSettlementService>();
		var sut = CreateSut(context, settlement);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var payouts = result.AsT0.Payouts;
		payouts["Carol"].Should().Be(5);
		payouts["Alice"].Should().Be(3);
		payouts["Bob"].Should().Be(1);
		await AssertStacksAsync(context, ("Carol", 105), ("Alice", 103), ("Bob", 101));
		(await context.GamePlayers.SumAsync(gamePlayer => gamePlayer.ChipStack)).Should().Be(309);

		var pots = await context.Pots
			.Where(pot => pot.GameId == game.Id && pot.HandNumber == game.CurrentHandNumber)
			.OrderBy(pot => pot.PotOrder)
			.ToListAsync();
		var main = ReadWinnerPayouts(pots[0].WinnerPayouts);
		main["Carol"].Should().Be(2);
		main["Alice"].Should().Be(1);
		main["Bob"].Should().Be(1);
		pots[0].WinnerPayouts.Should().NotContain("\"amount\":5");
		pots[0].WinnerPayouts.Should().NotContain("\"amount\":3");

		var side = ReadWinnerPayouts(pots[1].WinnerPayouts);
		side["Carol"].Should().Be(3);
		side["Alice"].Should().Be(2);
		side.Should().NotContainKey("Bob");
		pots[1].WinnerPayouts.Should().NotContain("\"amount\":5");
		pots[1].WinnerPayouts.Should().NotContain("Bob");

		await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		await AssertStacksAsync(context, ("Carol", 105), ("Alice", 103), ("Bob", 101));
		await settlement.Received(1).SettleHandAsync(
			Arg.Any<Game>(),
			Arg.Is<Dictionary<string, int>>(paid =>
				paid.Count == 3 && paid["Carol"] == 5 && paid["Alice"] == 3 && paid["Bob"] == 1),
			Arg.Any<CancellationToken>());
	}

	private static PerformShowdownCommandHandler CreateSut(
		CardsDbContext context,
		IHandSettlementService? settlement = null)
	{
		return new PerformShowdownCommandHandler(
			context,
			Substitute.For<IHandHistoryRecorder>(),
			settlement ?? Substitute.For<IHandSettlementService>(),
			new HandEvaluatorFactory());
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
			.Options;

		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedShowdownAsync(CardsDbContext context)
	{
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = nameof(Phases.Showdown),
			CurrentHandNumber = 1,
			CurrentHandGameTypeCode = HandEvaluatorFactory.FiveCardDrawCode,
			DealerPosition = 3,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};
		context.Games.Add(game);

		var gamePlayers = new Dictionary<string, GamePlayer>();
		foreach (var (name, seat) in Seats)
		{
			var player = CreatePlayer(name, now);
			var gamePlayer = CreateGamePlayer(game, player, seat, now);
			gamePlayers[name] = gamePlayer;
			context.Players.Add(player);
			context.GamePlayers.Add(gamePlayer);

			for (var cardIndex = 0; cardIndex < RoyalHearts.Length; cardIndex++)
			{
				context.GameCards.Add(CreateHoleCard(
					game,
					gamePlayer,
					cardIndex + 1,
					RoyalHearts[cardIndex].Suit,
					RoyalHearts[cardIndex].Symbol,
					now));
			}
		}

		var mainPot = CreatePot(game, PotType.Main, 0, 4, now);
		AddContribution(mainPot, gamePlayers["Alice"], now, true);
		AddContribution(mainPot, gamePlayers["Bob"], now, true);
		AddContribution(mainPot, gamePlayers["Carol"], now, true);

		var sidePot = CreatePot(game, PotType.Side, 1, 5, now);
		AddContribution(sidePot, gamePlayers["Alice"], now, true);
		AddContribution(sidePot, gamePlayers["Bob"], now, false);
		AddContribution(sidePot, gamePlayers["Carol"], now, true);

		context.Pots.AddRange(mainPot, sidePot);
		await context.SaveChangesAsync();
		return game;
	}

	private static Pot CreatePot(Game game, PotType potType, int potOrder, int amount, DateTimeOffset now)
	{
		return new Pot
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = potType,
			PotOrder = potOrder,
			Amount = amount,
			IsAwarded = false,
			CreatedAt = now
		};
	}

	private static void AddContribution(Pot pot, GamePlayer gamePlayer, DateTimeOffset now, bool isEligibleToWin)
	{
		pot.Contributions.Add(new PotContribution
		{
			Id = Guid.NewGuid(),
			PotId = pot.Id,
			Pot = pot,
			GamePlayerId = gamePlayer.Id,
			GamePlayer = gamePlayer,
			Amount = pot.Amount,
			IsEligibleToWin = isEligibleToWin,
			ContributedAt = now
		});
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

	private static Dictionary<string, int> ReadWinnerPayouts(string? json)
	{
		using var document = JsonDocument.Parse(json!);
		var payouts = new Dictionary<string, int>();
		foreach (var item in document.RootElement.EnumerateArray())
			payouts[item.GetProperty("playerName").GetString()!] = item.GetProperty("amount").GetInt32();

		return payouts;
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

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] RoyalHearts =
	[
		(CardSuit.Hearts, CardSymbol.Ace),
		(CardSuit.Hearts, CardSymbol.King),
		(CardSuit.Hearts, CardSymbol.Queen),
		(CardSuit.Hearts, CardSymbol.Jack),
		(CardSuit.Hearts, CardSymbol.Ten)
	];
}
