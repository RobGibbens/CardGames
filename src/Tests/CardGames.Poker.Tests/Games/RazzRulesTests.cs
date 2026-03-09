using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.Razz;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class RazzRulesTests
{
    [Fact]
    public void CreateGameRules_ShouldReturnValidRules()
    {
        var rules = RazzRules.CreateGameRules();

        rules.GameTypeCode.Should().Be("RAZZ");
        rules.GameTypeName.Should().Be("Razz");
    }

    [Fact]
    public void CreateGameRules_ShouldFollowStudStreetSequence()
    {
        var rules = RazzRules.CreateGameRules();

        rules.Phases.Should().HaveCount(9);
        rules.Phases[0].PhaseId.Should().Be("WaitingToStart");
        rules.Phases[1].PhaseId.Should().Be("CollectingAntes");
        rules.Phases[2].PhaseId.Should().Be("ThirdStreet");
        rules.Phases[6].PhaseId.Should().Be("SeventhStreet");
        rules.Phases[7].PhaseId.Should().Be("Showdown");
        rules.Phases[8].PhaseId.Should().Be("Complete");
    }

    [Fact]
    public void CreateGameRules_ShouldEncodeAceToFiveLowballRules()
    {
        var rules = RazzRules.CreateGameRules();

        rules.Showdown.HandRanking.Should().Be("Ace-to-Five Lowball");
        rules.SpecialRules!["AcesLow"].Should().Be(true);
        rules.SpecialRules["StraightsAndFlushesIgnored"].Should().Be(true);
        rules.SpecialRules["HasQualifier"].Should().Be(false);
    }

    [Fact]
    public void CreateGameRules_ShouldUseAnteBringInStructure()
    {
        var rules = RazzRules.CreateGameRules();

        rules.Betting.HasAntes.Should().BeTrue();
        rules.Betting.HasBlinds.Should().BeFalse();
        rules.Betting.Structure.Should().Be("Fixed Limit");
        rules.CardDealing.InitialVisibility.Should().Be(CardVisibility.Mixed);
    }
}
