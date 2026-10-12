using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;
using FtqShowdown = CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.PerformShowdown;

namespace CardGames.Poker.Tests.Api.Features.Games.FollowTheQueen.v1.Commands.PerformShowdown;

public class PerformShowdownCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenQueenIsFollowedByNine_PaysBobQuads()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedShowdownAsync(context, QueenFollowedByNine());
		var handler = Handler(scope);

		var result = await handler.Handle(new FtqShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 300 });
		success.PlayerHands.Single(hand => hand.PlayerName == "Bob").HandType.Should().Be("Quads");
		success.PlayerHands.Single(hand => hand.PlayerName == "Alice").HandType.Should().Be("Trips");
		success.PlayerHands.Single(hand => hand.PlayerName == "Carol").HandType.Should().Be("Flush");
		await AssertStacksAsync(context, alice: 900, bob: 1200, carol: 900);

		var second = await handler.Handle(new FtqShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		second.IsT0.Should().BeTrue();
		await AssertStacksAsync(context, alice: 900, bob: 1200, carol: 900);
	}

	[Fact]
	public async Task Handle_WhenQueenIsTheLastFaceUpCard_PaysAliceTrips()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedShowdownAsync(context, QueenLast());
		var handler = Handler(scope);

		var result = await handler.Handle(new FtqShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 300 });
		success.PlayerHands.Single(hand => hand.PlayerName == "Alice").HandType.Should().Be("Trips");
		success.PlayerHands.Single(hand => hand.PlayerName == "Bob").HandType.Should().Be("TwoPair");
	}

	[Fact]
	public async Task Handle_WhenTheLastFaceUpCardIsAQueen_HighlightsTheWildStraight()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedShowdownAsync(context, QueenWildStraight());
		var handler = Handler(scope);

		var result = await handler.Handle(new FtqShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 300 });
		var alice = success.PlayerHands.Single(hand => hand.PlayerName == "Alice");
		alice.HandType.Should().Be("Straight");
		alice.BestCardIndexes.Should().NotBeNull();
		alice.BestCardIndexes.Should().HaveCount(5);
		var highlighted = alice.BestCardIndexes!.Select(index => Format(alice.Cards[index])).ToList();
		highlighted.Should().BeEquivalentTo("Qh", "2c", "3d", "4h", "5s");
		highlighted.Should().NotContain("Kd");
		highlighted.Should().NotContain("9c");

		var history = provider.GetRequiredService<CapturingHandHistoryRecorder>().Last;
		history.Should().NotBeNull();
		history!.PlayerResults.Single(player => player.PlayerName == "Alice").ShowdownCards
			.Should().BeEquivalentTo("Qh", "2c", "3d", "4h", "5s");
		await AssertStacksAsync(context, alice: 1200, bob: 900, carol: 900);
	}

	[Fact]
	public async Task Handle_WhenTheShortAllInHasTheBestHand_PaysTheMainPotAndTheSidePotSeparately()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedShowdownAsync(context, QueenLast());
		var seats = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		foreach (var seat in seats)
		{
			seat.ChipStack = seat.Player.Name == "Bob" ? 0 : 600;
			seat.TotalContributedThisHand = seat.Player.Name == "Bob" ? 50 : 400;
			seat.IsAllIn = seat.Player.Name == "Bob";
		}

		var pot = await context.Pots.SingleAsync();
		pot.Amount = 850;
		var existingCards = await context.GameCards.ToListAsync();
		context.GameCards.RemoveRange(existingCards);
		AddDealtHand(context, game, seats.Single(seat => seat.Player.Name == "Alice"), "Ks Kd Kc Js Jd 8h 7h");
		AddDealtHand(context, game, seats.Single(seat => seat.Player.Name == "Bob"), "Ah Ad As Ac Kh Th 8s");
		AddDealtHand(context, game, seats.Single(seat => seat.Player.Name == "Carol"), "2c 4c 5d 6d 7c 8d Tc");
		await context.SaveChangesAsync();
		var handler = Handler(scope);

		var result = await handler.Handle(new FtqShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 150, ["Alice"] = 700 });
		await AssertStacksAsync(context, alice: 1300, bob: 150, carol: 600);
	}

	[Fact]
	public async Task Handle_WhenAliceAndBobTieForAnOddPot_PaysSeatOneTheLeftoverChip()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedShowdownAsync(context, []);
		var seats = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		var alice = seats.Single(seat => seat.Player.Name == "Alice");
		var bob = seats.Single(seat => seat.Player.Name == "Bob");
		var carol = seats.Single(seat => seat.Player.Name == "Carol");
		alice.SeatPosition = 3;
		alice.ChipStack = 100;
		alice.TotalContributedThisHand = 50;
		bob.SeatPosition = 1;
		bob.ChipStack = 100;
		bob.TotalContributedThisHand = 50;
		carol.SeatPosition = 2;
		carol.HasFolded = true;
		carol.ChipStack = 100;
		carol.TotalContributedThisHand = 1;
		game.DealerPosition = 0;
		var pot = await context.Pots.SingleAsync();
		pot.Amount = 101;
		AddDealtHand(context, game, alice, "Ah Ad Kc Qd 8s 7h 6c");
		AddDealtHand(context, game, bob, "Ac As Kd Qc 8d 7c 6h");
		await context.SaveChangesAsync();
		var handler = Handler(scope);

		var result = await handler.Handle(new FtqShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 51, ["Alice"] = 50 });
		await AssertStacksAsync(context, alice: 150, bob: 151, carol: 100);
	}

	private static IRequestHandler<FtqShowdown.PerformShowdownCommand,
		OneOf.OneOf<FtqShowdown.PerformShowdownSuccessful, FtqShowdown.PerformShowdownError>> Handler(AsyncServiceScope scope)
	{
		return scope.ServiceProvider
			.GetRequiredService<IRequestHandler<FtqShowdown.PerformShowdownCommand,
				OneOf.OneOf<FtqShowdown.PerformShowdownSuccessful, FtqShowdown.PerformShowdownError>>>();
	}

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
		services.AddDbContext<CardsDbContext>(options =>
			options.UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()));
		services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CardsDbContext).Assembly));
		services.AddSingleton<CapturingHandHistoryRecorder>();
		services.AddSingleton<IHandHistoryRecorder>(sp => sp.GetRequiredService<CapturingHandHistoryRecorder>());
		services.AddSingleton<IHandSettlementService, NoOpHandSettlementService>();
		return services.BuildServiceProvider();
	}

	private static async Task AssertStacksAsync(CardsDbContext context, int alice, int bob, int carol)
	{
		var players = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		players.Single(gamePlayer => gamePlayer.Player.Name == "Alice").ChipStack.Should().Be(alice);
		players.Single(gamePlayer => gamePlayer.Player.Name == "Bob").ChipStack.Should().Be(bob);
		players.Single(gamePlayer => gamePlayer.Player.Name == "Carol").ChipStack.Should().Be(carol);
	}

	private static async Task<Game> SeedShowdownAsync(CardsDbContext context, IReadOnlyList<SeedCard> cards)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "FOLLOWTHEQUEEN",
			Name = "Follow the Queen",
			BettingStructure = BettingStructure.Ante,
			MinPlayers = 2,
			MaxPlayers = 8,
			InitialHoleCards = 2,
			InitialBoardCards = 1,
			MaxCommunityCards = 0,
			MaxPlayerCards = 7,
			HasDrawPhase = false,
			MaxDiscards = 0,
			CreatedAt = now,
			UpdatedAt = now
		};
		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = gameType.Id,
			GameType = gameType,
			CurrentPhase = nameof(Phases.Showdown),
			CurrentHandNumber = 1,
			DealerPosition = 2,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};
		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);
		var carol = CreatePlayer("Carol", now);
		var aliceSeat = CreateGamePlayer(game, alice, 0, now);
		var bobSeat = CreateGamePlayer(game, bob, 1, now);
		var carolSeat = CreateGamePlayer(game, carol, 2, now);
		var seats = new Dictionary<string, GamePlayer>
		{
			["Alice"] = aliceSeat,
			["Bob"] = bobSeat,
			["Carol"] = carolSeat
		};

		context.AddRange(gameType, game, alice, bob, carol, aliceSeat, bobSeat, carolSeat);
		context.Pots.Add(new PotEntity
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 300,
			CreatedAt = now
		});

		foreach (var card in cards)
			AddCard(context, game, seats[card.Player], card, now);

		await context.SaveChangesAsync();
		return game;
	}

	/// <summary>
	///     Dealer seat 2. Third-street upcards are Qh, 9c, 4d. Queens and nines are wild.
	/// </summary>
	private static IReadOnlyList<SeedCard> QueenFollowedByNine()
	{
		return
		[
			Card("Alice", "Ah", 1, nameof(Phases.ThirdStreet), false),
			Card("Alice", "Ac", 2, nameof(Phases.ThirdStreet), false),
			Card("Alice", "Qh", 3, nameof(Phases.ThirdStreet), true),
			Card("Alice", "2s", 4, nameof(Phases.FourthStreet), true),
			Card("Alice", "7d", 5, nameof(Phases.FifthStreet), true),
			Card("Alice", "8d", 6, nameof(Phases.SixthStreet), true),
			Card("Alice", "3d", 7, nameof(Phases.SeventhStreet), false),
			Card("Bob", "Kh", 4, nameof(Phases.ThirdStreet), false),
			Card("Bob", "Kd", 5, nameof(Phases.ThirdStreet), false),
			Card("Bob", "9c", 6, nameof(Phases.ThirdStreet), true),
			Card("Bob", "3h", 4, nameof(Phases.FourthStreet), true),
			Card("Bob", "9d", 5, nameof(Phases.FifthStreet), true),
			Card("Bob", "4h", 6, nameof(Phases.SixthStreet), true),
			Card("Bob", "8s", 7, nameof(Phases.SeventhStreet), false),
			Card("Carol", "6s", 7, nameof(Phases.ThirdStreet), false),
			Card("Carol", "3c", 8, nameof(Phases.ThirdStreet), false),
			Card("Carol", "4d", 9, nameof(Phases.ThirdStreet), true),
			Card("Carol", "5c", 4, nameof(Phases.FourthStreet), true),
			Card("Carol", "7c", 5, nameof(Phases.FifthStreet), true),
			Card("Carol", "8c", 6, nameof(Phases.SixthStreet), true),
			Card("Carol", "9h", 7, nameof(Phases.SeventhStreet), false)
		];
	}

	/// <summary>
	///     Carol's sixth-street queen is the last face-up card. Her third-street nine has a higher DealOrder.
	///     Alice's hole queen is the only queen in her hand. Bob's nines are natural.
	/// </summary>
	private static IReadOnlyList<SeedCard> QueenLast()
	{
		return
		[
			Card("Alice", "Ah", 1, nameof(Phases.ThirdStreet), false),
			Card("Alice", "Ac", 2, nameof(Phases.ThirdStreet), false),
			Card("Alice", "2s", 3, nameof(Phases.ThirdStreet), true),
			Card("Alice", "8d", 4, nameof(Phases.FourthStreet), true),
			Card("Alice", "4d", 5, nameof(Phases.FifthStreet), true),
			Card("Alice", "6c", 6, nameof(Phases.SixthStreet), true),
			Card("Alice", "Qd", 7, nameof(Phases.SeventhStreet), false),
			Card("Bob", "9h", 4, nameof(Phases.ThirdStreet), false),
			Card("Bob", "9d", 5, nameof(Phases.ThirdStreet), false),
			Card("Bob", "Kh", 6, nameof(Phases.ThirdStreet), true),
			Card("Bob", "8c", 4, nameof(Phases.FourthStreet), true),
			Card("Bob", "Kd", 5, nameof(Phases.FifthStreet), true),
			Card("Bob", "7c", 6, nameof(Phases.SixthStreet), true),
			Card("Bob", "5h", 7, nameof(Phases.SeventhStreet), false),
			Card("Carol", "3s", 7, nameof(Phases.ThirdStreet), false),
			Card("Carol", "3d", 8, nameof(Phases.ThirdStreet), false),
			Card("Carol", "9c", 9, nameof(Phases.ThirdStreet), true),
			Card("Carol", "2h", 4, nameof(Phases.FourthStreet), true),
			Card("Carol", "6d", 5, nameof(Phases.FifthStreet), true),
			Card("Carol", "Qc", 6, nameof(Phases.SixthStreet), true),
			Card("Carol", "8s", 7, nameof(Phases.SeventhStreet), false)
		];
	}

	/// <summary>
	///     Alice's sixth-street queen is the last face-up card, so only queens are wild.
	///     Bob has king high with no queen and no straight.
	/// </summary>
	private static IReadOnlyList<SeedCard> QueenWildStraight()
	{
		return
		[
			Card("Alice", "2c", 1, nameof(Phases.ThirdStreet), false),
			Card("Alice", "3d", 2, nameof(Phases.ThirdStreet), false),
			Card("Alice", "4h", 3, nameof(Phases.ThirdStreet), true),
			Card("Alice", "5s", 4, nameof(Phases.FourthStreet), true),
			Card("Alice", "9c", 5, nameof(Phases.FifthStreet), true),
			Card("Alice", "Qh", 6, nameof(Phases.SixthStreet), true),
			Card("Alice", "Kd", 7, nameof(Phases.SeventhStreet), false),
			Card("Bob", "Ah", 4, nameof(Phases.ThirdStreet), false),
			Card("Bob", "7c", 5, nameof(Phases.ThirdStreet), false),
			Card("Bob", "Kd", 6, nameof(Phases.ThirdStreet), true),
			Card("Bob", "9d", 4, nameof(Phases.FourthStreet), true),
			Card("Bob", "8s", 5, nameof(Phases.FifthStreet), true),
			Card("Bob", "3h", 7, nameof(Phases.SeventhStreet), false)
		];
	}

	private static string Format(FtqShowdown.ShowdownCard card)
	{
		var rank = card.Symbol switch
		{
			CardSymbol.Ace => "A",
			CardSymbol.King => "K",
			CardSymbol.Queen => "Q",
			CardSymbol.Jack => "J",
			CardSymbol.Ten => "T",
			CardSymbol.Nine => "9",
			CardSymbol.Eight => "8",
			CardSymbol.Seven => "7",
			CardSymbol.Six => "6",
			CardSymbol.Five => "5",
			CardSymbol.Four => "4",
			CardSymbol.Three => "3",
			CardSymbol.Deuce => "2",
			_ => "?"
		};
		var suit = card.Suit switch
		{
			CardSuit.Clubs => "c",
			CardSuit.Diamonds => "d",
			CardSuit.Hearts => "h",
			CardSuit.Spades => "s",
			_ => "?"
		};
		return $"{rank}{suit}";
	}

	private static SeedCard Card(string player, string card, int dealOrder, string phase, bool visible)
	{
		return new SeedCard(player, card, dealOrder, phase, visible);
	}

	private static void AddDealtHand(CardsDbContext context, Game game, GamePlayer player, string cards)
	{
		var tokens = cards.Split(' ');
		string[] phases =
		[
			nameof(Phases.ThirdStreet),
			nameof(Phases.ThirdStreet),
			nameof(Phases.ThirdStreet),
			nameof(Phases.FourthStreet),
			nameof(Phases.FifthStreet),
			nameof(Phases.SixthStreet),
			nameof(Phases.SeventhStreet)
		];
		for (var i = 0; i < tokens.Length; i++)
		{
			var visible = i is >= 2 and <= 5;
			AddCard(
				context,
				game,
				player,
				new SeedCard(player.Player.Name, tokens[i], player.SeatPosition * 10 + i + 1, phases[i], visible),
				DateTimeOffset.UtcNow);
		}
	}

	private static void AddCard(CardsDbContext context, Game game, GamePlayer player, SeedCard card,
		DateTimeOffset now)
	{
		context.GameCards.Add(new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player.Id,
			GamePlayer = player,
			HandNumber = game.CurrentHandNumber,
			DealOrder = card.DealOrder,
			Location = card.Visible ? CardLocation.Board : CardLocation.Hole,
			IsVisible = card.Visible,
			Suit = ParseSuit(card.Card[^1]),
			Symbol = ParseSymbol(card.Card[..^1]),
			DealtAt = now,
			DealtAtPhase = card.Phase
		});
	}

	private static CardSuit ParseSuit(char suit)
	{
		return suit switch
		{
			'h' => CardSuit.Hearts,
			'd' => CardSuit.Diamonds,
			's' => CardSuit.Spades,
			'c' => CardSuit.Clubs,
			_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit")
		};
	}

	private static CardSymbol ParseSymbol(string symbol)
	{
		return symbol switch
		{
			"2" => CardSymbol.Deuce,
			"3" => CardSymbol.Three,
			"4" => CardSymbol.Four,
			"5" => CardSymbol.Five,
			"6" => CardSymbol.Six,
			"7" => CardSymbol.Seven,
			"8" => CardSymbol.Eight,
			"9" => CardSymbol.Nine,
			"T" => CardSymbol.Ten,
			"J" => CardSymbol.Jack,
			"Q" => CardSymbol.Queen,
			"K" => CardSymbol.King,
			"A" => CardSymbol.Ace,
			_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown symbol")
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

	private static GamePlayer CreateGamePlayer(Game game, Player player, int seat, DateTimeOffset now)
	{
		return new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = player.Id,
			Player = player,
			SeatPosition = seat,
			ChipStack = 900,
			StartingChips = 1000,
			TotalContributedThisHand = 100,
			Status = GamePlayerStatus.Active,
			JoinedAt = now
		};
	}

	private sealed record SeedCard(string Player, string Card, int DealOrder, string Phase, bool Visible);

	private sealed class CapturingHandHistoryRecorder : IHandHistoryRecorder
	{
		public RecordHandHistoryParameters? Last { get; private set; }

		public Task<bool> RecordHandHistoryAsync(RecordHandHistoryParameters parameters,
			CancellationToken cancellationToken = default)
		{
			Last = parameters;
			return Task.FromResult(true);
		}
	}

	private sealed class NoOpHandSettlementService : IHandSettlementService
	{
		public Task SettleHandAsync(Game game, Dictionary<string, int> payouts, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}
}
