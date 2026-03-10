using System;
using System.Linq;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class RazzOddsCalculatorTests
{
    [Fact]
    public void CalculateRazzOdds_ReturnsValidDistribution()
    {
        var heroHole = "Ah 2d".ToCards();
        var heroBoard = "3c".ToCards();

        var result = OddsCalculator.CalculateRazzOdds(heroHole, heroBoard, simulations: 400);

        result.HandTypeProbabilities.Should().NotBeEmpty();
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.02m);
    }

    [Fact]
    public void CalculateRazzOdds_WithSevenKnownCards_IsDeterministic()
    {
        var heroHole = "Ah 2d".ToCards();
        var heroBoard = "3c 4s 5h Kd Qc".ToCards();

        var result = OddsCalculator.CalculateRazzOdds(heroHole, heroBoard, simulations: 200);

        result.HandTypeProbabilities.Should().ContainSingle();
        result.HandTypeProbabilities.Should().ContainKey(HandType.HighCard);
        result.HandTypeProbabilities[HandType.HighCard].Should().Be(1.0m);
    }

    [Fact]
    public void CalculateRazzOdds_ThrowsWhenTotalCardsPerPlayerLessThanSeven()
    {
        var heroHole = "Ah 2d".ToCards();
        var heroBoard = "3c 4s".ToCards();

        var act = () => OddsCalculator.CalculateRazzOdds(heroHole, heroBoard, totalCardsPerPlayer: 6, simulations: 5);

        act.Should().Throw<ArgumentException>();
    }
}
