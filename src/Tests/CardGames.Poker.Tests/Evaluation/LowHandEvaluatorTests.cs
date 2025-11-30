using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class LowHandEvaluatorTests
{
    private readonly LowHandEvaluator _evaluator = LowHandEvaluator.EightOrBetter;

    [Fact]
    public void Evaluate_Wheel_ReturnsQualifyingLow()
    {
        var cards = "As 2h 3d 4c 5s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Strength.Should().BeGreaterThan(0);
        result.WinningCards.Should().HaveCount(5);
    }

    [Fact]
    public void Evaluate_EightLow_ReturnsQualifyingLow()
    {
        var cards = "As 2h 3d 4c 8s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Strength.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Evaluate_NineLow_DoesNotQualify_EightOrBetter()
    {
        var cards = "As 2h 3d 4c 9s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Strength.Should().Be(0);
        result.Type.Should().Be(HandType.Incomplete);
    }

    [Fact]
    public void Evaluate_PairedHand_DoesNotQualifyForLow()
    {
        var cards = "As Ah 3d 4c 5s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Strength.Should().Be(0);
        result.Type.Should().Be(HandType.Incomplete);
    }

    [Fact]
    public void Evaluate_WheelBeatsSixLow()
    {
        var wheel = _evaluator.Evaluate("As 2h 3d 4c 5s".ToCards());
        var sixLow = _evaluator.Evaluate("As 2h 3d 4c 6s".ToCards());

        var comparison = _evaluator.Compare(wheel, sixLow);

        comparison.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Evaluate_SixLowBeatsSevenLow()
    {
        var sixLow = _evaluator.Evaluate("As 2h 3d 4c 6s".ToCards());
        var sevenLow = _evaluator.Evaluate("As 2h 3d 4c 7s".ToCards());

        var comparison = _evaluator.Compare(sixLow, sevenLow);

        comparison.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Evaluate_BetterSecondCardWins()
    {
        // 6-4-3-2-A beats 6-5-3-2-A
        var better64 = _evaluator.Evaluate("As 2h 3d 4c 6s".ToCards());
        var worse65 = _evaluator.Evaluate("As 2h 3d 5c 6s".ToCards());

        var comparison = _evaluator.Compare(better64, worse65);

        comparison.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compare_NoQualifyingLow_AlwaysLoses()
    {
        var qualifyingLow = _evaluator.Evaluate("As 2h 3d 4c 8s".ToCards());
        var noQualifyingLow = _evaluator.Evaluate("As 2h 3d 4c 9s".ToCards());

        var comparison = _evaluator.Compare(noQualifyingLow, qualifyingLow);

        comparison.Should().BeLessThan(0);
    }

    [Fact]
    public void Compare_BothNoQualifyingLow_ReturnsTie()
    {
        var hand1 = _evaluator.Evaluate("As 2h 3d 4c 9s".ToCards());
        var hand2 = _evaluator.Evaluate("Ks Qh Jd Tc 9s".ToCards());

        var comparison = _evaluator.Compare(hand1, hand2);

        comparison.Should().Be(0);
    }

    [Fact]
    public void AnyLowEvaluator_NineLowQualifies()
    {
        var anyLowEvaluator = LowHandEvaluator.AnyLow;
        var cards = "As 2h 3d 4c 9s".ToCards();

        var result = anyLowEvaluator.Evaluate(cards);

        result.Strength.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Evaluate_SevenCards_FindsBestLowHand()
    {
        // Seven cards that contain a wheel (best low)
        var cards = "As 2h 3d 4c 5s Kh Qh".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Strength.Should().BeGreaterThan(0);
        result.WinningCards.Should().HaveCount(5);
    }

    [Fact]
    public void Evaluate_SevenCards_IgnoresHighCards()
    {
        // Seven cards where the best low is 8-5-4-2-A
        var cards = "As 2h 4d 5c 8s Kh Qh".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Strength.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compare_IdenticalLows_ReturnsTie()
    {
        var hand1 = _evaluator.Evaluate("As 2h 3d 4c 5s".ToCards());
        var hand2 = _evaluator.Evaluate("Ac 2d 3h 4s 5c".ToCards());

        var comparison = _evaluator.Compare(hand1, hand2);

        comparison.Should().Be(0);
    }

    [Fact]
    public void Evaluate_FlushesAndStraightsDontCount()
    {
        // This is a flush AND a straight, but still a valid low hand
        var cards = "As 2s 3s 4s 5s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Strength.Should().BeGreaterThan(0);
    }
}
