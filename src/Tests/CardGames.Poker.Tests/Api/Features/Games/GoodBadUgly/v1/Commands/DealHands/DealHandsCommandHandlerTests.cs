#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.GoodBadUgly.v1.Commands.DealHands;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.GoodBadUgly.v1.Commands.DealHands;

public class DealHandsCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context);

		var result = await sut.Handle(new DealHandsCommand(Guid.NewGuid()), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DealHandsErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInStreetPhase_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.FirstBettingRound));
		var sut = CreateSut(context);

		var result = await sut.Handle(new DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DealHandsErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain(nameof(Phases.FirstBettingRound));
	}

	[Fact]
	public async Task Handle_WhenDeckIsMissing_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, deckCardCount: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DealHandsErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain("No deck cards available");
	}

	[Fact]
	public async Task Handle_WhenDeckDoesNotContainEnoughCards_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, deckCardCount: 10);
		var sut = CreateSut(context);

		var result = await sut.Handle(new DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DealHandsErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain("Not enough cards in deck");
	}

	[Fact]
	public async Task Handle_WhenThirdStreetContainsStaleCards_CleansThemUpAndResetsDealOrder()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		Guid gameId;
		Guid firstPlayerId;
		Guid[] staleCardIds;

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			var game = await SeedGameAsync(setupContext, addStaleCards: true);
			gameId = game.Id;
			firstPlayerId = await setupContext.GamePlayers
				.Where(gamePlayer => gamePlayer.GameId == gameId)
				.OrderBy(gamePlayer => gamePlayer.SeatPosition)
				.Select(gamePlayer => gamePlayer.Id)
				.FirstAsync();

			staleCardIds = await setupContext.GameCards
				.Where(gameCard => gameCard.GameId == gameId && gameCard.GamePlayerId == firstPlayerId && gameCard.Location != CardLocation.Deck)
				.Select(gameCard => gameCard.Id)
				.ToArrayAsync();
		}

		await using (var context = CreateContext(databaseName, databaseRoot))
		{
			var sut = CreateSut(context);
			var result = await sut.Handle(new DealHandsCommand(gameId), CancellationToken.None);

			result.IsT0.Should().BeTrue();
			result.AsT0.PlayerHands.Should().HaveCount(2);
			result.AsT0.PlayerHands.Should().OnlyContain(playerHand => playerHand.Cards.Count == 4);
		}

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedPlayerCards = await verificationContext.GameCards
			.Where(gameCard => gameCard.GameId == gameId && gameCard.GamePlayerId != null && !gameCard.IsDiscarded)
			.OrderBy(gameCard => gameCard.GamePlayerId)
			.ThenBy(gameCard => gameCard.DealOrder)
			.ToListAsync();

		persistedPlayerCards.Should().HaveCount(8);
		persistedPlayerCards.Should().NotContain(gameCard => ((IEnumerable<Guid>)staleCardIds).Contains(gameCard.Id));

		persistedPlayerCards
			.Where(gameCard => gameCard.GamePlayerId == firstPlayerId)
			.Select(gameCard => gameCard.DealOrder)
			.Should()
			.Equal(1, 2, 3, 4);

		var communityCards = await verificationContext.GameCards
			.Where(gameCard => gameCard.GameId == gameId && gameCard.Location == CardLocation.Community)
			.OrderBy(gameCard => gameCard.DealOrder)
			.ToListAsync();

		communityCards.Should().HaveCount(3);
		communityCards.Select(gameCard => gameCard.DealtAtPhase).Should().Equal("TheGood", "TheBad", "TheUgly");
	}

	private static DealHandsCommandHandler CreateSut(CardsDbContext context)
	{
		return new DealHandsCommandHandler(context, Substitute.For<ILogger<DealHandsCommandHandler>>());
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
		int deckCardCount = 11,
		bool addStaleCards = false)
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
			BringIn = 5,
			SmallBet = 10,
			BigBet = 20,
			MinBet = 10,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);

		var aliceGamePlayer = CreateGamePlayer(game, alice, seatPosition: 0, now);
		var bobGamePlayer = CreateGamePlayer(game, bob, seatPosition: 1, now);

		context.Games.Add(game);
		context.GameTypes.Add(gameType);
		context.Players.AddRange(alice, bob);
		context.GamePlayers.AddRange(aliceGamePlayer, bobGamePlayer);

		for (var cardIndex = 0; cardIndex < deckCardCount; cardIndex++)
		{
			context.GameCards.Add(CreateDeckCard(game, cardIndex, now));
		}

		if (addStaleCards)
		{
			context.GameCards.AddRange(CreateStaleCards(game, aliceGamePlayer, now));
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

	private static GameCard CreateDeckCard(Game game, int cardIndex, DateTimeOffset now)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			DealOrder = 100 + cardIndex,
			Location = CardLocation.Deck,
			IsVisible = false,
			Symbol = (CardSymbol)((cardIndex % 13) + 2),
			Suit = (CardSuit)(cardIndex % 4),
			DealtAt = now,
			DealtAtPhase = nameof(Phases.ThirdStreet)
		};
	}

	private static GameCard[] CreateStaleCards(Game game, GamePlayer player, DateTimeOffset now)
	{
		return
		[
			new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				GamePlayerId = player.Id,
				GamePlayer = player,
				HandNumber = game.CurrentHandNumber,
				DealOrder = 7,
				Location = CardLocation.Hole,
				IsVisible = false,
				Symbol = CardSymbol.Ace,
				Suit = CardSuit.Spades,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.ThirdStreet)
			},
			new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				GamePlayerId = player.Id,
				GamePlayer = player,
				HandNumber = game.CurrentHandNumber,
				DealOrder = 8,
				Location = CardLocation.Board,
				IsVisible = true,
				Symbol = CardSymbol.Queen,
				Suit = CardSuit.Hearts,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.ThirdStreet)
			}
		];
	}
}