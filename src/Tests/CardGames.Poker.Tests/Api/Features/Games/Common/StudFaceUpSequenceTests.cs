using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.Common;

public class StudFaceUpSequenceTests
{
	[Fact]
	public async Task LoadAsync_FinishesEachStreetBeforeTheNextAndWalksClockwiseFromTheDealer()
	{
		await using var context = CreateContext();
		var game = await SeedVisibleCardsAsync(context);

		var cards = await StudFaceUpSequence.LoadAsync(
			context,
			game.Id,
			game.CurrentHandNumber,
			game.DealerPosition,
			CancellationToken.None);

		string.Join(" ", cards).Should().Be("Qh 9c 4d 2s 3h 5c 7d 9d 7c 8d 4h 8c");
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
			.Options;
		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedVisibleCardsAsync(CardsDbContext context)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "FOLLOWTHEQUEEN",
			Name = "Follow the Queen",
			BettingStructure = BettingStructure.Ante,
			MinPlayers = 2,
			MaxPlayers = 8,
			CreatedAt = now,
			UpdatedAt = now
		};
		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = gameType.Id,
			GameType = gameType,
			CurrentPhase = nameof(Phases.Showdown),
			CurrentHandNumber = 1,
			DealerPosition = 2,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};
		var alice = Seat(game, CreatePlayer("Alice", now), 0, now);
		var bob = Seat(game, CreatePlayer("Bob", now), 1, now);
		var carol = Seat(game, CreatePlayer("Carol", now), 2, now);
		context.AddRange(gameType, game, alice.Player, bob.Player, carol.Player, alice, bob, carol);

		Add(context, game, alice, "Qh", 3, nameof(Phases.ThirdStreet), now);
		Add(context, game, alice, "2s", 4, nameof(Phases.FourthStreet), now);
		Add(context, game, alice, "7d", 5, nameof(Phases.FifthStreet), now);
		Add(context, game, alice, "8d", 6, nameof(Phases.SixthStreet), now);
		Add(context, game, bob, "9c", 6, nameof(Phases.ThirdStreet), now);
		Add(context, game, bob, "3h", 4, nameof(Phases.FourthStreet), now);
		Add(context, game, bob, "9d", 5, nameof(Phases.FifthStreet), now);
		Add(context, game, bob, "4h", 6, nameof(Phases.SixthStreet), now);
		Add(context, game, carol, "4d", 9, nameof(Phases.ThirdStreet), now);
		Add(context, game, carol, "5c", 4, nameof(Phases.FourthStreet), now);
		Add(context, game, carol, "7c", 5, nameof(Phases.FifthStreet), now);
		Add(context, game, carol, "8c", 6, nameof(Phases.SixthStreet), now);

		await context.SaveChangesAsync();
		return game;
	}

	private static void Add(
		CardsDbContext context,
		Game game,
		GamePlayer player,
		string card,
		int dealOrder,
		string phase,
		DateTimeOffset now)
	{
		context.GameCards.Add(new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player.Id,
			GamePlayer = player,
			HandNumber = game.CurrentHandNumber,
			DealOrder = dealOrder,
			Location = CardLocation.Board,
			IsVisible = true,
			Suit = ParseSuit(card[^1]),
			Symbol = ParseSymbol(card[..^1]),
			DealtAt = now,
			DealtAtPhase = phase
		});
	}

	private static CardSuit ParseSuit(char suit)
	{
		return suit switch
		{
			'h' => CardSuit.Hearts,
			'd' => CardSuit.Diamonds,
			's' => CardSuit.Spades,
			'c' => CardSuit.Clubs,
			_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit")
		};
	}

	private static CardSymbol ParseSymbol(string symbol)
	{
		return symbol switch
		{
			"2" => CardSymbol.Deuce,
			"3" => CardSymbol.Three,
			"4" => CardSymbol.Four,
			"5" => CardSymbol.Five,
			"7" => CardSymbol.Seven,
			"8" => CardSymbol.Eight,
			"9" => CardSymbol.Nine,
			"Q" => CardSymbol.Queen,
			_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown symbol")
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

	private static GamePlayer Seat(Game game, Player player, int seat, DateTimeOffset now)
	{
		return new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = player.Id,
			Player = player,
			SeatPosition = seat,
			ChipStack = 900,
			Status = GamePlayerStatus.Active,
			JoinedAt = now
		};
	}
}
