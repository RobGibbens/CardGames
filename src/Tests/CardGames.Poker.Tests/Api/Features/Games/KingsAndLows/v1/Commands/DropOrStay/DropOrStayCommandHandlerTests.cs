#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;
using CardGames.Poker.Betting;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;

public class DropOrStayCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context);

		var result = await sut.Handle(new DropOrStayCommand(Guid.NewGuid(), Guid.NewGuid(), "Stay"), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DropOrStayErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInDropOrStayPhase_ReturnsInvalidPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.DrawPhase));
		var playerId = await context.GamePlayers.Where(gp => gp.GameId == game.Id).Select(gp => gp.PlayerId).FirstAsync();
		var sut = CreateSut(context);

		var result = await sut.Handle(new DropOrStayCommand(game.Id, playerId, "Stay"), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DropOrStayErrorCode.InvalidPhase);
		result.AsT1.Message.Should().Contain(nameof(Phases.DrawPhase));
	}

	[Fact]
	public async Task Handle_WhenPlayerIsMissing_ReturnsPlayerNotFound()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = CreateSut(context);

		var result = await sut.Handle(new DropOrStayCommand(game.Id, Guid.NewGuid(), "Stay"), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DropOrStayErrorCode.PlayerNotFound);
	}

	[Fact]
	public async Task Handle_WhenDecisionIsInvalid_ReturnsInvalidDecision()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var playerId = await context.GamePlayers.Where(gp => gp.GameId == game.Id).Select(gp => gp.PlayerId).FirstAsync();
		var sut = CreateSut(context);

		var result = await sut.Handle(new DropOrStayCommand(game.Id, playerId, "Maybe"), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DropOrStayErrorCode.InvalidDecision);
	}

	[Fact]
	public async Task Handle_WhenPlayerAlreadyDecided_ReturnsAlreadyDecided()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, firstDecision: CardGames.Poker.Api.Data.Entities.DropOrStayDecision.Stay);
		var playerId = await context.GamePlayers
			.Where(gp => gp.GameId == game.Id && gp.SeatPosition == 0)
			.Select(gp => gp.PlayerId)
			.FirstAsync();
		var sut = CreateSut(context);

		var result = await sut.Handle(new DropOrStayCommand(game.Id, playerId, "Stay"), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DropOrStayErrorCode.AlreadyDecided);
	}

	[Fact]
	public async Task Handle_WhenSecondDecisionStartsFromStaleSnapshot_AdvancesToDrawPhase()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		Guid gameId;
		Guid firstPlayerId;
		Guid secondPlayerId;

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			var game = await SeedGameAsync(setupContext);
			gameId = game.Id;
			var playerIds = await setupContext.GamePlayers
				.Where(gp => gp.GameId == gameId)
				.OrderBy(gp => gp.SeatPosition)
				.Select(gp => gp.PlayerId)
				.ToListAsync();
			firstPlayerId = playerIds[0];
			secondPlayerId = playerIds[1];
		}

		await using var staleContext = CreateContext(databaseName, databaseRoot);
		await using var freshContext = CreateContext(databaseName, databaseRoot);

		_ = await staleContext.Games
			.Include(g => g.GamePlayers)
			.FirstAsync(g => g.Id == gameId);

		var freshSut = CreateSut(freshContext);
		var staleSut = CreateSut(staleContext);

		var firstResult = await freshSut.Handle(new DropOrStayCommand(gameId, firstPlayerId, "Stay"), CancellationToken.None);
		var secondResult = await staleSut.Handle(new DropOrStayCommand(gameId, secondPlayerId, "Stay"), CancellationToken.None);

		firstResult.IsT0.Should().BeTrue();
		secondResult.IsT0.Should().BeTrue();
		secondResult.AsT0.AllPlayersDecided.Should().BeTrue();
		secondResult.AsT0.NextPhase.Should().Be(nameof(Phases.DrawPhase));

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedGame = await verificationContext.Games.FirstAsync(g => g.Id == gameId);
		var players = await verificationContext.GamePlayers
			.Where(gp => gp.GameId == gameId)
			.OrderBy(gp => gp.SeatPosition)
			.ToListAsync();

		persistedGame.CurrentPhase.Should().Be(nameof(Phases.DrawPhase));
		persistedGame.CurrentDrawPlayerIndex.Should().Be(1);
		persistedGame.CurrentPlayerIndex.Should().Be(1);
		players.Should().OnlyContain(gp => gp.DropOrStayDecision == CardGames.Poker.Api.Data.Entities.DropOrStayDecision.Stay);
	}

	private static DropOrStayCommandHandler CreateSut(CardsDbContext context)
	{
		return new DropOrStayCommandHandler(context, Substitute.For<IMediator>());
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
		string currentPhase = nameof(Phases.DropOrStay),
		CardGames.Poker.Api.Data.Entities.DropOrStayDecision? firstDecision = null,
		CardGames.Poker.Api.Data.Entities.DropOrStayDecision? secondDecision = null)
	{
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			CurrentPlayerIndex = 0,
			CurrentDrawPlayerIndex = -1,
			DealerPosition = 0,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);

		context.Games.Add(game);
		context.Players.AddRange(alice, bob);
		context.GamePlayers.AddRange(
			CreateGamePlayer(game, alice, 0, firstDecision, now),
			CreateGamePlayer(game, bob, 1, secondDecision, now));

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
		CardGames.Poker.Api.Data.Entities.DropOrStayDecision? decision,
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
			ChipStack = 100,
			StartingChips = 100,
			Status = GamePlayerStatus.Active,
			DropOrStayDecision = decision,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}
}