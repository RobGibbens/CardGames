using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.DrawHands;

public class KingsAndLowsDrawHandExamplesTests
{
    [Fact]
    public void Ace_Remains_High_Not_Wild_When_Three_Is_Lowest_And_Best_Is_Four_Aces()
    {
        var cards = "3h 6s Kc Ks As".ToCards();

        var hand = new KingsAndLowsDrawHand(cards);

        hand.Type.Should().Be(HandType.Quads);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Three);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.King && c.Suit == Suit.Clubs);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.King && c.Suit == Suit.Spades);
        hand.WildCards.Should().NotContain(c => c.Symbol == Symbol.Ace);
    }

    [Fact]
    public void Ace_Treated_As_Low_For_Five_To_Ace_Straight_Example()
    {
        var cards = "5h 6d 7c 8s Ac".ToCards();

        var hand = new KingsAndLowsDrawHand(cards);

        hand.Type.Should().Be(HandType.Straight);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Ace);
        hand.WildCards.Should().NotContain(c => c.Symbol == Symbol.Five);
    }

    [Fact]
    public void Ace_Treated_As_High_For_Broadway_Straight_Example()
    {
        var cards = "2s Jd Qh Kc As".ToCards();

        var hand = new KingsAndLowsDrawHand(cards);

        hand.Type.Should().Be(HandType.Straight);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Deuce);
        hand.WildCards.Should().NotContain(c => c.Symbol == Symbol.Ace);
    }
}
