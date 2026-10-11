using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Evaluation;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;
using StudShowdown = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.PerformShowdown;

namespace CardGames.Poker.Tests.Api.Features.Games.SevenCardStud.v1.Commands.PerformShowdown;

public class PerformShowdownCommandHandlerTests
{
	private const string BetterLow = "Ks Js 6s 5h Qs 2h 6d";
	private const string WorseLowTwoPair = "Qc Kh 9d 7h 9h 7d 5s";

	[Fact]
	public async Task Handle_WhenRazz_AwardsBestLowAndDescribesLowHand()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedShowdownAsync(context, "RAZZ", null);
		var handler = scope.ServiceProvider
			.GetRequiredService<IRequestHandler<StudShowdown.PerformShowdownCommand,
				OneOf.OneOf<StudShowdown.PerformShowdownSuccessful, StudShowdown.PerformShowdownError>>>();

		var result = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Payouts.Should().ContainKey("P1");
		success.Payouts.Should().NotContainKey("P2");
		var winner = success.PlayerHands.Should().ContainSingle(hand => hand.IsWinner).Subject;
		winner.PlayerName.Should().Be("P1");
		winner.HandType.Should().Be("12-11-6-5-2 low");
		winner.HandType.Should().NotContain("Pair");
		winner.BestCardIndexes.Should().NotBeNull();
		winner.BestCardIndexes.Should().HaveCount(5);
		var bestCards = winner.BestCardIndexes!.Select(index => winner.Cards[index]).ToList();
		bestCards.Select(card => card.Symbol).Should().BeEquivalentTo(
		[
			CardSymbol.Deuce,
			CardSymbol.Five,
			CardSymbol.Six,
			CardSymbol.Jack,
			CardSymbol.Queen
		]);

		var history = provider.GetRequiredService<CapturingHandHistoryRecorder>().Last;
		history.Should().NotBeNull();
		history!.WinningHandDescription.Should().Be("12-11-6-5-2 low");
		var p1Cards = history.PlayerResults.Single(player => player.PlayerName == "P1").ShowdownCards;
		p1Cards.Should().NotBeNull();
		p1Cards.Should().HaveCount(5);
		p1Cards.Should().NotContain("Ks");
		p1Cards.Should().Contain(["2h", "5h", "Js", "Qs"]);
	}

	[Fact]
	public async Task Handle_WhenDealersChoiceHandIsRazz_AwardsBestLow()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedShowdownAsync(context, "SEVENCARDSTUD", "RAZZ");
		var handler = scope.ServiceProvider
			.GetRequiredService<IRequestHandler<StudShowdown.PerformShowdownCommand,
				OneOf.OneOf<StudShowdown.PerformShowdownSuccessful, StudShowdown.PerformShowdownError>>>();

		var result = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts.Should().ContainKey("P1");
		result.AsT0.Payouts.Should().NotContainKey("P2");
		result.AsT0.PlayerHands.Single(hand => hand.IsWinner).HandType.Should().Be("12-11-6-5-2 low");
	}

	[Fact]
	public async Task Handle_WhenSevenCardStud_StillAwardsHighestHighHand()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedShowdownAsync(context, "SEVENCARDSTUD", null);
		var handler = scope.ServiceProvider
			.GetRequiredService<IRequestHandler<StudShowdown.PerformShowdownCommand,
				OneOf.OneOf<StudShowdown.PerformShowdownSuccessful, StudShowdown.PerformShowdownError>>>();

		var result = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Payouts.Should().ContainKey("P2");
		success.Payouts.Should().NotContainKey("P1");
		var winner = success.PlayerHands.Single(hand => hand.IsWinner);
		winner.PlayerName.Should().Be("P2");
		winner.HandType.Should().Be("TwoPair");
		winner.HandType.Should().NotContain("low");
	}

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
		services.AddDbContext<CardsDbContext>(options =>
			options.UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()));
		services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CardsDbContext).Assembly));
		services.AddSingleton<IGameFlowHandlerFactory, GameFlowHandlerFactory>();
		services.AddSingleton<IHandEvaluatorFactory, HandEvaluatorFactory>();
		services.AddSingleton<CapturingHandHistoryRecorder>();
		services.AddSingleton<IHandHistoryRecorder>(sp => sp.GetRequiredService<CapturingHandHistoryRecorder>());
		services.AddSingleton<IHandSettlementService, NoOpHandSettlementService>();
		return services.BuildServiceProvider();
	}

	private static async Task<Game> SeedShowdownAsync(CardsDbContext context, string gameTypeCode,
		string? currentHandGameTypeCode)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = gameTypeCode,
			Name = gameTypeCode,
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
			CurrentHandGameTypeCode = currentHandGameTypeCode,
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
		AddStudHand(context, game, p1Seat, BetterLow, now);
		AddStudHand(context, game, p2Seat, WorseLowTwoPair, now);
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
			"5" => CardSymbol.Five,
			"6" => CardSymbol.Six,
			"7" => CardSymbol.Seven,
			"9" => CardSymbol.Nine,
			"J" => CardSymbol.Jack,
			"Q" => CardSymbol.Queen,
			"K" => CardSymbol.King,
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
