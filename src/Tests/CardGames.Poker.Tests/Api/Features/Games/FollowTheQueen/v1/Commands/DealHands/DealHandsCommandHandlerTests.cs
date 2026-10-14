using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.DealHands;
using CardGames.Poker.Betting;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.FollowTheQueen.v1.Commands.DealHands;

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
		var game = await SeedGameAsync(context, nameof(Phases.FirstBettingRound));
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
		var game = await SeedGameAsync(context, deckCardCount: 5);
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
			var setup = await SeedGameAsync(setupContext, addStaleCards: true);
			gameId = setup.Id;
			firstPlayerId = await setupContext.GamePlayers
				.Where(gamePlayer => gamePlayer.GameId == setup.Id)
				.OrderBy(gamePlayer => gamePlayer.SeatPosition)
				.Select(gamePlayer => gamePlayer.Id)
				.FirstAsync();

			staleCardIds = await setupContext.GameCards
				.Where(gameCard => gameCard.GameId == setup.Id && gameCard.GamePlayerId == firstPlayerId &&
				                   gameCard.Location != CardLocation.Deck)
				.Select(gameCard => gameCard.Id)
				.ToArrayAsync();
		}

		await using (var context = CreateContext(databaseName, databaseRoot))
		{
			var sut = CreateSut(context);
			var result = await sut.Handle(new DealHandsCommand(gameId), CancellationToken.None);

			result.IsT0.Should().BeTrue();
			result.AsT0.PlayerHands.Should().HaveCount(2);
		}

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedPlayerCards = await verificationContext.GameCards
			.Where(gameCard => gameCard.GameId == gameId && gameCard.GamePlayerId != null && !gameCard.IsDiscarded)
			.OrderBy(gameCard => gameCard.GamePlayerId)
			.ThenBy(gameCard => gameCard.DealOrder)
			.ToListAsync();

		persistedPlayerCards.Should().HaveCount(6);
		persistedPlayerCards.Should().NotContain(gameCard => ((IEnumerable<Guid>)staleCardIds).Contains(gameCard.Id));

		persistedPlayerCards
			.Where(gameCard => gameCard.GamePlayerId == firstPlayerId)
			.Select(gameCard => gameCard.DealOrder)
			.Should()
			.Equal(1, 2, 3);
	}

	[Fact]
	public async Task Handle_ThirdStreet_PostsBringInIntoPotAndContribution()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var pot = new Pot
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			Amount = 10,
			CreatedAt = DateTimeOffset.UtcNow
		};
		var bob = await context.GamePlayers.SingleAsync(player => player.SeatPosition == 1);
		context.PotContributions.Add(new PotContribution
		{
			Id = Guid.NewGuid(),
			PotId = pot.Id,
			GamePlayerId = bob.Id,
			Amount = 5,
			IsEligibleToWin = true,
			ContributedAt = DateTimeOffset.UtcNow
		});
		bob.TotalContributedThisHand = 5;
		context.Pots.Add(pot);
		await context.SaveChangesAsync();

		var sut = CreateSut(context);
		var result = await sut.Handle(new DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var bringInPlayer = await context.GamePlayers.SingleAsync(player => player.CurrentBet == 5);
		bringInPlayer.ChipStack.Should().Be(195);
		bringInPlayer.TotalContributedThisHand.Should().Be(10);
		bringInPlayer.IsAllIn.Should().BeFalse();

		var updatedPot = await context.Pots.SingleAsync();
		updatedPot.Amount.Should().Be(15);

		var contribution = await context.PotContributions.SingleAsync();
		contribution.Amount.Should().Be(10);
		contribution.GamePlayerId.Should().Be(bringInPlayer.Id);

		var bettingRound = await context.BettingRounds.SingleAsync();
		bettingRound.CurrentBet.Should().Be(5);
		var nextSeat = await context.GamePlayers.SingleAsync(player => player.Id != bringInPlayer.Id);
		bettingRound.CurrentActorIndex.Should().Be(nextSeat.SeatPosition);
	}

	[Fact]
	public async Task Handle_ThirdStreet_ShortStackBringInPostsRemainderAndMarksAllIn()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var players = await context.GamePlayers.ToListAsync();
		foreach (var player in players)
			player.ChipStack = 3;
		await context.SaveChangesAsync();

		var sut = CreateSut(context);
		var result = await sut.Handle(new DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var bringInPlayer = await context.GamePlayers.SingleAsync(player => player.CurrentBet > 0);
		bringInPlayer.CurrentBet.Should().Be(3);
		bringInPlayer.ChipStack.Should().Be(0);
		bringInPlayer.IsAllIn.Should().BeTrue();
		bringInPlayer.TotalContributedThisHand.Should().Be(3);

		var otherPlayer = await context.GamePlayers.SingleAsync(player => player.Id != bringInPlayer.Id);
		otherPlayer.CurrentBet.Should().Be(0);
		otherPlayer.ChipStack.Should().Be(3);
		otherPlayer.IsAllIn.Should().BeFalse();
	}

	[Fact]
	public async Task Handle_ThirdStreet_DealsClockwiseFromTheSeatLeftOfTheDealer()
	{
		await using var context = CreateContext();
		var game = await SeedClockwiseGameAsync(context, nameof(Phases.ThirdStreet), includeThirdStreetCards: false);
		var sut = CreateSut(context);

		var result = await sut.Handle(new DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var updated = await context.Games.SingleAsync();
		updated.BringInPlayerIndex.Should().Be(0);

		await AssertSeatCardsAsync(context, seatPosition: 1,
			(CardSymbol.Ace, CardSuit.Spades, CardLocation.Hole, false, 1),
			(CardSymbol.King, CardSuit.Hearts, CardLocation.Hole, false, 2),
			(CardSymbol.Queen, CardSuit.Spades, CardLocation.Board, true, 3));
		await AssertSeatCardsAsync(context, seatPosition: 2,
			(CardSymbol.Jack, CardSuit.Diamonds, CardLocation.Hole, false, 1),
			(CardSymbol.Ten, CardSuit.Clubs, CardLocation.Hole, false, 2),
			(CardSymbol.Nine, CardSuit.Hearts, CardLocation.Board, true, 3));
		await AssertSeatCardsAsync(context, seatPosition: 0,
			(CardSymbol.Eight, CardSuit.Diamonds, CardLocation.Hole, false, 1),
			(CardSymbol.Seven, CardSuit.Clubs, CardLocation.Hole, false, 2),
			(CardSymbol.Six, CardSuit.Spades, CardLocation.Board, true, 3));
	}

	[Fact]
	public async Task Handle_FourthStreet_ContinuesClockwiseFromTheSeatLeftOfTheDealer()
	{
		await using var context = CreateContext();
		var game = await SeedClockwiseGameAsync(context, nameof(Phases.FourthStreet), includeThirdStreetCards: true);
		var sut = CreateSut(context);

		var result = await sut.Handle(new DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		await AssertSeatCardsAsync(context, seatPosition: 1,
			(CardSymbol.Five, CardSuit.Hearts, CardLocation.Board, true, 4));
		await AssertSeatCardsAsync(context, seatPosition: 2,
			(CardSymbol.Four, CardSuit.Diamonds, CardLocation.Board, true, 4));
		await AssertSeatCardsAsync(context, seatPosition: 0,
			(CardSymbol.Three, CardSuit.Clubs, CardLocation.Board, true, 4));
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
		int deckCardCount = 6,
		bool addStaleCards = false)
	{
		var now = DateTimeOffset.UtcNow;

		var game = new Game
		{
			Id = Guid.NewGuid(),
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

		var aliceGamePlayer = CreateGamePlayer(game, alice, 0, now);
		var bobGamePlayer = CreateGamePlayer(game, bob, 1, now);

		context.Games.Add(game);
		context.Players.AddRange(alice, bob);
		context.GamePlayers.AddRange(aliceGamePlayer, bobGamePlayer);

		for (var cardIndex = 0; cardIndex < deckCardCount; cardIndex++)
			context.GameCards.Add(CreateDeckCard(game, cardIndex, now));

		if (addStaleCards) context.GameCards.AddRange(CreateStaleCards(game, aliceGamePlayer, now));

		await context.SaveChangesAsync();
		return game;
	}

	private static readonly (CardSymbol Symbol, CardSuit Suit)[] ClockwisePrefix =
	[
		(CardSymbol.Ace, CardSuit.Spades),
		(CardSymbol.King, CardSuit.Hearts),
		(CardSymbol.Queen, CardSuit.Spades),
		(CardSymbol.Jack, CardSuit.Diamonds),
		(CardSymbol.Ten, CardSuit.Clubs),
		(CardSymbol.Nine, CardSuit.Hearts),
		(CardSymbol.Eight, CardSuit.Diamonds),
		(CardSymbol.Seven, CardSuit.Clubs),
		(CardSymbol.Six, CardSuit.Spades),
		(CardSymbol.Five, CardSuit.Hearts),
		(CardSymbol.Four, CardSuit.Diamonds),
		(CardSymbol.Three, CardSuit.Clubs)
	];

	private static async Task<Game> SeedClockwiseGameAsync(
		CardsDbContext context,
		string currentPhase,
		bool includeThirdStreetCards)
	{
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.NewGuid(),
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
		var carol = CreatePlayer("Carol", now);
		var aliceGamePlayer = CreateGamePlayer(game, alice, 0, now);
		var bobGamePlayer = CreateGamePlayer(game, bob, 1, now);
		var carolGamePlayer = CreateGamePlayer(game, carol, 2, now);
		var seats = new Dictionary<int, GamePlayer>
		{
			[0] = aliceGamePlayer,
			[1] = bobGamePlayer,
			[2] = carolGamePlayer
		};

		context.Games.Add(game);
		context.Players.AddRange(alice, bob, carol);
		context.GamePlayers.AddRange(aliceGamePlayer, bobGamePlayer, carolGamePlayer);

		if (includeThirdStreetCards)
		{
			AddDealtThirdStreet(context, game, seats[1], ClockwisePrefix.Take(3).ToArray(), now);
			AddDealtThirdStreet(context, game, seats[2], ClockwisePrefix.Skip(3).Take(3).ToArray(), now);
			AddDealtThirdStreet(context, game, seats[0], ClockwisePrefix.Skip(6).Take(3).ToArray(), now);

			for (var cardIndex = 9; cardIndex < ClockwisePrefix.Length; cardIndex++)
				context.GameCards.Add(CreatePrefixDeckCard(game, cardIndex, now));
		}
		else
		{
			for (var cardIndex = 0; cardIndex < 9; cardIndex++)
				context.GameCards.Add(CreatePrefixDeckCard(game, cardIndex, now));
		}

		await context.SaveChangesAsync();
		return game;
	}

	private static void AddDealtThirdStreet(
		CardsDbContext context,
		Game game,
		GamePlayer player,
		(CardSymbol Symbol, CardSuit Suit)[] cards,
		DateTimeOffset now)
	{
		for (var index = 0; index < cards.Length; index++)
		{
			var faceUp = index == 2;
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				GamePlayerId = player.Id,
				GamePlayer = player,
				HandNumber = game.CurrentHandNumber,
				DealOrder = index + 1,
				Location = faceUp ? CardLocation.Board : CardLocation.Hole,
				IsVisible = faceUp,
				Symbol = cards[index].Symbol,
				Suit = cards[index].Suit,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.ThirdStreet)
			});
		}
	}

	private static GameCard CreatePrefixDeckCard(Game game, int cardIndex, DateTimeOffset now)
	{
		var card = ClockwisePrefix[cardIndex];
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			DealOrder = 100 + cardIndex,
			Location = CardLocation.Deck,
			IsVisible = false,
			Symbol = card.Symbol,
			Suit = card.Suit,
			DealtAt = now,
			DealtAtPhase = nameof(Phases.ThirdStreet)
		};
	}

	private static async Task AssertSeatCardsAsync(
		CardsDbContext context,
		int seatPosition,
		params (CardSymbol Symbol, CardSuit Suit, CardLocation Location, bool Visible, int DealOrder)[] expected)
	{
		var playerId = await context.GamePlayers
			.Where(player => player.SeatPosition == seatPosition)
			.Select(player => player.Id)
			.SingleAsync();

		var cards = await context.GameCards
			.Where(card => card.GamePlayerId == playerId && card.Location != CardLocation.Deck && !card.IsDiscarded)
			.OrderBy(card => card.DealOrder)
			.ToListAsync();

		var matched = cards
			.Where(card => expected.Any(item => item.DealOrder == card.DealOrder))
			.Select(card => (card.Symbol, card.Suit, card.Location, card.IsVisible, card.DealOrder))
			.ToArray();

		matched.Should().Equal(expected);
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
			Symbol = (CardSymbol)(cardIndex % 13 + 2),
			Suit = (CardSuit)(cardIndex % 4),
			DealtAt = now,
			DealtAtPhase = nameof(Phases.ThirdStreet)
		};
	}

	private static IReadOnlyList<GameCard> CreateStaleCards(Game game, GamePlayer player, DateTimeOffset now)
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