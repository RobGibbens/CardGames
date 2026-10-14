using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class KnownDeadCardsTests
{
	[Fact]
	public async Task LoadAsync_ReturnsOnlyTheCurrentHandsVisibleUndiscardedCardsOfFoldedPlayers()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		var gameId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		await using (var seedContext = CreateContext(databaseName, databaseRoot))
		{
			var game = new Game
			{
				Id = gameId,
				CurrentPhase = "ThirdStreet",
				CurrentHandNumber = 2,
				CreatedAt = now,
				UpdatedAt = now
			};

			var alicePlayer = new Player { Name = "Alice", CreatedAt = now, UpdatedAt = now };
			var bobPlayer = new Player { Name = "Bob", CreatedAt = now, UpdatedAt = now };
			var carolPlayer = new Player { Name = "Carol", CreatedAt = now, UpdatedAt = now };

			var alice = CreateSeat(game, alicePlayer, seat: 0, hasFolded: false, now);
			var bob = CreateSeat(game, bobPlayer, seat: 1, hasFolded: true, now);
			var carol = CreateSeat(game, carolPlayer, seat: 2, hasFolded: false, now);

			seedContext.Games.Add(game);
			seedContext.Players.AddRange(alicePlayer, bobPlayer, carolPlayer);
			seedContext.GamePlayers.AddRange(alice, bob, carol);
			seedContext.GameCards.AddRange(
				CreateCard(game, bob, handNumber: 2, CardSymbol.Nine, CardSuit.Clubs, CardLocation.Board,
					isVisible: true, isDiscarded: false, now),
				CreateCard(game, bob, handNumber: 2, CardSymbol.Three, CardSuit.Hearts, CardLocation.Hole,
					isVisible: false, isDiscarded: false, now),
				CreateCard(game, bob, handNumber: 2, CardSymbol.Ace, CardSuit.Spades, CardLocation.Hole,
					isVisible: false, isDiscarded: false, now),
				CreateCard(game, bob, handNumber: 1, CardSymbol.King, CardSuit.Diamonds, CardLocation.Board,
					isVisible: true, isDiscarded: false, now),
				CreateCard(game, carol, handNumber: 2, CardSymbol.Ten, CardSuit.Diamonds, CardLocation.Board,
					isVisible: true, isDiscarded: false, now),
				CreateCard(game, alice, handNumber: 2, CardSymbol.Deuce, CardSuit.Hearts, CardLocation.Hole,
					isVisible: false, isDiscarded: false, now),
				CreateCard(game, bob, handNumber: 2, CardSymbol.Queen, CardSuit.Clubs, CardLocation.Board,
					isVisible: true, isDiscarded: true, now));

			await seedContext.SaveChangesAsync();
		}

		await using var context = CreateContext(databaseName, databaseRoot);
		var deadCards = await KnownDeadCards.LoadAsync(context, gameId, handNumber: 2, CancellationToken.None);

		deadCards.Should().ContainSingle();
		deadCards.Single().Symbol.Should().Be(CardSymbol.Nine);
		deadCards.Single().Suit.Should().Be(CardSuit.Clubs);
		deadCards.Should().NotContain(card => card.Symbol == CardSymbol.Three && card.Suit == CardSuit.Hearts);
		deadCards.Should().NotContain(card => card.Symbol == CardSymbol.Ace && card.Suit == CardSuit.Spades);
		deadCards.Should().NotContain(card => card.Symbol == CardSymbol.King && card.Suit == CardSuit.Diamonds);
		deadCards.Should().NotContain(card => card.Symbol == CardSymbol.Ten && card.Suit == CardSuit.Diamonds);
		deadCards.Should().NotContain(card => card.Symbol == CardSymbol.Deuce && card.Suit == CardSuit.Hearts);
		deadCards.Should().NotContain(card => card.Symbol == CardSymbol.Queen && card.Suit == CardSuit.Clubs);
	}

	private static CardsDbContext CreateContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot)
			.Options;

		return new CardsDbContext(options);
	}

	private static GamePlayer CreateSeat(Game game, Player player, int seat, bool hasFolded, DateTimeOffset now)
	{
		return new GamePlayer
		{
			GameId = game.Id,
			Game = game,
			PlayerId = player.Id,
			Player = player,
			SeatPosition = seat,
			ChipStack = 200,
			StartingChips = 200,
			HasFolded = hasFolded,
			JoinedAt = now,
			Status = GamePlayerStatus.Active,
			RowVersion = [1]
		};
	}

	private static GameCard CreateCard(
		Game game,
		GamePlayer owner,
		int handNumber,
		CardSymbol symbol,
		CardSuit suit,
		CardLocation location,
		bool isVisible,
		bool isDiscarded,
		DateTimeOffset now)
	{
		return new GameCard
		{
			GameId = game.Id,
			Game = game,
			GamePlayerId = owner.Id,
			GamePlayer = owner,
			HandNumber = handNumber,
			DealOrder = 1,
			Location = location,
			IsVisible = isVisible,
			IsDiscarded = isDiscarded,
			Symbol = symbol,
			Suit = suit,
			DealtAt = now,
			RowVersion = [1]
		};
	}
}
