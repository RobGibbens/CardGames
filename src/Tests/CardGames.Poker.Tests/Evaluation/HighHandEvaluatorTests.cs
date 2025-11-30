using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class HighHandEvaluatorTests
{
    private readonly HighHandEvaluator _evaluator = HighHandEvaluator.Classic;

    [Fact]
    public void Evaluate_RoyalFlush_ReturnsCorrectType()
    {
        var cards = "As Ks Qs Js Ts".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.StraightFlush);
        result.WinningCards.Should().HaveCount(5);
    }

    [Fact]
    public void Evaluate_StraightFlush_ReturnsCorrectType()
    {
        var cards = "9h 8h 7h 6h 5h".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.StraightFlush);
    }

    [Fact]
    public void Evaluate_Quads_ReturnsCorrectTypeAndPrimaryCards()
    {
        var cards = "As Ah Ad Ac Kc".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.Quads);
        result.PrimaryCards.Should().HaveCount(4);
        result.Kickers.Should().HaveCount(1);
    }

    [Fact]
    public void Evaluate_FullHouse_ReturnsCorrectType()
    {
        var cards = "As Ah Ad Kc Kd".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.FullHouse);
        result.PrimaryCards.Should().HaveCount(5); // Both trips and pair are primary
    }

    [Fact]
    public void Evaluate_Flush_ReturnsCorrectType()
    {
        var cards = "As 9s 7s 5s 2s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.Flush);
    }

    [Fact]
    public void Evaluate_Straight_ReturnsCorrectType()
    {
        var cards = "9s 8h 7d 6c 5s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.Straight);
    }

    [Fact]
    public void Evaluate_WheelStraight_ReturnsCorrectType()
    {
        var cards = "As 2h 3d 4c 5s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.Straight);
    }

    [Fact]
    public void Evaluate_Trips_ReturnsCorrectTypeAndKickers()
    {
        var cards = "As Ah Ad Kc Qd".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.Trips);
        result.PrimaryCards.Should().HaveCount(3);
        result.Kickers.Should().HaveCount(2);
    }

    [Fact]
    public void Evaluate_TwoPair_ReturnsCorrectTypeAndKickers()
    {
        var cards = "As Ah Kc Kd Qd".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.TwoPair);
        result.PrimaryCards.Should().HaveCount(4);
        result.Kickers.Should().HaveCount(1);
    }

    [Fact]
    public void Evaluate_OnePair_ReturnsCorrectTypeAndKickers()
    {
        var cards = "As Ah Kc Qd Jd".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.OnePair);
        result.PrimaryCards.Should().HaveCount(2);
        result.Kickers.Should().HaveCount(3);
    }

    [Fact]
    public void Evaluate_HighCard_ReturnsCorrectTypeAndAllKickers()
    {
        var cards = "As Kh 9d 7c 5s".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.HighCard);
        result.PrimaryCards.Should().BeEmpty();
        result.Kickers.Should().HaveCount(5);
    }

    [Fact]
    public void Evaluate_SevenCards_FindsBestFiveCardHand()
    {
        // Seven cards that contain a flush (best hand)
        var cards = "As Ks Qs 7s 2s Ah Kh".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.Flush);
        result.WinningCards.Should().HaveCount(5);
    }

    [Fact]
    public void Evaluate_LessThanFiveCards_ReturnsIncomplete()
    {
        var cards = "As Kh 9d 7c".ToCards();

        var result = _evaluator.Evaluate(cards);

        result.Type.Should().Be(HandType.Incomplete);
    }

    [Fact]
    public void Compare_HigherHandWins()
    {
        var flush = _evaluator.Evaluate("As Ks Qs 7s 2s".ToCards());
        var straight = _evaluator.Evaluate("9s 8h 7d 6c 5s".ToCards());

        var comparison = _evaluator.Compare(flush, straight);

        comparison.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compare_SameTypeHigherKickerWins()
    {
        var aceHighFlush = _evaluator.Evaluate("As Ks Qs 7s 2s".ToCards());
        var kingHighFlush = _evaluator.Evaluate("Ks Qs Js 7s 2s".ToCards());

        var comparison = _evaluator.Compare(aceHighFlush, kingHighFlush);

        comparison.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compare_IdenticalHands_ReturnsZero()
    {
        var hand1 = _evaluator.Evaluate("As Ks Qs Js Ts".ToCards());
        var hand2 = _evaluator.Evaluate("As Ks Qs Js Ts".ToCards());

        var comparison = _evaluator.Compare(hand1, hand2);

        comparison.Should().Be(0);
    }

    [Fact]
    public void ShortDeck_FlushBeatsFullHouse()
    {
        var shortDeckEvaluator = HighHandEvaluator.ShortDeck;

        var flush = shortDeckEvaluator.Evaluate("As Ks Qs 7s 6s".ToCards());
        var fullHouse = shortDeckEvaluator.Evaluate("As Ah Ad Kc Kd".ToCards());

        var comparison = shortDeckEvaluator.Compare(flush, fullHouse);

        comparison.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Classic_FullHouseBeatsFlush()
    {
        var flush = _evaluator.Evaluate("As Ks Qs 7s 6s".ToCards());
        var fullHouse = _evaluator.Evaluate("As Ah Ad Kc Kd".ToCards());

        var comparison = _evaluator.Compare(fullHouse, flush);

        comparison.Should().BeGreaterThan(0);
    }
}
