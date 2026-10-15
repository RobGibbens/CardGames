using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Testing;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using BaseballDealHands = CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;
using FiveCardDrawDealHands = CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using FollowTheQueenDealHands = CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.DealHands;
using GoodBadUglyDealHands = CardGames.Poker.Api.Features.Games.GoodBadUgly.v1.Commands.DealHands;
using StudDealHands = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands;
using TollboothDealHands = CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.DealHands;
using TwosJacksDealHands = CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.DealHands;

namespace CardGames.Poker.Tests.Api.Features.Games.DealHands;

public class DealHandsResponseOmitsHoleCardsTests
{
	private static readonly JsonSerializerOptions HttpJson = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter() }
	};

	[Fact]
	public async Task StudThirdStreet_OmitsHoleCardsAndKeepsDoors()
	{
		await using var context = CreateContext();
		var game = await SeedStreetGameAsync(context, nameof(Phases.ThirdStreet), dealerPosition: 1, deck:
		[
			(CardSuit.Spades, CardSymbol.Ace),
			(CardSuit.Diamonds, CardSymbol.Deuce),
			(CardSuit.Hearts, CardSymbol.King),
			(CardSuit.Clubs, CardSymbol.Nine),
			(CardSuit.Hearts, CardSymbol.Three),
			(CardSuit.Clubs, CardSymbol.Jack)
		]);

		var result = await new StudDealHands.DealHandsCommandHandler(context, Substitute.For<ILogger<StudDealHands.DealHandsCommandHandler>>())
			.Handle(new StudDealHands.DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		var json = JsonSerializer.Serialize(success, HttpJson);

		success.GameId.Should().Be(game.Id);
		success.CurrentPhase.Should().Be(nameof(Phases.ThirdStreet));
		success.HandNumber.Should().Be(1);
		success.CurrentPlayerName.Should().NotBeNullOrWhiteSpace();
		success.PlayerHands.Should().HaveCount(2);
		success.PlayerHands.Single(hand => hand.SeatPosition == 0).Cards.Should().Equal(
			new StudDealHands.DealtCard { Suit = CardSuit.Hearts, Symbol = CardSymbol.King, DealOrder = 3 });
		success.PlayerHands.Single(hand => hand.SeatPosition == 1).Cards.Should().Equal(
			new StudDealHands.DealtCard { Suit = CardSuit.Clubs, Symbol = CardSymbol.Jack, DealOrder = 3 });

		json.Should().Contain("\"playerHands\"");
		json.Should().Contain("\"suit\":\"Hearts\"").And.Contain("\"symbol\":\"King\"");
		json.Should().Contain("\"suit\":\"Clubs\"").And.Contain("\"symbol\":\"Jack\"");
		json.Should().NotContain("Spades").And.NotContain("Ace").And.NotContain("Diamonds").And.NotContain("Deuce");
		json.Should().NotContain("Nine").And.NotContain("Three");

		var persisted = await context.GameCards.Where(card => card.GameId == game.Id && card.Location != CardLocation.Deck)
			.ToListAsync();
		var aliceId = await context.GamePlayers
			.Where(player => player.GameId == game.Id && player.SeatPosition == 0)
			.Select(player => player.Id)
			.SingleAsync();
		persisted.Should().HaveCount(6);
		persisted.Where(card => card.Location == CardLocation.Hole).Should().OnlyContain(card => !card.IsVisible);
		persisted.Where(card => card.Location == CardLocation.Board).Should().OnlyContain(card => card.IsVisible);
		persisted.Single(card => card.Location == CardLocation.Board && card.GamePlayerId == aliceId)
			.Symbol.Should().Be(CardSymbol.King);
	}

	[Fact]
	public async Task RazzThirdStreet_OmitsTheSameHoleCardsAndBringsInTheHigherDoor()
	{
		await using var context = CreateContext();
		var game = await SeedStreetGameAsync(
			context,
			nameof(Phases.ThirdStreet),
			dealerPosition: 1,
			currentHandGameTypeCode: "RAZZ",
			deck:
			[
				(CardSuit.Spades, CardSymbol.Ace),
				(CardSuit.Diamonds, CardSymbol.Deuce),
				(CardSuit.Hearts, CardSymbol.King),
				(CardSuit.Clubs, CardSymbol.Nine),
				(CardSuit.Hearts, CardSymbol.Three),
				(CardSuit.Clubs, CardSymbol.Jack)
			]);

		var result = await new StudDealHands.DealHandsCommandHandler(context, Substitute.For<ILogger<StudDealHands.DealHandsCommandHandler>>())
			.Handle(new StudDealHands.DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var json = JsonSerializer.Serialize(result.AsT0, HttpJson);
		json.Should().Contain("\"symbol\":\"King\"").And.Contain("\"symbol\":\"Jack\"");
		json.Should().NotContain("Spades").And.NotContain("Ace").And.NotContain("Diamonds").And.NotContain("Deuce");
		json.Should().NotContain("Nine").And.NotContain("Three");

		var stored = await context.Games.SingleAsync(candidate => candidate.Id == game.Id);
		stored.BringInPlayerIndex.Should().Be(0);
	}

	[Fact]
	public async Task StudSeventhStreet_OmitsTheNewHoleCard()
	{
		await using var context = CreateContext();
		var game = await SeedStreetGameAsync(context, nameof(Phases.SeventhStreet), playerCount: 1, deck:
		[
			(CardSuit.Spades, CardSymbol.Queen)
		]);

		var result = await new StudDealHands.DealHandsCommandHandler(context, Substitute.For<ILogger<StudDealHands.DealHandsCommandHandler>>())
			.Handle(new StudDealHands.DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		var json = JsonSerializer.Serialize(success, HttpJson);
		success.PlayerHands.Should().ContainSingle().Which.Cards.Should().BeEmpty();
		json.Should().Contain("\"playerHands\"").And.Contain("\"cards\":[]");
		json.Should().NotContain("Spades").And.NotContain("Queen");

		var hole = await context.GameCards.SingleAsync(card => card.GameId == game.Id && card.Location == CardLocation.Hole);
		hole.IsVisible.Should().BeFalse();
		hole.Suit.Should().Be(CardSuit.Spades);
		hole.Symbol.Should().Be(CardSymbol.Queen);
	}

	[Fact]
	public async Task TollboothThirdStreet_ReturnsOnlyDoorsAndKeepsDisplayCardsOnTheTable()
	{
		await using var context = CreateContext();
		var game = await SeedStreetGameAsync(context, nameof(Phases.ThirdStreet), dealerPosition: 1, deck:
		[
			(CardSuit.Spades, CardSymbol.Ace),
			(CardSuit.Diamonds, CardSymbol.Deuce),
			(CardSuit.Hearts, CardSymbol.King),
			(CardSuit.Clubs, CardSymbol.Nine),
			(CardSuit.Hearts, CardSymbol.Three),
			(CardSuit.Clubs, CardSymbol.Jack),
			(CardSuit.Spades, CardSymbol.Queen),
			(CardSuit.Diamonds, CardSymbol.Ten)
		]);
		var stud = new StudDealHands.DealHandsCommandHandler(context, Substitute.For<ILogger<StudDealHands.DealHandsCommandHandler>>());
		var handler = new TollboothDealHands.DealHandsCommandHandler(stud, context);

		var result = await handler.Handle(new TollboothDealHands.DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var json = JsonSerializer.Serialize(result.AsT0, HttpJson);
		result.AsT0.PlayerHands.Should().OnlyContain(hand => hand.Cards.Count == 1);
		json.Should().Contain("\"symbol\":\"King\"").And.Contain("\"symbol\":\"Jack\"");
		json.Should().NotContain("Queen").And.NotContain("Ten").And.NotContain("Ace").And.NotContain("Deuce").And.NotContain("Nine");

		var community = await context.GameCards
			.Where(card => card.GameId == game.Id && card.Location == CardLocation.Community)
			.ToListAsync();
		community.Should().HaveCount(2);
		community.Should().OnlyContain(card => card.IsVisible);
	}

	[Fact]
	public async Task FiveCardDraw_ReturnsEmptyCardArraysAndKeepsHoleRows()
	{
		await using var context = CreateContext();
		var game = await SeedDrawGameAsync(context);
		var dealt = new (CardSuit Suit, CardSymbol Symbol)[]
		{
			(CardSuit.Spades, CardSymbol.Ace),
			(CardSuit.Hearts, CardSymbol.King),
			(CardSuit.Diamonds, CardSymbol.Queen),
			(CardSuit.Clubs, CardSymbol.Jack),
			(CardSuit.Spades, CardSymbol.Ten),
			(CardSuit.Hearts, CardSymbol.Nine),
			(CardSuit.Diamonds, CardSymbol.Eight),
			(CardSuit.Clubs, CardSymbol.Seven),
			(CardSuit.Spades, CardSymbol.Six),
			(CardSuit.Hearts, CardSymbol.Five)
		};
		ScriptedDeckStore.Instance.Set(game.Id, dealt);

		var result = await new FiveCardDrawDealHands.DealHandsCommandHandler(context)
			.Handle(new FiveCardDrawDealHands.DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		var json = JsonSerializer.Serialize(success, HttpJson);
		success.PlayerHands.Should().HaveCount(2);
		success.PlayerHands.Should().OnlyContain(hand => hand.Cards.Count == 0);
		success.CurrentPhase.Should().Be(nameof(Phases.FirstBettingRound));
		json.Should().Contain("\"playerHands\"").And.Contain("\"cards\":[]");
		foreach (var card in dealt)
		{
			json.Should().NotContain(card.Suit.ToString());
			json.Should().NotContain(card.Symbol.ToString());
		}

		var rows = await context.GameCards.Where(card => card.GameId == game.Id && card.Location == CardLocation.Hole)
			.ToListAsync();
		rows.Should().HaveCount(10);
		rows.Should().OnlyContain(card => !card.IsVisible);
		var expectedOrder = new[] { 1, 2, 3, 4, 5 };
		rows.GroupBy(card => card.GamePlayerId).Should().OnlyContain(group =>
			group.Select(card => card.DealOrder).OrderBy(order => order).SequenceEqual(expectedOrder));
	}

	[Fact]
	public async Task TwosJacks_ReturnsEmptyCardArraysAndKeepsHoleRows()
	{
		await using var context = CreateContext();
		var game = await SeedDrawGameAsync(context);
		var dealt = new (CardSuit Suit, CardSymbol Symbol)[]
		{
			(CardSuit.Clubs, CardSymbol.Ace),
			(CardSuit.Diamonds, CardSymbol.King),
			(CardSuit.Hearts, CardSymbol.Queen),
			(CardSuit.Spades, CardSymbol.Jack),
			(CardSuit.Clubs, CardSymbol.Ten)
		};
		ScriptedDeckStore.Instance.Set(game.Id, CompleteDeck(dealt));

		var result = await new TwosJacksDealHands.DealHandsCommandHandler(context)
			.Handle(new TwosJacksDealHands.DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var json = JsonSerializer.Serialize(result.AsT0, HttpJson);
		result.AsT0.PlayerHands.Should().HaveCount(2);
		result.AsT0.PlayerHands.Should().OnlyContain(hand => hand.Cards.Count == 0);
		json.Should().NotContain("Ace").And.NotContain("King").And.NotContain("Queen").And.NotContain("Jack");
		json.Should().NotContain("Spades").And.NotContain("Hearts").And.NotContain("Diamonds").And.NotContain("Clubs");

		var rows = await context.GameCards.Where(card => card.GameId == game.Id && card.Location == CardLocation.Hole)
			.ToListAsync();
		rows.Should().HaveCount(10);
		rows.Should().OnlyContain(card => !card.IsVisible);
	}

	[Fact]
	public async Task GoodBadUgly_OmitsHoleAndCommunityRanks()
	{
		await using var context = CreateContext();
		var holeAndCommunity = new (CardSuit Suit, CardSymbol Symbol)[]
		{
			(CardSuit.Spades, CardSymbol.Ace),
			(CardSuit.Hearts, CardSymbol.King),
			(CardSuit.Diamonds, CardSymbol.Queen),
			(CardSuit.Clubs, CardSymbol.Jack),
			(CardSuit.Spades, CardSymbol.Ten),
			(CardSuit.Hearts, CardSymbol.Nine),
			(CardSuit.Diamonds, CardSymbol.Eight),
			(CardSuit.Clubs, CardSymbol.Seven),
			(CardSuit.Spades, CardSymbol.Six),
			(CardSuit.Hearts, CardSymbol.Five),
			(CardSuit.Diamonds, CardSymbol.Four)
		};
		var game = await SeedStreetGameAsync(
			context,
			nameof(Phases.ThirdStreet),
			gameTypeCode: "GOODBADUGLY",
			deck: holeAndCommunity);

		var result = await new GoodBadUglyDealHands.DealHandsCommandHandler(
				context,
				Substitute.For<ILogger<GoodBadUglyDealHands.DealHandsCommandHandler>>())
			.Handle(new GoodBadUglyDealHands.DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var json = JsonSerializer.Serialize(result.AsT0, HttpJson);
		result.AsT0.PlayerHands.Should().HaveCount(2);
		result.AsT0.PlayerHands.Should().OnlyContain(hand => hand.Cards.Count == 0);
		json.Should().Contain("\"playerHands\"").And.Contain("\"cards\":[]");
		json.Should().NotContain("TheGood").And.NotContain("TheBad").And.NotContain("TheUgly");
		foreach (var card in holeAndCommunity)
			json.Should().NotContain(card.Symbol.ToString());

		var community = await context.GameCards
			.Where(card => card.GameId == game.Id && card.Location == CardLocation.Community)
			.OrderBy(card => card.DealOrder)
			.ToListAsync();
		community.Should().HaveCount(3);
		community.Should().OnlyContain(card => !card.IsVisible);
		community.Select(card => card.DealtAtPhase).Should().Equal("TheGood", "TheBad", "TheUgly");
	}

	[Fact]
	public async Task BaseballThirdStreet_KeepsDoorsIncludingAFour()
	{
		await using var context = CreateContext();
		var game = await SeedStreetGameAsync(context, nameof(Phases.ThirdStreet), deck:
		[
			(CardSuit.Spades, CardSymbol.Five),
			(CardSuit.Diamonds, CardSymbol.Six),
			(CardSuit.Hearts, CardSymbol.King),
			(CardSuit.Clubs, CardSymbol.Seven),
			(CardSuit.Hearts, CardSymbol.Eight),
			(CardSuit.Diamonds, CardSymbol.Jack)
		]);

		var handler = new BaseballDealHands.DealHandsCommandHandler(
			context,
			Substitute.For<ILogger<BaseballDealHands.DealHandsCommandHandler>>());
		var result = await handler.Handle(new BaseballDealHands.DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var json = JsonSerializer.Serialize(result.AsT0, HttpJson);
		json.Should().Contain("\"symbol\":\"King\"").And.Contain("\"symbol\":\"Jack\"");
		json.Should().NotContain("Five").And.NotContain("Six").And.NotContain("Seven").And.NotContain("Eight");
		json.Should().NotContain("Spades");
		var stored = await context.Games.SingleAsync(candidate => candidate.Id == game.Id);
		stored.BringInPlayerIndex.Should().Be(0);

		await using var fourContext = CreateContext();
		var fourGame = await SeedStreetGameAsync(fourContext, nameof(Phases.ThirdStreet), deck:
		[
			(CardSuit.Spades, CardSymbol.Five),
			(CardSuit.Diamonds, CardSymbol.Six),
			(CardSuit.Clubs, CardSymbol.Four)
		]);
		var fourResult = await new BaseballDealHands.DealHandsCommandHandler(
				fourContext,
				Substitute.For<ILogger<BaseballDealHands.DealHandsCommandHandler>>())
			.Handle(new BaseballDealHands.DealHandsCommand(fourGame.Id), CancellationToken.None);

		fourResult.IsT0.Should().BeTrue();
		var fourJson = JsonSerializer.Serialize(fourResult.AsT0, HttpJson);
		fourResult.AsT0.CurrentPhase.Should().Be(nameof(Phases.BuyCardOffer));
		fourResult.AsT0.PlayerHands.Should().ContainSingle().Which.Cards.Should().Equal(
			new BaseballDealHands.DealtCard { Suit = CardSuit.Clubs, Symbol = CardSymbol.Four, DealOrder = 3 });
		fourJson.Should().Contain("\"symbol\":\"Four\"").And.Contain("\"suit\":\"Clubs\"");
		fourJson.Should().NotContain("Five").And.NotContain("Six").And.NotContain("Spades").And.NotContain("Diamonds");
		var door = await fourContext.GameCards.SingleAsync(card =>
			card.GameId == fourGame.Id && card.Location == CardLocation.Board);
		door.IsVisible.Should().BeTrue();
		door.Symbol.Should().Be(CardSymbol.Four);
	}

	[Fact]
	public async Task FollowTheQueenThirdStreet_OmitsHoleCardsAndKeepsTheDoor()
	{
		await using var context = CreateContext();
		var game = await SeedStreetGameAsync(context, nameof(Phases.ThirdStreet), dealerPosition: 1, deck:
		[
			(CardSuit.Spades, CardSymbol.Ace),
			(CardSuit.Diamonds, CardSymbol.Deuce),
			(CardSuit.Hearts, CardSymbol.King),
			(CardSuit.Clubs, CardSymbol.Nine),
			(CardSuit.Hearts, CardSymbol.Three),
			(CardSuit.Clubs, CardSymbol.Jack)
		]);

		var result = await new FollowTheQueenDealHands.DealHandsCommandHandler(
				context,
				Substitute.For<ILogger<FollowTheQueenDealHands.DealHandsCommandHandler>>())
			.Handle(new FollowTheQueenDealHands.DealHandsCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var json = JsonSerializer.Serialize(result.AsT0, HttpJson);
		result.AsT0.PlayerHands.Should().OnlyContain(hand =>
			hand.Cards.Count == 1 && hand.Cards[0].DealOrder == 3);
		json.Should().Contain("\"symbol\":\"King\"").And.Contain("\"symbol\":\"Jack\"");
		json.Should().NotContain("Ace").And.NotContain("Deuce").And.NotContain("Nine").And.NotContain("Three");
		json.Should().NotContain("Spades").And.NotContain("Diamonds");

		var stored = await context.Games.SingleAsync(candidate => candidate.Id == game.Id);
		stored.BringInPlayerIndex.Should().Be(1);
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
			.Options;
		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedDrawGameAsync(CardsDbContext context)
	{
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = nameof(Phases.Dealing),
			CurrentHandNumber = 1,
			DealerPosition = 0,
			MinBet = 10,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};
		AddPlayers(context, game, now, 2);
		context.Games.Add(game);
		await context.SaveChangesAsync();
		return game;
	}

	private static async Task<Game> SeedStreetGameAsync(
		CardsDbContext context,
		string phase,
		IReadOnlyList<(CardSuit Suit, CardSymbol Symbol)> deck,
		int playerCount = 2,
		int dealerPosition = 0,
		string? gameTypeCode = null,
		string? currentHandGameTypeCode = null)
	{
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = phase,
			CurrentHandNumber = 1,
			DealerPosition = dealerPosition,
			CurrentHandGameTypeCode = currentHandGameTypeCode,
			BringIn = 0,
			SmallBet = 10,
			BigBet = 20,
			MinBet = 10,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		if (gameTypeCode is not null)
		{
			var gameType = new GameType
			{
				Id = Guid.NewGuid(),
				Code = gameTypeCode,
				Name = gameTypeCode,
				BettingStructure = BettingStructure.AnteBringIn,
				MinPlayers = 2,
				MaxPlayers = 8,
				CreatedAt = now,
				UpdatedAt = now
			};
			game.GameTypeId = gameType.Id;
			game.GameType = gameType;
			context.GameTypes.Add(gameType);
		}

		AddPlayers(context, game, now, playerCount);
		context.Games.Add(game);

		for (var index = 0; index < deck.Count; index++)
		{
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				HandNumber = game.CurrentHandNumber,
				DealOrder = index + 1,
				Location = CardLocation.Deck,
				IsVisible = false,
				Suit = deck[index].Suit,
				Symbol = deck[index].Symbol,
				DealtAt = now
			});
		}

		await context.SaveChangesAsync();
		return game;
	}

	private static void AddPlayers(CardsDbContext context, Game game, DateTimeOffset now, int playerCount)
	{
		for (var seat = 0; seat < playerCount; seat++)
		{
			var player = new Player
			{
				Id = Guid.NewGuid(),
				Name = seat == 0 ? "Alice" : "Bob",
				CreatedAt = now,
				UpdatedAt = now
			};
			var gamePlayer = new GamePlayer
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				PlayerId = player.Id,
				Player = player,
				SeatPosition = seat,
				ChipStack = 200,
				StartingChips = 200,
				Status = GamePlayerStatus.Active,
				JoinedAt = now,
				RowVersion = [1]
			};
			context.Players.Add(player);
			context.GamePlayers.Add(gamePlayer);
		}
	}

	private static List<(CardSuit Suit, CardSymbol Symbol)> CompleteDeck(
		IReadOnlyList<(CardSuit Suit, CardSymbol Symbol)> leading)
	{
		var cards = new List<(CardSuit Suit, CardSymbol Symbol)>(leading);
		var seen = leading.ToHashSet();
		foreach (var suit in Enum.GetValues<CardSuit>())
		foreach (var symbol in Enum.GetValues<CardSymbol>())
		{
			var card = (suit, symbol);
			if (seen.Add(card))
				cards.Add(card);
		}

		return cards;
	}
}
