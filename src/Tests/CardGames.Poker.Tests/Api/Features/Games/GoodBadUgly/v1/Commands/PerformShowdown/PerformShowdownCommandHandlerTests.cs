#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.GoodBadUgly.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.GoodBadUgly.v1.Commands.PerformShowdown;

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
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.SixthStreet));
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PerformShowdownErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain(nameof(Phases.SixthStreet));
	}

	[Fact]
	public async Task Handle_WhenNoEligibleWinningHandCanBeDetermined_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.Showdown));
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PerformShowdownErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain("No eligible winning hand");
	}

	private static PerformShowdownCommandHandler CreateSut(CardsDbContext context)
	{
		return new PerformShowdownCommandHandler(
			context,
			Substitute.For<IHandHistoryRecorder>(),
			Substitute.For<IHandSettlementService>());
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

	private static async Task<Game> SeedGameAsync(CardsDbContext context, string currentPhase)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
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

		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = gameType.Id,
			GameType = gameType,
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			DealerPosition = 0,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(alice, bob);
		context.GamePlayers.AddRange(
			CreateGamePlayer(game, alice, 0, now),
			CreateGamePlayer(game, bob, 1, now));
		context.Pots.Add(new PotEntity
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 120,
			CreatedAt = now
		});

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
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}
}