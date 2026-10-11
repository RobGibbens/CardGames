using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;

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
		var game = await SeedGameAsync(context, nameof(Phases.River));
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PerformShowdownErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain(nameof(Phases.River));
	}

	[Fact]
	public async Task Handle_WhenPlayersDoNotHaveEnoughHoleCards_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, nameof(Phases.Showdown), 1);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PerformShowdownErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain("No eligible hands could be evaluated");
	}

	[Fact]
	public async Task Handle_WhenOnlyOnePlayerRemains_ReturnsFoldWinWithoutHoleCards()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, nameof(Phases.Showdown), holeCardsPerPlayer: 2);
		var bob = await context.GamePlayers.Include(gp => gp.Player).FirstAsync(gp => gp.Player.Name == "Bob");
		bob.HasFolded = true;
		await context.SaveChangesAsync();

		var sut = CreateSut(context);
		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.WonByFold.Should().BeTrue();
		success.PlayerHands.Should().ContainSingle();
		success.PlayerHands[0].PlayerName.Should().Be("Alice");
		success.PlayerHands[0].IsWinner.Should().BeTrue();
		success.PlayerHands[0].Cards.Should().BeEmpty();
		success.PlayerHands[0].HandType.Should().BeNull();
		success.PlayerHands[0].HandStrength.Should().BeNull();
	}

	[Fact]
	public async Task Handle_WhenContestedAllInShowdown_RefundsUncalledBetAndPaysEachLayer()
	{
		await using var context = CreateContext();
		var game = await SeedContestedShowdownAsync(context, 1400,
		[
			Seat("Alice", 0, 0, 1000, true, false, CardSuit.Spades, CardSymbol.Queen, CardSuit.Hearts, CardSymbol.Queen),
			Seat("Bob", 1, 0, 100, true, false, CardSuit.Spades, CardSymbol.Ace, CardSuit.Hearts, CardSymbol.Ace),
			Seat("Carol", 2, 0, 300, true, false, CardSuit.Spades, CardSymbol.King, CardSuit.Hearts, CardSymbol.King)
		]);
		var settlement = Substitute.For<IHandSettlementService>();
		var sut = CreateSut(context, settlement);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.WonByFold.Should().BeFalse();
		success.Payouts.Should().Contain(new KeyValuePair<string, int>("Bob", 300));
		success.Payouts.Should().Contain(new KeyValuePair<string, int>("Carol", 400));
		success.Payouts.Should().NotContainKey("Alice");
		Hand(success, "Bob").IsWinner.Should().BeTrue();
		Hand(success, "Bob").AmountWon.Should().Be(300);
		Hand(success, "Carol").IsWinner.Should().BeTrue();
		Hand(success, "Carol").AmountWon.Should().Be(400);
		Hand(success, "Alice").IsWinner.Should().BeFalse();
		Hand(success, "Alice").AmountWon.Should().Be(0);

		await AssertStacksAsync(context, ("Alice", 700, 300), ("Bob", 300, 100), ("Carol", 400, 300));
		var pots = await CurrentHandPotsAsync(context, game);
		pots.Select(pot => (pot.PotOrder, pot.Amount, pot.PotType, pot.IsAwarded))
			.Should().Equal((0, 300, PotType.Main, true), (1, 400, PotType.Side, true));
		pots[0].WinnerPayouts.Should().Contain("Bob").And.Contain("300").And.NotContain("400");
		pots[1].WinnerPayouts.Should().Contain("Carol").And.Contain("400");

		await settlement.Received(1).SettleHandAsync(
			Arg.Any<Game>(),
			Arg.Is<Dictionary<string, int>>(payouts =>
				payouts.Count == 2 && payouts["Bob"] == 300 && payouts["Carol"] == 400),
			Arg.Any<CancellationToken>());

		var repeat = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		repeat.IsT0.Should().BeTrue();
		repeat.AsT0.WonByFold.Should().BeFalse();
		repeat.AsT0.CurrentPhase.Should().Be("Complete");
		repeat.AsT0.Payouts.Should().Contain(new KeyValuePair<string, int>("Bob", 300));
		repeat.AsT0.Payouts.Should().Contain(new KeyValuePair<string, int>("Carol", 400));
		repeat.AsT0.Payouts.Should().NotContainKey("Alice");
		Hand(repeat.AsT0, "Alice").IsWinner.Should().BeFalse();
		Hand(repeat.AsT0, "Alice").AmountWon.Should().Be(0);
		await AssertStacksAsync(context, ("Alice", 700, 300), ("Bob", 300, 100), ("Carol", 400, 300));
		await settlement.Received(1).SettleHandAsync(
			Arg.Any<Game>(),
			Arg.Any<Dictionary<string, int>>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Handle_WhenFoldedChipsAreInTheMainPot_PaysTheShortAllInThatLayer()
	{
		await using var context = CreateContext();
		var game = await SeedContestedShowdownAsync(context, 290,
		[
			Seat("Alice", 0, 1000, 110, false, false, CardSuit.Spades, CardSymbol.Queen, CardSuit.Hearts, CardSymbol.Queen),
			Seat("Bob", 1, 0, 60, true, false, CardSuit.Spades, CardSymbol.Ace, CardSuit.Hearts, CardSymbol.Ace),
			Seat("Carol", 2, 1000, 110, false, false, CardSuit.Spades, CardSymbol.King, CardSuit.Hearts, CardSymbol.King),
			Seat("Doug", 3, 250, 10, false, true, CardSuit.Clubs, CardSymbol.Deuce, CardSuit.Diamonds, CardSymbol.Deuce, false)
		]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		Hand(result.AsT0, "Bob").AmountWon.Should().Be(190);
		(await PlayerAsync(context, "Doug")).ChipStack.Should().Be(250);
		var mainPot = (await CurrentHandPotsAsync(context, game)).Single(pot => pot.Amount == 190);
		var doug = await PlayerAsync(context, "Doug");
		var dougContribution = await context.PotContributions.SingleAsync(contribution =>
			contribution.PotId == mainPot.Id && contribution.GamePlayerId == doug.Id);
		dougContribution.Amount.Should().Be(10);
		dougContribution.IsEligibleToWin.Should().BeFalse();
	}

	[Fact]
	public async Task Handle_WhenFoldedPlayerContributedMoreThanTheCap_KeepsTheExcessInTheMainPot()
	{
		await using var context = CreateContext();
		var game = await SeedContestedShowdownAsync(context, 700,
		[
			Seat("Bob", 0, 50, 100, false, false, CardSuit.Spades, CardSymbol.Ace, CardSuit.Hearts, CardSymbol.Ace),
			Seat("Carol", 1, 50, 100, false, false, CardSuit.Spades, CardSymbol.King, CardSuit.Hearts, CardSymbol.King),
			Seat("Doug", 2, 400, 500, false, true, CardSuit.Clubs, CardSymbol.Deuce, CardSuit.Diamonds, CardSymbol.Deuce, false)
		]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		Hand(result.AsT0, "Bob").AmountWon.Should().Be(700);
		Hand(result.AsT0, "Carol").AmountWon.Should().Be(0);
		result.AsT0.PlayerHands.Where(hand => hand.PlayerName == "Doug").Should().OnlyContain(hand => hand.AmountWon == 0);
		result.AsT0.Payouts.Should().NotContainKey("Doug");
		var pots = await CurrentHandPotsAsync(context, game);
		pots.Should().ContainSingle();
		pots[0].PotOrder.Should().Be(0);
		pots[0].Amount.Should().Be(700);
		pots.Should().NotContain(pot => pot.PotOrder == 1);
	}

	[Fact]
	public async Task Handle_WhenPotTotalDoesNotMatchContributions_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedContestedShowdownAsync(context, 100,
		[
			Seat("Alice", 0, 200, 40, false, false, CardSuit.Spades, CardSymbol.Queen, CardSuit.Hearts, CardSymbol.Queen),
			Seat("Bob", 1, 180, 50, false, false, CardSuit.Spades, CardSymbol.Ace, CardSuit.Hearts, CardSymbol.Ace)
		]);
		var settlement = Substitute.For<IHandSettlementService>();
		var sut = CreateSut(context, settlement);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PerformShowdownErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain("100").And.Contain("90");
		(await PlayerAsync(context, "Alice")).ChipStack.Should().Be(200);
		(await PlayerAsync(context, "Bob")).ChipStack.Should().Be(180);
		var pot = (await CurrentHandPotsAsync(context, game)).Should().ContainSingle().Subject;
		pot.Amount.Should().Be(100);
		pot.IsAwarded.Should().BeFalse();
		await settlement.DidNotReceive().SettleHandAsync(
			Arg.Any<Game>(),
			Arg.Any<Dictionary<string, int>>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Handle_WhenTiedPlayersSplitAnOddPot_GivesTheRemainderToTheSeatAfterTheButton()
	{
		await using var context = CreateContext();
		var game = await SeedContestedShowdownAsync(context, 11,
		[
			Seat("Alice", 0, 100, 5, false, false, CardSuit.Spades, CardSymbol.Ace, CardSuit.Hearts, CardSymbol.Ace),
			Seat("Bob", 1, 100, 5, false, false, CardSuit.Spades, CardSymbol.Ace, CardSuit.Hearts, CardSymbol.Ace),
			Seat("Doug", 2, 50, 1, false, true, CardSuit.Clubs, CardSymbol.Deuce, CardSuit.Diamonds, CardSymbol.Deuce, false)
		]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		Hand(result.AsT0, "Bob").AmountWon.Should().Be(6);
		Hand(result.AsT0, "Alice").AmountWon.Should().Be(5);
		result.AsT0.Payouts.Should().NotContainKey("Doug");
	}

	[Fact]
	public async Task Handle_TiedShowdown_OddChipGoesToSeatAfterButton()
	{
		await using var context = CreateContext();
		var game = await SeedRoyalTieAsync(context, 100,
		[
			Seat("Alice", 0, 1000, 34, false, false, CardSuit.Clubs, CardSymbol.Deuce, CardSuit.Diamonds, CardSymbol.Three),
			Seat("Bob", 2, 1000, 34, false, false, CardSuit.Clubs, CardSymbol.Four, CardSuit.Diamonds, CardSymbol.Five),
			Seat("Carol", 5, 1000, 32, false, false, CardSuit.Clubs, CardSymbol.Six, CardSuit.Diamonds, CardSymbol.Seven)
		]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Payouts["Carol"].Should().Be(34);
		success.Payouts["Alice"].Should().Be(33);
		success.Payouts["Bob"].Should().Be(33);
		success.Payouts.Values.Sum().Should().Be(100);
		Hand(success, "Carol").AmountWon.Should().Be(34);
		Hand(success, "Alice").AmountWon.Should().Be(33);
		Hand(success, "Bob").AmountWon.Should().Be(33);
		(await PlayerAsync(context, "Carol")).ChipStack.Should().Be(1034);
		(await PlayerAsync(context, "Alice")).ChipStack.Should().Be(1033);
		(await PlayerAsync(context, "Bob")).ChipStack.Should().Be(1033);
		(await context.Games.SingleAsync(gameRow => gameRow.Id == game.Id)).DealerPosition.Should().Be(5);
	}

	[Fact]
	public async Task Handle_TiedShowdown_SecondOddChipGoesToNextSeat()
	{
		await using var context = CreateContext();
		var game = await SeedRoyalTieAsync(context, 101,
		[
			Seat("Alice", 0, 1000, 34, false, false, CardSuit.Clubs, CardSymbol.Deuce, CardSuit.Diamonds, CardSymbol.Three),
			Seat("Bob", 2, 1000, 34, false, false, CardSuit.Clubs, CardSymbol.Four, CardSuit.Diamonds, CardSymbol.Five),
			Seat("Carol", 5, 1000, 33, false, false, CardSuit.Clubs, CardSymbol.Six, CardSuit.Diamonds, CardSymbol.Seven)
		]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Payouts["Carol"].Should().Be(34);
		success.Payouts["Alice"].Should().Be(34);
		success.Payouts["Bob"].Should().Be(33);
		success.Payouts.Values.Sum().Should().Be(101);
		Hand(success, "Carol").AmountWon.Should().Be(34);
		Hand(success, "Alice").AmountWon.Should().Be(34);
		Hand(success, "Bob").AmountWon.Should().Be(33);
		(await PlayerAsync(context, "Carol")).ChipStack.Should().Be(1034);
		(await PlayerAsync(context, "Alice")).ChipStack.Should().Be(1034);
		(await PlayerAsync(context, "Bob")).ChipStack.Should().Be(1033);
	}

	[Fact]
	public async Task Handle_TiedShowdown_WrapsPastButtonInsteadOfCollectionOrder()
	{
		await using var context = CreateContext();
		var game = await SeedRoyalTieAsync(context, 101,
		[
			Seat("Bob", 2, 1000, 50, false, false, CardSuit.Clubs, CardSymbol.Four, CardSuit.Diamonds, CardSymbol.Five),
			Seat("Alice", 0, 1000, 50, false, false, CardSuit.Clubs, CardSymbol.Deuce, CardSuit.Diamonds, CardSymbol.Three),
			Seat("Carol", 5, 1000, 1, false, true, CardSuit.Clubs, CardSymbol.Six, CardSuit.Diamonds, CardSymbol.Seven)
		]);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Payouts["Alice"].Should().Be(51);
		success.Payouts["Bob"].Should().Be(50);
		success.Payouts.Values.Sum().Should().Be(101);
		Hand(success, "Alice").AmountWon.Should().Be(51);
		Hand(success, "Bob").AmountWon.Should().Be(50);
		(await PlayerAsync(context, "Alice")).ChipStack.Should().Be(1051);
		(await PlayerAsync(context, "Bob")).ChipStack.Should().Be(1050);
	}

	private static PerformShowdownCommandHandler CreateSut(
		CardsDbContext context,
		IHandSettlementService? handSettlementService = null)
	{
		return new PerformShowdownCommandHandler(
			context,
			Substitute.For<IHandHistoryRecorder>(),
			handSettlementService ?? Substitute.For<IHandSettlementService>());
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

	private static async Task<Game> SeedGameAsync(CardsDbContext context, string currentPhase,
		int holeCardsPerPlayer = 0)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "HOLDEM",
			Name = "Texas Hold'em",
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
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			DealerPosition = 0,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);
		var aliceGamePlayer = CreateGamePlayer(game, alice, 0, now);
		var bobGamePlayer = CreateGamePlayer(game, bob, 1, now);

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(alice, bob);
		context.GamePlayers.AddRange(aliceGamePlayer, bobGamePlayer);
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

		for (var cardIndex = 0; cardIndex < holeCardsPerPlayer; cardIndex++)
		{
			context.GameCards.Add(CreateHoleCard(game, aliceGamePlayer, cardIndex + 1, CardSuit.Hearts,
				(CardSymbol)(cardIndex + 13), now));
			context.GameCards.Add(CreateHoleCard(game, bobGamePlayer, cardIndex + 1, CardSuit.Spades,
				(CardSymbol)(cardIndex + 11), now));
		}

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
			ChipStack = 200,
			StartingChips = 200,
			Status = GamePlayerStatus.Active,
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

	private static ShowdownPlayerHand Hand(PerformShowdownSuccessful success, string name)
	{
		return success.PlayerHands.Single(hand => hand.PlayerName == name);
	}

	private static async Task AssertStacksAsync(
		CardsDbContext context,
		params (string Name, int Stack, int Contributed)[] expected)
	{
		foreach (var (name, stack, contributed) in expected)
		{
			var player = await PlayerAsync(context, name);
			player.ChipStack.Should().Be(stack);
			player.TotalContributedThisHand.Should().Be(contributed);
		}
	}

	private static Task<GamePlayer> PlayerAsync(CardsDbContext context, string name)
	{
		return context.GamePlayers.Include(gamePlayer => gamePlayer.Player)
			.SingleAsync(gamePlayer => gamePlayer.Player.Name == name);
	}

	private static async Task<List<PotEntity>> CurrentHandPotsAsync(CardsDbContext context, Game game)
	{
		return await context.Pots
			.Where(pot => pot.GameId == game.Id && pot.HandNumber == game.CurrentHandNumber)
			.OrderBy(pot => pot.PotOrder)
			.ToListAsync();
	}

	private static ShowdownSeat Seat(
		string name,
		int seat,
		int stack,
		int contributed,
		bool isAllIn,
		bool hasFolded,
		CardSuit firstSuit,
		CardSymbol firstSymbol,
		CardSuit secondSuit,
		CardSymbol secondSymbol,
		bool dealHoleCards = true)
	{
		return new ShowdownSeat(name, seat, stack, contributed, isAllIn, hasFolded, firstSuit, firstSymbol,
			secondSuit, secondSymbol, dealHoleCards);
	}

	private static readonly (CardSuit Suit, CardSymbol Symbol)[] RoyalBoard =
	[
		(CardSuit.Spades, CardSymbol.Ace),
		(CardSuit.Spades, CardSymbol.King),
		(CardSuit.Spades, CardSymbol.Queen),
		(CardSuit.Spades, CardSymbol.Jack),
		(CardSuit.Spades, CardSymbol.Ten)
	];

	private static Task<Game> SeedRoyalTieAsync(CardsDbContext context, int potAmount, ShowdownSeat[] seats)
	{
		return SeedContestedShowdownAsync(context, potAmount, seats, dealerPosition: 2, board: RoyalBoard);
	}

	private static async Task<Game> SeedContestedShowdownAsync(
		CardsDbContext context,
		int potAmount,
		ShowdownSeat[] seats,
		int dealerPosition = 0,
		(CardSuit Suit, CardSymbol Symbol)[]? board = null)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "HOLDEM",
			Name = "Texas Hold'em",
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
			DealerPosition = dealerPosition,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		context.GameTypes.Add(gameType);
		context.Games.Add(game);

		foreach (var seat in seats)
		{
			var player = CreatePlayer(seat.Name, now);
			player.Id = PlayerIdFor(seat.Name);
			var gamePlayer = CreateGamePlayer(game, player, seat.SeatPosition, now);
			gamePlayer.Id = Guid.Parse($"00000000-0000-0000-0000-{seat.SeatPosition + 1:D12}");
			gamePlayer.ChipStack = seat.Stack;
			gamePlayer.TotalContributedThisHand = seat.Contributed;
			gamePlayer.IsAllIn = seat.IsAllIn;
			gamePlayer.HasFolded = seat.HasFolded;
			context.Players.Add(player);
			context.GamePlayers.Add(gamePlayer);

			if (!seat.DealHoleCards)
				continue;

			context.GameCards.Add(CreateHoleCard(game, gamePlayer, 1, seat.FirstSuit, seat.FirstSymbol, now));
			context.GameCards.Add(CreateHoleCard(game, gamePlayer, 2, seat.SecondSuit, seat.SecondSymbol, now));
		}

		var community = board ??
		[
			(CardSuit.Clubs, CardSymbol.Deuce),
			(CardSuit.Diamonds, CardSymbol.Seven),
			(CardSuit.Clubs, CardSymbol.Nine),
			(CardSuit.Spades, CardSymbol.Jack),
			(CardSuit.Hearts, CardSymbol.Three)
		];
		for (var index = 0; index < community.Length; index++)
		{
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				HandNumber = game.CurrentHandNumber,
				DealOrder = index + 1,
				Location = CardLocation.Community,
				IsVisible = true,
				Suit = community[index].Suit,
				Symbol = community[index].Symbol,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing)
			});
		}

		context.Pots.Add(new PotEntity
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = potAmount,
			CreatedAt = now
		});

		await context.SaveChangesAsync();
		return game;
	}

	private static Guid PlayerIdFor(string name)
	{
		return name switch
		{
			"Alice" => Guid.Parse("11111111-1111-1111-1111-111111111111"),
			"Bob" => Guid.Parse("22222222-2222-2222-2222-222222222222"),
			"Carol" => Guid.Parse("33333333-3333-3333-3333-333333333333"),
			"Doug" => Guid.Parse("44444444-4444-4444-4444-444444444444"),
			_ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown showdown player")
		};
	}

	private sealed record ShowdownSeat(
		string Name,
		int SeatPosition,
		int Stack,
		int Contributed,
		bool IsAllIn,
		bool HasFolded,
		CardSuit FirstSuit,
		CardSymbol FirstSymbol,
		CardSuit SecondSuit,
		CardSymbol SecondSymbol,
		bool DealHoleCards);
}