using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.StartHand;
using OneOf;
using Xunit.Sdk;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Games.HoldEm;

[CollectionDefinition("RedRiverSequential", DisableParallelization = true)]
public sealed class RedRiverSequentialCollection;

/// <summary>
///     Integration tests for Red River lifecycle and river bonus-card behavior.
/// </summary>
[Collection("RedRiverSequential")]
public class RedRiverHandLifecycleTests : IntegrationTestBase
{
	private const string GameTypeCode = "REDRIVER";

	protected override async Task SeedBaseDataAsync()
	{
		await base.SeedBaseDataAsync();

		var exists = await DbContext.GameTypes.AnyAsync(gt => gt.Code == GameTypeCode);
		if (exists) return;

		await DbContext.GameTypes.AddAsync(new GameType
		{
			Id = Guid.CreateVersion7(),
			Code = GameTypeCode,
			Name = "Red River",
			MinPlayers = 2,
			MaxPlayers = 10,
			InitialHoleCards = 2,
			InitialBoardCards = 0,
			MaxCommunityCards = 6,
			MaxPlayerCards = 8,
			BettingStructure = BettingStructure.Ante
		});

		await DbContext.SaveChangesAsync();
	}

	[Fact]
	public async Task FlowHandler_IsConfiguredAsHoldEmStyle()
	{
		var handler = new RedRiverFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, GameTypeCode, 3);

