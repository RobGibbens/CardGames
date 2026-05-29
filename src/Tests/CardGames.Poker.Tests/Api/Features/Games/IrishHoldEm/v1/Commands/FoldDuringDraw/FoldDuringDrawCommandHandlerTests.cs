#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.FoldDuringDraw;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.IrishHoldEm.v1.Commands.FoldDuringDraw;

public class FoldDuringDrawCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsError()
	{
		await using var context = CreateContext();
		var sut = new FoldDuringDrawCommandHandler(context, NullLogger<FoldDuringDrawCommandHandler>.Instance);

		var result = await sut.Handle(new FoldDuringDrawCommand(Guid.NewGuid(), PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Message.Should().Contain("was not found");
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInDrawPhase_ReturnsError()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.Turn));
		var sut = new FoldDuringDrawCommandHandler(context, NullLogger<FoldDuringDrawCommandHandler>.Instance);

		var result = await sut.Handle(new FoldDuringDrawCommand(game.Id, PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Message.Should().Contain(nameof(Phases.Turn));
	}

	[Fact]
	public async Task Handle_WhenSeatDoesNotExist_ReturnsError()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = new FoldDuringDrawCommandHandler(context, NullLogger<FoldDuringDrawCommandHandler>.Instance);

		var result = await sut.Handle(new FoldDuringDrawCommand(game.Id, PlayerSeatIndex: 99), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Message.Should().Be("Player not found at the specified seat.");
	}

	[Fact]
	public async Task Handle_WhenPlayerAlreadyFolded_ReturnsError()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, firstPlayerFolded: true);
		var sut = new FoldDuringDrawCommandHandler(context, NullLogger<FoldDuringDrawCommandHandler>.Instance);

		var result = await sut.Handle(new FoldDuringDrawCommand(game.Id, PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Message.Should().Be("Player has already folded.");
	}

	[Fact]
	public async Task Handle_WhenFoldLeavesOneActivePlayer_AdvancesToShowdown()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, secondPlayerFolded: true);
		var sut = new FoldDuringDrawCommandHandler(context, NullLogger<FoldDuringDrawCommandHandler>.Instance);

		var result = await sut.Handle(new FoldDuringDrawCommand(game.Id, PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.OnlyOnePlayerRemains.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));

		var persistedGame = await context.Games.SingleAsync(g => g.Id == game.Id);
		persistedGame.CurrentPhase.Should().Be(nameof(Phases.Showdown));
		persistedGame.CurrentPlayerIndex.Should().Be(-1);
		persistedGame.CurrentDrawPlayerIndex.Should().Be(-1);
	}

	[Fact]
	public async Task Handle_WhenRemainingPlayersAlreadyDrew_AdvancesToDrawComplete()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, secondPlayerHasDrawn: true, includeThirdPlayer: true, thirdPlayerHasDrawn: true);
		var sut = new FoldDuringDrawCommandHandler(context, NullLogger<FoldDuringDrawCommandHandler>.Instance);

		var result = await sut.Handle(new FoldDuringDrawCommand(game.Id, PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.OnlyOnePlayerRemains.Should().BeFalse();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.DrawComplete));

		var persistedGame = await context.Games.SingleAsync(g => g.Id == game.Id);
		persistedGame.CurrentPhase.Should().Be(nameof(Phases.DrawComplete));
		persistedGame.DrawCompletedAt.Should().NotBeNull();
		persistedGame.CurrentPlayerIndex.Should().Be(-1);
		persistedGame.CurrentDrawPlayerIndex.Should().Be(-1);
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedGameAsync(
		CardsDbContext context,
		string currentPhase = nameof(Phases.DrawPhase),
		bool firstPlayerFolded = false,
		bool secondPlayerFolded = false,
		bool secondPlayerHasDrawn = false,
		bool includeThirdPlayer = false,
		bool thirdPlayerFolded = false,
		bool thirdPlayerHasDrawn = false)
	{
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			CurrentDrawPlayerIndex = 0,
			CurrentPlayerIndex = 0,
			DealerPosition = 0,
			MinBet = 10,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var firstPlayer = CreatePlayer("Alice", now);
		var secondPlayer = CreatePlayer("Bob", now);
		var thirdPlayer = includeThirdPlayer ? CreatePlayer("Carol", now) : null;

		var firstGamePlayer = CreateGamePlayer(game, firstPlayer, seatPosition: 0, firstPlayerFolded, hasDrawnThisRound: false, now);
		var secondGamePlayer = CreateGamePlayer(game, secondPlayer, seatPosition: 1, secondPlayerFolded, secondPlayerHasDrawn, now);
		var thirdGamePlayer = includeThirdPlayer && thirdPlayer is not null
			? CreateGamePlayer(game, thirdPlayer, seatPosition: 2, thirdPlayerFolded, thirdPlayerHasDrawn, now)
			: null;

		context.Games.Add(game);
		context.Players.AddRange(firstPlayer, secondPlayer);
		if (thirdPlayer is not null)
		{
			context.Players.Add(thirdPlayer);
		}

		context.GamePlayers.AddRange(firstGamePlayer, secondGamePlayer);
		if (thirdGamePlayer is not null)
		{
			context.GamePlayers.Add(thirdGamePlayer);
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
		bool hasFolded,
		bool hasDrawnThisRound,
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
			HasFolded = hasFolded,
			HasDrawnThisRound = hasDrawnThisRound,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}
}