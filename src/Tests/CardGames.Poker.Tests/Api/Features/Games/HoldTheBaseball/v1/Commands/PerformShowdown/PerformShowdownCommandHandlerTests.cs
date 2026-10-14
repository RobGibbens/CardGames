using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;
using CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.HoldTheBaseball.v1.Commands.PerformShowdown;

public class PerformShowdownCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenThreePlayersTie_PaysOddChipsClockwiseFromTheDealer()
	{
		await using var context = CreateContext();
		var game = await SeedShowdownAsync(context, 5);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var payouts = result.AsT0.Payouts;
		payouts["Carol"].Should().Be(2);
		payouts["Alice"].Should().Be(2);
		payouts["Bob"].Should().Be(1);
		payouts.Values.Sum().Should().Be(5);
		await AssertStacksAsync(context, ("Carol", 102), ("Alice", 102), ("Bob", 101));
	}

	private static PerformShowdownCommandHandler CreateSut(CardsDbContext context)
	{
		return new PerformShowdownCommandHandler(
			context,
			Substitute.For<IHandHistoryRecorder>(),
			Substitute.For<IHandSettlementService>());
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
			.Options;

		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedShowdownAsync(CardsDbContext context, int potAmount)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "HOLDTHEBASEBALL",
			Name = "Hold the Baseball",
			BettingStructure = BettingStructure.Blinds,
			MinPlayers = 2,
			MaxPlayers = 10,
			InitialHoleCards = 2,
			InitialBoardCards = 0,
			MaxCommunityCards = 5,
			MaxPlayerCards = 7,
			HasDrawPhase = false,
			MaxDiscards = 0,
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
			DealerPosition = 3,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};
		context.GameTypes.Add(gameType);
		context.Games.Add(game);

		foreach (var (name, seat) in Seats)
		{
			var player = CreatePlayer(name, now);
			var gamePlayer = CreateGamePlayer(game, player, seat, now);
			context.Players.Add(player);
			context.GamePlayers.Add(gamePlayer);
			context.GameCards.Add(CreateHoleCard(game, gamePlayer, 1, CardSuit.Hearts, CardSymbol.Ace, now));
			context.GameCards.Add(CreateHoleCard(game, gamePlayer, 2, CardSuit.Diamonds, CardSymbol.Ace, now));
		}

		for (var cardIndex = 0; cardIndex < Community.Length; cardIndex++)
		{
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				HandNumber = game.CurrentHandNumber,
				DealOrder = cardIndex + 1,
				Location = CardLocation.Community,
				IsVisible = true,
				Suit = Community[cardIndex].Suit,
				Symbol = Community[cardIndex].Symbol,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing)
			});
		}

		context.Pots.Add(new Pot
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = potAmount,
			IsAwarded = false,
			CreatedAt = now
		});

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
			HasFolded = false,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}

	private static GameCard CreateHoleCard(
		Game game,
		GamePlayer player,
		int dealOrder,
		CardSuit suit,
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
			DealOrder = dealOrder,
			Location = CardLocation.Hole,
			IsVisible = false,
			Suit = suit,
			Symbol = symbol,
			DealtAt = now,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}

	private static async Task AssertStacksAsync(CardsDbContext context, params (string Name, int Stack)[] expected)
	{
		var stacks = await context.GamePlayers
			.Include(gamePlayer => gamePlayer.Player)
			.ToDictionaryAsync(gamePlayer => gamePlayer.Player.Name, gamePlayer => gamePlayer.ChipStack);

		foreach (var (name, stack) in expected)
			stacks[name].Should().Be(stack);
	}

	private static readonly (string Name, int Seat)[] Seats = [("Alice", 0), ("Bob", 3), ("Carol", 5)];

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] Community =
	[
		(CardSuit.Clubs, CardSymbol.King),
		(CardSuit.Clubs, CardSymbol.Queen),
		(CardSuit.Clubs, CardSymbol.Jack),
		(CardSuit.Clubs, CardSymbol.Ten),
		(CardSuit.Clubs, CardSymbol.Deuce)
	];
}
