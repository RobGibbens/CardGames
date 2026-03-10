using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;

namespace CardGames.IntegrationTests.GameFlow;

public class RazzFlowHandlerTests : IntegrationTestBase
{
    [Fact]
    public void GameTypeCode_ReturnsRazz()
    {
        var handler = new RazzFlowHandler();

        handler.GameTypeCode.Should().Be("RAZZ");
    }

    [Fact]
    public void GetDealingConfiguration_UsesStudPattern()
    {
        var handler = new RazzFlowHandler();

        var config = handler.GetDealingConfiguration();

        config.PatternType.Should().Be(DealingPatternType.StreetBased);
        config.DealingRounds.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetNextPhase_FollowsStreetSequence()
    {
        var handler = new RazzFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "RAZZ", 4);

        handler.GetNextPhase(setup.Game, nameof(Phases.CollectingAntes)).Should().Be(nameof(Phases.ThirdStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.ThirdStreet)).Should().Be(nameof(Phases.FourthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.FourthStreet)).Should().Be(nameof(Phases.FifthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.FifthStreet)).Should().Be(nameof(Phases.SixthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.SixthStreet)).Should().Be(nameof(Phases.SeventhStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.SeventhStreet)).Should().Be(nameof(Phases.Showdown));
    }
}
