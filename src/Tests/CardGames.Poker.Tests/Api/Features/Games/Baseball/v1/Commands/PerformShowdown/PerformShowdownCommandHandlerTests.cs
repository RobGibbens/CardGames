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
using BaseballShowdown = CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.PerformShowdown;

namespace CardGames.Poker.Tests.Api.Features.Games.Baseball.v1.Commands.PerformShowdown;

public class PerformShowdownCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenThreesAreWild_RecordsTheWildSourceCardsInHistory()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedShowdownAsync(context);
		var handler = scope.ServiceProvider
			.GetRequiredService<IRequestHandler<BaseballShowdown.PerformShowdownCommand,
				OneOf.OneOf<BaseballShowdown.PerformShowdownSuccessful, BaseballShowdown.PerformShowdownError>>>();

		var result = await handler.Handle(new BaseballShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var winner = result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "P1");
		winner.HandType.Should().Be("StraightFlush");
		var highlighted = winner.BestCardIndexes!.Select(index => Format(winner.Cards[index])).ToList();
		highlighted.Should().Contain("3c");
		highlighted.Should().NotContain("2d");

		var history = provider.GetRequiredService<CapturingHandHistoryRecorder>().Last;
		history.Should().NotBeNull();
		var showdownCards = history!.PlayerResults.Single(player => player.PlayerName == "P1").ShowdownCards;
		showdownCards.Should().Contain("3c");
		showdownCards.Should().NotContain("2d");
	}

	[Fact]
	public async Task Handle_WhenTheShortAllInHasTheBestHand_PaysTheMainPotAndTheSidePotSeparately()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedShowdownAsync(context);
		var seats = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		var alice = seats.Single(seat => seat.Player.Name == "P1");
		var bob = seats.Single(seat => seat.Player.Name == "P2");
		alice.Player.Name = "Alice";
		alice.ChipStack = 600;
		alice.TotalContributedThisHand = 400;
		bob.Player.Name = "Bob";
		bob.ChipStack = 0;
		bob.IsAllIn = true;
		bob.TotalContributedThisHand = 50;
		var carolPlayer = CreatePlayer("Carol", DateTimeOffset.UtcNow);
		var carol = CreateGamePlayer(game, carolPlayer, 2, DateTimeOffset.UtcNow);
		carol.ChipStack = 600;
		carol.TotalContributedThisHand = 400;
		context.Add(carolPlayer);
		context.Add(carol);
		var pot = await context.Pots.SingleAsync();
		pot.Amount = 850;
		var existingCards = await context.GameCards.ToListAsync();
		context.GameCards.RemoveRange(existingCards);
		AddStudHand(context, game, alice, "Ks Kd Kc Qs Qd 8h 7h", DateTimeOffset.UtcNow);
		AddStudHand(context, game, bob, "Ah Ad As Ac Kh Th 8s", DateTimeOffset.UtcNow);
		AddStudHand(context, game, carol, "2c 4c 5d 6d 7c 8d Tc", DateTimeOffset.UtcNow);
		await context.SaveChangesAsync();
		var handler = scope.ServiceProvider
			.GetRequiredService<IRequestHandler<BaseballShowdown.PerformShowdownCommand,
				OneOf.OneOf<BaseballShowdown.PerformShowdownSuccessful, BaseballShowdown.PerformShowdownError>>>();

		var result = await handler.Handle(new BaseballShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 150, ["Alice"] = 700 });
		var stacks = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		stacks.Single(seat => seat.Player.Name == "Alice").ChipStack.Should().Be(1300);
		stacks.Single(seat => seat.Player.Name == "Bob").ChipStack.Should().Be(150);
		stacks.Single(seat => seat.Player.Name == "Carol").ChipStack.Should().Be(600);
	}

	[Fact]
	public async Task Handle_WhenAliceAndBobTieForAnOddPot_PaysSeatOneTheLeftoverChip()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedShowdownAsync(context);
		var seats = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		var alice = seats.Single(seat => seat.Player.Name == "P1");
		var bob = seats.Single(seat => seat.Player.Name == "P2");
		alice.Player.Name = "Alice";
		alice.SeatPosition = 3;
		alice.ChipStack = 100;
		alice.TotalContributedThisHand = 50;
		bob.Player.Name = "Bob";
		bob.SeatPosition = 1;
		bob.ChipStack = 100;
		bob.TotalContributedThisHand = 50;
		var now = DateTimeOffset.UtcNow;
		var carolPlayer = CreatePlayer("Carol", now);
		var carol = CreateGamePlayer(game, carolPlayer, 2, now);
		carol.HasFolded = true;
		carol.ChipStack = 100;
		carol.TotalContributedThisHand = 1;
		context.Add(carolPlayer);
		context.Add(carol);
		var pot = await context.Pots.SingleAsync();
		pot.Amount = 101;
		context.GameCards.RemoveRange(await context.GameCards.ToListAsync());
		AddStudHand(context, game, alice, "Ah Ad Kc Qd 8s 7h 6c", now);
		AddStudHand(context, game, bob, "Ac As Kd Qc 8d 7c 6h", now);
		await context.SaveChangesAsync();
		var handler = scope.ServiceProvider
			.GetRequiredService<IRequestHandler<BaseballShowdown.PerformShowdownCommand,
				OneOf.OneOf<BaseballShowdown.PerformShowdownSuccessful, BaseballShowdown.PerformShowdownError>>>();

		var result = await handler.Handle(new BaseballShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 51, ["Alice"] = 50 });
		var stacks = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		stacks.Single(seat => seat.Player.Name == "Bob").ChipStack.Should().Be(151);
		stacks.Single(seat => seat.Player.Name == "Alice").ChipStack.Should().Be(150);
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

	private static async Task<Game> SeedShowdownAsync(CardsDbContext context)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "BASEBALL",
			Name = "Baseball",
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
			DealerPosition = 0,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};
		var p1 = CreatePlayer("P1", now);
		var p2 = CreatePlayer("P2", now);
		var p1Seat = CreateGamePlayer(game, p1, 0, now);
		var p2Seat = CreateGamePlayer(game, p2, 1, now);

		context.AddRange(gameType, game, p1, p2, p1Seat, p2Seat);
		context.Pots.Add(new PotEntity
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 100,
			CreatedAt = now
		});
		AddStudHand(context, game, p1Seat, "Ah Ad Kh Qh Jh 3c 2d", now);
		AddStudHand(context, game, p2Seat, "7s 8c 6d 5h 4s Tc 2s", now);
		await context.SaveChangesAsync();
		return game;
	}

	private static void AddStudHand(CardsDbContext context, Game game, GamePlayer player, string cards,
		DateTimeOffset now)
	{
		var tokens = cards.Split(' ');
		for (var i = 0; i < tokens.Length; i++)
		{
			var isUpCard = i is >= 2 and <= 5;
			var isSeventh = i == 6;
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				GamePlayerId = player.Id,
				GamePlayer = player,
				HandNumber = game.CurrentHandNumber,
				DealOrder = i + 1,
				Location = isUpCard ? CardLocation.Board : CardLocation.Hole,
				IsVisible = isUpCard,
				Suit = ParseSuit(tokens[i][^1]),
				Symbol = ParseSymbol(tokens[i][..^1]),
				DealtAt = now,
				DealtAtPhase = isSeventh ? nameof(Phases.SeventhStreet) : nameof(Phases.ThirdStreet)
			});
		}
	}

	private static string Format(BaseballShowdown.ShowdownCard card)
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
			"J" => CardSymbol.Jack,
			"Q" => CardSymbol.Queen,
			"K" => CardSymbol.King,
			"A" => CardSymbol.Ace,
			"T" => CardSymbol.Ten,
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
			ChipStack = 200,
			StartingChips = 200,
			TotalContributedThisHand = 50,
			Status = GamePlayerStatus.Active,
			JoinedAt = now
		};
	}

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