		handler.SkipsAnteCollection.Should().BeTrue();
		handler.GameTypeCode.Should().Be(GameTypeCode);
		handler.GetInitialPhase(setup.Game).Should().Be("CollectingBlinds");
		handler.GetDealingConfiguration().InitialCardsPerPlayer.Should().Be(2);
	}

	[Fact]
	public async Task StartHand_ForRedRiver_DealsTwoHoleCardsPerPlayer_NotFiveCardDrawHands()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, GameTypeCode, 2);
		setup.Game.CurrentPhase = nameof(Phases.WaitingToStart);
		setup.Game.CurrentHandNumber = 0;
		await DbContext.SaveChangesAsync();

		var startResult = await Mediator.Send(new StartHandCommand(setup.Game.Id));

		startResult.IsT0.Should().BeTrue();

		var context = GetFreshDbContext();
		var game = await context.Games
			.AsNoTracking()
			.FirstAsync(g => g.Id == setup.Game.Id);

		var dealtHoleCards = await context.GameCards
			.AsNoTracking()
			.Where(gc => gc.GameId == setup.Game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Hand
			             && !gc.IsDiscarded)
			.ToListAsync();

		dealtHoleCards.Should().HaveCount(4, "two-player Red River should deal exactly two hole cards per player");

		var cardsPerPlayer = dealtHoleCards
			.Where(gc => gc.GamePlayerId.HasValue)
			.GroupBy(gc => gc.GamePlayerId!.Value)
			.ToDictionary(g => g.Key, g => g.Count());

		cardsPerPlayer.Should().HaveCount(2);
		cardsPerPlayer.Values.Should().OnlyContain(count => count == 2);
	}

	[Fact]
	public async Task RiverRound_WhenRiverIsRed_DealsBonusCommunityCardBeforeShowdown()
	{
		var setup = await CreateDealtRedRiverGameAsync(3, 0);

		await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "River");
		await SetRiverSuitAsync(setup.Game.Id, CardSuit.Hearts);

		await CompleteCurrentBettingRoundWithChecksAsync(setup.Game.Id);

		var fresh = GetFreshDbContext();
		var game = await fresh.Games.FirstAsync(g => g.Id == setup.Game.Id);
		game.CurrentPhase.Should().Be("Showdown");

		var communityCards = await fresh.GameCards
			.Where(gc => gc.GameId == setup.Game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Community
			             && !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync();

		communityCards.Should().HaveCount(6);
		communityCards.Count(c => c.DealtAtPhase == "RedRiverBonus").Should().Be(1);
	}

	[Fact]
	public async Task RiverRound_WhenRiverIsBlack_DoesNotDealBonusCommunityCard()
	{
		var setup = await CreateDealtRedRiverGameAsync(3, 0);

		await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "River");
		await SetRiverSuitAsync(setup.Game.Id, CardSuit.Spades);

		await CompleteCurrentBettingRoundWithChecksAsync(setup.Game.Id);

		var fresh = GetFreshDbContext();
		var game = await fresh.Games.FirstAsync(g => g.Id == setup.Game.Id);
		game.CurrentPhase.Should().Be("Showdown");

		var communityCards = await fresh.GameCards
			.Where(gc => gc.GameId == setup.Game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Community
			             && !gc.IsDiscarded)
			.ToListAsync();

		communityCards.Should().HaveCount(5);
		communityCards.Should().OnlyContain(c => c.DealtAtPhase != "RedRiverBonus");
	}

	[Fact]
	public async Task ProcessBettingAction_WhenNotInBettingPhase_ReturnsInvalidGameState()
	{
		var setup = await CreateRedRiverGameSetupAsync(3, 0);
		setup.Game.CurrentPhase = "CollectingBlinds";
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.Check)
		{
			BoundSeat = await TryCurrentBettingSeatAsync(setup.Game.Id)
		});

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidGameState);
	}

	[Fact]
	public async Task RiverAllInRunout_WithControlledDeck_DealsDeterministicBonusCardWhenRiverIsRed()
	{
		var setup = await CreateDealtRedRiverGameAsync(2, 0);

		await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "River");
		await SetRiverSuitAsync(setup.Game.Id, CardSuit.Spades);
		await ConfigureNextDeckCardAsync(setup.Game.Id, CardSuit.Clubs, CardSymbol.King);
		await SetRiverSuitAsync(setup.Game.Id, CardSuit.Hearts);

		var preAllInContext = GetFreshDbContext();
		var preAllInGame = await preAllInContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
		var preAllInRiverCards = await preAllInContext.GameCards
			.Where(gc => gc.GameId == setup.Game.Id
			             && gc.HandNumber == preAllInGame.CurrentHandNumber
			             && gc.Location == CardLocation.Community
			             && gc.DealtAtPhase == "River")
			.ToListAsync();

		preAllInRiverCards.Should().NotBeEmpty();
		preAllInRiverCards.Should().AllSatisfy(c => c.Suit.Should().Be(CardSuit.Hearts));

		var firstAllIn = await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.AllIn)
		{
			BoundSeat = await TryCurrentBettingSeatAsync(setup.Game.Id)
		});
		firstAllIn.IsT0.Should().BeTrue("first all-in at River should succeed");

		var stateAfterFirstAllIn = await GetFreshDbContext().Games
			.AsNoTracking()
			.FirstAsync(g => g.Id == setup.Game.Id);

		if (string.Equals(stateAfterFirstAllIn.CurrentPhase, "River", StringComparison.OrdinalIgnoreCase))
		{
			var secondAllIn =
				await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.AllIn)
		{
			BoundSeat = await TryCurrentBettingSeatAsync(setup.Game.Id)
		});
			secondAllIn.IsT0.Should()
				.BeTrue("second all-in at River should succeed when runout does not occur on first action");
		}

		var context = GetFreshDbContext();
		var game = await context.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
		game.CurrentPhase.Should().Be("Showdown");

		var communityCards = await context.GameCards
			.Where(gc => gc.GameId == setup.Game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Community
			             && !gc.IsDiscarded)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync();

		communityCards.Should().HaveCount(6, "red river in all-in runout should still trigger the bonus board card");

		var riverCard = communityCards.LastOrDefault(c => c.DealtAtPhase == "River");
		riverCard.Should().NotBeNull();
		riverCard!.Suit.Should().Be(CardSuit.Hearts);

		var bonusCard = communityCards.SingleOrDefault(c => c.DealtAtPhase == "RedRiverBonus");
		bonusCard.Should().NotBeNull();
		bonusCard!.Suit.Should().Be(CardSuit.Clubs);
		bonusCard.Symbol.Should().Be(CardSymbol.King);
		bonusCard.DealOrder.Should().Be(6);
	}

	[Fact]
	public async Task Showdown_WhenRiverIsBlack_AwardsPotAndCompletesHand()
	{
		const int playerCount = 3;
		const int startingChips = 1000;
		var setup = await CreateDealtRedRiverGameAsync(playerCount, 0);

		await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "River");
		await SetRiverSuitAsync(setup.Game.Id, CardSuit.Spades);
		await CompleteCurrentBettingRoundWithChecksAsync(setup.Game.Id);

		var before = GetFreshDbContext();
		var gameBefore = await before.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
		gameBefore.CurrentPhase.Should().Be("Showdown");

		var communityCount = await before.GameCards.CountAsync(gc =>
			gc.GameId == setup.Game.Id
			&& gc.HandNumber == gameBefore.CurrentHandNumber
			&& gc.Location == CardLocation.Community
			&& !gc.IsDiscarded);
		communityCount.Should().Be(5);

		var potTotal = await before.Pots
			.Where(p => p.GameId == setup.Game.Id && p.HandNumber == gameBefore.CurrentHandNumber)
			.SumAsync(p => p.Amount);
		potTotal.Should().BeGreaterThan(0);

		var showdown = await Mediator.Send(new PerformShowdownCommand(setup.Game.Id));

		showdown.IsT0.Should().BeTrue(showdown.IsT1 ? showdown.AsT1.Message : "showdown should succeed");
		showdown.AsT0.Payouts.Values.Sum().Should().Be(potTotal);
		showdown.AsT0.PlayerHands.Should().Contain(hand => hand.IsWinner);

		var after = GetFreshDbContext();
		var game = await after.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
		game.CurrentPhase.Should().Be("Complete");
		game.HandCompletedAt.Should().NotBeNull();
		game.NextHandStartsAt.Should().NotBeNull();

		var pots = await after.Pots
			.Where(p => p.GameId == setup.Game.Id && p.HandNumber == game.CurrentHandNumber)
			.ToListAsync();
		pots.Should().NotBeEmpty();
		pots.Should().OnlyContain(p => p.IsAwarded);

		var stacks = await after.GamePlayers
			.Where(gp => gp.GameId == setup.Game.Id)
			.SumAsync(gp => gp.ChipStack);
		stacks.Should().Be(playerCount * startingChips);
	}

	[Fact]
	public async Task Showdown_WithSixCardBonusBoard_AwardsPotWithoutEvaluationError()
	{
		var setup = await CreateDealtRedRiverGameAsync(2, 0);

		await AdvanceToPhaseByCheckingAsync(setup.Game.Id, "River");
		await SetRiverSuitAsync(setup.Game.Id, CardSuit.Spades);
		await ConfigureNextDeckCardAsync(setup.Game.Id, CardSuit.Clubs, CardSymbol.King);
		await SetRiverSuitAsync(setup.Game.Id, CardSuit.Hearts);

		var firstAllIn = await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.AllIn)
		{
			BoundSeat = await TryCurrentBettingSeatAsync(setup.Game.Id)
		});
		firstAllIn.IsT0.Should().BeTrue("first all-in at River should succeed");

		var stateAfterFirstAllIn = await GetFreshDbContext().Games
			.AsNoTracking()
			.FirstAsync(g => g.Id == setup.Game.Id);

		if (string.Equals(stateAfterFirstAllIn.CurrentPhase, "River", StringComparison.OrdinalIgnoreCase))
		{
			var secondAllIn =
				await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.AllIn)
		{
			BoundSeat = await TryCurrentBettingSeatAsync(setup.Game.Id)
		});
			secondAllIn.IsT0.Should().BeTrue("second all-in at River should complete the runout");
		}

		var before = GetFreshDbContext();
		var gameBefore = await before.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
		gameBefore.CurrentPhase.Should().Be("Showdown");

		var communityCards = await before.GameCards
			.Where(gc => gc.GameId == setup.Game.Id
			             && gc.HandNumber == gameBefore.CurrentHandNumber
			             && gc.Location == CardLocation.Community
			             && !gc.IsDiscarded)
			.ToListAsync();
		communityCards.Should().HaveCount(6);
		communityCards.Should().Contain(c => c.DealtAtPhase == "RedRiverBonus");

		var showdown = await Mediator.Send(new PerformShowdownCommand(setup.Game.Id));

		showdown.IsT0.Should().BeTrue(showdown.IsT1 ? showdown.AsT1.Message : "six-card board showdown should succeed");
		showdown.AsT0.WonByFold.Should().BeFalse();
		showdown.AsT0.PlayerHands.Should().NotBeEmpty();
		showdown.AsT0.PlayerHands.Should().Contain(hand => hand.IsWinner);
		showdown.AsT0.Payouts.Should().NotBeEmpty();

		var after = GetFreshDbContext();
		var game = await after.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);
		game.CurrentPhase.Should().Be("Complete");

		var pots = await after.Pots
			.Where(p => p.GameId == setup.Game.Id && p.HandNumber == game.CurrentHandNumber)
			.ToListAsync();
		pots.Should().NotBeEmpty();
		pots.Should().OnlyContain(p => p.IsAwarded);
		pots.Sum(p => p.Amount).Should().Be(showdown.AsT0.Payouts.Values.Sum());
	}

	private async Task<GameSetup> CreateRedRiverGameSetupAsync(
		int playerCount,
		int dealerPosition,
		int startingChips = 1000)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, GameTypeCode, playerCount, startingChips);

		var game = setup.Game;
		game.SmallBlind = 5;
		game.BigBlind = 10;
		game.DealerPosition = dealerPosition;
		game.CurrentHandNumber = 1;
		game.Status = GameStatus.InProgress;

		await DbContext.SaveChangesAsync();

		return setup;
	}

	private async Task<GameSetup> CreateDealtRedRiverGameAsync(int playerCount, int dealerPosition)
	{
		var setup = await CreateRedRiverGameSetupAsync(playerCount, dealerPosition);
		await DatabaseSeeder.CreatePotAsync(DbContext, setup.Game, 0);

		var handler = FlowHandlerFactory.GetHandler(GameTypeCode);
		await handler.DealCardsAsync(
			DbContext, setup.Game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

		return setup;
	}

	private async Task AdvanceToPhaseByCheckingAsync(Guid gameId, string targetPhase)
	{
		for (var i = 0; i < 64; i++)
		{
			var game = await GetFreshDbContext().Games.AsNoTracking().FirstAsync(g => g.Id == gameId);
			if (string.Equals(game.CurrentPhase, targetPhase, StringComparison.OrdinalIgnoreCase)) return;

			var result = await SendPassiveBettingActionAsync(gameId);
			result.IsT0.Should()
				.BeTrue($"passive betting action at iteration {i} should succeed while advancing to {targetPhase}");
		}

		throw new XunitException($"Failed to advance game {gameId} to phase {targetPhase} within iteration budget.");
	}

	private async Task CompleteCurrentBettingRoundWithChecksAsync(Guid gameId)
	{
		var round = await GetFreshDbContext().BettingRounds
			.Where(br => br.GameId == gameId && !br.IsComplete)
			.OrderByDescending(br => br.RoundNumber)
			.FirstOrDefaultAsync();

		if (round is null) return;

		for (var i = 0; i < 16; i++)
		{
			var freshRound = await GetFreshDbContext().BettingRounds
				.AsNoTracking()
				.FirstOrDefaultAsync(br => br.Id == round.Id);

			if (freshRound is null || freshRound.IsComplete) return;

			var result = await SendPassiveBettingActionAsync(gameId);
			result.IsT0.Should().BeTrue($"passive betting action at iteration {i} should complete the current round");
		}

		throw new XunitException($"Failed to complete betting round {round.Id} for game {gameId}.");
	}

	private async Task SetRiverSuitAsync(Guid gameId, CardSuit suit)
	{
		var context = GetFreshDbContext();
		var game = await context.Games.FirstAsync(g => g.Id == gameId);

		var riverCards = await context.GameCards
			.Where(gc => gc.GameId == gameId
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Community
			             && gc.DealtAtPhase == "River")
			.ToListAsync();

		riverCards.Should().NotBeEmpty("river card should already be dealt before suit adjustment");
		foreach (var riverCard in riverCards) riverCard.Suit = suit;

		// When forcing a non-red suit, revert any bonus card that was dealt
		// because the original River card happened to be red.
		var isRedSuit = suit is CardSuit.Hearts or CardSuit.Diamonds;
		if (!isRedSuit)
		{
			var bonusCards = await context.GameCards
				.Where(gc => gc.GameId == gameId
				             && gc.HandNumber == game.CurrentHandNumber
				             && gc.Location == CardLocation.Community
				             && gc.DealtAtPhase == "RedRiverBonus")
				.ToListAsync();

			foreach (var bonus in bonusCards)
			{
				bonus.Location = CardLocation.Deck;
				bonus.DealtAtPhase = null;
				bonus.IsVisible = false;
			}
		}

		await context.SaveChangesAsync();
		DbContext.ChangeTracker.Clear();
	}

	private async Task ConfigureNextDeckCardAsync(
		Guid gameId,
		CardSuit bonusSuit,
		CardSymbol bonusSymbol,
		int deckOffset = 0)
	{
		var context = GetFreshDbContext();
		var game = await context.Games.AsNoTracking().FirstAsync(g => g.Id == gameId);

		var deckCards = await context.GameCards
			.Where(gc => gc.GameId == gameId
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync();

		deckCards.Count.Should().BeGreaterThan(deckOffset);

		deckCards[deckOffset].Suit = bonusSuit;
		deckCards[deckOffset].Symbol = bonusSymbol;

		await context.SaveChangesAsync();
		DbContext.ChangeTracker.Clear();
	}

	private async Task<OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>>
		SendPassiveBettingActionAsync(Guid gameId)
	{
		var context = GetFreshDbContext();
		var game = await context.Games
			.AsNoTracking()
			.FirstAsync(g => g.Id == gameId);

		var bettingRound = await context.BettingRounds
			.AsNoTracking()
			.Where(br => br.GameId == gameId
			             && br.HandNumber == game.CurrentHandNumber
			             && !br.IsComplete)
			.OrderByDescending(br => br.RoundNumber)
			.FirstAsync();

		var actor = await context.GamePlayers
			.AsNoTracking()
			.FirstAsync(gp => gp.GameId == gameId && gp.SeatPosition == bettingRound.CurrentActorIndex);

		var action = bettingRound.CurrentBet > actor.CurrentBet
			? BettingActionType.Call
			: BettingActionType.Check;

		return await Mediator.Send(new ProcessBettingActionCommand(gameId, action)
		{
			BoundSeat = await TryCurrentBettingSeatAsync(gameId)
		});
	}
}