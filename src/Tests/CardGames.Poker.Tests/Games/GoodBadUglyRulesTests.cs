using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.GoodBadUgly;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class GoodBadUglyRulesTests
{
    [Fact]
    public void CreateGameRules_ShouldReturnValidRules()
    {
        var rules = GoodBadUglyRules.CreateGameRules();

        rules.Should().NotBeNull();
        rules.GameTypeCode.Should().Be("GOODBADUGLY");
        rules.GameTypeName.Should().Be("The Good, the Bad, and the Ugly");
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectPlayerLimits()
    {
        var rules = GoodBadUglyRules.CreateGameRules();

        rules.MinPlayers.Should().Be(2);
        rules.MaxPlayers.Should().Be(7);
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectPhaseCount()
    {
        // WaitingToStart, CollectingAntes, ThirdStreet, FourthStreet, RevealTheGood,
        // FifthStreet, RevealTheBad, SixthStreet, RevealTheUgly, SeventhStreet,
        // Showdown, Complete = 12 phases
        var rules = GoodBadUglyRules.CreateGameRules();

        rules.Phases.Should().HaveCount(12);
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectPhaseOrder()
    {
        var rules = GoodBadUglyRules.CreateGameRules();

        rules.Phases[0].PhaseId.Should().Be("WaitingToStart");
        rules.Phases[1].PhaseId.Should().Be("CollectingAntes");
        rules.Phases[2].PhaseId.Should().Be("ThirdStreet");
        rules.Phases[3].PhaseId.Should().Be("FourthStreet");
        rules.Phases[4].PhaseId.Should().Be("RevealTheGood");
        rules.Phases[5].PhaseId.Should().Be("FifthStreet");
        rules.Phases[6].PhaseId.Should().Be("RevealTheBad");
        rules.Phases[7].PhaseId.Should().Be("SixthStreet");
        rules.Phases[8].PhaseId.Should().Be("RevealTheUgly");
        rules.Phases[9].PhaseId.Should().Be("SeventhStreet");
        rules.Phases[10].PhaseId.Should().Be("Showdown");
        rules.Phases[11].PhaseId.Should().Be("Complete");
    }

    [Fact]
    public void CreateGameRules_BettingPhases_ShouldRequirePlayerAction()
    {
        var rules = GoodBadUglyRules.CreateGameRules();

        rules.Phases[2].RequiresPlayerAction.Should().BeTrue();  // ThirdStreet
        rules.Phases[3].RequiresPlayerAction.Should().BeTrue();  // FourthStreet
        rules.Phases[5].RequiresPlayerAction.Should().BeTrue();  // FifthStreet
        rules.Phases[7].RequiresPlayerAction.Should().BeTrue();  // SixthStreet
        rules.Phases[9].RequiresPlayerAction.Should().BeTrue();  // SeventhStreet
    }

    [Fact]
    public void CreateGameRules_RevealPhases_ShouldNotRequirePlayerAction()
    {
        var rules = GoodBadUglyRules.CreateGameRules();

        rules.Phases[4].RequiresPlayerAction.Should().BeFalse();  // RevealTheGood
        rules.Phases[6].RequiresPlayerAction.Should().BeFalse();  // RevealTheBad
        rules.Phases[8].RequiresPlayerAction.Should().BeFalse();  // RevealTheUgly
    }

    [Fact]
    public void CreateGameRules_RevealPhases_ShouldHaveSpecialCategory()
    {
        var rules = GoodBadUglyRules.CreateGameRules();

        rules.Phases[4].Category.Should().Be("Special");  // RevealTheGood
        rules.Phases[6].Category.Should().Be("Special");  // RevealTheBad
        rules.Phases[8].Category.Should().Be("Special");  // RevealTheUgly
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectCardDealingConfig()
    {
        var rules = GoodBadUglyRules.CreateGameRules();

        rules.CardDealing.Should().NotBeNull();
        rules.CardDealing.InitialCards.Should().Be(3);
        rules.CardDealing.InitialVisibility.Should().Be(CardVisibility.Mixed);
        rules.CardDealing.HasCommunityCards.Should().BeFalse();
        rules.CardDealing.DealingRounds.Should().HaveCount(6);
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectBettingConfig()
    {
        var rules = GoodBadUglyRules.CreateGameRules();

        rules.Betting.Should().NotBeNull();
        rules.Betting.HasAntes.Should().BeTrue();
        rules.Betting.HasBlinds.Should().BeFalse();
        rules.Betting.BettingRounds.Should().Be(5);
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectShowdownConfig()
    {
        var rules = GoodBadUglyRules.CreateGameRules();

        rules.Showdown.Should().NotBeNull();
        rules.Showdown.HandRanking.Should().Contain("Wild Cards");
        rules.Showdown.IsHighLow.Should().BeFalse();
    }

    [Fact]
    public void CreateGameRules_ShouldHaveTableCardSpecialRules()
    {
        var rules = GoodBadUglyRules.CreateGameRules();

        rules.SpecialRules.Should().ContainKey("TableCards");
        rules.SpecialRules.Should().ContainKey("TheGood");
        rules.SpecialRules.Should().ContainKey("TheBad");
        rules.SpecialRules.Should().ContainKey("TheUgly");
        rules.SpecialRules.Should().ContainKey("WildCards");
    }

    [Fact]
    public void CreateGameRules_CompletePhaseShouldBeTerminal()
    {
        var rules = GoodBadUglyRules.CreateGameRules();

        var completePhase = rules.Phases[^1];
        completePhase.PhaseId.Should().Be("Complete");
        completePhase.IsTerminal.Should().BeTrue();
    }
}
