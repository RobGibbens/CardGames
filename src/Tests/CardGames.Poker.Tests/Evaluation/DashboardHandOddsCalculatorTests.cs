using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Web.Services;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class DashboardHandOddsCalculatorTests
{
    [Fact]
    public void Calculate_HoldEm_WithFlopPairAlreadyMade_DoesNotReturnHighCard()
    {
        var playerCards = "8c Kh".ToCards();
        var communityCards = "7d Kc Jc".ToCards();

        var result = DashboardHandOddsCalculator.Calculate("HOLDEM", playerCards, communityCards, []);

        result.Should().NotBeNull();
        result!.HandTypeProbabilities.Should().ContainKey(HandType.OnePair);
        result.HandTypeProbabilities.Should().NotContainKey(HandType.HighCard);
    }

    [Fact]
    public void Calculate_GoodBadUgly_WithVisibleCommunityPairAlreadyMade_DoesNotReturnHighCard()
    {
        var playerCards = "8c Kh 2d 3s".ToCards();
        var communityCards = "7d Kc".ToCards();

        var result = DashboardHandOddsCalculator.Calculate("GOODBADUGLY", playerCards, communityCards, []);

        result.Should().NotBeNull();
        result!.HandTypeProbabilities.Should().ContainKey(HandType.OnePair);
        result.HandTypeProbabilities.Should().NotContainKey(HandType.HighCard);
    }

    [Fact]
    public void Calculate_Nebraska_PreFlopWithFiveHoleCards_ReturnsValidOdds()
    {
        var playerCards = "As Ad Ks Kd Qh".ToCards();
        var communityCards = "".ToCards();

        var result = DashboardHandOddsCalculator.Calculate("NEBRASKA", playerCards, communityCards, []);

        result.Should().NotBeNull();
        result!.HandTypeProbabilities.Should().NotBeEmpty();
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
    }
}
