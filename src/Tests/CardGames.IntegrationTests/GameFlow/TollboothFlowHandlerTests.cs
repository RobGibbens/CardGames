using CardGames.Poker.Api.Features.Games.Tollbooth;
using CardGames.Poker.Api.Games;

namespace CardGames.IntegrationTests.GameFlow;

public class TollboothFlowHandlerTests : IntegrationTestBase
{
	private const string TollboothCode = "TOLLBOOTH";

	protected override async Task SeedBaseDataAsync()
	{
		await base.SeedBaseDataAsync();
		await EnsureTollboothGameTypeAsync();
	}

	[Fact]
	public void GameTypeCode_ReturnsTollbooth()
	{
		var handler = new TollboothFlowHandler();

		handler.GameTypeCode.Should().Be(PokerGameMetadataRegistry.TollboothCode);
	}

	[Fact]
	public void GetGameRules_ReturnsValidRules()
	{
		var handler = new TollboothFlowHandler();

		var rules = handler.GetGameRules();

		rules.Should().NotBeNull();
		rules.Phases.Should().NotBeEmpty();
		rules.GameTypeCode.Should().Be("TOLLBOOTH");
	}

	[Fact]
	public void GetDealingConfiguration_ReturnsStreetBased()
	{
		var handler = new TollboothFlowHandler();

		var config = handler.GetDealingConfiguration();

		config.PatternType.Should().Be(DealingPatternType.StreetBased);
		config.DealingRounds.Should().NotBeNull();
		config.DealingRounds.Should().HaveCount(5);
	}

	[Fact]
	public void GetDealingConfiguration_ThirdStreet_Has2Hole1Board()
	{
		var handler = new TollboothFlowHandler();

		var config = handler.GetDealingConfiguration();
		var thirdStreet = config.DealingRounds!.First(r => r.PhaseName == nameof(Phases.ThirdStreet));

		thirdStreet.HoleCards.Should().Be(2);
		thirdStreet.BoardCards.Should().Be(1);
		thirdStreet.HasBettingAfter.Should().BeTrue();
	}

	[Fact]
	public void GetDealingConfiguration_FourthThroughSeventhStreet_HaveZeroCards()
	{
		var handler = new TollboothFlowHandler();
		var config = handler.GetDealingConfiguration();

		var streets = new[] { nameof(Phases.FourthStreet), nameof(Phases.FifthStreet), nameof(Phases.SixthStreet), nameof(Phases.SeventhStreet) };
		foreach (var streetName in streets)
		{
			var round = config.DealingRounds!.First(r => r.PhaseName == streetName);
			round.HoleCards.Should().Be(0, $"{streetName} cards are dealt via Tollbooth, not the dealing pipeline");
			round.BoardCards.Should().Be(0);
			round.HasBettingAfter.Should().BeTrue();
		}
	}

	[Fact]
	public void SpecialPhases_ContainsTollboothOffer()
	{
		var handler = new TollboothFlowHandler();

		handler.SpecialPhases.Should().ContainSingle()
			.Which.Should().Be(nameof(Phases.TollboothOffer));
	}

	[Fact]
	public async Task GetInitialPhase_ReturnsCollectingAntes()
	{
		var handler = new TollboothFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, TollboothCode, 3);

		var initialPhase = handler.GetInitialPhase(setup.Game);

		initialPhase.Should().Be(nameof(Phases.CollectingAntes));
	}

	[Fact]
	public async Task GetNextPhase_ThirdStreet_ReturnsTollboothOffer()
	{
		var handler = new TollboothFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, TollboothCode, 3);

		var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.ThirdStreet));

		nextPhase.Should().Be(nameof(Phases.TollboothOffer));
	}

	[Fact]
	public async Task GetNextPhase_BettingStreets_ReturnTollboothOffer()
	{
		var handler = new TollboothFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, TollboothCode, 3);

		handler.GetNextPhase(setup.Game, nameof(Phases.FourthStreet)).Should().Be(nameof(Phases.TollboothOffer));
		handler.GetNextPhase(setup.Game, nameof(Phases.FifthStreet)).Should().Be(nameof(Phases.TollboothOffer));
		handler.GetNextPhase(setup.Game, nameof(Phases.SixthStreet)).Should().Be(nameof(Phases.TollboothOffer));
	}

	[Fact]
	public async Task GetNextPhase_SeventhStreet_ReturnsShowdown()
	{
		var handler = new TollboothFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, TollboothCode, 3);

		handler.GetNextPhase(setup.Game, nameof(Phases.SeventhStreet)).Should().Be(nameof(Phases.Showdown));
	}

	[Fact]
	public async Task GetNextPhase_Showdown_ReturnsComplete()
	{
		var handler = new TollboothFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, TollboothCode, 3);

		handler.GetNextPhase(setup.Game, nameof(Phases.Showdown)).Should().Be(nameof(Phases.Complete));
	}

	[Fact]
	public async Task GetNextPhase_TollboothOffer_ResolvesFromVariantState()
	{
		var handler = new TollboothFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, TollboothCode, 3);

		TollboothVariantState.SetPreviousBettingStreet(setup.Game, nameof(Phases.ThirdStreet));
		handler.GetNextPhase(setup.Game, nameof(Phases.TollboothOffer)).Should().Be(nameof(Phases.FourthStreet));

		TollboothVariantState.SetPreviousBettingStreet(setup.Game, nameof(Phases.FourthStreet));
		handler.GetNextPhase(setup.Game, nameof(Phases.TollboothOffer)).Should().Be(nameof(Phases.FifthStreet));

		TollboothVariantState.SetPreviousBettingStreet(setup.Game, nameof(Phases.FifthStreet));
		handler.GetNextPhase(setup.Game, nameof(Phases.TollboothOffer)).Should().Be(nameof(Phases.SixthStreet));

		TollboothVariantState.SetPreviousBettingStreet(setup.Game, nameof(Phases.SixthStreet));
		handler.GetNextPhase(setup.Game, nameof(Phases.TollboothOffer)).Should().Be(nameof(Phases.SeventhStreet));
	}

	[Fact]
	public async Task GetNextPhase_SinglePlayerRemaining_SkipsToShowdown()
	{
		var handler = new TollboothFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, TollboothCode, 3);

		foreach (var gp in setup.GamePlayers.Skip(1))
		{
			gp.HasFolded = true;
		}
		await DbContext.SaveChangesAsync();

		handler.GetNextPhase(setup.Game, nameof(Phases.FourthStreet)).Should().Be(nameof(Phases.Showdown));
	}

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
