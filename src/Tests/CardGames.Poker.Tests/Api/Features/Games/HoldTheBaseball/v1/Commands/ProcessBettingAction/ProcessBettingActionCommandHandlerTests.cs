#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using BettingRoundEntity = CardGames.Poker.Api.Data.Entities.BettingRound;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.HoldTheBaseball.v1.Commands.ProcessBettingAction;

public class ProcessBettingActionCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(Guid.NewGuid(), BettingActionType.Check), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInBettingPhase_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.CollectingBlinds), includeBettingRound: false);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Check), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain(nameof(Phases.CollectingBlinds));
	}

	[Fact]
	public async Task Handle_WhenActiveBettingRoundIsMissing_ReturnsNoBettingRound()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, includeBettingRound: false);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Check), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.NoBettingRound);
	}

	[Fact]
	public async Task Handle_WhenCurrentActorSeatDoesNotExist_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentActorIndex: 99);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Check), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain("Could not determine current player");
	}

	[Fact]
	public async Task Handle_WhenBetAmountIsBelowMinimum_ReturnsInvalidAction()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentBet: 0, minBet: 10);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Bet, Amount: 5), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidAction);
		result.AsT1.Message.Should().Contain("at least 10");
	}

	[Fact]
	public async Task Handle_WhenAllPlayersBecomeAllIn_DealsRemainingCommunityCardsOnceWithDistinctDeckCards()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			currentPhase: nameof(Phases.PreFlop),
			currentBet: 0,
			currentActorIndex: 0,
			firstPlayerStack: 50,
			secondPlayerAllIn: true);

		context.GameCards.Add(CreateDeckCard(game, dealOrder: 1, CardSuit.Hearts, CardSymbol.Ace));
		context.GameCards.Add(CreateDeckCard(game, dealOrder: 2, CardSuit.Clubs, CardSymbol.King));
		context.GameCards.Add(CreateDeckCard(game, dealOrder: 3, CardSuit.Spades, CardSymbol.Queen));
		context.GameCards.Add(CreateDeckCard(game, dealOrder: 4, CardSuit.Diamonds, CardSymbol.Jack));
		context.GameCards.Add(CreateDeckCard(game, dealOrder: 5, CardSuit.Hearts, CardSymbol.Ten));
		await context.SaveChangesAsync();

		var sut = CreateSut(context);
		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.AllIn), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));

		var communityCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id && gc.Location == CardLocation.Community)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync();

		communityCards.Should().HaveCount(5);
		communityCards.Select(card => card.Id).Should().OnlyHaveUniqueItems();
		communityCards.Select(card => card.DealtAtPhase).Should().Equal("Flop", "Flop", "Flop", "Turn", "River");

		using var gameSettings = JsonDocument.Parse((await context.Games.SingleAsync(g => g.Id == game.Id)).GameSettings!);
		gameSettings.RootElement.GetProperty("allInRunout").GetBoolean().Should().BeTrue();
		gameSettings.RootElement.GetProperty("runoutStreets").EnumerateArray().Select(item => item.GetString()).Should().Equal("Flop", "Turn", "River");
	}

	private static ProcessBettingActionCommandHandler CreateSut(CardsDbContext context)
	{
		return new ProcessBettingActionCommandHandler(
			context,
			Substitute.For<ILogger<ProcessBettingActionCommandHandler>>());
	}

	private static CardsDbContext CreateContext()
	{
		return CreateContext(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot());
	}

	private static CardsDbContext CreateContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot)
			.Options;

		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedGameAsync(
		CardsDbContext context,
		string currentPhase = nameof(Phases.PreFlop),
		bool includeBettingRound = true,
		int currentBet = 0,
		int minBet = 10,
		int currentActorIndex = 0,
		int firstPlayerStack = 200,
		bool secondPlayerAllIn = false)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
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

		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = gameType.Id,
			GameType = gameType,
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			DealerPosition = 0,
			CurrentPlayerIndex = currentActorIndex,
			SmallBlind = 5,
			BigBlind = 10,
			MinBet = minBet,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);

		var aliceGamePlayer = CreateGamePlayer(game, alice, seatPosition: 0, chipStack: firstPlayerStack, now);
		var bobGamePlayer = CreateGamePlayer(
			game,
			bob,
			seatPosition: 1,
			chipStack: secondPlayerAllIn ? 0 : 200,
			now,
			isAllIn: secondPlayerAllIn);

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(alice, bob);
		context.GamePlayers.AddRange(aliceGamePlayer, bobGamePlayer);
		context.Pots.Add(new PotEntity
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 0,
			CreatedAt = now
		});

		if (includeBettingRound)
		{
			context.BettingRounds.Add(new BettingRoundEntity
			{
				GameId = game.Id,
				Game = game,
				HandNumber = game.CurrentHandNumber,
				RoundNumber = 1,
				Street = currentPhase,
				CurrentBet = currentBet,
				MinBet = minBet,
				RaiseCount = currentBet > 0 ? 1 : 0,
				MaxRaises = 4,
				LastRaiseAmount = currentBet > 0 ? currentBet : minBet,
				PlayersInHand = 2,
				PlayersActed = 0,
				CurrentActorIndex = currentActorIndex,
				LastAggressorIndex = currentBet > 0 ? 1 : -1,
				IsComplete = false,
				StartedAt = now
			});
		}

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

	private static GamePlayer CreateGamePlayer(
		Game game,
		Player player,
		int seatPosition,
		int chipStack,
		DateTimeOffset now,
		bool isAllIn = false)
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
			StartingChips = Math.Max(chipStack, 200),
			CurrentBet = 0,
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			IsAllIn = isAllIn,
			RowVersion = [1, 2, 3]
		};
	}

	private static GameCard CreateDeckCard(Game game, int dealOrder, CardSuit suit, CardSymbol symbol)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			DealOrder = dealOrder,
			Location = CardLocation.Deck,
			IsVisible = false,
			Suit = suit,
			Symbol = symbol,
			DealtAt = DateTimeOffset.UtcNow,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}
}