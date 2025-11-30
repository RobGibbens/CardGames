using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class HiLoHandEvaluatorTests
{
    private readonly HiLoHandEvaluator _evaluator = HiLoHandEvaluator.Classic;

    [Fact]
    public void Evaluate_WheelStraight_WinsHighAndLow()
    {
        var cards = "As 2h 3d 4c 5s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.High.Type.Should().Be(HandType.Straight);
        result.HasQualifyingLow.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_HighOnlyHand_NoQualifyingLow()
    {
        var cards = "As Ah Ad Kc Qd".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.High.Type.Should().Be(HandType.Trips);
        result.HasQualifyingLow.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_FlushWithLow_BothQualify()
    {
        // A spade flush that also qualifies for low
        var cards = "As 2s 3s 4s 7s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.High.Type.Should().Be(HandType.Flush);
        result.HasQualifyingLow.Should().BeTrue();
    }

    [Fact]
    public void Compare_Hand1WinsHighAndLow_Scoops()
    {
        // Hand 1: Better flush and better low
        var hand1 = _evaluator.Evaluate("As 2s 3s 4s 7s".ToCards());
        var hand2 = _evaluator.Evaluate("Ks 5s 6s 8s 9s".ToCards()); // Flush but no low

        var comparison = _evaluator.Compare(hand1, hand2);

        comparison.Hand1WinsHigh.Should().BeTrue();
        comparison.Hand1WinsLow.Should().BeTrue();
        comparison.Hand1Scoops.Should().BeTrue();
    }

    [Fact]
    public void Compare_Hand1WinsHigh_Hand2WinsLow_Split()
    {
        // Hand 1: Better high hand (straight flush)
        // Hand 2: Better low hand
        var hand1 = _evaluator.Evaluate("9s 8s 7s 6s 5s".ToCards()); // Straight flush, no low
        var hand2 = _evaluator.Evaluate("As 2h 3d 4c 8s".ToCards()); // 8-low, no real high

        var comparison = _evaluator.Compare(hand1, hand2);

        comparison.Hand1WinsHigh.Should().BeTrue();
        comparison.Hand2WinsLow.Should().BeTrue();
        comparison.Hand1Scoops.Should().BeFalse();
        comparison.Hand2Scoops.Should().BeFalse();
    }

    [Fact]
    public void Compare_TiedHigh_TiedLow()
    {
        var hand1 = _evaluator.Evaluate("As 2h 3d 4c 5s".ToCards());
        var hand2 = _evaluator.Evaluate("Ac 2d 3h 4s 5c".ToCards());

        var comparison = _evaluator.Compare(hand1, hand2);

        comparison.HighIsTie.Should().BeTrue();
        comparison.LowIsTie.Should().BeTrue();
    }

    [Fact]
    public void Compare_NeitherHasLow_LowIsTie()
    {
        var hand1 = _evaluator.Evaluate("As Ah Ad Kc Qd".ToCards());
        var hand2 = _evaluator.Evaluate("Ks Kh Kd Ac Qd".ToCards());

        var comparison = _evaluator.Compare(hand1, hand2);

        comparison.LowIsTie.Should().BeTrue();
        comparison.Hand1WinsLow.Should().BeFalse();
        comparison.Hand2WinsLow.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_SevenCards_FindsBestHighAndLow()
    {
        // Seven cards that have both flush and low
        var cards = "As 2s 3s 4s 7s Kh Qh".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.High.Type.Should().Be(HandType.Flush);
        result.High.WinningCards.Should().HaveCount(5);
        result.HasQualifyingLow.Should().BeTrue();
        result.Low.WinningCards.Should().HaveCount(5);
    }

    [Fact]
    public void Compare_Hand2Scoops()
    {
        // Hand 1: Weak high, no low (high card, no straight/flush)
        // Hand 2: Better high and has low (has pair of Kings + qualifying low)
        var hand1 = _evaluator.Evaluate("9h 7h 3c 2d 4s".ToCards()); // High card 9, no qualifying low (9 too high)
        var hand2 = _evaluator.Evaluate("As 2h 3d 4c 8s Kh Kd".ToCards()); // Pair of Kings for high, 8-4-3-2-A for low

        var comparison = _evaluator.Compare(hand1, hand2);

        comparison.Hand2WinsHigh.Should().BeTrue();
        comparison.Hand2WinsLow.Should().BeTrue();
        comparison.Hand2Scoops.Should().BeTrue();
    }

    [Fact]
    public void HiLoEvaluationResult_HasQualifyingLow_ReturnsTrueWhenStrengthPositive()
    {
        var cards = "As 2h 3d 4c 5s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.HasQualifyingLow.Should().BeTrue();
        result.Low.Strength.Should().BeGreaterThan(0);
    }

    [Fact]
    public void HiLoEvaluationResult_HasQualifyingLow_ReturnsFalseWhenStrengthZero()
    {
        var cards = "As Ah Ad Kc Qd".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.HasQualifyingLow.Should().BeFalse();
        result.Low.Strength.Should().Be(0);
    }
}
