using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class KingsAndLowsDrawOddsCalculatorTests
{
    [Fact]
    public void CalculateKingsAndLowsDrawOdds_With_King_Increases_Chance_Of_Pair_Or_Better()
    {
        var simulations = 3_000;

        // Compare hand without a wild card (Ace high with mixed cards)
        var nonWild = OddsCalculator.CalculateDrawOdds("Ah 9c 8c".ToCards(), simulations: simulations);
        
        // Compare hand with a King (which is wild in Kings and Lows)
        var withWild = OddsCalculator.CalculateKingsAndLowsDrawOdds("Kh 9c 8c".ToCards(), simulations: simulations);

        var nonWildPairOrBetter = 0m;
        foreach (var (handType, probability) in nonWild.HandTypeProbabilities)
        {
            if (handType != HandType.HighCard)
            {
                nonWildPairOrBetter += probability;
            }
        }

        var withWildPairOrBetter = 0m;
        foreach (var (handType, probability) in withWild.HandTypeProbabilities)
        {
            if (handType != HandType.HighCard)
            {
                withWildPairOrBetter += probability;
            }
        }

        withWildPairOrBetter.Should().BeGreaterThan(nonWildPairOrBetter);
    }

    [Fact]
    public void CalculateKingsAndLowsDrawOdds_Low_Cards_Are_Wild()
    {
        var simulations = 3_000;

        // In Kings and Lows, the lowest card(s) are wild.
        // A hand with a 2 (lowest possible) should get wild card benefits.
        var withLowCard = OddsCalculator.CalculateKingsAndLowsDrawOdds("2h 9c 8c".ToCards(), simulations: simulations);

        var pairOrBetter = 0m;
        foreach (var (handType, probability) in withLowCard.HandTypeProbabilities)
        {
            if (handType != HandType.HighCard)
            {
                pairOrBetter += probability;
            }
        }

        // With a wild card (the 2), we should have a high probability of pair or better
        pairOrBetter.Should().BeGreaterThan(0.5m);
    }

    [Fact]
    public void CalculateKingsAndLowsDrawOdds_Produces_FiveOfAKind_Probability_When_Wild_Cards_Present()
    {
        var simulations = 5_000;

        // With a King (wild) already in hand, there is a non-zero chance to end up with five-of-a-kind.
        var result = OddsCalculator.CalculateKingsAndLowsDrawOdds("Kh".ToCards(), simulations: simulations);

        result.HandTypeProbabilities.ContainsKey(HandType.FiveOfAKind).Should().BeTrue();
        result.HandTypeProbabilities[HandType.FiveOfAKind].Should().BeGreaterThan(0m);
    }

    [Fact]
    public void CalculateKingsAndLowsDrawOdds_Full_Hand_Evaluates_Correctly()
    {
        var simulations = 100;

        // A full hand should evaluate to exactly one hand type with 100% probability
        var result = OddsCalculator.CalculateKingsAndLowsDrawOdds("Kh Kd Kc Ks Ah".ToCards(), simulations: simulations);

        // With 4 Kings (all wild) and an Ace, this should be evaluated as Five of a Kind
        result.HandTypeProbabilities.Should().ContainKey(HandType.FiveOfAKind);
        result.HandTypeProbabilities[HandType.FiveOfAKind].Should().Be(1.0m);
    }
}

