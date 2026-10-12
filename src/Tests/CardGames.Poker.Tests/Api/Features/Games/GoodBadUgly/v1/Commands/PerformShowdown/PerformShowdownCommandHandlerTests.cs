using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.GoodBadUgly.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.GoodBadUgly.v1.Commands.PerformShowdown;

public class PerformShowdownCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(Guid.NewGuid()), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PerformShowdownErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInShowdown_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, nameof(Phases.SixthStreet));
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PerformShowdownErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain(nameof(Phases.SixthStreet));
	}

	[Fact]
	public async Task Handle_WhenNoEligibleWinningHandCanBeDetermined_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, nameof(Phases.Showdown));
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PerformShowdownErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain("No eligible winning hand");
	}

	[Fact]
	public async Task Handle_WhenTheGoodCardIsADeuce_HighlightsTheWildStraightFlush()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, nameof(Phases.Showdown));
		var seats = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		var alice = seats.Single(gamePlayer => gamePlayer.Player.Name == "Alice");
		var bob = seats.Single(gamePlayer => gamePlayer.Player.Name == "Bob");
		var now = DateTimeOffset.UtcNow;
		AddPlayerCards(context, game, alice, ["Kh", "Kd", "Ah", "Qh", "Jh"], now);
		AddPlayerCards(context, game, bob, ["9s", "8c", "7d", "6s", "4c"], now);
		AddCommunityCard(context, game, "2c", 1, "TheGood", now);
		AddCommunityCard(context, game, "3d", 2, "TheBad", now);
		await context.SaveChangesAsync();
		var recorder = new CapturingHandHistoryRecorder();
		var sut = new PerformShowdownCommandHandler(context, recorder, Substitute.For<IHandSettlementService>());

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var aliceHand = result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Alice");
		aliceHand.HandType.Should().Be("StraightFlush");
		aliceHand.IsWinner.Should().BeTrue();
		var highlighted = aliceHand.BestCardIndexes!.Select(index => Format(aliceHand.Cards[index])).ToList();
		highlighted.Should().BeEquivalentTo("2c", "Ah", "Kh", "Qh", "Jh");
		highlighted.Should().NotContain("Kd");
		highlighted.Should().NotContain("3d");
		recorder.Last.Should().NotBeNull();
		recorder.Last!.PlayerResults.Single(player => player.PlayerName == "Alice").ShowdownCards
			.Should().BeEquivalentTo("2c", "Ah", "Kh", "Qh", "Jh");
	}

	[Fact]
	public async Task Handle_WhenAnEliminatedPlayerCoveredMoreThanTheLivePlayer_ReturnsTheExtra()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, nameof(Phases.Showdown));
		var seats = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		var alice = seats.Single(seat => seat.Player.Name == "Alice");
		var bob = seats.Single(seat => seat.Player.Name == "Bob");
		alice.ChipStack = 500;
		alice.TotalContributedThisHand = 100;
		bob.ChipStack = 0;
		bob.IsAllIn = true;
		bob.TotalContributedThisHand = 300;
		bob.VariantState = "UGLY_ELIMINATED";
		var pot = await context.Pots.SingleAsync();
		pot.Amount = 400;
		var now = DateTimeOffset.UtcNow;
		AddPlayerCards(context, game, alice, ["9s", "8c", "7d", "6s", "4c"], now);
		AddPlayerCards(context, game, bob, ["Ah", "Ad", "As", "Ac", "Kh"], now);
		await context.SaveChangesAsync();
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Alice").AmountWon.Should().Be(200);
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Bob").AmountWon.Should().Be(0);
		var saved = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		saved.Single(seat => seat.Player.Name == "Alice").ChipStack.Should().Be(700);
		var savedBob = saved.Single(seat => seat.Player.Name == "Bob");
		savedBob.ChipStack.Should().Be(200);
		savedBob.TotalContributedThisHand.Should().Be(100);
	}

	[Fact]
	public async Task Handle_WhenEveryRemainingPlayerIsEliminated_SplitsTheContestedPotAndReturnsTheExtra()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, nameof(Phases.Showdown));
		var seats = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		var alice = seats.Single(seat => seat.Player.Name == "Alice");
		var bob = seats.Single(seat => seat.Player.Name == "Bob");
		alice.ChipStack = 0;
		alice.IsAllIn = true;
		alice.TotalContributedThisHand = 100;
		alice.VariantState = "UGLY_ELIMINATED";
		bob.ChipStack = 0;
		bob.IsAllIn = true;
		bob.TotalContributedThisHand = 300;
		bob.VariantState = "UGLY_ELIMINATED";
		var pot = await context.Pots.SingleAsync();
		pot.Amount = 400;
		var now = DateTimeOffset.UtcNow;
		AddPlayerCards(context, game, alice, ["As", "Ks", "Qs", "Js", "9d"], now);
		AddPlayerCards(context, game, bob, ["Ad", "Kd", "Qd", "Jd", "9c"], now);
		await context.SaveChangesAsync();
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Alice").AmountWon.Should().Be(100);
		result.AsT0.PlayerHands.Single(hand => hand.PlayerName == "Bob").AmountWon.Should().Be(100);
		var saved = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		saved.Single(seat => seat.Player.Name == "Alice").ChipStack.Should().Be(100);
		var savedBob = saved.Single(seat => seat.Player.Name == "Bob");
		savedBob.ChipStack.Should().Be(300);
		savedBob.TotalContributedThisHand.Should().Be(100);
	}

	[Fact]
	public async Task Handle_WhenAliceAndBobTieForAnOddPot_PaysSeatOneTheLeftoverChip()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, nameof(Phases.Showdown));
		var seats = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		var alice = seats.Single(seat => seat.Player.Name == "Alice");
		var bob = seats.Single(seat => seat.Player.Name == "Bob");
		alice.SeatPosition = 3;
		alice.ChipStack = 100;
		alice.TotalContributedThisHand = 50;
		bob.SeatPosition = 1;
		bob.ChipStack = 100;
		bob.TotalContributedThisHand = 50;
		var now = DateTimeOffset.UtcNow;
		var carolPlayer = CreatePlayer("Carol", now);
		var carol = CreateGamePlayer(game, carolPlayer, 2, now);
		carol.HasFolded = true;
		carol.ChipStack = 100;
		carol.TotalContributedThisHand = 1;
		context.Add(carolPlayer);
		context.Add(carol);
		var pot = await context.Pots.SingleAsync();
		pot.Amount = 101;
		AddPlayerCards(context, game, alice, ["Ah", "Ad", "Kc", "Qd", "8s"], now);
		AddPlayerCards(context, game, bob, ["Ac", "As", "Kd", "Qc", "8d"], now);
		await context.SaveChangesAsync();
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);
		result.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 51, ["Alice"] = 50 });
		var saved = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player).ToListAsync();
		saved.Single(seat => seat.Player.Name == "Bob").ChipStack.Should().Be(151);
		saved.Single(seat => seat.Player.Name == "Alice").ChipStack.Should().Be(150);
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
		return CreateContext(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot());
	}

	private static CardsDbContext CreateContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot)
			.Options;

		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedGameAsync(CardsDbContext context, string currentPhase)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
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

		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = gameType.Id,
			GameType = gameType,
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			DealerPosition = 0,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(alice, bob);
		context.GamePlayers.AddRange(
			CreateGamePlayer(game, alice, 0, now),
			CreateGamePlayer(game, bob, 1, now));
		context.Pots.Add(new PotEntity
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 120,
			CreatedAt = now
		});

		await context.SaveChangesAsync();
		return game;
	}

	private static void AddPlayerCards(CardsDbContext context, Game game, GamePlayer player,
		IReadOnlyList<string> cards, DateTimeOffset now)
	{
		for (var i = 0; i < cards.Count; i++)
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				GamePlayerId = player.Id,
				GamePlayer = player,
				HandNumber = game.CurrentHandNumber,
				DealOrder = i + 1,
				Location = CardLocation.Hole,
				IsVisible = false,
				Suit = ParseSuit(cards[i][^1]),
				Symbol = ParseSymbol(cards[i][..^1]),
				DealtAt = now,
				DealtAtPhase = nameof(Phases.ThirdStreet)
			});
	}

	private static void AddCommunityCard(CardsDbContext context, Game game, string card, int dealOrder, string phase,
		DateTimeOffset now)
	{
		context.GameCards.Add(new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			DealOrder = dealOrder,
			Location = CardLocation.Community,
			IsVisible = true,
			Suit = ParseSuit(card[^1]),
			Symbol = ParseSymbol(card[..^1]),
			DealtAt = now,
			DealtAtPhase = phase
		});
	}

	private static string Format(ShowdownCard card)
	{
		var rank = card.Symbol switch
		{
			CardSymbol.Ace => "A",
			CardSymbol.King => "K",
			CardSymbol.Queen => "Q",
			CardSymbol.Jack => "J",
			CardSymbol.Ten => "T",
			CardSymbol.Nine => "9",
			CardSymbol.Eight => "8",
			CardSymbol.Seven => "7",
			CardSymbol.Six => "6",
			CardSymbol.Five => "5",
			CardSymbol.Four => "4",
			CardSymbol.Three => "3",
			CardSymbol.Deuce => "2",
			_ => "?"
		};
		var suit = card.Suit switch
		{
			CardSuit.Clubs => "c",
			CardSuit.Diamonds => "d",
			CardSuit.Hearts => "h",
			CardSuit.Spades => "s",
			_ => "?"
		};
		return $"{rank}{suit}";
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
			"6" => CardSymbol.Six,
			"7" => CardSymbol.Seven,
			"8" => CardSymbol.Eight,
			"9" => CardSymbol.Nine,
			"J" => CardSymbol.Jack,
			"Q" => CardSymbol.Queen,
			"K" => CardSymbol.King,
			"A" => CardSymbol.Ace,
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
			TotalContributedThisHand = 60,
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}

	private sealed class CapturingHandHistoryRecorder : IHandHistoryRecorder
	{
		public RecordHandHistoryParameters? Last { get; private set; }

		public Task<bool> RecordHandHistoryAsync(RecordHandHistoryParameters parameters,
			CancellationToken cancellationToken = default)
		{
			Last = parameters;
			return Task.FromResult(true);
		}
	}
}