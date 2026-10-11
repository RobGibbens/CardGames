using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using BettingRoundEntity = CardGames.Poker.Api.Data.Entities.BettingRound;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;

public class ProcessBettingActionCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(Guid.NewGuid(), BettingActionType.Check),
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInBettingPhase_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.CollectingBlinds),
			includeBettingRound: false);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Check),
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain(nameof(Phases.CollectingBlinds));
	}

	[Fact]
	public async Task Handle_WhenActiveBettingRoundIsMissing_ReturnsNoBettingRound()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, includeBettingRound: false);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Check),
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.NoBettingRound);
	}

	[Fact]
	public async Task Handle_WhenCurrentActorSeatDoesNotExist_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentActorIndex: 99);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Check),
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain("Could not determine current player");
	}

	[Fact]
	public async Task Handle_WhenCheckIsUsedAgainstOutstandingBet_ReturnsInvalidAction()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentBet: 20);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Check) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidAction);
		result.AsT1.Message.Should().Contain("Cannot check");
	}

	[Fact]
	public async Task Handle_WhenHoldEmPreFlopAllIn_RunsOutToShowdownWithoutDrawPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			currentPhase: nameof(Phases.PreFlop),
			currentBet: 0,
			currentActorIndex: 0,
			firstPlayerStack: 50,
			secondPlayerAllIn: true);

		context.GameCards.AddRange(
			CreateDeckCard(game, 1, CardSuit.Hearts, CardSymbol.Ace),
			CreateDeckCard(game, 2, CardSuit.Clubs, CardSymbol.King),
			CreateDeckCard(game, 3, CardSuit.Diamonds, CardSymbol.Queen),
			CreateDeckCard(game, 4, CardSuit.Spades, CardSymbol.Jack),
			CreateDeckCard(game, 5, CardSuit.Hearts, CardSymbol.Ten));
		await context.SaveChangesAsync();

		var sut = CreateSut(context);
		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.AllIn) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));
		result.AsT0.CurrentPhase.Should().NotBe(nameof(Phases.DrawPhase));

		var communityCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id && gc.Location == CardLocation.Community)
			.ToListAsync();
		communityCards.Should().HaveCount(5);
	}

	[Fact]
	public async Task Handle_WhenRedRiverRunoutDealsRiverAndBonusInSameUnitOfWork_UsesDistinctDeckCards()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			"REDRIVER",
			nameof(Phases.Turn),
			currentBet: 0,
			currentActorIndex: 0,
			firstPlayerStack: 50,
			secondPlayerAllIn: true);

		var spare = CreateDeckCard(game, 3, CardSuit.Diamonds, CardSymbol.Ace);
		context.GameCards.Add(CreateDeckCard(game, 1, CardSuit.Hearts, CardSymbol.Ace));
		context.GameCards.Add(CreateDeckCard(game, 2, CardSuit.Clubs, CardSymbol.King));
		context.GameCards.Add(spare);
		await context.SaveChangesAsync();

		var sut = CreateSut(context);
		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.AllIn) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));

		var communityCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id && gc.Location == CardLocation.Community)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync();

		communityCards.Should().HaveCount(2);
		communityCards.Select(card => card.Id).Should().OnlyHaveUniqueItems();
		communityCards[0].DealtAtPhase.Should().Be(nameof(Phases.River));
		communityCards[0].Suit.Should().Be(CardSuit.Hearts);
		communityCards[1].DealtAtPhase.Should().Be("RedRiverBonus");
		communityCards[1].Suit.Should().Be(CardSuit.Clubs);
		communityCards[1].Symbol.Should().Be(CardSymbol.King);
		communityCards.Count(card => card.DealtAtPhase == "RedRiverBonus").Should().Be(1);
		communityCards.Select(card => card.Id).Should().NotContain(spare.Id);

		var spareAfter = await context.GameCards.SingleAsync(gc => gc.Id == spare.Id);
		spareAfter.Location.Should().Be(CardLocation.Deck);
	}

	[Fact]
	public async Task Handle_WhenRedRiverRunoutDealsBlackRiver_DoesNotDealBonusOrConsumeLaterDeckCards()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			"REDRIVER",
			nameof(Phases.Turn),
			currentBet: 0,
			currentActorIndex: 0,
			firstPlayerStack: 50,
			secondPlayerAllIn: true);

		var river = CreateDeckCard(game, 1, CardSuit.Spades, CardSymbol.Ace);
		var later = CreateDeckCard(game, 2, CardSuit.Hearts, CardSymbol.King);
		var spare = CreateDeckCard(game, 3, CardSuit.Diamonds, CardSymbol.Queen);
		context.GameCards.AddRange(river, later, spare);
		await context.SaveChangesAsync();

		var sut = CreateSut(context);
		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.AllIn) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));

		var communityCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id && gc.Location == CardLocation.Community)
			.ToListAsync();

		communityCards.Should().ContainSingle();
		communityCards[0].Id.Should().Be(river.Id);
		communityCards[0].DealtAtPhase.Should().Be(nameof(Phases.River));
		communityCards[0].Suit.Should().Be(CardSuit.Spades);

		var stillInDeck = await context.GameCards
			.Where(gc => gc.Id == later.Id || gc.Id == spare.Id)
			.ToListAsync();
		stillInDeck.Should().OnlyContain(card => card.Location == CardLocation.Deck);
	}

	[Fact]
	public async Task Handle_WhenRedRiverPreFlopRunoutDealsRedRiver_DealsFiveBoardCardsPlusOneBonus()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			"REDRIVER",
			nameof(Phases.PreFlop),
			currentBet: 0,
			currentActorIndex: 0,
			firstPlayerStack: 50,
			secondPlayerAllIn: true);

		var deck = new[]
		{
			CreateDeckCard(game, 1, CardSuit.Clubs, CardSymbol.Deuce),
			CreateDeckCard(game, 2, CardSuit.Diamonds, CardSymbol.Seven),
			CreateDeckCard(game, 3, CardSuit.Spades, CardSymbol.Nine),
			CreateDeckCard(game, 4, CardSuit.Clubs, CardSymbol.Jack),
			CreateDeckCard(game, 5, CardSuit.Hearts, CardSymbol.Five),
			CreateDeckCard(game, 6, CardSuit.Clubs, CardSymbol.Three),
			CreateDeckCard(game, 7, CardSuit.Diamonds, CardSymbol.Ace)
		};
		context.GameCards.AddRange(deck);
		await context.SaveChangesAsync();

		var sut = CreateSut(context);
		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.AllIn) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));

		var communityCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id && gc.Location == CardLocation.Community)
			.ToListAsync();

		communityCards.Should().HaveCount(6);
		communityCards.Count(card => card.DealtAtPhase == nameof(Phases.Flop)).Should().Be(3);
		communityCards.Count(card => card.DealtAtPhase == nameof(Phases.Turn)).Should().Be(1);
		communityCards.Count(card => card.DealtAtPhase == nameof(Phases.River)).Should().Be(1);
		communityCards.Should().ContainSingle(card => card.DealtAtPhase == "RedRiverBonus" && card.Id == deck[5].Id);

		var spare = await context.GameCards.SingleAsync(gc => gc.Id == deck[6].Id);
		spare.Location.Should().Be(CardLocation.Deck);
	}

	[Fact]
	public async Task Handle_WhenRedRiverAllInOnRiverAndBonusAlreadySaved_DoesNotDealSecondBonus()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			"REDRIVER",
			nameof(Phases.River),
			currentBet: 0,
			currentActorIndex: 0,
			firstPlayerStack: 50,
			secondPlayerAllIn: true);

		var river = CreateDeckCard(game, 5, CardSuit.Hearts, CardSymbol.Ace);
		river.Location = CardLocation.Community;
		river.DealtAtPhase = nameof(Phases.River);
		river.DealOrder = 5;
		river.IsVisible = true;

		var bonus = CreateDeckCard(game, 6, CardSuit.Clubs, CardSymbol.King);
		bonus.Location = CardLocation.Community;
		bonus.DealtAtPhase = "RedRiverBonus";
		bonus.DealOrder = 6;
		bonus.IsVisible = true;

		var spare = CreateDeckCard(game, 7, CardSuit.Diamonds, CardSymbol.Ace);
		context.GameCards.AddRange(river, bonus, spare);
		await context.SaveChangesAsync();

		var sut = CreateSut(context);
		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.AllIn) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));

		var communityCards = await context.GameCards
			.Where(gc => gc.GameId == game.Id && gc.Location == CardLocation.Community)
			.ToListAsync();

		communityCards.Should().HaveCount(2);
		communityCards.Count(card => card.DealtAtPhase == "RedRiverBonus").Should().Be(1);

		var spareAfter = await context.GameCards.SingleAsync(gc => gc.Id == spare.Id);
		spareAfter.Location.Should().Be(CardLocation.Deck);
	}

	[Fact]
	public async Task Handle_RaiseAboveStack_ReturnsInvalidAction()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentBet: 2, firstPlayerStack: 100);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, 1000) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidAction);
		result.AsT1.Message.Should().Contain("stack");

		var player = await context.GamePlayers.SingleAsync(gp => gp.GameId == game.Id && gp.SeatPosition == 0);
		player.ChipStack.Should().Be(100);
		var pot = await context.Pots.SingleAsync(p => p.GameId == game.Id);
		pot.Amount.Should().Be(0);
	}

	[Fact]
	public async Task Handle_RaiseToMaxStack_Succeeds()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentBet: 10, firstPlayerStack: 50);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, 50) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var player = await context.GamePlayers.SingleAsync(gp => gp.GameId == game.Id && gp.SeatPosition == 0);
		player.ChipStack.Should().Be(0);
		player.CurrentBet.Should().Be(50);
		player.IsAllIn.Should().BeTrue();
		var pot = await context.Pots.SingleAsync(p => p.GameId == game.Id);
		pot.Amount.Should().Be(50);
	}

	[Fact]
	public async Task Handle_RaiseBelowStreetBet_ReturnsInvalidAction()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentBet: 20, firstPlayerStack: 5);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, 5) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidAction);
		var round = await context.BettingRounds.SingleAsync(br => br.GameId == game.Id);
		round.CurrentBet.Should().Be(20);
	}

	[Fact]
	public async Task Handle_ShortAllInRaiseBelowMinRaise_Succeeds()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentBet: 20, firstPlayerStack: 25);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, 25) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var player = await context.GamePlayers.SingleAsync(gp => gp.GameId == game.Id && gp.SeatPosition == 0);
		player.ChipStack.Should().Be(0);
		player.IsAllIn.Should().BeTrue();
		player.CurrentBet.Should().Be(25);
		var round = await context.BettingRounds.SingleAsync(br => br.GameId == game.Id);
		round.CurrentBet.Should().Be(25);
	}

	[Fact]
	public async Task Handle_SmallBlindCompletesLimpAfterAFold_LeavesBigBlindOption()
	{
		await using var context = CreateContext();
		var game = await SeedBigBlindOptionAsync(context, currentActorIndex: 1);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Call) { BoundSeat = 1 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.RoundComplete.Should().BeFalse();
		result.AsT0.NextPlayerIndex.Should().Be(2);
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.PreFlop));
	}

	[Fact]
	public async Task Handle_BigBlindChecksLimpedPot_CompletesPreFlop()
	{
		await using var context = CreateContext();
		var game = await SeedBigBlindOptionAsync(context, currentActorIndex: 2, smallBlindHasCalled: true);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Check) { BoundSeat = 2 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.RoundComplete.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.Flop));
	}

	[Fact]
	public async Task Handle_WhenBoundSeatMatchesCurrentActor_FoldsThatPlayer()
	{
		await using var context = CreateContext();
		var game = await SeedWorkedHandAsync(context);
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Fold) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var players = await context.GamePlayers.Where(player => player.GameId == game.Id)
			.OrderBy(player => player.SeatPosition).ToListAsync();
		players[0].HasFolded.Should().BeTrue();
		players[0].ChipStack.Should().Be(1000);
		players[1].HasFolded.Should().BeFalse();
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentActorIndex.Should().Be(1);
		var actions = await context.BettingActionRecords.Where(action => action.BettingRoundId == round.Id).ToListAsync();
		actions.Should().ContainSingle();
		actions[0].IsTimeout.Should().BeFalse();
	}

	[Fact]
	public async Task Handle_WhenBoundSeatIsNull_ReturnsNotPlayerTurn()
	{
		await using var context = CreateContext();
		var game = await SeedWorkedHandAsync(context);
		var sut = CreateSut(context);

		var result = await sut.Handle(new ProcessBettingActionCommand(game.Id, BettingActionType.Fold),
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.NotPlayerTurn);
		result.AsT1.Message.Should().Be("It is not your turn.");
		var players = await context.GamePlayers.Where(player => player.GameId == game.Id).ToListAsync();
		players.Should().OnlyContain(player => !player.HasFolded);
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentActorIndex.Should().Be(0);
		(await context.BettingActionRecords.CountAsync(action => action.BettingRoundId == round.Id)).Should().Be(0);
	}

	[Fact]
	public async Task Handle_WhenBoundSeatIsThePreviousActor_ReturnsNotPlayerTurn()
	{
		await using var context = CreateContext();
		var game = await SeedWorkedHandAsync(context);
		var sut = CreateSut(context);

		var first = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Fold) { BoundSeat = 0 },
			CancellationToken.None);
		var second = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Fold) { BoundSeat = 0 },
			CancellationToken.None);

		first.IsT0.Should().BeTrue();
		second.IsT1.Should().BeTrue();
		second.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.NotPlayerTurn);
		second.AsT1.Message.Should().Be("It is not your turn.");
		var players = await context.GamePlayers.Where(player => player.GameId == game.Id)
			.OrderBy(player => player.SeatPosition).ToListAsync();
		players[0].HasFolded.Should().BeTrue();
		players[1].HasFolded.Should().BeFalse();
		players[1].ChipStack.Should().Be(1000);
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentActorIndex.Should().Be(1);
		(await context.BettingActionRecords.CountAsync(action => action.BettingRoundId == round.Id)).Should().Be(1);
	}

	private static ProcessBettingActionCommandHandler CreateSut(CardsDbContext context)
	{
		return new ProcessBettingActionCommandHandler(
			context,
			Substitute.For<ILogger<ProcessBettingActionCommandHandler>>());
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
		string gameTypeCode = "HOLDEM",
		string currentPhase = nameof(Phases.PreFlop),
		bool includeBettingRound = true,
		int currentBet = 0,
		int minBet = 10,
		int currentActorIndex = 0,
		int firstPlayerStack = 200,
		bool secondPlayerAllIn = false)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = gameTypeCode,
			Name = gameTypeCode,
			BettingStructure = BettingStructure.Blinds,
			MinPlayers = 2,
			MaxPlayers = 10,
			InitialHoleCards = 2,
			InitialBoardCards = 0,
			MaxCommunityCards = gameTypeCode == "REDRIVER" ? 6 : 5,
			MaxPlayerCards = gameTypeCode == "REDRIVER" ? 8 : 7,
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
			CurrentPlayerIndex = currentActorIndex,
			SmallBlind = 5,
			BigBlind = 10,
			MinBet = minBet,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);

		var aliceGamePlayer = CreateGamePlayer(game, alice, 0, firstPlayerStack, now);
		var bobGamePlayer = CreateGamePlayer(
			game,
			bob,
			1,
			secondPlayerAllIn ? 0 : 200,
			now,
			secondPlayerAllIn);

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
			Amount = 0,
			CreatedAt = now
		});

		if (includeBettingRound)
			context.BettingRounds.Add(new BettingRoundEntity
			{
				GameId = game.Id,
				Game = game,
				HandNumber = game.CurrentHandNumber,
				RoundNumber = 1,
				Street = currentPhase,
				CurrentBet = currentBet,
				MinBet = minBet,
				RaiseCount = currentBet > 0 ? 1 : 0,
				MaxRaises = 4,
				LastRaiseAmount = currentBet > 0 ? currentBet : minBet,
				PlayersInHand = 2,
				PlayersActed = 0,
				CurrentActorIndex = currentActorIndex,
				LastAggressorIndex = currentBet > 0 ? 1 : -1,
				IsComplete = false,
				StartedAt = now
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

	private static GamePlayer CreateGamePlayer(
		Game game,
		Player player,
		int seatPosition,
		int chipStack,
		DateTimeOffset now,
		bool isAllIn = false)
	{
		return new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = player.Id,
			Player = player,
			SeatPosition = seatPosition,
			ChipStack = chipStack,
			StartingChips = Math.Max(chipStack, 200),
			CurrentBet = 0,
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			IsAllIn = isAllIn,
			RowVersion = [1, 2, 3]
		};
	}

	/// <summary>
	///     Three-handed preflop. Alice at seat 0 faces a bet, so a fold is legal and moves no chips.
	/// </summary>
	private static async Task<Game> SeedWorkedHandAsync(CardsDbContext context)
	{
		var now = DateTimeOffset.UtcNow;
		var game = await SeedGameAsync(context, currentBet: 20, firstPlayerStack: 1000);
		var bob = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 1);
		bob.ChipStack = 1000;
		bob.StartingChips = 1000;

		var carolPlayer = CreatePlayer("Carol", now);
		var carol = CreateGamePlayer(game, carolPlayer, 2, 1000, now);
		context.Players.Add(carolPlayer);
		context.GamePlayers.Add(carol);

		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.PlayersInHand = 3;

		var alice = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 0);
		context.GameCards.AddRange(
			CreateHoleCard(game, alice, 1, CardSuit.Spades, CardSymbol.Ace),
			CreateHoleCard(game, alice, 2, CardSuit.Hearts, CardSymbol.King),
			CreateHoleCard(game, bob, 3, CardSuit.Clubs, CardSymbol.Deuce),
			CreateHoleCard(game, bob, 4, CardSuit.Diamonds, CardSymbol.Seven),
			CreateHoleCard(game, carol, 5, CardSuit.Hearts, CardSymbol.Queen),
			CreateHoleCard(game, carol, 6, CardSuit.Diamonds, CardSymbol.Jack));

		await context.SaveChangesAsync();
		return game;
	}

	private static GameCard CreateHoleCard(Game game, GamePlayer player, int dealOrder, CardSuit suit, CardSymbol symbol)
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
			DealtAt = DateTimeOffset.UtcNow,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}

	private static GameCard CreateDeckCard(Game game, int dealOrder, CardSuit suit, CardSymbol symbol)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			DealOrder = dealOrder,
			Location = CardLocation.Deck,
			IsVisible = false,
			Suit = suit,
			Symbol = symbol,
			DealtAt = DateTimeOffset.UtcNow,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}

	/// <summary>
	///     Four-handed pre-flop: UTG has folded, button has called 10, small blind still owes 5,
	///     big blind has only posted. PlayersActed already equals the number of players who can still act
	///     once the small blind calls — the counter that used to skip the big blind.
	/// </summary>
	private static async Task<Game> SeedBigBlindOptionAsync(
		CardsDbContext context,
		int currentActorIndex,
		bool smallBlindHasCalled = false)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "HOLDEM",
			Name = "HOLDEM",
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
			CurrentPhase = nameof(Phases.PreFlop),
			CurrentHandNumber = 1,
			DealerPosition = 0,
			CurrentPlayerIndex = currentActorIndex,
			SmallBlind = 5,
			BigBlind = 10,
			MinBet = 10,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var button = CreatePlayer("Button", now);
		var smallBlind = CreatePlayer("SmallBlind", now);
		var bigBlind = CreatePlayer("BigBlind", now);
		var utg = CreatePlayer("Utg", now);

		var buttonSeat = CreateGamePlayer(game, button, 0, 990, now);
		buttonSeat.CurrentBet = 10;
		var smallBlindSeat = CreateGamePlayer(game, smallBlind, 1, smallBlindHasCalled ? 990 : 995, now);
		smallBlindSeat.CurrentBet = smallBlindHasCalled ? 10 : 5;
		var bigBlindSeat = CreateGamePlayer(game, bigBlind, 2, 990, now);
		bigBlindSeat.CurrentBet = 10;
		var utgSeat = CreateGamePlayer(game, utg, 3, 1000, now);
		utgSeat.HasFolded = true;

		var bettingRound = new BettingRoundEntity
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			RoundNumber = 1,
			Street = nameof(Phases.PreFlop),
			CurrentBet = 10,
			MinBet = 10,
			RaiseCount = 0,
			MaxRaises = 4,
			LastRaiseAmount = 10,
			PlayersInHand = 4,
			PlayersActed = smallBlindHasCalled ? 3 : 2,
			CurrentActorIndex = currentActorIndex,
			LastAggressorIndex = -1,
			IsComplete = false,
			StartedAt = now
		};

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(button, smallBlind, bigBlind, utg);
		context.GamePlayers.AddRange(buttonSeat, smallBlindSeat, bigBlindSeat, utgSeat);
		context.Pots.Add(new PotEntity
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 25,
			CreatedAt = now
		});
		context.BettingRounds.Add(bettingRound);
		context.BettingActionRecords.Add(new BettingActionRecord
		{
			BettingRoundId = bettingRound.Id,
			BettingRound = bettingRound,
			GamePlayerId = utgSeat.Id,
			GamePlayer = utgSeat,
			ActionOrder = 1,
			ActionType = BettingActionType.Fold,
			ActionAt = now
		});
		context.BettingActionRecords.Add(new BettingActionRecord
		{
			BettingRoundId = bettingRound.Id,
			BettingRound = bettingRound,
			GamePlayerId = buttonSeat.Id,
			GamePlayer = buttonSeat,
			ActionOrder = 2,
			ActionType = BettingActionType.Call,
			Amount = 10,
			ActionAt = now
		});
		if (smallBlindHasCalled)
			context.BettingActionRecords.Add(new BettingActionRecord
			{
				BettingRoundId = bettingRound.Id,
				BettingRound = bettingRound,
				GamePlayerId = smallBlindSeat.Id,
				GamePlayer = smallBlindSeat,
				ActionOrder = 3,
				ActionType = BettingActionType.Call,
				Amount = 5,
				ActionAt = now
			});

		await context.SaveChangesAsync();
		return game;
	}
}