#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.PairPressure.v1.Commands.StartHand;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.PairPressure.v1.Commands.StartHand;

public class StartHandCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context);

		var result = await sut.Handle(new StartHandCommand(Guid.NewGuid()), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(StartHandErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsInInvalidPhase_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.ThirdStreet));
		var sut = CreateSut(context);

		var result = await sut.Handle(new StartHandCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(StartHandErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain(nameof(Phases.ThirdStreet));
	}

	[Fact]
	public async Task Handle_WhenFewerThanTwoPlayersCanCoverAnte_ReturnsNotEnoughPlayers()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, ante: 25, chipStacks: [25, 10]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new StartHandCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(StartHandErrorCode.NotEnoughPlayers);
	}

	[Fact]
	public async Task Handle_WhenPreviousHandCardsExist_RemovesThemBeforeCreatingNewDeck()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentHandNumber: 3, includeExistingCards: true);
		var staleCardIds = await context.GameCards
			.Where(card => card.GameId == game.Id)
			.Select(card => card.Id)
			.ToListAsync();
		var sut = CreateSut(context);

		var result = await sut.Handle(new StartHandCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));
		result.AsT0.HandNumber.Should().Be(4);

		var persistedCards = await context.GameCards
			.Where(card => card.GameId == game.Id)
			.ToListAsync();

		persistedCards.Should().HaveCount(52);
		persistedCards.Select(card => card.Id).Should().NotIntersectWith(staleCardIds);
		persistedCards.Should().OnlyContain(card => card.HandNumber == 4 && card.Location == CardLocation.Deck);
	}

	private static StartHandCommandHandler CreateSut(CardsDbContext context)
	{
		return new StartHandCommandHandler(
			context,
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
		int currentHandNumber = 0,
		int ante = 10,
		int[]? chipStacks = null,
		bool includeExistingCards = false)
	{
		var now = DateTimeOffset.UtcNow;
		chipStacks ??= [200, 200, 200];

		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = currentPhase,
			CurrentHandNumber = currentHandNumber,
			Ante = ante,
			DealerPosition = 0,
			Status = GameStatus.WaitingForPlayers,
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
				StartingChips = Math.Max(chipStacks[index], 200),
				Status = GamePlayerStatus.Active,
				JoinedAt = now,
				RowVersion = [1, 2, 3]
			});
		}

		if (includeExistingCards)
		{
			for (var dealOrder = 0; dealOrder < 4; dealOrder++)
			{
				context.GameCards.Add(new GameCard
				{
					Id = Guid.NewGuid(),
					GameId = game.Id,
					Game = game,
					HandNumber = currentHandNumber,
					DealOrder = dealOrder + 1,
					Location = CardLocation.Deck,
					IsVisible = false,
					Suit = (CardSuit)(dealOrder % 4),
					Symbol = (CardSymbol)(dealOrder + 2),
					DealtAt = now,
					DealtAtPhase = nameof(Phases.Dealing)
				});
			}
		}

		await context.SaveChangesAsync();
		return game;
	}
}