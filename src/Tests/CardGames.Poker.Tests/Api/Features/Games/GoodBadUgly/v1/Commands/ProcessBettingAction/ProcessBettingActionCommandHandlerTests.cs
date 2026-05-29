#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.GoodBadUgly.v1.Commands.ProcessBettingAction;
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

namespace CardGames.Poker.Tests.Api.Features.Games.GoodBadUgly.v1.Commands.ProcessBettingAction;

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
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.CollectingAntes), includeBettingRound: false);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Check), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain(nameof(Phases.CollectingAntes));
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
	public async Task Handle_WhenCheckIsUsedAgainstAnOutstandingBet_ReturnsInvalidAction()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentBet: 20);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Check), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidAction);
		result.AsT1.Message.Should().Contain("Cannot check");
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
		int minBet = 10)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = CreateGameType(now);
		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = gameType.Id,
			GameType = gameType,
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			DealerPosition = 0,
			CurrentPlayerIndex = 0,
			SmallBet = minBet,
			BigBet = minBet * 2,
			MinBet = minBet,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);

		var aliceGamePlayer = CreateGamePlayer(game, alice, seatPosition: 0, now);
		var bobGamePlayer = CreateGamePlayer(game, bob, seatPosition: 1, now);

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
				MaxRaises = 0,
				LastRaiseAmount = currentBet > 0 ? currentBet : minBet,
				PlayersInHand = 2,
				PlayersActed = 0,
				CurrentActorIndex = 0,
				LastAggressorIndex = currentBet > 0 ? 1 : -1,
				IsComplete = false,
				StartedAt = now
			});
		}

		await context.SaveChangesAsync();
		return game;
	}

	private static GameType CreateGameType(DateTimeOffset now)
	{
		return new GameType
		{
			Id = Guid.NewGuid(),
			Code = "GOODBADUGLY",
			Name = "The Good, the Bad, and the Ugly",
			BettingStructure = BettingStructure.AnteBringIn,
			MinPlayers = 2,
			MaxPlayers = 7,
			InitialHoleCards = 4,
			InitialBoardCards = 0,
			MaxCommunityCards = 3,
			MaxPlayerCards = 7,
			HasDrawPhase = false,
			MaxDiscards = 0,
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
			ChipStack = 200,
			StartingChips = 200,
			CurrentBet = 0,
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}
}