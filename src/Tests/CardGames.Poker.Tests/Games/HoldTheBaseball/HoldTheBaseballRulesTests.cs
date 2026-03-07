using System.Linq;
using CardGames.Poker.Games.HoldTheBaseball;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games.HoldTheBaseball;

public class HoldTheBaseballRulesTests
{
    [Fact]
    public void CreateGameRules_GameTypeCode_IsHoldTheBaseball()
    {
        var rules = HoldTheBaseballRules.CreateGameRules();

        rules.GameTypeCode.Should().Be("HOLDTHEBASEBALL");
    }

    [Fact]
    public void CreateGameRules_GameTypeName_IsCorrect()
    {
        var rules = HoldTheBaseballRules.CreateGameRules();

        rules.GameTypeName.Should().Be("Hold the Baseball");
    }

    [Fact]
    public void CreateGameRules_PhasesAreInCorrectOrder()
    {
        var rules = HoldTheBaseballRules.CreateGameRules();

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
    public void CreateGameRules_HasCorrectPhaseCount()
    {
        var rules = HoldTheBaseballRules.CreateGameRules();

        rules.Phases.Should().HaveCount(9);
    }

    [Fact]
    public void CreateGameRules_HasCommunityCards()
    {
        var rules = HoldTheBaseballRules.CreateGameRules();

        rules.CardDealing.HasCommunityCards.Should().BeTrue();
    }

    [Fact]
    public void CreateGameRules_HasBlinds_NotAntes()
    {
        var rules = HoldTheBaseballRules.CreateGameRules();

        rules.Betting.HasBlinds.Should().BeTrue();
        rules.Betting.HasAntes.Should().BeFalse();
    }

    [Fact]
    public void CreateGameRules_SpecialRules_ContainsWildCards()
    {
        var rules = HoldTheBaseballRules.CreateGameRules();

        rules.SpecialRules.Should().ContainKey("WildCards");
        rules.SpecialRules["WildCards"].ToString().Should().Contain("3s and 9s");
    }

    [Fact]
    public void CreateGameRules_ShowdownConfig_MentionsWildCards()
    {
        var rules = HoldTheBaseballRules.CreateGameRules();

        rules.Showdown.HandRanking.Should().Contain("Wild");
    }

    [Fact]
    public void CreateGameRules_BettingPhasesRequirePlayerAction()
    {
        var rules = HoldTheBaseballRules.CreateGameRules();

        var bettingPhaseIds = new[] { "PreFlop", "Flop", "Turn", "River" };
        foreach (var phaseId in bettingPhaseIds)
        {
            var phase = rules.Phases.Single(p => p.PhaseId == phaseId);
            phase.RequiresPlayerAction.Should().BeTrue($"because {phaseId} is a betting phase");
        }
    }

    [Fact]
    public void CreateGameRules_SetupPhasesDoNotRequirePlayerAction()
    {
        var rules = HoldTheBaseballRules.CreateGameRules();

        var setupPhaseIds = new[] { "WaitingToStart", "CollectingBlinds", "Dealing" };
        foreach (var phaseId in setupPhaseIds)
        {
            var phase = rules.Phases.Single(p => p.PhaseId == phaseId);
            phase.RequiresPlayerAction.Should().BeFalse($"because {phaseId} is a setup phase");
        }
    }

    [Fact]
    public void CreateGameRules_PlayerLimits_AreCorrect()
    {
        var rules = HoldTheBaseballRules.CreateGameRules();

        rules.MinPlayers.Should().Be(2);
        rules.MaxPlayers.Should().Be(10);
    }
}
