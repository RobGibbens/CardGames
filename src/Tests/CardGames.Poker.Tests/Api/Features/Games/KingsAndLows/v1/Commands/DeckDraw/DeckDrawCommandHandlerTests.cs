#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DeckDraw;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.KingsAndLows.v1.Commands.DeckDraw;

public class DeckDrawCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = new DeckDrawCommandHandler(context);

		var result = await sut.Handle(new DeckDrawCommand(Guid.NewGuid(), Guid.NewGuid(), [0]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DeckDrawErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInPlayerVsDeckPhase_ReturnsInvalidPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.Showdown));
		var playerId = await context.GamePlayers.Select(gp => gp.PlayerId).FirstAsync();
		var sut = new DeckDrawCommandHandler(context);

		var result = await sut.Handle(new DeckDrawCommand(game.Id, playerId, [0]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DeckDrawErrorCode.InvalidPhase);
		result.AsT1.Message.Should().Contain(nameof(Phases.Showdown));
	}

	[Fact]
	public async Task Handle_WhenPlayerIsMissing_ReturnsPlayerNotFound()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = new DeckDrawCommandHandler(context);

		var result = await sut.Handle(new DeckDrawCommand(game.Id, Guid.NewGuid(), [0]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DeckDrawErrorCode.PlayerNotFound);
	}

	[Fact]
	public async Task Handle_WhenDiscardIndexIsOutOfRange_ReturnsInvalidDiscardIndices()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var playerId = await context.GamePlayers.Select(gp => gp.PlayerId).FirstAsync();
		var sut = new DeckDrawCommandHandler(context);

		var result = await sut.Handle(new DeckDrawCommand(game.Id, playerId, [-1, 5]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DeckDrawErrorCode.InvalidDiscardIndices);
	}

	[Fact]
	public async Task Handle_WhenTooManyDiscardIndicesAreProvided_ReturnsTooManyDiscards()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var playerId = await context.GamePlayers.Select(gp => gp.PlayerId).FirstAsync();
		var sut = new DeckDrawCommandHandler(context);

		var result = await sut.Handle(new DeckDrawCommand(game.Id, playerId, [0, 1, 2, 3, 4, 0]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DeckDrawErrorCode.TooManyDiscards);
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

	private static async Task<Game> SeedGameAsync(CardsDbContext context, string currentPhase = nameof(Phases.PlayerVsDeck))
	{
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var player = new Player
		{
			Id = Guid.NewGuid(),
			Name = "Alice",
			CreatedAt = now,
			UpdatedAt = now
		};

		context.Games.Add(game);
		context.Players.Add(player);
		context.GamePlayers.Add(new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = player.Id,
			Player = player,
			SeatPosition = 0,
			ChipStack = 100,
			StartingChips = 100,
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		});

		await context.SaveChangesAsync();
		return game;
	}
}