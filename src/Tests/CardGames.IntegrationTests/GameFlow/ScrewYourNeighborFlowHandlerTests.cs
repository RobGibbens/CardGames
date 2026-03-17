using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Services;

namespace CardGames.IntegrationTests.GameFlow;

/// <summary>
/// Integration tests for <see cref="ScrewYourNeighborFlowHandler"/>.
/// Tests phase transitions, dealing configuration, and game properties.
/// </summary>
public class ScrewYourNeighborFlowHandlerTests : IntegrationTestBase
{
	[Fact]
	public void GameTypeCode_ReturnsScrewYourNeighbor()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		handler.GameTypeCode.Should().Be("SCREWYOURNEIGHBOR");
	}

	[Fact]
	public void GetGameRules_ReturnsValidRules()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		var rules = handler.GetGameRules();

		rules.Should().NotBeNull();
		rules.Phases.Should().NotBeEmpty();
		rules.GameTypeName.Should().Be("Screw Your Neighbor");
	}

	[Fact]
	public void GetDealingConfiguration_Returns1CardFaceDown()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		var config = handler.GetDealingConfiguration();

		config.PatternType.Should().Be(DealingPatternType.AllAtOnce);
		config.InitialCardsPerPlayer.Should().Be(1);
		config.AllFaceDown.Should().BeTrue();
	}

	[Fact]
	public async Task GetInitialPhase_ReturnsDealing()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 4);

		var initialPhase = handler.GetInitialPhase(setup.Game);
		initialPhase.Should().Be(nameof(Phases.Dealing));
	}

	[Fact]
	public void SkipsAnteCollection_ReturnsTrue()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		handler.SkipsAnteCollection.Should().BeTrue();
	}

	[Fact]
	public void IsMultiHandVariant_ReturnsTrue()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		handler.IsMultiHandVariant.Should().BeTrue();
	}

	[Fact]
	public void SupportsInlineShowdown_ReturnsTrue()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		handler.SupportsInlineShowdown.Should().BeTrue();
	}

	[Fact]
	public void RequiresChipCoverageCheck_ReturnsFalse()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		handler.RequiresChipCoverageCheck.Should().BeFalse();
	}

	[Fact]
	public void SpecialPhases_ContainsKeepOrTradeAndReveal()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		handler.SpecialPhases.Should().Contain(nameof(Phases.KeepOrTrade));
		handler.SpecialPhases.Should().Contain(nameof(Phases.Reveal));
	}

	[Fact]
	public async Task GetNextPhase_FromDealing_ReturnsKeepOrTrade()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 4);

		var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.Dealing));
		nextPhase.Should().Be(nameof(Phases.KeepOrTrade));
	}

	[Fact]
	public async Task GetNextPhase_FromKeepOrTrade_ReturnsReveal()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 4);

		var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.KeepOrTrade));
		nextPhase.Should().Be(nameof(Phases.Reveal));
	}

	[Fact]
	public async Task GetNextPhase_FromReveal_ReturnsShowdown()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 4);

		var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.Reveal));
		nextPhase.Should().Be(nameof(Phases.Showdown));
	}

	[Fact]
	public async Task GetNextPhase_FromShowdown_ReturnsComplete()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 4);

		var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.Showdown));
		nextPhase.Should().Be(nameof(Phases.Complete));
	}

	[Fact]
	public async Task CompletePhaseSequence_VerifyAllTransitions()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 4);

		handler.GetNextPhase(setup.Game, nameof(Phases.Dealing))
			.Should().Be(nameof(Phases.KeepOrTrade));

		handler.GetNextPhase(setup.Game, nameof(Phases.KeepOrTrade))
			.Should().Be(nameof(Phases.Reveal));

		handler.GetNextPhase(setup.Game, nameof(Phases.Reveal))
			.Should().Be(nameof(Phases.Showdown));

		handler.GetNextPhase(setup.Game, nameof(Phases.Showdown))
			.Should().Be(nameof(Phases.Complete));
	}

	[Fact]
	public async Task DealCards_DealsOneCardPerPlayer()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 4);
		var handler = FlowHandlerFactory.GetHandler("SCREWYOURNEIGHBOR");

		// Start hand to set up game state
		setup.Game.CurrentPhase = nameof(Phases.Dealing);
		setup.Game.CurrentHandNumber = 1;
		setup.Game.Status = GameStatus.InProgress;
		await DbContext.SaveChangesAsync();

		await handler.DealCardsAsync(
			DbContext,
			setup.Game,
			setup.GamePlayers,
			DateTimeOffset.UtcNow,
			CancellationToken.None);

		var cards = await DbContext.GameCards
			.Where(c => c.GameId == setup.Game.Id && c.HandNumber == setup.Game.CurrentHandNumber)
			.ToListAsync();

		// Each active player should have exactly 1 card
		var playerCards = cards.Where(c => c.GamePlayerId.HasValue).ToList();
		playerCards.Should().HaveCount(4);

		// SYN rule: Kings are immediately face-up; all other dealt cards stay face-down.
		playerCards.Should().AllSatisfy(c => c.IsVisible.Should().Be(c.Symbol == CardSymbol.King));

		// Phase should transition to KeepOrTrade
		setup.Game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));
	}

	[Fact]
	public async Task PerformShowdown_TwoPlayers_AceLoses_OnlyLoserLosesStack()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext,
			"SCREWYOURNEIGHBOR",
			2,
			startingChips: 100,
			ante: 25);

		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;

		await DatabaseSeeder.CreatePotAsync(DbContext, game, amount: 0, potType: PotType.Main);

		var rob = setup.GamePlayers[0];
		var lynne = setup.GamePlayers[1];
		var now = DateTimeOffset.UtcNow;

		DbContext.GameCards.AddRange(
			new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				GamePlayerId = rob.Id,
				HandNumber = game.CurrentHandNumber,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Four,
				Location = CardLocation.Hand,
				DealOrder = 1,
				IsVisible = false,
				DealtAt = now
			},
			new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				GamePlayerId = lynne.Id,
				HandNumber = game.CurrentHandNumber,
				Suit = CardSuit.Spades,
				Symbol = CardSymbol.Ace,
				Location = CardLocation.Hand,
				DealOrder = 1,
				IsVisible = false,
				DealtAt = now
			});

		await DbContext.SaveChangesAsync();

		var handler = new ScrewYourNeighborFlowHandler();
		var handHistoryRecorder = Scope.ServiceProvider.GetRequiredService<IHandHistoryRecorder>();

		// Act
		var result = await handler.PerformShowdownAsync(
			DbContext,
			game,
			handHistoryRecorder,
			now,
			CancellationToken.None);

		// Assert
		result.IsSuccess.Should().BeTrue();
		result.LoserPlayerIds.Should().ContainSingle().Which.Should().Be(lynne.PlayerId);
		result.WinnerPlayerIds.Should().Contain(rob.PlayerId);

		var updatedRob = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == rob.Id);
		var updatedLynne = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == lynne.Id);
		var nextHandPot = await DbContext.Pots
			.AsNoTracking()
			.FirstOrDefaultAsync(p => p.GameId == game.Id &&
			                          p.HandNumber == game.CurrentHandNumber + 1 &&
			                          p.PotType == PotType.Main);

		updatedRob.ChipStack.Should().Be(100);
		updatedLynne.ChipStack.Should().Be(75);
		nextHandPot.Should().NotBeNull();
		nextHandPot!.Amount.Should().Be(25);
	}

	[Fact]
	public async Task PerformShowdown_AnteZero_LoserStillPaysDefaultStack_WinnerDoesNotPay()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext,
			"SCREWYOURNEIGHBOR",
			2,
			startingChips: 100,
			ante: 0);

		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;

		await DatabaseSeeder.CreatePotAsync(DbContext, game, amount: 0, potType: PotType.Main);

		var winner = setup.GamePlayers[0];
		var loser = setup.GamePlayers[1];
		var now = DateTimeOffset.UtcNow;

		DbContext.GameCards.AddRange(
			new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				GamePlayerId = winner.Id,
				HandNumber = game.CurrentHandNumber,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Four,
				Location = CardLocation.Hand,
				DealOrder = 1,
				IsVisible = false,
				DealtAt = now
			},
			new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				GamePlayerId = loser.Id,
				HandNumber = game.CurrentHandNumber,
				Suit = CardSuit.Spades,
				Symbol = CardSymbol.Ace,
				Location = CardLocation.Hand,
				DealOrder = 1,
				IsVisible = false,
				DealtAt = now
			});

		await DbContext.SaveChangesAsync();

		var handler = new ScrewYourNeighborFlowHandler();
		var handHistoryRecorder = Scope.ServiceProvider.GetRequiredService<IHandHistoryRecorder>();

		// Act
		var result = await handler.PerformShowdownAsync(
			DbContext,
			game,
			handHistoryRecorder,
			now,
			CancellationToken.None);

		// Assert
		result.IsSuccess.Should().BeTrue();
		result.TotalPotAwarded.Should().Be(25);
		result.LoserPlayerIds.Should().ContainSingle().Which.Should().Be(loser.PlayerId);
		result.WinnerPlayerIds.Should().ContainSingle().Which.Should().Be(winner.PlayerId);

		var updatedWinner = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == winner.Id);
		var updatedLoser = await DbContext.GamePlayers.AsNoTracking().FirstAsync(gp => gp.Id == loser.Id);
		var nextHandPot = await DbContext.Pots
			.AsNoTracking()
			.FirstOrDefaultAsync(p => p.GameId == game.Id &&
			                          p.HandNumber == game.CurrentHandNumber + 1 &&
			                          p.PotType == PotType.Main);

		updatedWinner.ChipStack.Should().Be(100);
		updatedLoser.ChipStack.Should().Be(75);
		nextHandPot.Should().NotBeNull();
		nextHandPot!.Amount.Should().Be(25);
	}

	[Fact]
	public async Task ProcessPostShowdownAsync_GameCompleted_ReturnsEnded()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 2);
		setup.Game.Status = GameStatus.Completed;

		var nextPhase = await handler.ProcessPostShowdownAsync(
			DbContext,
			setup.Game,
			ShowdownResult.Success([setup.GamePlayers[0].PlayerId], [setup.GamePlayers[1].PlayerId], 25, "winner"),
			DateTimeOffset.UtcNow,
			CancellationToken.None);

		nextPhase.Should().Be("Ended");
	}

	[Fact]
	public async Task ProcessPostShowdownAsync_GameContinues_ReturnsComplete()
	{
		var handler = new ScrewYourNeighborFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 2);
		setup.Game.Status = GameStatus.InProgress;

		var nextPhase = await handler.ProcessPostShowdownAsync(
			DbContext,
			setup.Game,
			ShowdownResult.Success([setup.GamePlayers[0].PlayerId], [setup.GamePlayers[1].PlayerId], 25, "winner"),
			DateTimeOffset.UtcNow,
			CancellationToken.None);

		nextPhase.Should().Be(nameof(Phases.Complete));
	}
}
