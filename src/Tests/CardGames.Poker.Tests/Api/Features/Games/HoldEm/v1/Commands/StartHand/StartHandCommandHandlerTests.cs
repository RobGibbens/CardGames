#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.StartHand;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.HoldEm.v1.Commands.StartHand;

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
	public async Task Handle_WhenGameTypeCodeIsMissing_UsesHoldEmFallback()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, includeGameType: false);
		var sut = CreateSut(context, out var flowHandlerFactory);

		var result = await sut.Handle(new StartHandCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.CollectingBlinds));
		flowHandlerFactory.Received(1).GetHandler("HOLDEM");
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
		bool includeGameType = true)
	{
		var now = DateTimeOffset.UtcNow;
		GameType? gameType = null;

		if (includeGameType)
		{
			gameType = new GameType
			{
				Id = Guid.NewGuid(),
				Code = "HOLDEM",
				Name = "Texas Hold'em",
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
			context.GameTypes.Add(gameType);
		}

		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = gameType?.Id,
			GameType = gameType,
			CurrentPhase = currentPhase,
			CurrentHandNumber = 0,
			DealerPosition = 0,
			SmallBlind = 5,
			BigBlind = 10,
			MinBet = 10,
			Status = GameStatus.WaitingForPlayers,
			CreatedAt = now,
			UpdatedAt = now
		};

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

		await context.SaveChangesAsync();
		return game;
	}
}