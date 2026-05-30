#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Betting;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using BettingRoundEntity = CardGames.Poker.Api.Data.Entities.BettingRound;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction;

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
	public async Task Handle_WhenCheckIsUsedAgainstOutstandingBet_ReturnsInvalidAction()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentBet: 20);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Check), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidAction);
		result.AsT1.Message.Should().Contain("Cannot check");
	}

	private static ProcessBettingActionCommandHandler CreateSut(CardsDbContext context)
	{
		return new ProcessBettingActionCommandHandler(
			context,
			Substitute.For<IMediator>(),
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
		string currentPhase = nameof(Phases.ThirdStreet),
		bool includeBettingRound = true,
		int currentBet = 0,
		int minBet = 10,
		int currentActorIndex = 0)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "SEVENCARDSTUD",
			Name = "Seven Card Stud",
			BettingStructure = BettingStructure.Ante,
			MinPlayers = 2,
			MaxPlayers = 8,
			InitialHoleCards = 3,
			InitialBoardCards = 0,
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
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			DealerPosition = 0,
			CurrentPlayerIndex = currentActorIndex,
			MinBet = minBet,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);

		var aliceGamePlayer = CreateGamePlayer(game, alice, seatPosition: 0, chipStack: 200, now);
		var bobGamePlayer = CreateGamePlayer(game, bob, seatPosition: 1, chipStack: 200, now);

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
			CurrentBet = 0,
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}
}