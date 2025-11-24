using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.WildCards;

public class FiveOfAKindTests
{
    [Fact]
    public void Five_Of_A_Kind_Is_Detected()
    {
        var hand = new DrawHand("As Ah Ad Ac As".ToCards());

        hand.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Theory]
    [InlineData("2s 2h 2d 2c 2s")]
    [InlineData("Ks Kh Kd Kc Ks")]
    [InlineData("5s 5h 5d 5c 5s")]
    public void Five_Of_A_Kind_With_Different_Values(string cardString)
    {
        var hand = new DrawHand(cardString.ToCards());

        hand.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Five_Of_A_Kind_Beats_Straight_Flush()
    {
        var fiveOfAKind = new DrawHand("As Ah Ad Ac As".ToCards());
        var straightFlush = new DrawHand("2s 3s 4s 5s 6s".ToCards());

        fiveOfAKind.Strength.Should().BeGreaterThan(straightFlush.Strength);
    }

    [Fact]
    public void Higher_Five_Of_A_Kind_Beats_Lower()
    {
        var fiveAces = new DrawHand("As Ah Ad Ac As".ToCards());
        var fiveKings = new DrawHand("Ks Kh Kd Kc Ks".ToCards());

        fiveAces.Strength.Should().BeGreaterThan(fiveKings.Strength);
    }
}
