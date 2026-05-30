#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.CollectAntes;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.SevenCardStud.v1.Commands.CollectAntes;

public class CollectAntesCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = new CollectAntesCommandHandler(context);

		var result = await sut.Handle(new CollectAntesCommand(Guid.NewGuid()), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(CollectAntesErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsInInvalidPhase_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.ThirdStreet));
		var sut = new CollectAntesCommandHandler(context);

		var result = await sut.Handle(new CollectAntesCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(CollectAntesErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain(nameof(Phases.ThirdStreet));
	}

	[Fact]
	public async Task Handle_WhenPlayerCannotCoverAnte_UsesRemainingChipsAndMarksPlayerAllIn()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, chipStacks: [100, 5], ante: 10);
		var sut = new CollectAntesCommandHandler(context);

		var result = await sut.Handle(new CollectAntesCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.ThirdStreet));
		result.AsT0.TotalAntesCollected.Should().Be(15);
		result.AsT0.AnteContributions.Should().ContainSingle(c => c.PlayerName == "Player 2" && c.Amount == 5 && c.WentAllIn);

		var players = await context.GamePlayers
			.Where(player => player.GameId == game.Id)
			.OrderBy(player => player.SeatPosition)
			.ToListAsync();
		var mainPot = await context.Pots.SingleAsync(pot => pot.GameId == game.Id && pot.HandNumber == game.CurrentHandNumber && pot.PotType == PotType.Main);

		players[0].ChipStack.Should().Be(90);
		players[0].CurrentBet.Should().Be(0);
		players[0].IsAllIn.Should().BeFalse();
		players[1].ChipStack.Should().Be(0);
		players[1].IsAllIn.Should().BeTrue();
		mainPot.Amount.Should().Be(15);
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
		string currentPhase = nameof(Phases.CollectingAntes),
		int ante = 10,
		int[]? chipStacks = null)
	{
		var now = DateTimeOffset.UtcNow;
		chipStacks ??= [100, 100];

		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			Ante = ante,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		context.Games.Add(game);

		for (var index = 0; index < chipStacks.Length; index++)
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
				ChipStack = chipStacks[index],
				StartingChips = 100,
				Status = GamePlayerStatus.Active,
				JoinedAt = now,
				RowVersion = [1, 2, 3]
			});
		}

		await context.SaveChangesAsync();
		return game;
	}
}