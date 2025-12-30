using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class TwosJacksManWithTheAxeOddsCalculatorTests
{
    [Fact]
    public void CalculateTwosJacksManWithTheAxeDrawOdds_With_One_Wild_Card_Increases_Chance_Of_Pair_Or_Better()
    {
        var simulations = 3_000;

        var nonWild = OddsCalculator.CalculateDrawOdds("Ah 9c 8c".ToCards(), simulations: simulations);
        var withWild = OddsCalculator.CalculateTwosJacksManWithTheAxeDrawOdds("2h 9c 8c".ToCards(), simulations: simulations);

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
    public void CalculateTwosJacksManWithTheAxeDrawOdds_Produces_FiveOfAKind_Probability_When_Wild_Cards_Present()
    {
        var simulations = 5_000;

        // With a wild already in hand, there is a non-zero chance to end up with five-of-a-kind.
        var result = OddsCalculator.CalculateTwosJacksManWithTheAxeDrawOdds("2h".ToCards(), simulations: simulations);

        result.HandTypeProbabilities.ContainsKey(HandType.FiveOfAKind).Should().BeTrue();
        result.HandTypeProbabilities[HandType.FiveOfAKind].Should().BeGreaterThan(0m);
    }
}
