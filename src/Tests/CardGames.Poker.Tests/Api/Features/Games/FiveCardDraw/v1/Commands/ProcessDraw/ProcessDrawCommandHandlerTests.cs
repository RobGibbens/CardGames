using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;

public class ProcessDrawCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(Guid.NewGuid(), [0]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInDrawPhase_ReturnsNotInDrawPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, nameof(Phases.FirstBettingRound));
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [0]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.NotInDrawPhase);
	}

	[Fact]
	public async Task Handle_WhenDiscardIndexIsOutOfRange_ReturnsInvalidCardIndex()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [5]) { BoundSeat = 0 }, CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.InvalidCardIndex);
	}

	[Fact]
	public async Task Handle_WhenPlayerHasTooFewCards_ReturnsInvalidCardIndex()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPlayerCardCount: 4);
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [0]) { BoundSeat = 0 }, CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.InvalidCardIndex);
	}

	[Fact]
	public async Task Handle_WhenTooManyDiscardsWithoutAce_ReturnsTooManyDiscards()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPlayerHasAce: false);
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [0, 1, 2, 3]) { BoundSeat = 0 }, CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.TooManyDiscards);
	}

	[Fact]
	public async Task Handle_WhenCurrentDrawPlayerIndexIsMissing_ReturnsNoEligiblePlayers()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentDrawPlayerIndex: -1);
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, []), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.NoEligiblePlayers);
	}

	[Fact]
	public async Task Handle_WhenCurrentDrawPlayerSeatIsMissing_ReturnsNotPlayerTurn()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentDrawPlayerIndex: 99);
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, []), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.NotPlayerTurn);
	}

	[Fact]
	public async Task Handle_WhenStubIsShortAndNoEarlierDiscardExists_DealsStubAndOneOwnDiscard()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, deckCardCount: 1);
		var alice = await PlayerAtSeatAsync(context, game.Id, 0);
		var hand = await LiveHandAsync(context, alice.Id);
		var discardedIds = new[] { hand[0].Id, hand[1].Id };
		var stubId = await context.GameCards
			.Where(c => c.GameId == game.Id && c.Location == CardLocation.Deck)
			.Select(c => c.Id)
			.SingleAsync();
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [0, 1]) { BoundSeat = 0 }, CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.NewCards.Should().HaveCount(2);

		var cards = await context.GameCards.Where(c => c.GameId == game.Id).ToListAsync();
		var live = cards.Where(c => c.GamePlayerId == alice.Id && !c.IsDiscarded).ToList();
		live.Should().HaveCount(5);
		live.Should().Contain(c => c.Id == stubId && c.Location == CardLocation.Hole);

		var dealtBack = discardedIds.Where(id => live.Any(c => c.Id == id)).ToList();
		dealtBack.Should().ContainSingle();
		cards.Single(c => c.Id == dealtBack[0]).Location.Should().Be(CardLocation.Hole);
		cards.Single(c => c.Id == dealtBack[0]).IsDiscarded.Should().BeFalse();

		var leftDiscarded = discardedIds.Single(id => id != dealtBack[0]);
		var left = cards.Single(c => c.Id == leftDiscarded);
		left.IsDiscarded.Should().BeTrue();
		live.Should().NotContain(c => c.Id == leftDiscarded);
	}

	[Fact]
	public async Task Handle_WhenStubCoversTheDraw_DealsOnlyStubCards()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, deckCardCount: 5);
		var alice = await PlayerAtSeatAsync(context, game.Id, 0);
		var hand = await LiveHandAsync(context, alice.Id);
		var discardedIds = new[] { hand[0].Id, hand[1].Id };
		var deckIds = await context.GameCards
			.Where(c => c.GameId == game.Id && c.Location == CardLocation.Deck)
			.OrderBy(c => c.DealOrder)
			.Select(c => c.Id)
			.ToListAsync();
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [0, 1]) { BoundSeat = 0 }, CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.NewCards.Should().HaveCount(2);

		var cards = await context.GameCards.Where(c => c.GameId == game.Id).ToListAsync();
		var dealtIds = cards
			.Where(c => c.GamePlayerId == alice.Id && c.IsDrawnCard && !c.IsDiscarded)
			.Select(c => c.Id)
			.ToList();
		dealtIds.Should().BeEquivalentTo(deckIds.Take(2));
		dealtIds.Should().NotIntersectWith(discardedIds);

		cards.Where(c => deckIds.Skip(2).Contains(c.Id)).Should().OnlyContain(c =>
			c.Location == CardLocation.Deck && c.GamePlayerId == null);
	}

	[Fact]
	public async Task Handle_WhenStubIsShort_DealsEarlierDiscardBeforeOwnDiscard()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, deckCardCount: 1);
		var alice = await PlayerAtSeatAsync(context, game.Id, 0);
		var bob = await PlayerAtSeatAsync(context, game.Id, 1);
		var aliceHand = await LiveHandAsync(context, alice.Id);
		var aliceDiscardIds = new[] { aliceHand[0].Id, aliceHand[1].Id };
		var stub = await context.GameCards.SingleAsync(c => c.GameId == game.Id && c.Location == CardLocation.Deck);
		var bobDiscard = CreateDiscardedCard(game, bob, CardSymbol.Ace, CardSuit.Clubs, 50, DateTimeOffset.UtcNow);
		context.GameCards.Add(bobDiscard);
		await context.SaveChangesAsync();
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [0, 1]) { BoundSeat = 0 }, CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var newCards = result.AsT0.NewCards.ToList();
		newCards.Should().HaveCount(2);
		newCards[0].Symbol.Should().Be(stub.Symbol);
		newCards[0].Suit.Should().Be(stub.Suit);
		newCards[1].Symbol.Should().Be(CardSymbol.Ace);
		newCards[1].Suit.Should().Be(CardSuit.Clubs);

		var cards = await context.GameCards.Where(c => c.GameId == game.Id).ToListAsync();
		var dealtBobDiscard = cards.Single(c => c.Id == bobDiscard.Id);
		dealtBobDiscard.Location.Should().Be(CardLocation.Hole);
		dealtBobDiscard.GamePlayerId.Should().Be(alice.Id);
		dealtBobDiscard.IsDiscarded.Should().BeFalse();
		cards.Where(c => aliceDiscardIds.Contains(c.Id)).Should().OnlyContain(c => c.IsDiscarded);
		cards.Should().NotContain(c => c.Id == bobDiscard.Id && c.GamePlayerId == bob.Id && !c.IsDiscarded);
	}

	[Fact]
	public async Task Handle_WhenReturnedDiscardIsUnused_NextPlayerDrawsThatCard()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, deckCardCount: 0);
		var alice = await PlayerAtSeatAsync(context, game.Id, 0);
		var bob = await PlayerAtSeatAsync(context, game.Id, 1);
		var aliceHand = await LiveHandAsync(context, alice.Id);
		var aliceDiscardId = aliceHand[0].Id;
		var firstBobDiscard = CreateDiscardedCard(game, bob, CardSymbol.Seven, CardSuit.Hearts, 50, DateTimeOffset.UtcNow);
		var secondBobDiscard = CreateDiscardedCard(game, bob, CardSymbol.Ace, CardSuit.Clubs, 51, DateTimeOffset.UtcNow);
		context.GameCards.AddRange(firstBobDiscard, secondBobDiscard);
		await context.SaveChangesAsync();
		var bobDiscardIds = new[] { firstBobDiscard.Id, secondBobDiscard.Id };
		var sut = new ProcessDrawCommandHandler(context);

		var aliceResult = await sut.Handle(new ProcessDrawCommand(game.Id, [0]) { BoundSeat = 0 }, CancellationToken.None);

		aliceResult.IsT0.Should().BeTrue();
		aliceResult.AsT0.NewCards.Should().ContainSingle();

		var afterAlice = await context.GameCards.Where(c => c.GameId == game.Id).ToListAsync();
		var dealtToAlice = afterAlice.Single(c => bobDiscardIds.Contains(c.Id) && c.GamePlayerId == alice.Id);
		dealtToAlice.IsDiscarded.Should().BeFalse();
		var parked = afterAlice.Single(c => bobDiscardIds.Contains(c.Id) && c.Id != dealtToAlice.Id);
		parked.Location.Should().Be(CardLocation.Deck);
		parked.GamePlayerId.Should().BeNull();
		parked.IsDiscarded.Should().BeFalse();
		var remainingStubOrders = afterAlice
			.Where(c => c.Location == CardLocation.Deck && c.Id != parked.Id)
			.Select(c => c.DealOrder)
			.ToList();
		if (remainingStubOrders.Count > 0)
			parked.DealOrder.Should().BeGreaterThan(remainingStubOrders.Max());

		var bobResult = await sut.Handle(new ProcessDrawCommand(game.Id, [0]) { BoundSeat = 1 }, CancellationToken.None);

		bobResult.IsT0.Should().BeTrue();
		var cards = await context.GameCards.Where(c => c.GameId == game.Id).ToListAsync();
		var bobNewCard = cards.Single(c => c.Id == parked.Id);
		bobNewCard.GamePlayerId.Should().Be(bob.Id);
		bobNewCard.Location.Should().Be(CardLocation.Hole);
		bobNewCard.IsDiscarded.Should().BeFalse();
		cards.Single(c => c.Id == aliceDiscardId).IsDiscarded.Should().BeTrue();
		bobResult.AsT0.NewCards.Should().ContainSingle()
			.Which.Symbol.Should().Be(parked.Symbol);
	}

	[Fact]
	public async Task Handle_WhenBoundSeatIsThePreviousDrawSeat_ReturnsNotPlayerTurn()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var bob = await PlayerAtSeatAsync(context, game.Id, 1);
		var bobCardsBefore = (await LiveHandAsync(context, bob.Id)).Select(card => card.Id).ToList();
		var sut = new ProcessDrawCommandHandler(context);

		var first = await sut.Handle(new ProcessDrawCommand(game.Id, [0]) { BoundSeat = 0 }, CancellationToken.None);
		var second = await sut.Handle(new ProcessDrawCommand(game.Id, [0]) { BoundSeat = 0 }, CancellationToken.None);

		first.IsT0.Should().BeTrue();
		second.IsT1.Should().BeTrue();
		second.AsT1.Code.Should().Be(ProcessDrawErrorCode.NotPlayerTurn);
		second.AsT1.Message.Should().Be("It is not your turn.");
		var bobCardsAfter = (await LiveHandAsync(context, bob.Id)).Select(card => card.Id).ToList();
		bobCardsAfter.Should().Equal(bobCardsBefore);
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
		int currentDrawPlayerIndex = 0,
		int currentPlayerCardCount = 5,
		bool currentPlayerHasAce = false,
		int deckCardCount = 5)
	{
		var now = DateTimeOffset.UtcNow;

		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			CurrentDrawPlayerIndex = currentDrawPlayerIndex,
			CurrentPlayerIndex = currentDrawPlayerIndex,
			DealerPosition = 0,
			MinBet = 10,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var firstPlayer = CreatePlayer("Alice", now);
		var secondPlayer = CreatePlayer("Bob", now);

		var firstGamePlayer = CreateGamePlayer(game, firstPlayer, 0, now);
		var secondGamePlayer = CreateGamePlayer(game, secondPlayer, 1, now);

		context.Games.Add(game);
		context.Players.AddRange(firstPlayer, secondPlayer);
		context.GamePlayers.AddRange(firstGamePlayer, secondGamePlayer);

		var firstPlayerSymbols = currentPlayerHasAce
			? new[] { CardSymbol.Ace, CardSymbol.Three, CardSymbol.Four, CardSymbol.Five, CardSymbol.Six }
			: new[] { CardSymbol.Deuce, CardSymbol.Three, CardSymbol.Four, CardSymbol.Five, CardSymbol.Six };

		for (var cardIndex = 0; cardIndex < currentPlayerCardCount; cardIndex++)
			context.GameCards.Add(
				CreatePlayerCard(game, firstGamePlayer, cardIndex, firstPlayerSymbols[cardIndex], now));

		for (var cardIndex = 0; cardIndex < 5; cardIndex++)
			context.GameCards.Add(CreatePlayerCard(game, secondGamePlayer, cardIndex, (CardSymbol)(cardIndex + 8),
				now));

		for (var cardIndex = 0; cardIndex < deckCardCount; cardIndex++)
			context.GameCards.Add(CreateDeckCard(game, cardIndex, now));

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
			ChipStack = 100,
			StartingChips = 100,
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}

	private static GameCard CreatePlayerCard(
		Game game,
		GamePlayer player,
		int cardIndex,
		CardSymbol symbol,
		DateTimeOffset now)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player.Id,
			GamePlayer = player,
			HandNumber = game.CurrentHandNumber,
			DealOrder = cardIndex + 1,
			Location = CardLocation.Hole,
			IsVisible = false,
			Symbol = symbol,
			Suit = (CardSuit)(cardIndex % 4),
			DealtAt = now,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}

	private static async Task<GamePlayer> PlayerAtSeatAsync(CardsDbContext context, Guid gameId, int seat)
	{
		return await context.GamePlayers.SingleAsync(p => p.GameId == gameId && p.SeatPosition == seat);
	}

	private static async Task<List<GameCard>> LiveHandAsync(CardsDbContext context, Guid gamePlayerId)
	{
		return await context.GameCards
			.Where(c => c.GamePlayerId == gamePlayerId && !c.IsDiscarded)
			.OrderBy(c => c.DealOrder)
			.ToListAsync();
	}

	private static GameCard CreateDiscardedCard(
		Game game,
		GamePlayer player,
		CardSymbol symbol,
		CardSuit suit,
		int dealOrder,
		DateTimeOffset now)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player.Id,
			GamePlayer = player,
			HandNumber = game.CurrentHandNumber,
			DealOrder = dealOrder,
			Location = CardLocation.Hole,
			IsVisible = false,
			IsDiscarded = true,
			DiscardedAtDrawRound = 1,
			Symbol = symbol,
			Suit = suit,
			DealtAt = now,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}

	private static GameCard CreateDeckCard(Game game, int cardIndex, DateTimeOffset now)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = null,
			HandNumber = game.CurrentHandNumber,
			DealOrder = 100 + cardIndex,
			Location = CardLocation.Deck,
			IsVisible = false,
			Symbol = (CardSymbol)(cardIndex + 9),
			Suit = (CardSuit)(cardIndex % 4),
			DealtAt = now,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}
}