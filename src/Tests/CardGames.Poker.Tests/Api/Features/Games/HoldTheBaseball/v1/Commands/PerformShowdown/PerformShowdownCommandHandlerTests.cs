using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using CoreCard = CardGames.Core.French.Cards.Card;
using CoreSuit = CardGames.Core.French.Cards.Suit;
using CoreSymbol = CardGames.Core.French.Cards.Symbol;
using GenericPerformShowdownCommand = CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownCommand;
using GenericPerformShowdownCommandHandler = CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownCommandHandler;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.HoldTheBaseball.v1.Commands.PerformShowdown;

public class PerformShowdownCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(Guid.NewGuid()), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PerformShowdownErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInShowdown_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, nameof(Phases.River));
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PerformShowdownErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain(nameof(Phases.River));
	}

	[Fact]
	public async Task Handle_WhenPlayersDoNotHaveEnoughHoleCards_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, nameof(Phases.Showdown), 1);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		var stacks = await context.GamePlayers.Select(gamePlayer => gamePlayer.ChipStack).ToListAsync();
		stacks.Should().Equal(200, 200);
	}

	[Fact]
	public async Task Handle_WhenAliceIsShortAllIn_PaysTheSidePotToTheTiedCoveringHands()
	{
		await using var context = CreateContext();
		var game = await SeedSidePotAsync(context);
		var settlement = Substitute.For<IHandSettlementService>();
		var sut = CreateSut(context, settlement);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);
		var payouts = result.AsT0.Payouts;
		payouts["Alice"].Should().Be(150);
		payouts["Bob"].Should().Be(100);
		payouts["Carol"].Should().Be(100);
		await AssertStacksAsync(context, ("Alice", 150), ("Bob", 200), ("Carol", 200));
		var stacks = await context.GamePlayers.SumAsync(gamePlayer => gamePlayer.ChipStack);
		stacks.Should().Be(550);

		var pots = await context.Pots
			.Where(pot => pot.GameId == game.Id && pot.HandNumber == 1)
			.OrderBy(pot => pot.PotOrder)
			.ToListAsync();
		pots.Should().HaveCount(2);
		pots.Should().OnlyContain(pot => pot.IsAwarded);
		pots[0].PotOrder.Should().Be(0);
		pots[0].Amount.Should().Be(150);
		Names(pots[0].WinnerPayouts).Should().Equal("Alice");
		pots[1].PotOrder.Should().Be(1);
		pots[1].Amount.Should().Be(200);
		Amounts(pots[1].WinnerPayouts).Should().BeEquivalentTo(new Dictionary<string, int>
		{
			["Bob"] = 100,
			["Carol"] = 100
		});
		pots.Should().OnlyContain(pot => pot.WinnerPayouts != null && !pot.WinnerPayouts.Contains("350"));

		var repeat = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);
		repeat.IsT0.Should().BeTrue(repeat.IsT1 ? repeat.AsT1.Message : string.Empty);
		await AssertStacksAsync(context, ("Alice", 150), ("Bob", 200), ("Carol", 200));
		await settlement.Received(1).SettleHandAsync(
			Arg.Any<Game>(),
			Arg.Is<Dictionary<string, int>>(paid =>
				paid.Count == 3 && paid["Alice"] == 150 && paid["Bob"] == 100 && paid["Carol"] == 100),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Handle_WhenThreeHandsTie_GivesTheLeftoverChipToTheSeatClockwiseFromTheDealer()
	{
		var board = new[]
		{
			new CoreCard(CoreSuit.Clubs, CoreSymbol.Deuce),
			new CoreCard(CoreSuit.Diamonds, CoreSymbol.Four),
			new CoreCard(CoreSuit.Hearts, CoreSymbol.Five),
			new CoreCard(CoreSuit.Spades, CoreSymbol.Six),
			new CoreCard(CoreSuit.Clubs, CoreSymbol.Eight)
		};
		var aliceHand = new HoldTheBaseballHand(
			[new CoreCard(CoreSuit.Hearts, CoreSymbol.Ace), new CoreCard(CoreSuit.Diamonds, CoreSymbol.King)],
			board);
		var bobHand = new HoldTheBaseballHand(
			[new CoreCard(CoreSuit.Diamonds, CoreSymbol.Ace), new CoreCard(CoreSuit.Clubs, CoreSymbol.King)],
			board);
		var carolHand = new HoldTheBaseballHand(
			[new CoreCard(CoreSuit.Spades, CoreSymbol.Ace), new CoreCard(CoreSuit.Hearts, CoreSymbol.King)],
			board);
		aliceHand.Strength.Should().Be(bobHand.Strength);
		bobHand.Strength.Should().Be(carolHand.Strength);

		await using var context = CreateContext();
		var game = await SeedLeftoverChipAsync(context);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);
		var payouts = result.AsT0.Payouts;
		payouts["Carol"].Should().Be(6);
		payouts["Alice"].Should().Be(5);
		payouts["Bob"].Should().Be(5);
		await AssertStacksAsync(context, ("Carol", 106), ("Alice", 105), ("Bob", 105), ("Dave", 99));

		var pot = await context.Pots.SingleAsync(item => item.GameId == game.Id && item.HandNumber == 1);
		pot.IsAwarded.Should().BeTrue();
		pot.Amount.Should().Be(16);
		Amounts(pot.WinnerPayouts)["Alice"].Should().NotBe(6);

		await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);
		await AssertStacksAsync(context, ("Carol", 106), ("Alice", 105), ("Bob", 105), ("Dave", 99));
	}

	private static PerformShowdownCommandHandler CreateSut(
		CardsDbContext context,
		IHandSettlementService? settlement = null)
	{
		settlement ??= Substitute.For<IHandSettlementService>();
		var flow = Substitute.For<IGameFlowHandlerFactory>();
		flow.GetHandler("HOLDTHEBASEBALL").Returns(new HoldTheBaseballFlowHandler());
		var generic = new GenericPerformShowdownCommandHandler(
			context,
			flow,
			new HandEvaluatorFactory(),
			Substitute.For<IHandHistoryRecorder>(),
			settlement,
			Substitute.For<ILogger<GenericPerformShowdownCommandHandler>>());
		var mediator = Substitute.For<IMediator>();
		mediator.Send(
				Arg.Any<GenericPerformShowdownCommand>(),
				Arg.Any<CancellationToken>())
			.Returns(call => generic.Handle(
				call.Arg<GenericPerformShowdownCommand>(),
				call.Arg<CancellationToken>()));
		return new PerformShowdownCommandHandler(mediator);
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
			.Options;

		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedGameAsync(CardsDbContext context, string currentPhase,
		int holeCardsPerPlayer = 0)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = CreateGameType(now);
		var game = CreateGame(gameType, currentPhase, dealerPosition: 0, now);
		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);
		var aliceGamePlayer = CreateGamePlayer(game, alice, 0, 200, 0, false, now);
		var bobGamePlayer = CreateGamePlayer(game, bob, 1, 200, 0, false, now);

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(alice, bob);
		context.GamePlayers.AddRange(aliceGamePlayer, bobGamePlayer);
		context.Pots.Add(CreatePot(game, 120, now));

		for (var cardIndex = 0; cardIndex < holeCardsPerPlayer; cardIndex++)
		{
			context.GameCards.Add(CreateHoleCard(game, aliceGamePlayer, cardIndex + 1, CardSuit.Hearts,
				(CardSymbol)(cardIndex + 13), now));
			context.GameCards.Add(CreateHoleCard(game, bobGamePlayer, cardIndex + 1, CardSuit.Spades,
				(CardSymbol)(cardIndex + 11), now));
		}

		await context.SaveChangesAsync();
		return game;
	}

	private static async Task<Game> SeedSidePotAsync(CardsDbContext context)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = CreateGameType(now);
		var game = CreateGame(gameType, nameof(Phases.Showdown), dealerPosition: 3, now);
		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);
		var carol = CreatePlayer("Carol", now);
		var aliceSeat = CreateGamePlayer(game, alice, 0, 0, 50, true, now);
		var bobSeat = CreateGamePlayer(game, bob, 3, 100, 150, false, now);
		var carolSeat = CreateGamePlayer(game, carol, 5, 100, 150, false, now);

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(alice, bob, carol);
		context.GamePlayers.AddRange(aliceSeat, bobSeat, carolSeat);
		AddHoleCards(context, game, aliceSeat, now, (CardSuit.Hearts, CardSymbol.Ace), (CardSuit.Diamonds, CardSymbol.Ace));
		AddHoleCards(context, game, bobSeat, now, (CardSuit.Hearts, CardSymbol.Eight), (CardSuit.Hearts, CardSymbol.Seven));
		AddHoleCards(context, game, carolSeat, now, (CardSuit.Diamonds, CardSymbol.Eight), (CardSuit.Diamonds, CardSymbol.Seven));
		AddCommunity(context, game, now,
			(CardSuit.Spades, CardSymbol.King),
			(CardSuit.Spades, CardSymbol.Queen),
			(CardSuit.Spades, CardSymbol.Jack),
			(CardSuit.Spades, CardSymbol.Ten),
			(CardSuit.Clubs, CardSymbol.Deuce));
		context.Pots.Add(CreatePot(game, 350, now));
		await context.SaveChangesAsync();
		return game;
	}

	private static async Task<Game> SeedLeftoverChipAsync(CardsDbContext context)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = CreateGameType(now);
		var game = CreateGame(gameType, nameof(Phases.Showdown), dealerPosition: 3, now);
		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);
		var carol = CreatePlayer("Carol", now);
		var dave = CreatePlayer("Dave", now);
		var aliceSeat = CreateGamePlayer(game, alice, 0, 100, 5, false, now);
		var bobSeat = CreateGamePlayer(game, bob, 3, 100, 5, false, now);
		var carolSeat = CreateGamePlayer(game, carol, 5, 100, 5, false, now);
		var daveSeat = CreateGamePlayer(game, dave, 6, 99, 1, false, now);
		daveSeat.HasFolded = true;

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(alice, bob, carol, dave);
		context.GamePlayers.AddRange(aliceSeat, bobSeat, carolSeat, daveSeat);
		AddHoleCards(context, game, aliceSeat, now, (CardSuit.Hearts, CardSymbol.Ace), (CardSuit.Diamonds, CardSymbol.King));
		AddHoleCards(context, game, bobSeat, now, (CardSuit.Diamonds, CardSymbol.Ace), (CardSuit.Clubs, CardSymbol.King));
		AddHoleCards(context, game, carolSeat, now, (CardSuit.Spades, CardSymbol.Ace), (CardSuit.Hearts, CardSymbol.King));
		AddCommunity(context, game, now,
			(CardSuit.Clubs, CardSymbol.Deuce),
			(CardSuit.Diamonds, CardSymbol.Four),
			(CardSuit.Hearts, CardSymbol.Five),
			(CardSuit.Spades, CardSymbol.Six),
			(CardSuit.Clubs, CardSymbol.Eight));
		context.Pots.Add(CreatePot(game, 16, now));
		await context.SaveChangesAsync();
		return game;
	}

	private static GameType CreateGameType(DateTimeOffset now)
	{
		return new GameType
		{
			Id = Guid.NewGuid(),
			Code = "HOLDTHEBASEBALL",
			Name = "Hold the Baseball",
			BettingStructure = BettingStructure.Blinds,
			MinPlayers = 2,
			MaxPlayers = 10,
			InitialHoleCards = 2,
			InitialBoardCards = 0,
			MaxCommunityCards = 5,
			MaxPlayerCards = 7,
			HasDrawPhase = false,
			MaxDiscards = 0,
			CreatedAt = now,
			UpdatedAt = now
		};
	}

	private static Game CreateGame(GameType gameType, string currentPhase, int dealerPosition, DateTimeOffset now)
	{
		return new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = gameType.Id,
			GameType = gameType,
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			CurrentHandGameTypeCode = "HOLDTHEBASEBALL",
			DealerPosition = dealerPosition,
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

	private static GamePlayer CreateGamePlayer(
		Game game,
		Player player,
		int seatPosition,
		int chipStack,
		int contributed,
		bool isAllIn,
		DateTimeOffset now)
	{
		return new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = player.Id,
			Player = player,
			SeatPosition = seatPosition,
			ChipStack = chipStack,
			StartingChips = chipStack,
			TotalContributedThisHand = contributed,
			Status = GamePlayerStatus.Active,
			HasFolded = false,
			IsAllIn = isAllIn,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}

	private static Pot CreatePot(Game game, int amount, DateTimeOffset now)
	{
		return new Pot
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = amount,
			IsAwarded = false,
			CreatedAt = now
		};
	}

	private static void AddHoleCards(
		CardsDbContext context,
		Game game,
		GamePlayer player,
		DateTimeOffset now,
		params (CardSuit Suit, CardSymbol Symbol)[] cards)
	{
		for (var index = 0; index < cards.Length; index++)
			context.GameCards.Add(CreateHoleCard(game, player, index + 1, cards[index].Suit, cards[index].Symbol, now));
	}

	private static void AddCommunity(
		CardsDbContext context,
		Game game,
		DateTimeOffset now,
		params (CardSuit Suit, CardSymbol Symbol)[] cards)
	{
		for (var index = 0; index < cards.Length; index++)
		{
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				HandNumber = game.CurrentHandNumber,
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

	private static async Task AssertStacksAsync(CardsDbContext context, params (string Name, int Stack)[] expected)
	{
		var stacks = await context.GamePlayers
			.Include(gamePlayer => gamePlayer.Player)
			.ToDictionaryAsync(gamePlayer => gamePlayer.Player.Name, gamePlayer => gamePlayer.ChipStack);

		foreach (var (name, stack) in expected)
			stacks[name].Should().Be(stack);
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
