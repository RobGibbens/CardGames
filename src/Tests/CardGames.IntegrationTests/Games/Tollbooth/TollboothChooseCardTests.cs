using CardGames.Poker.Api.Features.Games.Tollbooth;
using CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.ChooseCard;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction;

namespace CardGames.IntegrationTests.Games.Tollbooth;

public class TollboothChooseCardTests : IntegrationTestBase
{
	private const string TollboothCode = "TOLLBOOTH";

	protected override async Task SeedBaseDataAsync()
	{
		await base.SeedBaseDataAsync();
		await EnsureTollboothGameTypeAsync();
	}

	[Fact]
	public async Task ChooseCard_Furthest_Free_AssignsCardToPlayer()
	{
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 3);
		var game = setup.Game;
		var actingPlayer = setup.GamePlayers.First(gp => gp.SeatPosition == game.CurrentDrawPlayerIndex);
		var chipsBefore = actingPlayer.ChipStack;

		var result = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Furthest, actingPlayer.SeatPosition));

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Choice.Should().Be(TollboothChoice.Furthest);
		success.Cost.Should().Be(0);
		success.PlayerSeatIndex.Should().Be(actingPlayer.SeatPosition);

		var freshPlayer = await GetFreshDbContext().GamePlayers
			.FirstAsync(gp => gp.Id == actingPlayer.Id);
		freshPlayer.ChipStack.Should().Be(chipsBefore, "Furthest choice is free");
		freshPlayer.HasDrawnThisRound.Should().BeTrue();
	}

	[Fact]
	public async Task ChooseCard_Nearest_ChargesOneAnte()
	{
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 2);
		var game = setup.Game;
		var ante = game.Ante ?? 10;
		var actingPlayer = setup.GamePlayers.First(gp => gp.SeatPosition == game.CurrentDrawPlayerIndex);
		var chipsBefore = actingPlayer.ChipStack;

		var result = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Nearest, actingPlayer.SeatPosition));

		result.IsT0.Should().BeTrue();
		result.AsT0.Cost.Should().Be(ante);

		var freshPlayer = await GetFreshDbContext().GamePlayers
			.FirstAsync(gp => gp.Id == actingPlayer.Id);
		freshPlayer.ChipStack.Should().Be(chipsBefore - ante);
	}

	[Fact]
	public async Task ChooseCard_Deck_ChargesTwoAntes()
	{
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 2);
		var game = setup.Game;
		var ante = game.Ante ?? 10;
		var actingPlayer = setup.GamePlayers.First(gp => gp.SeatPosition == game.CurrentDrawPlayerIndex);
		var chipsBefore = actingPlayer.ChipStack;

		var result = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Deck, actingPlayer.SeatPosition));

		result.IsT0.Should().BeTrue();
		result.AsT0.Cost.Should().Be(ante * 2);

		var freshPlayer = await GetFreshDbContext().GamePlayers
			.FirstAsync(gp => gp.Id == actingPlayer.Id);
		freshPlayer.ChipStack.Should().Be(chipsBefore - ante * 2);
	}

	[Fact]
	public async Task ChooseCard_WhenCannotAfford_ReturnsError()
	{
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 2, startingChips: 5);
		var game = setup.Game;
		var actingPlayer = setup.GamePlayers.First(gp => gp.SeatPosition == game.CurrentDrawPlayerIndex);

		var result = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Deck, actingPlayer.SeatPosition));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.CannotAfford);
	}

	[Fact]
	public async Task ChooseCard_WrongPhase_ReturnsNotInTollboothPhase()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, TollboothCode, 2, ante: 10);
		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.ThirdStreet);
		game.CurrentHandGameTypeCode = TollboothCode;
		game.Status = GameStatus.InProgress;
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Furthest, 0));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.NotInTollboothPhase);
	}

	[Fact]
	public async Task ChooseCard_AlreadyChosen_ReturnsAlreadyChosenError()
	{
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 3);
		var game = setup.Game;
		var actingPlayer = setup.GamePlayers.First(gp => gp.SeatPosition == game.CurrentDrawPlayerIndex);

		// First choice should succeed
		var firstResult = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Furthest, actingPlayer.SeatPosition));
		firstResult.IsT0.Should().BeTrue();

		// Second choice by same player should fail
		var secondResult = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Nearest, actingPlayer.SeatPosition));
		secondResult.IsT1.Should().BeTrue();
		secondResult.AsT1.Code.Should().Be(ChooseCardErrorCode.AlreadyChosen);
	}

	[Fact]
	public async Task ChooseCard_AllPlayersChoose_TransitionsToNextBettingStreet()
	{
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 2);
		var game = setup.Game;

		var players = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

		// First player chooses
		var firstResult = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Furthest, players[0].SeatPosition));
		firstResult.IsT0.Should().BeTrue();
		firstResult.AsT0.OfferRoundComplete.Should().BeFalse();

		// Second player chooses
		var secondResult = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Deck, players[1].SeatPosition));
		secondResult.IsT0.Should().BeTrue();
		secondResult.AsT0.OfferRoundComplete.Should().BeTrue();
		secondResult.AsT0.CurrentPhase.Should().Be(nameof(Phases.FourthStreet));

		// Verify the game actually transitioned
		var freshGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
		freshGame.CurrentPhase.Should().Be(nameof(Phases.FourthStreet));
	}

	[Fact]
	public async Task ChooseCard_FinalOffer_WhenFewerThanTwoPlayersCanBet_TransitionsDirectlyToShowdown()
	{
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 2, startingChips: 20, ante: 10);
		var game = setup.Game;
		var players = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

		TollboothVariantState.SetPreviousBettingStreet(game, nameof(Phases.SixthStreet));
		await DbContext.SaveChangesAsync();

		var firstResult = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Deck, players[0].SeatPosition));
		firstResult.IsT0.Should().BeTrue();
		firstResult.AsT0.OfferRoundComplete.Should().BeFalse();

		var secondResult = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Deck, players[1].SeatPosition));
		secondResult.IsT0.Should().BeTrue();
		secondResult.AsT0.OfferRoundComplete.Should().BeTrue();
		secondResult.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));

		await using var freshDb = GetFreshDbContext();
		var freshGame = await freshDb.Games.FirstAsync(g => g.Id == game.Id);
		freshGame.CurrentPhase.Should().Be(nameof(Phases.Showdown));
		freshGame.CurrentPlayerIndex.Should().Be(-1);
		freshGame.CurrentDrawPlayerIndex.Should().Be(-1);

		var activeBettingRounds = await freshDb.BettingRounds
			.Where(br => br.GameId == game.Id &&
						 br.HandNumber == game.CurrentHandNumber &&
						 !br.IsComplete)
			.ToListAsync();
		activeBettingRounds.Should().BeEmpty("the final Tollbooth offer should not create a dead seventh-street betting round when nobody can bet");
	}

	[Fact]
	public async Task ChooseCard_FinalOffer_WhenLateBettingActionArrivesDuringShowdown_ReturnsNoOpSuccess()
	{
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 2, startingChips: 20, ante: 10);
		var game = setup.Game;
		var players = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

		TollboothVariantState.SetPreviousBettingStreet(game, nameof(Phases.SixthStreet));
		await DbContext.SaveChangesAsync();

		var firstChoice = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Deck, players[0].SeatPosition));
		firstChoice.IsT0.Should().BeTrue();

		var finalChoice = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Deck, players[1].SeatPosition));
		finalChoice.IsT0.Should().BeTrue();
		finalChoice.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));

		var staleBettingAction = await Mediator.Send(new ProcessBettingActionCommand(
			game.Id,
			CardGames.Poker.Api.Data.Entities.BettingActionType.Check,
			0));

		staleBettingAction.IsT0.Should().BeTrue();
		staleBettingAction.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown));
		staleBettingAction.AsT0.RoundComplete.Should().BeTrue();
		staleBettingAction.AsT0.ShouldBroadcastGameState.Should().BeFalse();

		await using var freshDb = GetFreshDbContext();
		var freshGame = await freshDb.Games.FirstAsync(g => g.Id == game.Id);
		freshGame.CurrentPhase.Should().Be(nameof(Phases.Showdown));

		var activeBettingRounds = await freshDb.BettingRounds
			.Where(br => br.GameId == game.Id &&
						 br.HandNumber == game.CurrentHandNumber &&
						 !br.IsComplete)
			.ToListAsync();
		activeBettingRounds.Should().BeEmpty();
	}

	[Fact]
	public async Task ChooseCard_Furthest_ReplenishesDisplayFromDeck()
	{
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 2);
		var game = setup.Game;
		var actingPlayer = setup.GamePlayers.First(gp => gp.SeatPosition == game.CurrentDrawPlayerIndex);

		// Count community cards before
		var communityBefore = await DbContext.GameCards
			.CountAsync(gc => gc.GameId == game.Id &&
							  gc.HandNumber == game.CurrentHandNumber &&
							  gc.Location == CardLocation.Community &&
							  gc.GamePlayerId == null);
		communityBefore.Should().Be(2, "two display cards should be placed");

		await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Furthest, actingPlayer.SeatPosition));

		// Community cards should be replenished (still 2 after one taken + one replaced)
		var freshDb = GetFreshDbContext();
		var communityAfter = await freshDb.GameCards
			.CountAsync(gc => gc.GameId == game.Id &&
							  gc.HandNumber == game.CurrentHandNumber &&
							  gc.Location == CardLocation.Community &&
							  gc.GamePlayerId == null);
		communityAfter.Should().Be(2, "display card removed should be replenished from deck");
	}

	[Fact]
	public async Task ChooseCard_Deck_DoesNotReplenishDisplay()
	{
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 2);
		var game = setup.Game;
		var actingPlayer = setup.GamePlayers.First(gp => gp.SeatPosition == game.CurrentDrawPlayerIndex);

		await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Deck, actingPlayer.SeatPosition));

		var freshDb = GetFreshDbContext();
		var communityAfter = await freshDb.GameCards
			.CountAsync(gc => gc.GameId == game.Id &&
							  gc.HandNumber == game.CurrentHandNumber &&
							  gc.Location == CardLocation.Community &&
							  gc.GamePlayerId == null);
		communityAfter.Should().Be(2, "display cards should remain unchanged when drawing from deck");
	}

	[Fact]
	public async Task ChooseCard_GameNotFound_ReturnsError()
	{
		var result = await Mediator.Send(new ChooseCardCommand(
			Guid.NewGuid(), TollboothChoice.Furthest));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.GameNotFound);
	}

	[Fact]
	public async Task ChooseCard_FinalOffer_WithBettablePlayers_SeventhStreetBettingCompletesToShowdown()
	{
		// Set up a game in final TollboothOffer (after SixthStreet) where players CAN bet
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 2, startingChips: 1000, ante: 10);
		var game = setup.Game;
		var players = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

		// Position as the final TollboothOffer (for 7th street cards)
		TollboothVariantState.SetPreviousBettingStreet(game, nameof(Phases.SixthStreet));
		await DbContext.SaveChangesAsync();

		// Both players choose free cards (Furthest) — they keep their chips
		var firstResult = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Furthest, players[0].SeatPosition));
		firstResult.IsT0.Should().BeTrue();
		firstResult.AsT0.OfferRoundComplete.Should().BeFalse();

		var secondResult = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Furthest, players[1].SeatPosition));
		secondResult.IsT0.Should().BeTrue();
		secondResult.AsT0.OfferRoundComplete.Should().BeTrue();
		secondResult.AsT0.CurrentPhase.Should().Be(nameof(Phases.SeventhStreet),
			"both players can bet, so a SeventhStreet betting round should be created");

		// Verify SeventhStreet betting round exists
		await using var freshDb1 = GetFreshDbContext();
		var freshGame1 = await freshDb1.Games.FirstAsync(g => g.Id == game.Id);
		freshGame1.CurrentPhase.Should().Be(nameof(Phases.SeventhStreet));

		var bettingRound = await freshDb1.BettingRounds
			.Where(br => br.GameId == game.Id &&
						 br.HandNumber == game.CurrentHandNumber &&
						 !br.IsComplete &&
						 br.Street == nameof(Phases.SeventhStreet))
			.FirstOrDefaultAsync();
		bettingRound.Should().NotBeNull("a SeventhStreet betting round should have been created");

		// Complete the SeventhStreet betting round: first player checks, second player checks
		var firstBet = await Mediator.Send(new ProcessBettingActionCommand(
			game.Id,
			CardGames.Poker.Api.Data.Entities.BettingActionType.Check,
			0));
		firstBet.IsT0.Should().BeTrue("first player check should succeed");

		var secondBet = await Mediator.Send(new ProcessBettingActionCommand(
			game.Id,
			CardGames.Poker.Api.Data.Entities.BettingActionType.Check,
			0));
		secondBet.IsT0.Should().BeTrue("second player check should succeed");
		secondBet.AsT0.RoundComplete.Should().BeTrue("both players checked so round is complete");
		secondBet.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown),
			"SeventhStreet is the last betting street; game should advance to Showdown");

		// Verify final game state
		await using var freshDb2 = GetFreshDbContext();
		var freshGame2 = await freshDb2.Games.FirstAsync(g => g.Id == game.Id);
		freshGame2.CurrentPhase.Should().Be(nameof(Phases.Showdown));
		freshGame2.CurrentPlayerIndex.Should().Be(-1);
	}

	[Fact]
	public async Task FullGame_AllFourOfferBettingCycles_TransitionsFromThirdStreetToShowdown()
	{
		// Start from ThirdStreet TollboothOffer and play all the way through to Showdown:
		// TollboothOffer(4th) → 4th betting → TollboothOffer(5th) → 5th betting →
		// TollboothOffer(6th) → 6th betting → TollboothOffer(7th) → 7th betting → Showdown
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 2, startingChips: 1000, ante: 10);
		var game = setup.Game;
		var players = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

		// --- Cycle 1: TollboothOffer (previousStreet=ThirdStreet) → FourthStreet ---
		var r1a = await Mediator.Send(new ChooseCardCommand(game.Id, TollboothChoice.Furthest, players[0].SeatPosition));
		r1a.IsT0.Should().BeTrue("cycle 1: first player choose should succeed");
		r1a.AsT0.OfferRoundComplete.Should().BeFalse();

		var r1b = await Mediator.Send(new ChooseCardCommand(game.Id, TollboothChoice.Furthest, players[1].SeatPosition));
		r1b.IsT0.Should().BeTrue("cycle 1: second player choose should succeed");
		r1b.AsT0.OfferRoundComplete.Should().BeTrue();
		r1b.AsT0.CurrentPhase.Should().Be(nameof(Phases.FourthStreet));

		// FourthStreet betting: check/check
		var b1a = await Mediator.Send(new ProcessBettingActionCommand(game.Id, CardGames.Poker.Api.Data.Entities.BettingActionType.Check, 0));
		b1a.IsT0.Should().BeTrue("cycle 1: first player check should succeed");

		var b1b = await Mediator.Send(new ProcessBettingActionCommand(game.Id, CardGames.Poker.Api.Data.Entities.BettingActionType.Check, 0));
		b1b.IsT0.Should().BeTrue("cycle 1: second player check should succeed");
		b1b.AsT0.RoundComplete.Should().BeTrue();
		b1b.AsT0.CurrentPhase.Should().Be(nameof(Phases.TollboothOffer),
			"after FourthStreet betting, Tollbooth should redirect to TollboothOffer for 5th street cards");

		// Verify previousBettingStreet was updated correctly
		{
			await using var db = GetFreshDbContext();
			var g = await db.Games.FirstAsync(x => x.Id == game.Id);
			var prevStreet = TollboothVariantState.GetPreviousBettingStreet(g);
			prevStreet.Should().Be(nameof(Phases.FourthStreet),
				"after FourthStreet betting completes, previousBettingStreet should be FourthStreet");
		}

		// --- Cycle 2: TollboothOffer (previousStreet=FourthStreet) → FifthStreet ---
		var r2a = await Mediator.Send(new ChooseCardCommand(game.Id, TollboothChoice.Furthest, players[0].SeatPosition));
		r2a.IsT0.Should().BeTrue("cycle 2: first player choose should succeed");

		var r2b = await Mediator.Send(new ChooseCardCommand(game.Id, TollboothChoice.Furthest, players[1].SeatPosition));
		r2b.IsT0.Should().BeTrue("cycle 2: second player choose should succeed");
		r2b.AsT0.OfferRoundComplete.Should().BeTrue();
		r2b.AsT0.CurrentPhase.Should().Be(nameof(Phases.FifthStreet));

		// FifthStreet betting: check/check
		var b2a = await Mediator.Send(new ProcessBettingActionCommand(game.Id, CardGames.Poker.Api.Data.Entities.BettingActionType.Check, 0));
		b2a.IsT0.Should().BeTrue("cycle 2: first player check should succeed");

		var b2b = await Mediator.Send(new ProcessBettingActionCommand(game.Id, CardGames.Poker.Api.Data.Entities.BettingActionType.Check, 0));
		b2b.IsT0.Should().BeTrue("cycle 2: second player check should succeed");
		b2b.AsT0.RoundComplete.Should().BeTrue();
		b2b.AsT0.CurrentPhase.Should().Be(nameof(Phases.TollboothOffer),
			"after FifthStreet betting, Tollbooth should redirect to TollboothOffer for 6th street cards");

		// --- Cycle 3: TollboothOffer (previousStreet=FifthStreet) → SixthStreet ---
		var r3a = await Mediator.Send(new ChooseCardCommand(game.Id, TollboothChoice.Furthest, players[0].SeatPosition));
		r3a.IsT0.Should().BeTrue("cycle 3: first player choose should succeed");

		var r3b = await Mediator.Send(new ChooseCardCommand(game.Id, TollboothChoice.Furthest, players[1].SeatPosition));
		r3b.IsT0.Should().BeTrue("cycle 3: second player choose should succeed");
		r3b.AsT0.OfferRoundComplete.Should().BeTrue();
		r3b.AsT0.CurrentPhase.Should().Be(nameof(Phases.SixthStreet));

		// SixthStreet betting: check/check
		var b3a = await Mediator.Send(new ProcessBettingActionCommand(game.Id, CardGames.Poker.Api.Data.Entities.BettingActionType.Check, 0));
		b3a.IsT0.Should().BeTrue("cycle 3: first player check should succeed");

		var b3b = await Mediator.Send(new ProcessBettingActionCommand(game.Id, CardGames.Poker.Api.Data.Entities.BettingActionType.Check, 0));
		b3b.IsT0.Should().BeTrue("cycle 3: second player check should succeed");
		b3b.AsT0.RoundComplete.Should().BeTrue();
		b3b.AsT0.CurrentPhase.Should().Be(nameof(Phases.TollboothOffer),
			"after SixthStreet betting, Tollbooth should redirect to TollboothOffer for 7th street cards");

		// --- Cycle 4: TollboothOffer (previousStreet=SixthStreet) → SeventhStreet → Showdown ---
		var r4a = await Mediator.Send(new ChooseCardCommand(game.Id, TollboothChoice.Furthest, players[0].SeatPosition));
		r4a.IsT0.Should().BeTrue("cycle 4: first player choose should succeed");

		var r4b = await Mediator.Send(new ChooseCardCommand(game.Id, TollboothChoice.Furthest, players[1].SeatPosition));
		r4b.IsT0.Should().BeTrue("cycle 4: second player choose should succeed");
		r4b.AsT0.OfferRoundComplete.Should().BeTrue();
		r4b.AsT0.CurrentPhase.Should().Be(nameof(Phases.SeventhStreet));

		// SeventhStreet betting: check/check
		var b4a = await Mediator.Send(new ProcessBettingActionCommand(game.Id, CardGames.Poker.Api.Data.Entities.BettingActionType.Check, 0));
		b4a.IsT0.Should().BeTrue("cycle 4: first player check should succeed");

		var b4b = await Mediator.Send(new ProcessBettingActionCommand(game.Id, CardGames.Poker.Api.Data.Entities.BettingActionType.Check, 0));
		b4b.IsT0.Should().BeTrue("cycle 4: second player check should succeed");
		b4b.AsT0.RoundComplete.Should().BeTrue();
		b4b.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown),
			"SeventhStreet is the last betting street; game should advance to Showdown");

		// Verify final state
		await using var freshDb = GetFreshDbContext();
		var freshGame = await freshDb.Games.FirstAsync(g => g.Id == game.Id);
		freshGame.CurrentPhase.Should().Be(nameof(Phases.Showdown));
		freshGame.CurrentPlayerIndex.Should().Be(-1);

		// Verify each player has 7 cards (2 hole + 1 board from ThirdStreet + 4 from TollboothOffers)
		foreach (var player in players)
		{
			var cardCount = await freshDb.GameCards
				.CountAsync(gc => gc.GamePlayerId == player.Id &&
								  gc.HandNumber == game.CurrentHandNumber &&
								  !gc.IsDiscarded &&
								  gc.Location != CardLocation.Deck);
			cardCount.Should().Be(7, $"player at seat {player.SeatPosition} should have 7 cards after all streets");
		}

		// Verify no active (incomplete) betting rounds remain
		var activeBettingRounds = await freshDb.BettingRounds
			.Where(br => br.GameId == game.Id &&
						 br.HandNumber == game.CurrentHandNumber &&
						 !br.IsComplete)
			.ToListAsync();
		activeBettingRounds.Should().BeEmpty("all betting rounds should be complete");
	}

	[Fact]
	public async Task ChooseCard_AllPlayersAllIn_PreSeventhStreet_DealsRemainingAndTransitionsToShowdown()
	{
		// startingChips: 20, ante: 10 → Deck choice costs 20 and depletes stack
		var setup = await CreateTollboothGameInOfferPhaseAsync(playerCount: 2, startingChips: 20, ante: 10);
		var game = setup.Game;
		var players = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

		// Previous street = ThirdStreet → nextStreet = FourthStreet (pre-SeventhStreet)
		TollboothVariantState.SetPreviousBettingStreet(game, nameof(Phases.ThirdStreet));
		await DbContext.SaveChangesAsync();

		// Both players choose Deck (costs 2×ante = 20 → all-in)
		var firstResult = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Deck, players[0].SeatPosition));
		firstResult.IsT0.Should().BeTrue();
		firstResult.AsT0.OfferRoundComplete.Should().BeFalse();

		var secondResult = await Mediator.Send(new ChooseCardCommand(
			game.Id, TollboothChoice.Deck, players[1].SeatPosition));
		secondResult.IsT0.Should().BeTrue();
		secondResult.AsT0.OfferRoundComplete.Should().BeTrue();
		secondResult.AsT0.CurrentPhase.Should().Be(nameof(Phases.Showdown),
			"all players are all-in on pre-SeventhStreet; should deal remaining and transition to Showdown");

		await using var freshDb = GetFreshDbContext();
		var freshGame = await freshDb.Games.FirstAsync(g => g.Id == game.Id);
		freshGame.CurrentPhase.Should().Be(nameof(Phases.Showdown));
		freshGame.CurrentPlayerIndex.Should().Be(-1);

		// Each player should have 7 cards (3 from ThirdStreet + 1 from this offer + 3 dealt remaining)
		foreach (var player in players)
		{
			var cardCount = await freshDb.GameCards
				.CountAsync(gc => gc.GamePlayerId == player.Id &&
								  gc.HandNumber == game.CurrentHandNumber &&
								  !gc.IsDiscarded &&
								  gc.Location != CardLocation.Deck);
			cardCount.Should().Be(7, $"player at seat {player.SeatPosition} should have all 7 cards after all-in runout");
		}

		// 7th street card should be a hole card
		foreach (var player in players)
		{
			var seventhStreetCard = await freshDb.GameCards
				.FirstOrDefaultAsync(gc => gc.GamePlayerId == player.Id &&
										   gc.HandNumber == game.CurrentHandNumber &&
										   gc.DealtAtPhase == nameof(Phases.SeventhStreet) &&
										   gc.Location != CardLocation.Deck);
			seventhStreetCard.Should().NotBeNull();
			seventhStreetCard!.Location.Should().Be(CardLocation.Hole, "7th street cards should be hole cards");
			seventhStreetCard.IsVisible.Should().BeFalse("7th street cards should be face down");
		}

		// No active betting rounds should remain
		var activeBettingRounds = await freshDb.BettingRounds
			.Where(br => br.GameId == game.Id &&
						 br.HandNumber == game.CurrentHandNumber &&
						 !br.IsComplete)
			.ToListAsync();
		activeBettingRounds.Should().BeEmpty("all-in runout should skip betting rounds");
	}

	/// <summary>
	/// Sets up a Tollbooth game in TollboothOffer phase with 2 display community cards,
	/// cards dealt to all players (3 each: 2 hole + 1 board), and deck cards remaining.
	/// </summary>
	private async Task<GameSetup> CreateTollboothGameInOfferPhaseAsync(
		int playerCount = 3,
		int startingChips = 1000,
		int ante = 10)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, TollboothCode, playerCount, startingChips, ante);
		var game = setup.Game;
		var now = DateTimeOffset.UtcNow;

		game.CurrentHandNumber = 1;
		game.CurrentHandGameTypeCode = TollboothCode;
		game.CurrentPhase = nameof(Phases.TollboothOffer);
		game.Status = GameStatus.InProgress;
		game.DealerPosition = 0;
		game.Ante = ante;
		game.SmallBet = ante * 2;
		game.BigBet = ante * 4;

		// Set variant state: previous street = ThirdStreet (first Tollbooth round)
		TollboothVariantState.SetPreviousBettingStreet(game, nameof(Phases.ThirdStreet));

		// Set up draw tracking
		var activePlayers = setup.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.HasFolded)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		game.CurrentDrawPlayerIndex = activePlayers.First().SeatPosition;
		game.CurrentPlayerIndex = game.CurrentDrawPlayerIndex;

		foreach (var gp in activePlayers)
		{
			gp.HasDrawnThisRound = false;
		}

		var dealOrder = 1;
		var cardSymbol = CardSymbol.Deuce;

		// Deal 3 cards to each player (2 hole + 1 board)
		foreach (var gp in setup.GamePlayers)
		{
			// 2 hole cards (face down)
			for (var i = 0; i < 2; i++)
			{
				DbContext.GameCards.Add(new GameCard
				{
					Id = Guid.CreateVersion7(),
					GameId = game.Id,
					GamePlayerId = gp.Id,
					HandNumber = game.CurrentHandNumber,
					Location = CardLocation.Hole,
					Suit = (CardSuit)(dealOrder % 4),
					Symbol = cardSymbol,
					DealOrder = dealOrder++,
					IsVisible = false,
					IsDiscarded = false,
					DealtAt = now,
					DealtAtPhase = nameof(Phases.ThirdStreet)
				});
				cardSymbol = GetNextSymbol(cardSymbol);
			}

			// 1 board card (face up)
			DbContext.GameCards.Add(new GameCard
			{
				Id = Guid.CreateVersion7(),
				GameId = game.Id,
				GamePlayerId = gp.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Board,
				Suit = (CardSuit)(dealOrder % 4),
				Symbol = cardSymbol,
				DealOrder = dealOrder++,
				IsVisible = true,
				IsDiscarded = false,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.ThirdStreet)
			});
			cardSymbol = GetNextSymbol(cardSymbol);
		}

		// 2 display community cards (Tollbooth display)
		for (var i = 0; i < 2; i++)
		{
			DbContext.GameCards.Add(new GameCard
			{
				Id = Guid.CreateVersion7(),
				GameId = game.Id,
				GamePlayerId = null,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Community,
				Suit = (CardSuit)(dealOrder % 4),
				Symbol = cardSymbol,
				DealOrder = dealOrder++,
				IsVisible = true,
				IsDiscarded = false,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.TollboothOffer)
			});
			cardSymbol = GetNextSymbol(cardSymbol);
		}

		// Remaining deck cards (enough for the game)
		for (var i = 0; i < 30; i++)
		{
			DbContext.GameCards.Add(new GameCard
			{
				Id = Guid.CreateVersion7(),
				GameId = game.Id,
				GamePlayerId = null,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Deck,
				Suit = (CardSuit)(dealOrder % 4),
				Symbol = cardSymbol,
				DealOrder = dealOrder++,
				IsVisible = false,
				IsDiscarded = false,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.TollboothOffer)
			});
			cardSymbol = GetNextSymbol(cardSymbol);
		}

		// Create the main pot
		DbContext.Add(new CardGames.Poker.Api.Data.Entities.Pot
		{
			Id = Guid.CreateVersion7(),
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = ante * playerCount,
			IsAwarded = false,
			CreatedAt = now
		});

		await DbContext.SaveChangesAsync();

		// Reload game with navigation properties
		var loadedGame = await DbContext.Games
			.Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
			.Include(g => g.GameType)
			.Include(g => g.Pots)
			.FirstAsync(g => g.Id == game.Id);

		return new GameSetup(loadedGame, setup.Players, setup.GamePlayers);
	}

	private static CardSymbol GetNextSymbol(CardSymbol current) =>
		current == CardSymbol.Ace ? CardSymbol.Deuce : current + 1;

	private async Task EnsureTollboothGameTypeAsync()
	{
		var existing = await DbContext.GameTypes.FirstOrDefaultAsync(gt => gt.Code == TollboothCode);
		if (existing is not null)
		{
			return;
		}

		DbContext.GameTypes.Add(new GameType
		{
			Id = Guid.CreateVersion7(),
			Code = TollboothCode,
			Name = "Tollbooth",
			MinPlayers = 2,
			MaxPlayers = 7,
			InitialHoleCards = 2,
			InitialBoardCards = 1,
			MaxCommunityCards = 0,
			MaxPlayerCards = 7,
			BettingStructure = BettingStructure.Ante
		});

		await DbContext.SaveChangesAsync();
	}
}
