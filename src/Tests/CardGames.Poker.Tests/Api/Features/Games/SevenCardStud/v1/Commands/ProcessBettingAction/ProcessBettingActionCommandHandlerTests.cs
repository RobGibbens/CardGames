using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Evaluation;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OneOf;
using Xunit;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using BettingRoundEntity = CardGames.Poker.Api.Data.Entities.BettingRound;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction;

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
	public async Task Handle_WhenBoundSeatMatchesCurrentActor_FoldsThatPlayer()
	{
		await using var context = CreateContext();
		var game = await SeedThreeHandedStudAsync(context);
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Fold) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var players = await context.GamePlayers.Where(player => player.GameId == game.Id)
			.OrderBy(player => player.SeatPosition).ToListAsync();
		players[0].HasFolded.Should().BeTrue();
		players[1].HasFolded.Should().BeFalse();
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentActorIndex.Should().Be(1);
		(await context.BettingActionRecords.CountAsync(action => action.BettingRoundId == round.Id)).Should().Be(1);
	}

	[Fact]
	public async Task Handle_WhenBoundSeatIsNull_ReturnsNotPlayerTurn()
	{
		await using var context = CreateContext();
		var game = await SeedThreeHandedStudAsync(context);
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
		var game = await SeedThreeHandedStudAsync(context);
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
		var players = await context.GamePlayers.Where(player => player.GameId == game.Id)
			.OrderBy(player => player.SeatPosition).ToListAsync();
		players[1].HasFolded.Should().BeFalse();
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentActorIndex.Should().Be(1);
		(await context.BettingActionRecords.CountAsync(action => action.BettingRoundId == round.Id)).Should().Be(1);
	}

	[Fact]
	public async Task Handle_BringInCall_CompletesRoundWithoutLettingThePosterAct()
	{
		await using var context = CreateContext(ignoreTransactions: true);
		var game = await SeedBringInHeadsUpAsync(context);
		var mediator = Substitute.For<IMediator>();
		mediator
			.Send(Arg.Any<IRequest<OneOf<DealHandsSuccessful, DealHandsError>>>(), Arg.Any<CancellationToken>())
			.Returns(new DealHandsSuccessful
			{
				CurrentPhase = nameof(Phases.FourthStreet),
				CurrentPlayerIndex = -1,
				PlayerHands = []
			});
		var sut = CreateSut(context, mediator);

		var call = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Call) { BoundSeat = 0 },
			CancellationToken.None);

		call.IsT0.Should().BeTrue();
		call.AsT0.RoundComplete.Should().BeTrue();
		var players = await context.GamePlayers.Where(player => player.GameId == game.Id)
			.OrderBy(player => player.SeatPosition).ToListAsync();
		players[0].ChipStack.Should().Be(990);
		players[1].ChipStack.Should().Be(990);
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.IsComplete.Should().BeTrue();
		round.CurrentBet.Should().Be(5);

		var lynneRaise = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, 10) { BoundSeat = 1 },
			CancellationToken.None);

		lynneRaise.IsT1.Should().BeTrue();
		lynneRaise.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.NoBettingRound);
		players = await context.GamePlayers.Where(player => player.GameId == game.Id)
			.OrderBy(player => player.SeatPosition).ToListAsync();
		players[1].ChipStack.Should().Be(990);
	}

	[Fact]
	public async Task Handle_BringInRaise_LeavesThePosterToAct()
	{
		await using var context = CreateContext();
		var game = await SeedBringInHeadsUpAsync(context);
		var sut = CreateSut(context);

		var raise = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, 10) { BoundSeat = 0 },
			CancellationToken.None);

		raise.IsT0.Should().BeTrue();
		raise.AsT0.RoundComplete.Should().BeFalse();
		raise.AsT0.NextPlayerIndex.Should().Be(1);
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentBet.Should().Be(10);
		round.LastAggressorIndex.Should().Be(0);
		round.CurrentActorIndex.Should().Be(1);
		var lynne = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 1);
		lynne.ChipStack.Should().Be(990);
	}

	[Theory]
	[InlineData(6)]
	[InlineData(20)]
	public async Task Handle_CompleteThatIsNotTheSmallBet_IsRejected(int amount)
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			currentBet: 5,
			minBet: 10,
			firstPlayerStack: 1000,
			lastRaiseAmount: 0,
			potAmount: 15);
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, amount) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidAction);
		result.AsT1.Message.Should().Be("Raise must be to 10");
		await AssertStreetUntouchedAsync(context, game.Id, 1000, 0, 5, 0, 15);
	}

	[Fact]
	public async Task Handle_CompleteToTheSmallBet_IsAccepted()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			currentBet: 5,
			minBet: 10,
			firstPlayerStack: 1000,
			lastRaiseAmount: 0,
			potAmount: 15);
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, 10) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue();
		var alice = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 0);
		alice.ChipStack.Should().Be(990);
		alice.CurrentBet.Should().Be(10);
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentBet.Should().Be(10);
		round.LastRaiseAmount.Should().Be(10);
	}

	[Fact]
	public async Task Handle_OpeningBetAboveTheStreetLimit_IsRejected()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			currentPhase: nameof(Phases.FourthStreet),
			currentBet: 0,
			minBet: 10,
			firstPlayerStack: 1000,
			lastRaiseAmount: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Bet, 200) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Message.Should().Be("Bet must be 10");
		await AssertStreetUntouchedAsync(context, game.Id, 1000, 0, 0, 0, 0);
	}

	[Fact]
	public async Task Handle_OpeningAllInOfALargerStack_IsRejected()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			currentPhase: nameof(Phases.FourthStreet),
			currentBet: 0,
			minBet: 10,
			firstPlayerStack: 1000,
			lastRaiseAmount: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.AllIn) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Message.Should().Be("All-in must be the 10 bet");
		await AssertStreetUntouchedAsync(context, game.Id, 1000, 0, 0, 0, 0);
	}

	[Fact]
	public async Task Handle_FifthStreetBetBelowTheBigBet_IsRejected()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			currentPhase: nameof(Phases.FifthStreet),
			currentBet: 0,
			minBet: 20,
			firstPlayerStack: 1000,
			lastRaiseAmount: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Bet, 10) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Message.Should().Be("Bet must be 20");
		await AssertStreetUntouchedAsync(context, game.Id, 1000, 0, 0, 0, 0);
	}

	[Fact]
	public async Task Handle_FifthStreetRaiseThatIsNotAnotherBigBet_IsRejected()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			currentPhase: nameof(Phases.FifthStreet),
			currentBet: 20,
			minBet: 20,
			firstPlayerStack: 1000,
			lastRaiseAmount: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, 30) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Message.Should().Be("Raise must be to 40");
		await AssertStreetUntouchedAsync(context, game.Id, 1000, 0, 20, 0, 0);
	}

	[Fact]
	public async Task Handle_ShortOpeningBet_DoesNotMarkAFullRaise_AndTheNextCompleteIsTheLimit()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			currentPhase: nameof(Phases.FourthStreet),
			currentBet: 0,
			minBet: 10,
			currentActorIndex: 2,
			firstPlayerStack: 1000,
			secondPlayerStack: 1000,
			lastRaiseAmount: 0,
			lastAggressorIndex: -1);
		var now = DateTimeOffset.UtcNow;
		var carolPlayer = CreatePlayer("Carol", now);
		var carol = CreateGamePlayer(game, carolPlayer, 2, 4, now);
		context.Players.Add(carolPlayer);
		context.GamePlayers.Add(carol);
		var seededRound = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		seededRound.PlayersInHand = 3;
		game.CurrentPlayerIndex = 2;
		await context.SaveChangesAsync();
		var sut = CreateSut(context);

		var open = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Bet, 4) { BoundSeat = 2 },
			CancellationToken.None);

		open.IsT0.Should().BeTrue();
		carol = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 2);
		carol.ChipStack.Should().Be(0);
		carol.IsAllIn.Should().BeTrue();
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentBet.Should().Be(4);
		round.LastRaiseAmount.Should().Be(0);
		round.LastAggressorIndex.Should().Be(-1);

		var tooHigh = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, 14) { BoundSeat = 0 },
			CancellationToken.None);

		tooHigh.IsT1.Should().BeTrue();
		tooHigh.AsT1.Message.Should().Be("Raise must be to 10");
		var alice = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 0);
		alice.ChipStack.Should().Be(1000);
		alice.CurrentBet.Should().Be(0);
		round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentBet.Should().Be(4);
		round.LastRaiseAmount.Should().Be(0);

		var complete = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Raise, 10) { BoundSeat = 0 },
			CancellationToken.None);

		complete.IsT0.Should().BeTrue();
		alice = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 0);
		alice.ChipStack.Should().Be(990);
		alice.CurrentBet.Should().Be(10);
		round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentBet.Should().Be(10);
		round.LastRaiseAmount.Should().Be(10);
		round.LastAggressorIndex.Should().Be(0);
	}

	[Fact]
	public async Task Handle_ShortOpeningAllIn_DoesNotMarkAFullRaise()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			currentPhase: nameof(Phases.FourthStreet),
			currentBet: 0,
			minBet: 10,
			currentActorIndex: 2,
			firstPlayerStack: 1000,
			lastRaiseAmount: 0,
			lastAggressorIndex: -1);
		var now = DateTimeOffset.UtcNow;
		var carolPlayer = CreatePlayer("Carol", now);
		var carol = CreateGamePlayer(game, carolPlayer, 2, 4, now);
		context.Players.Add(carolPlayer);
		context.GamePlayers.Add(carol);
		game.CurrentPlayerIndex = 2;
		await context.SaveChangesAsync();
		var sut = CreateSut(context);

		var open = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.AllIn) { BoundSeat = 2 },
			CancellationToken.None);

		open.IsT0.Should().BeTrue();
		carol = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 2);
		carol.ChipStack.Should().Be(0);
		carol.IsAllIn.Should().BeTrue();
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentBet.Should().Be(4);
		round.LastRaiseAmount.Should().Be(0);
		round.LastAggressorIndex.Should().Be(-1);
	}

	[Fact]
	public async Task Handle_WhenSevenCardStudAllInOnSixthStreet_DealsTheNextDeckCard()
	{
		await using var context = CreateContext();
		var game = await SeedAllInSixthStreetAsync(context, tollbooth: false);
		var now = DateTimeOffset.UtcNow;
		AddCard(context, game, null, CardSuit.Diamonds, CardSymbol.Three, CardLocation.Deck, 1, now);
		AddCard(context, game, null, CardSuit.Spades, CardSymbol.Seven, CardLocation.Deck, 2, now);
		await context.SaveChangesAsync();
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Check) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));
		var cards = await context.GameCards.Where(card => card.GameId == game.Id).ToListAsync();
		var alice = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 0);
		var bob = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 1);
		CardFor(cards, alice.Id).Symbol.Should().Be(CardSymbol.Three);
		CardFor(cards, alice.Id).Suit.Should().Be(CardSuit.Diamonds);
		CardFor(cards, bob.Id).Symbol.Should().Be(CardSymbol.Seven);
		CardFor(cards, bob.Id).Suit.Should().Be(CardSuit.Spades);
	}

	[Fact]
	public async Task Handle_WhenTollboothAllInOnSixthStreet_DealsFurthestDisplayCardAndPaysTheFlush()
	{
		await using var context = CreateContext(ignoreTransactions: true);
		var game = await SeedAllInSixthStreetAsync(context, tollbooth: true);
		var now = DateTimeOffset.UtcNow;
		var alice = await context.GamePlayers.Include(player => player.Player)
			.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 0);
		var bob = await context.GamePlayers.Include(player => player.Player)
			.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 1);
		AddStudCards(context, game, alice, now, "9h 8h 7h 6h 2d 3c");
		// Ad with Kd Qd Jd Td is a royal flush and would win even without a seventh card.
		// These kickers leave Bob with a pair of aces, which Alice's heart flush beats.
		AddStudCards(context, game, bob, now, "Ac Ad Kc Qh Js 9c");
		AddCard(context, game, null, CardSuit.Clubs, CardSymbol.Deuce, CardLocation.Community, 1, now,
			nameof(Phases.TollboothOffer), true);
		AddCard(context, game, null, CardSuit.Hearts, CardSymbol.Ace, CardLocation.Community, 2, now,
			nameof(Phases.TollboothOffer), true);
		AddCard(context, game, null, CardSuit.Diamonds, CardSymbol.Three, CardLocation.Deck, 1, now);
		AddCard(context, game, null, CardSuit.Spades, CardSymbol.Seven, CardLocation.Deck, 2, now);
		await context.SaveChangesAsync();
		var sut = CreateSut(context);

		var result = await sut.Handle(
			new ProcessBettingActionCommand(game.Id, BettingActionType.Check) { BoundSeat = 0 },
			CancellationToken.None);

		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));
		alice = await context.GamePlayers.Include(player => player.Player)
			.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 0);
		bob = await context.GamePlayers.Include(player => player.Player)
			.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 1);
		alice.ChipStack.Should().Be(100);
		bob.ChipStack.Should().Be(100);
		alice.TotalContributedThisHand.Should().Be(100);
		bob.TotalContributedThisHand.Should().Be(100);
		(await context.Pots.SingleAsync(pot => pot.GameId == game.Id)).Amount.Should().Be(200);

		var cards = await context.GameCards.Where(card => card.GameId == game.Id).ToListAsync();
		var aliceSeventh = cards.Single(card =>
			card.GamePlayerId == alice.Id && card.DealtAtPhase == nameof(Phases.SeventhStreet));
		aliceSeventh.Suit.Should().Be(CardSuit.Hearts);
		aliceSeventh.Symbol.Should().Be(CardSymbol.Ace);
		aliceSeventh.Location.Should().Be(CardLocation.Hole);
		aliceSeventh.IsVisible.Should().BeFalse();
		var bobSeventh = cards.Single(card =>
			card.GamePlayerId == bob.Id && card.DealtAtPhase == nameof(Phases.SeventhStreet));
		bobSeventh.Suit.Should().Be(CardSuit.Diamonds);
		bobSeventh.Symbol.Should().Be(CardSymbol.Three);
		bobSeventh.Location.Should().Be(CardLocation.Hole);
		bobSeventh.IsVisible.Should().BeFalse();
		bobSeventh.DealtAtPhase.Should().Be(nameof(Phases.SeventhStreet));
		var deuce = cards.Single(card => card.Suit == CardSuit.Clubs && card.Symbol == CardSymbol.Deuce);
		deuce.Location.Should().Be(CardLocation.Community);
		deuce.GamePlayerId.Should().BeNull();
		var seven = cards.Single(card => card.Suit == CardSuit.Spades && card.Symbol == CardSymbol.Seven);
		seven.Location.Should().Be(CardLocation.Deck);
		seven.GamePlayerId.Should().BeNull();

		var showdown = new PerformShowdownCommandHandler(
			context,
			new GameFlowHandlerFactory(),
			new HandEvaluatorFactory(),
			Substitute.For<IHandHistoryRecorder>(),
			Substitute.For<IHandSettlementService>(),
			Substitute.For<ILogger<PerformShowdownCommandHandler>>());
		var paid = await showdown.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		paid.IsT0.Should().BeTrue(paid.IsT1 ? paid.AsT1.Message : string.Empty);
		paid.AsT0.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 200 });
		alice = await context.GamePlayers.Include(player => player.Player)
			.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 0);
		bob = await context.GamePlayers.Include(player => player.Player)
			.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 1);
		alice.ChipStack.Should().Be(300);
		bob.ChipStack.Should().Be(100);
	}

	private static async Task AssertStreetUntouchedAsync(
		CardsDbContext context,
		Guid gameId,
		int chipStack,
		int playerCurrentBet,
		int streetCurrentBet,
		int lastRaiseAmount,
		int potAmount)
	{
		var player = await context.GamePlayers.SingleAsync(gamePlayer => gamePlayer.GameId == gameId && gamePlayer.SeatPosition == 0);
		player.ChipStack.Should().Be(chipStack);
		player.CurrentBet.Should().Be(playerCurrentBet);
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == gameId);
		round.CurrentBet.Should().Be(streetCurrentBet);
		round.LastRaiseAmount.Should().Be(lastRaiseAmount);
		var pot = await context.Pots.SingleAsync(p => p.GameId == gameId);
		pot.Amount.Should().Be(potAmount);
	}

	private static async Task<Game> SeedThreeHandedStudAsync(CardsDbContext context)
	{
		var game = await SeedGameAsync(context, currentBet: 20);
		var now = DateTimeOffset.UtcNow;
		var carolPlayer = CreatePlayer("Carol", now);
		var carol = CreateGamePlayer(game, carolPlayer, 2, 200, now);
		context.Players.Add(carolPlayer);
		context.GamePlayers.Add(carol);
		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.PlayersInHand = 3;
		await context.SaveChangesAsync();
		return game;
	}

	private static ProcessBettingActionCommandHandler CreateSut(CardsDbContext context, IMediator? mediator = null)
	{
		return new ProcessBettingActionCommandHandler(
			context,
			mediator ?? Substitute.For<IMediator>(),
			Substitute.For<ILogger<ProcessBettingActionCommandHandler>>());
	}

	private static CardsDbContext CreateContext(bool ignoreTransactions = false)
	{
		return CreateContext(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot(), ignoreTransactions);
	}

	private static CardsDbContext CreateContext(
		string databaseName,
		InMemoryDatabaseRoot databaseRoot,
		bool ignoreTransactions = false)
	{
		var builder = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot);
		if (ignoreTransactions)
			builder.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));

		return new CardsDbContext(builder.Options);
	}

	private static async Task<Game> SeedBringInHeadsUpAsync(CardsDbContext context)
	{
		var now = DateTimeOffset.UtcNow;
		var game = await SeedGameAsync(context, currentBet: 5, minBet: 10, currentActorIndex: 0, firstPlayerStack: 995);
		var lynne = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 1);
		var rob = await context.GamePlayers.SingleAsync(player => player.GameId == game.Id && player.SeatPosition == 0);
		rob.Player.Name = "Rob";
		lynne.Player.Name = "Lynne";
		lynne.ChipStack = 990;
		lynne.CurrentBet = 5;
		game.DealerPosition = 1;
		game.BringInPlayerIndex = 1;
		game.CurrentPlayerIndex = 0;

		var round = await context.BettingRounds.SingleAsync(bettingRound => bettingRound.GameId == game.Id);
		round.CurrentBet = 5;
		round.MinBet = 10;
		round.LastRaiseAmount = 0;
		round.RaiseCount = 0;
		round.PlayersActed = 1;
		round.CurrentActorIndex = 0;
		round.LastAggressorIndex = 1;

		var pot = await context.Pots.SingleAsync(p => p.GameId == game.Id);
		pot.Amount = 15;
		await context.SaveChangesAsync();
		return game;
	}

	private static async Task<Game> SeedGameAsync(
		CardsDbContext context,
		string currentPhase = nameof(Phases.ThirdStreet),
		bool includeBettingRound = true,
		int currentBet = 0,
		int minBet = 10,
		int currentActorIndex = 0,
		int firstPlayerStack = 200,
		int secondPlayerStack = 200,
		int? lastRaiseAmount = null,
		int? lastAggressorIndex = null,
		int potAmount = 0)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "SEVENCARDSTUD",
			Name = "Seven Card Stud",
			BettingStructure = BettingStructure.Ante,
			MinPlayers = 2,
			MaxPlayers = 8,
			InitialHoleCards = 3,
			InitialBoardCards = 0,
			MaxCommunityCards = 0,
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
			CurrentPlayerIndex = currentActorIndex,
			MinBet = minBet,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);

		var aliceGamePlayer = CreateGamePlayer(game, alice, 0, firstPlayerStack, now);
		var bobGamePlayer = CreateGamePlayer(game, bob, 1, secondPlayerStack, now);

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
			Amount = potAmount,
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
				LastRaiseAmount = lastRaiseAmount ?? (currentBet > 0 ? currentBet : minBet),
				PlayersInHand = 2,
				PlayersActed = 0,
				CurrentActorIndex = currentActorIndex,
				LastAggressorIndex = lastAggressorIndex ?? (currentBet > 0 ? 1 : -1),
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
		DateTimeOffset now)
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
			StartingChips = chipStack,
			CurrentBet = 0,
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}

	private static async Task<Game> SeedAllInSixthStreetAsync(CardsDbContext context, bool tollbooth)
	{
		var game = await SeedGameAsync(
			context,
			currentPhase: nameof(Phases.SixthStreet),
			currentBet: 0,
			currentActorIndex: 0,
			firstPlayerStack: 100,
			secondPlayerStack: 100,
			potAmount: 200,
			lastRaiseAmount: 0,
			lastAggressorIndex: -1);
		game.DealerPosition = 1;
		if (tollbooth)
		{
			game.CurrentHandGameTypeCode = "TOLLBOOTH";
			if (game.GameType is not null) game.GameType.Code = "TOLLBOOTH";
		}

		var players = await context.GamePlayers.Where(player => player.GameId == game.Id).ToListAsync();
		foreach (var player in players)
		{
			player.StartingChips = 200;
			player.ChipStack = 100;
			player.TotalContributedThisHand = 100;
			player.IsAllIn = true;
			player.CurrentBet = 0;
		}

		await context.SaveChangesAsync();
		return game;
	}

	private static void AddStudCards(CardsDbContext context, Game game, GamePlayer player, DateTimeOffset now,
		string cards)
	{
		var tokens = cards.Split(' ');
		for (var index = 0; index < tokens.Length; index++)
		{
			var faceUp = index is >= 2 and <= 5;
			AddCard(
				context,
				game,
				player,
				ParseSuit(tokens[index][^1]),
				ParseSymbol(tokens[index][..^1]),
				faceUp ? CardLocation.Board : CardLocation.Hole,
				index + 1,
				now,
				nameof(Phases.ThirdStreet),
				faceUp);
		}
	}

	private static void AddCard(
		CardsDbContext context,
		Game game,
		GamePlayer? player,
		CardSuit suit,
		CardSymbol symbol,
		CardLocation location,
		int dealOrder,
		DateTimeOffset now,
		string? dealtAtPhase = null,
		bool visible = false)
	{
		context.GameCards.Add(new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player?.Id,
			GamePlayer = player,
			HandNumber = game.CurrentHandNumber,
			Suit = suit,
			Symbol = symbol,
			Location = location,
			DealOrder = dealOrder,
			IsVisible = visible,
			DealtAt = now,
			DealtAtPhase = dealtAtPhase
		});
	}

	private static GameCard CardFor(List<GameCard> cards, Guid playerId)
	{
		return cards.Single(card => card.GamePlayerId == playerId);
	}

	private static CardSuit ParseSuit(char suit)
	{
		return suit switch
		{
			'h' => CardSuit.Hearts,
			'd' => CardSuit.Diamonds,
			'c' => CardSuit.Clubs,
			's' => CardSuit.Spades,
			_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, null)
		};
	}

	private static CardSymbol ParseSymbol(string symbol)
	{
		return symbol switch
		{
			"A" => CardSymbol.Ace,
			"K" => CardSymbol.King,
			"Q" => CardSymbol.Queen,
			"J" => CardSymbol.Jack,
			"T" => CardSymbol.Ten,
			"9" => CardSymbol.Nine,
			"8" => CardSymbol.Eight,
			"7" => CardSymbol.Seven,
			"6" => CardSymbol.Six,
			"3" => CardSymbol.Three,
			"2" => CardSymbol.Deuce,
			_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null)
		};
	}
}