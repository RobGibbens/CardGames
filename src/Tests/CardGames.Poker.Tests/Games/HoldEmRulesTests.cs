using System.Linq;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.HoldEm;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class HoldEmRulesTests
{
    [Fact]
    public void CreateGameRules_PhasesIncludeCollectingBlindsAndDealing()
    {
        var rules = HoldEmRules.CreateGameRules();

        var phaseIds = rules.Phases.Select(p => p.PhaseId).ToList();

        phaseIds.Should().Contain("CollectingBlinds");
        phaseIds.Should().Contain("Dealing");
    }

    [Fact]
    public void CreateGameRules_PhasesAreInCorrectOrder()
    {
        var rules = HoldEmRules.CreateGameRules();

        var expectedOrder = new[]
        {
            "WaitingToStart",
            "CollectingBlinds",
            "Dealing",
            "PreFlop",
            "Flop",
            "Turn",
            "River",
            "Showdown",
            "Complete"
        };

        rules.Phases.Select(p => p.PhaseId).Should().ContainInConsecutiveOrder(expectedOrder);
    }

    [Fact]
    public void CreateGameRules_DealingPhasesAreNotPlayerAction()
    {
        var rules = HoldEmRules.CreateGameRules();

        var collectingBlinds = rules.Phases.Single(p => p.PhaseId == "CollectingBlinds");
        var dealing = rules.Phases.Single(p => p.PhaseId == "Dealing");

        collectingBlinds.RequiresPlayerAction.Should().BeFalse();
        dealing.RequiresPlayerAction.Should().BeFalse();
    }

    [Fact]
    public void CreateGameRules_BettingPhasesRequirePlayerAction()
    {
        var rules = HoldEmRules.CreateGameRules();

        var bettingPhaseIds = new[] { "PreFlop", "Flop", "Turn", "River" };
        foreach (var phaseId in bettingPhaseIds)
        {
            var phase = rules.Phases.Single(p => p.PhaseId == phaseId);
            phase.RequiresPlayerAction.Should().BeTrue($"because {phaseId} is a betting phase");
        }
    }

    [Fact]
    public void CreateGameRules_HasBlindsConfigured()
    {
        var rules = HoldEmRules.CreateGameRules();

        rules.Betting.HasBlinds.Should().BeTrue();
        rules.Betting.HasAntes.Should().BeFalse();
    }
}
