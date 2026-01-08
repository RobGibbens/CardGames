using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.SevenCardStud;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class SevenCardStudRulesTests
{
    [Fact]
    public void CreateGameRules_ShouldReturnValidRules()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert
        rules.Should().NotBeNull();
        rules.GameTypeCode.Should().Be("SEVENCARDSTUD");
        rules.GameTypeName.Should().Be("Seven Card Stud");
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectPlayerLimits()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert
        rules.MinPlayers.Should().Be(2);
        rules.MaxPlayers.Should().Be(7);
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectPhaseCount()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert
        rules.Phases.Should().HaveCount(9); // WaitingToStart, CollectingAntes, ThirdStreet, FourthStreet, FifthStreet, SixthStreet, SeventhStreet, Showdown, Complete
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectPhaseOrder()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert
        rules.Phases[0].PhaseId.Should().Be("WaitingToStart");
        rules.Phases[1].PhaseId.Should().Be("CollectingAntes");
        rules.Phases[2].PhaseId.Should().Be("ThirdStreet");
        rules.Phases[3].PhaseId.Should().Be("FourthStreet");
        rules.Phases[4].PhaseId.Should().Be("FifthStreet");
        rules.Phases[5].PhaseId.Should().Be("SixthStreet");
        rules.Phases[6].PhaseId.Should().Be("SeventhStreet");
        rules.Phases[7].PhaseId.Should().Be("Showdown");
        rules.Phases[8].PhaseId.Should().Be("Complete");
    }

    [Fact]
    public void CreateGameRules_BettingPhases_ShouldRequirePlayerAction()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert - betting phases should require player action
        rules.Phases[2].RequiresPlayerAction.Should().BeTrue(); // ThirdStreet
        rules.Phases[3].RequiresPlayerAction.Should().BeTrue(); // FourthStreet
        rules.Phases[4].RequiresPlayerAction.Should().BeTrue(); // FifthStreet
        rules.Phases[5].RequiresPlayerAction.Should().BeTrue(); // SixthStreet
        rules.Phases[6].RequiresPlayerAction.Should().BeTrue(); // SeventhStreet
    }

    [Fact]
    public void CreateGameRules_BettingPhases_ShouldHaveCorrectCategory()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert - all street phases should be Betting category
        rules.Phases[2].Category.Should().Be("Betting"); // ThirdStreet
        rules.Phases[3].Category.Should().Be("Betting"); // FourthStreet
        rules.Phases[4].Category.Should().Be("Betting"); // FifthStreet
        rules.Phases[5].Category.Should().Be("Betting"); // SixthStreet
        rules.Phases[6].Category.Should().Be("Betting"); // SeventhStreet
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectBettingConfig()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert
        rules.Betting.HasAntes.Should().BeTrue();
        rules.Betting.HasBlinds.Should().BeFalse();
        rules.Betting.BettingRounds.Should().Be(5); // Third, Fourth, Fifth, Sixth, Seventh streets
        rules.Betting.Structure.Should().Be("Fixed Limit");
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectCardDealingConfig()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert
        rules.CardDealing.InitialCards.Should().Be(3);
        rules.CardDealing.InitialVisibility.Should().Be(CardVisibility.Mixed);
        rules.CardDealing.HasCommunityCards.Should().BeFalse();
        rules.CardDealing.DealingRounds.Should().HaveCount(6);
    }

    [Fact]
    public void CreateGameRules_DealingRounds_ShouldHaveCorrectPattern()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();
        var dealingRounds = rules.CardDealing.DealingRounds!;

        // Assert - Verify the dealing pattern for Seven Card Stud
        dealingRounds[0].CardCount.Should().Be(2);
        dealingRounds[0].Visibility.Should().Be(CardVisibility.FaceDown);
        dealingRounds[0].Target.Should().Be(DealingTarget.Players);

        dealingRounds[1].CardCount.Should().Be(1);
        dealingRounds[1].Visibility.Should().Be(CardVisibility.FaceUp);
        dealingRounds[1].Target.Should().Be(DealingTarget.Players);

        // Fourth, Fifth, Sixth streets - all face up
        for (int i = 2; i <= 4; i++)
        {
            dealingRounds[i].CardCount.Should().Be(1);
            dealingRounds[i].Visibility.Should().Be(CardVisibility.FaceUp);
            dealingRounds[i].Target.Should().Be(DealingTarget.Players);
        }

        // Seventh street - face down
        dealingRounds[5].CardCount.Should().Be(1);
        dealingRounds[5].Visibility.Should().Be(CardVisibility.FaceDown);
        dealingRounds[5].Target.Should().Be(DealingTarget.Players);
    }

    [Fact]
    public void CreateGameRules_ShouldNotAllowDrawing()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert
        rules.Drawing.Should().BeNull(); // Stud games don't have drawing
    }

    [Fact]
    public void CreateGameRules_ShouldHaveCorrectShowdownConfig()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert
        rules.Showdown.HandRanking.Should().Be("Standard Poker (High)");
        rules.Showdown.IsHighLow.Should().BeFalse();
        rules.Showdown.HasSpecialSplitRules.Should().BeFalse();
    }

    [Fact]
    public void CreateGameRules_ShouldHaveSpecialRulesForBringIn()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert
        rules.SpecialRules.Should().NotBeNull();
        rules.SpecialRules.Should().ContainKey("HasBringIn");
        rules.SpecialRules!["HasBringIn"].Should().Be(true);
        rules.SpecialRules.Should().ContainKey("BringInOnLowestCard");
        rules.SpecialRules["BringInOnLowestCard"].Should().Be(true);
    }

    [Fact]
    public void CreateGameRules_ShouldSpecifyMaxPlayerCards()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();

        // Assert
        rules.SpecialRules.Should().ContainKey("MaxPlayerCards");
        rules.SpecialRules!["MaxPlayerCards"].Should().Be(7);
    }

    [Fact]
    public void CompletePhase_ShouldBeTerminal()
    {
        // Act
        var rules = SevenCardStudRules.CreateGameRules();
        var completePhase = rules.Phases[8]; // Complete phase

        // Assert
        completePhase.IsTerminal.Should().BeTrue();
    }
}
