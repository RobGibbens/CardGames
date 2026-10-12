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

	[Fact]
	public async Task Handle_WhenTheShortAllInHasTheBestHand_PaysTheMainPotAndTheSidePotSeparately()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedContributionsAsync(context, "SEVENCARDSTUD", null,
		[
			new Seat("Alice", 600, 400, false, false, "Ks Kd Kc Qs Qd 8h 7h"),
			new Seat("Bob", 0, 50, true, false, "Ah Ad As Ac Kh Qh Jh"),
			new Seat("Carol", 600, 400, false, false, "2c 4c 5d 6d 7c 8d Tc")
		], 850);
		var handler = StudHandler(scope);

		var result = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 150, ["Alice"] = 700 });
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Bob").AmountWon.Should().Be(150);
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Alice").AmountWon.Should().Be(700);
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Carol").AmountWon.Should().Be(0);
		await AssertStacksAsync(context, ("Alice", 1300), ("Bob", 150), ("Carol", 600));

		var pots = await context.Pots.Include(pot => pot.Contributions).ThenInclude(contribution => contribution.GamePlayer)
			.ThenInclude(gamePlayer => gamePlayer.Player).ToListAsync();
		pots.Should().HaveCount(2);
		pots.Single(pot => pot.PotType == PotType.Main).Amount.Should().Be(150);
		var side = pots.Single(pot => pot.PotType == PotType.Side);
		side.Amount.Should().Be(700);
		side.WinnerPayouts.Should().Contain("Alice").And.NotContain("Bob");
		side.Contributions.Should().NotContain(contribution =>
			contribution.GamePlayer.Player.Name == "Bob" && contribution.IsEligibleToWin);

		var again = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		again.IsT0.Should().BeTrue();
		again.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 150, ["Alice"] = 700 });
		await AssertStacksAsync(context, ("Alice", 1300), ("Bob", 150), ("Carol", 600));
		(await context.Pots.ToListAsync()).Should().OnlyContain(pot => pot.IsAwarded);
	}

	[Fact]
	public async Task Handle_WhenRazzShortAllInHasTheBestLow_PaysTheSidePotToTheSecondBestLow()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedContributionsAsync(context, "RAZZ", null,
		[
			new Seat("Alice", 600, 400, false, false, "8s 9d Th Jc 5d 6h 7h"),
			new Seat("Bob", 0, 50, true, false, "As 2d 3c 4h 5s 6d 7c"),
			new Seat("Carol", 600, 400, false, false, "Ks Kd Kc Qs Qd 9h 8h")
		], 850);
		var handler = StudHandler(scope);

		var result = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 150, ["Alice"] = 700 });
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Bob").AmountWon.Should().Be(150);
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Carol").AmountWon.Should().Be(0);
		await AssertStacksAsync(context, ("Alice", 1300), ("Bob", 150), ("Carol", 600));
	}

	[Fact]
	public async Task Handle_WhenAliceIsLoadedBeforeBob_PaysTheOddChipToTheSeatAfterTheDealer()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedContributionsAsync(context, "SEVENCARDSTUD", null,
		[
			new Seat("Alice", 100, 50, false, false, "Ah Ad 2c 3d 4h 5s 6c", 3),
			new Seat("Bob", 100, 50, false, false, "Ac As 2d 3h 4c 5d 6h", 1),
			new Seat("Carol", 100, 1, false, true, "", 2)
		], 101);
		var handler = StudHandler(scope);

		var result = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 51, ["Alice"] = 50 });
		await AssertStacksAsync(context, ("Bob", 151), ("Alice", 150), ("Carol", 100));
	}

	[Fact]
	public async Task Handle_WhenTheDealerIsSeatTwo_PaysTheOddChipToAlice()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedContributionsAsync(context, "SEVENCARDSTUD", null,
		[
			new Seat("Alice", 100, 50, false, false, "Ah Ad 2c 3d 4h 5s 6c", 3),
			new Seat("Bob", 100, 50, false, false, "Ac As 2d 3h 4c 5d 6h", 1),
			new Seat("Carol", 100, 1, false, true, "", 2)
		], 101);
		game.DealerPosition = 2;
		await context.SaveChangesAsync();
		var handler = StudHandler(scope);

		var result = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 51, ["Bob"] = 50 });
		await AssertStacksAsync(context, ("Alice", 151), ("Bob", 150));
	}

	[Fact]
	public async Task Handle_WhenHeadsUpShoveIsUncalled_RefundsTheExcessAndAwardsOnlyTheMatchedPot()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedContributionsAsync(context, "SEVENCARDSTUD", null,
		[
			new Seat("Alice", 600, 400, false, false, "Ah Ad As Ac Kh Qh Jh"),
			new Seat("Bob", 0, 100, true, false, "Ks Kd Kc Qs Qd 8h 7h")
		], 500);
		var handler = StudHandler(scope);

		var result = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 200 });
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Alice").AmountWon.Should().Be(200);
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Bob").AmountWon.Should().Be(0);
		var seats = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		seats.Single(seat => seat.Player.Name == "Alice").ChipStack.Should().Be(1100);
		seats.Single(seat => seat.Player.Name == "Alice").TotalContributedThisHand.Should().Be(100);
		seats.Single(seat => seat.Player.Name == "Bob").ChipStack.Should().Be(0);
	}

	[Fact]
	public async Task Handle_WhenTheAllInPlayerHasTheBestHand_StillRefundsTheUncalledChips()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedContributionsAsync(context, "SEVENCARDSTUD", null,
		[
			new Seat("Alice", 600, 400, false, false, "Ks Kd Kc Qs Qd 8h 7h"),
			new Seat("Bob", 0, 100, true, false, "Ah Ad As Ac Kh Qh Jh")
		], 500);
		var handler = StudHandler(scope);

		var result = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Alice").AmountWon.Should().Be(0);
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Bob").AmountWon.Should().Be(200);
		var seats = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		seats.Single(seat => seat.Player.Name == "Alice").ChipStack.Should().Be(900);
		seats.Single(seat => seat.Player.Name == "Bob").ChipStack.Should().Be(200);
	}

	[Fact]
	public async Task Handle_WhenEveryoneElseFolded_PaysThePotOnce()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedContributionsAsync(context, "SEVENCARDSTUD", null,
		[
			new Seat("Alice", 600, 400, false, false, "Ah Ad As Ac Kh Qh Jh"),
			new Seat("Bob", 400, 100, false, true, "Ks Kd Kc Qs Qd 8h 7h")
		], 500);
		var handler = StudHandler(scope);

		var result = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.WonByFold.Should().BeTrue();
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 500 });
		await AssertStacksAsync(context, ("Alice", 1100), ("Bob", 400));

		await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		await AssertStacksAsync(context, ("Alice", 1100), ("Bob", 400));
	}

	[Fact]
	public async Task Handle_WhenThePotDoesNotMatchContributions_WritesNothing()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var game = await SeedContributionsAsync(context, "SEVENCARDSTUD", null,
		[
			new Seat("Alice", 600, 250, false, false, "Ah Ad As Ac Kh Qh Jh"),
			new Seat("Bob", 600, 240, false, false, "Ks Kd Kc Qs Qd 8h 7h")
		], 500);
		var handler = StudHandler(scope);

		var result = await handler.Handle(new StudShowdown.PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(StudShowdown.PerformShowdownErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Be("Pot total 500 does not equal contributed chips 490.");
		var saved = await context.Games.Include(savedGame => savedGame.Pots).SingleAsync();
		saved.CurrentPhase.Should().Be(nameof(Phases.Showdown));
		saved.Pots.Should().ContainSingle().Which.Amount.Should().Be(500);
		saved.Pots.Should().OnlyContain(pot => !pot.IsAwarded);
		await AssertStacksAsync(context, ("Alice", 600), ("Bob", 600));
	}

	private static IRequestHandler<StudShowdown.PerformShowdownCommand,
		OneOf.OneOf<StudShowdown.PerformShowdownSuccessful, StudShowdown.PerformShowdownError>> StudHandler(
		AsyncServiceScope scope)
	{
		return scope.ServiceProvider
			.GetRequiredService<IRequestHandler<StudShowdown.PerformShowdownCommand,
				OneOf.OneOf<StudShowdown.PerformShowdownSuccessful, StudShowdown.PerformShowdownError>>>();
	}

	private sealed record Seat(
		string Name,
		int Stack,
		int Contributed,
		bool AllIn,
		bool Folded,
		string Cards,
		int? SeatNumber = null);

	private static async Task<Game> SeedContributionsAsync(
		CardsDbContext context,
		string gameTypeCode,
		string? currentHandGameTypeCode,
		IReadOnlyList<Seat> seats,
		int potAmount)
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
		context.Add(gameType);
		context.Add(game);
		for (var index = 0; index < seats.Count; index++)
		{
			var spec = seats[index];
			var player = CreatePlayer(spec.Name, now);
			var gamePlayer = CreateGamePlayer(game, player, spec.SeatNumber ?? index, now);
			gamePlayer.ChipStack = spec.Stack;
			gamePlayer.TotalContributedThisHand = spec.Contributed;
			gamePlayer.IsAllIn = spec.AllIn;
			gamePlayer.HasFolded = spec.Folded;
			context.Add(player);
			context.Add(gamePlayer);
			if (!spec.Folded)
				AddStudHand(context, game, gamePlayer, spec.Cards, now);
		}

		context.Pots.Add(new PotEntity
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = potAmount,
			CreatedAt = now
		});
		await context.SaveChangesAsync();
		return game;
	}

	private static async Task AssertStacksAsync(CardsDbContext context, params (string Name, int Stack)[] expected)
	{
		var seats = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		foreach (var (name, stack) in expected)
			seats.Single(seat => seat.Player.Name == name).ChipStack.Should().Be(stack);
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
