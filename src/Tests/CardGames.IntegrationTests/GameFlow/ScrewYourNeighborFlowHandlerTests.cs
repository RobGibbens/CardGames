using CardGames.Poker.Api.GameFlow;

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

		// Cards should be face down
		playerCards.Should().AllSatisfy(c => c.IsVisible.Should().BeFalse());

		// Phase should transition to KeepOrTrade
		setup.Game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));
	}
}
