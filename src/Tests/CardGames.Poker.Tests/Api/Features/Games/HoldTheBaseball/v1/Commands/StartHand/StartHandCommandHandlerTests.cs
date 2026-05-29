#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.StartHand;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using BettingRoundEntity = CardGames.Poker.Api.Data.Entities.BettingRound;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.HoldTheBaseball.v1.Commands.StartHand;

public class StartHandCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context, out _);

		var result = await sut.Handle(new StartHandCommand(Guid.NewGuid()), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(StartHandErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsInInvalidPhase_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.PreFlop));
		var sut = CreateSut(context, out _);

		var result = await sut.Handle(new StartHandCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(StartHandErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain(nameof(Phases.PreFlop));
	}

	[Fact]
	public async Task Handle_WhenFewerThanTwoPlayersAreEligible_ReturnsNotEnoughPlayers()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, activePlayerCount: 2, zeroChipPlayers: 1);
		var sut = CreateSut(context, out _);

		var result = await sut.Handle(new StartHandCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(StartHandErrorCode.NotEnoughPlayers);
	}

	[Fact]
	public async Task Handle_WhenPreviousHandDataExists_CleansUpCardsAndBettingRoundsBeforeStarting()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentHandNumber: 4, addPreviousHandData: true);
		var sut = CreateSut(context, out var flowHandlerFactory);

		var result = await sut.Handle(new StartHandCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.HandNumber.Should().Be(5);
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.CollectingBlinds));

		context.GameCards.Should().BeEmpty();
		context.BettingRounds.Should().OnlyContain(round => round.IsComplete);

		var mainPot = await context.Pots.SingleAsync(pot => pot.GameId == game.Id && pot.HandNumber == 5);
		mainPot.PotOrder.Should().Be(0);
		mainPot.Amount.Should().Be(0);

		flowHandlerFactory.Received(1).GetHandler("HOLDTHEBASEBALL");
	}

	private static StartHandCommandHandler CreateSut(CardsDbContext context, out IGameFlowHandlerFactory flowHandlerFactory)
	{
		var flowHandler = Substitute.For<IGameFlowHandler>();
		flowHandler.GetInitialPhase(Arg.Any<Game>()).Returns(nameof(Phases.CollectingBlinds));
		flowHandler.OnHandStartingAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		flowHandler.DealCardsAsync(
			Arg.Any<CardsDbContext>(),
			Arg.Any<Game>(),
			Arg.Any<List<GamePlayer>>(),
			Arg.Any<DateTimeOffset>(),
			Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

		flowHandlerFactory = Substitute.For<IGameFlowHandlerFactory>();
		flowHandlerFactory.GetHandler(Arg.Any<string?>()).Returns(flowHandler);

		return new StartHandCommandHandler(
			context,
			flowHandlerFactory,
			Substitute.For<ILogger<StartHandCommandHandler>>());
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
		string currentPhase = nameof(Phases.WaitingToStart),
		int activePlayerCount = 3,
		int zeroChipPlayers = 0,
		int currentHandNumber = 0,
		bool addPreviousHandData = false)
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
			CurrentHandNumber = currentHandNumber,
			DealerPosition = 0,
			SmallBlind = 5,
			BigBlind = 10,
			MinBet = 10,
			Status = GameStatus.WaitingForPlayers,
			CreatedAt = now,
			UpdatedAt = now
		};

		context.GameTypes.Add(gameType);
		context.Games.Add(game);

		for (var index = 0; index < activePlayerCount; index++)
		{
			var player = new Player
			{
				Id = Guid.NewGuid(),
				Name = $"Player {index + 1}",
				CreatedAt = now,
				UpdatedAt = now
			};

			context.Players.Add(player);
			context.GamePlayers.Add(new GamePlayer
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				PlayerId = player.Id,
				Player = player,
				SeatPosition = index,
				ChipStack = index < zeroChipPlayers ? 0 : 200,
				StartingChips = 200,
				Status = GamePlayerStatus.Active,
				JoinedAt = now,
				RowVersion = [1, 2, 3]
			});
		}

		if (addPreviousHandData)
		{
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				HandNumber = currentHandNumber,
				DealOrder = 1,
				Location = CardLocation.Hole,
				IsVisible = false,
				Suit = CardGames.Poker.Api.Data.Entities.CardSuit.Hearts,
				Symbol = CardGames.Poker.Api.Data.Entities.CardSymbol.Ace,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing)
			});

			context.BettingRounds.Add(new BettingRoundEntity
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				HandNumber = currentHandNumber,
				RoundNumber = 1,
				Street = nameof(Phases.PreFlop),
				CurrentBet = 10,
				MinBet = 10,
				LastRaiseAmount = 10,
				CurrentActorIndex = 0,
				PlayersInHand = activePlayerCount,
				IsComplete = false,
				StartedAt = now
			});

			context.Pots.Add(new PotEntity
			{
				GameId = game.Id,
				Game = game,
				HandNumber = currentHandNumber,
				PotType = PotType.Main,
				PotOrder = 0,
				Amount = 60,
				CreatedAt = now
			});
		}

		await context.SaveChangesAsync();
		return game;
	}
}