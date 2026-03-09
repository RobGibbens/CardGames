using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Betting;

namespace CardGames.IntegrationTests.EndToEnd;

public class RazzGameFlowTests : IntegrationTestBase
{
    [Fact]
    public async Task StartHand_TransitionsToCollectingAntes()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "RAZZ", 4);

        var result = await Mediator.Send(new StartHandCommand(setup.Game.Id));

        result.IsT0.Should().BeTrue();
        result.AsT0.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));
    }

    [Fact]
    public async Task FlowHandler_Sequence_MatchesStudStreets()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "RAZZ", 4);
        var handler = FlowHandlerFactory.GetHandler("RAZZ");

        handler.GetNextPhase(setup.Game, nameof(Phases.CollectingAntes)).Should().Be(nameof(Phases.ThirdStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.ThirdStreet)).Should().Be(nameof(Phases.FourthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.FourthStreet)).Should().Be(nameof(Phases.FifthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.FifthStreet)).Should().Be(nameof(Phases.SixthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.SixthStreet)).Should().Be(nameof(Phases.SeventhStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.SeventhStreet)).Should().Be(nameof(Phases.Showdown));
        handler.GetNextPhase(setup.Game, nameof(Phases.Showdown)).Should().Be(nameof(Phases.Complete));
    }
}
