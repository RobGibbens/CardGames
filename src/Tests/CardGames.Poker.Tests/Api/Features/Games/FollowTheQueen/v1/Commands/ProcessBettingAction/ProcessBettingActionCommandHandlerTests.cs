using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.ProcessBettingAction;
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

namespace CardGames.Poker.Tests.Api.Features.Games.FollowTheQueen.v1.Commands.ProcessBettingAction;

public class ProcessBettingActionCommandHandlerTests
{
	[Fact]
	public async Task Handle_OpeningBetAboveTheStreetLimit_IsRejected()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentBet: 0, minBet: 10, lastRaiseAmount: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Bet, 200) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidAction);
		result.AsT1.Message.Should().Be("Bet must be 10");
		await AssertStreetUntouchedAsync(context, game.Id, 0, 0);
	}

	[Fact]
	public async Task Handle_CompleteThatIsNotTheSmallBet_IsRejected()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentBet: 5, minBet: 10, lastRaiseAmount: 0, potAmount: 15);
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, 6) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidAction);
		result.AsT1.Message.Should().Be("Raise must be to 10");
		await AssertStreetUntouchedAsync(context, game.Id, 5, 15);
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
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
			.Options;
		return new CardsDbContext(options);
	}

	private static async Task AssertStreetUntouchedAsync(
		CardsDbContext context,
		Guid gameId,
		int streetCurrentBet,
		int potAmount)
	{
		var player = await context.GamePlayers.SingleAsync(gamePlayer => gamePlayer.GameId == gameId && gamePlayer.SeatPosition == 0);
		player.ChipStack.Should().Be(1000);
		player.CurrentBet.Should().Be(0);
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == gameId);
		round.CurrentBet.Should().Be(streetCurrentBet);
		round.LastRaiseAmount.Should().Be(0);
		var pot = await context.Pots.SingleAsync(p => p.GameId == gameId);
		pot.Amount.Should().Be(potAmount);
	}

	private static async Task<Game> SeedGameAsync(
		CardsDbContext context,
		int currentBet,
		int minBet,
		int lastRaiseAmount,
		int potAmount = 0)
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
			CurrentPhase = nameof(Phases.FourthStreet),
			CurrentHandNumber = 1,
			DealerPosition = 0,
			CurrentPlayerIndex = 0,
			MinBet = minBet,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = new Player { Id = Guid.NewGuid(), Name = "Alice", CreatedAt = now, UpdatedAt = now };
		var bob = new Player { Id = Guid.NewGuid(), Name = "Bob", CreatedAt = now, UpdatedAt = now };
		var aliceSeat = new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = alice.Id,
			Player = alice,
			SeatPosition = 0,
			ChipStack = 1000,
			StartingChips = 1000,
			CurrentBet = 0,
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
		var bobSeat = new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = bob.Id,
			Player = bob,
			SeatPosition = 1,
			ChipStack = 1000,
			StartingChips = 1000,
			CurrentBet = 0,
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(alice, bob);
		context.GamePlayers.AddRange(aliceSeat, bobSeat);
		context.Pots.Add(new PotEntity
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = potAmount,
			CreatedAt = now
		});
		context.BettingRounds.Add(new BettingRoundEntity
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			RoundNumber = 1,
			Street = nameof(Phases.FourthStreet),
			CurrentBet = currentBet,
			MinBet = minBet,
			LastRaiseAmount = lastRaiseAmount,
			PlayersInHand = 2,
			CurrentActorIndex = 0,
			LastAggressorIndex = -1,
			IsComplete = false,
			StartedAt = now
		});
		await context.SaveChangesAsync();
		return game;
	}
}
