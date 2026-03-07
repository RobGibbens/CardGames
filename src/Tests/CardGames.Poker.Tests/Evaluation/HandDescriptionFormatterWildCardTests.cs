using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.DrawHands;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class HandDescriptionFormatterWildCardTests
{
    [Fact]
    public void TwosJacksManWithTheAxe_WildCard_Returns_Description_For_Evaluated_Hand()
    {
        // The Deuce is wild. With AA + Q + 9 + wild, best hand becomes trips Aces.
        var hand = new TwosJacksManWithTheAxeDrawHand("Ah Ad Qh 9c 2h".ToCards());

        var description = HandDescriptionFormatter.GetHandDescription(hand);

        description.Should().Be("Three of a kind, Aces");
    }

    [Fact]
    public void HoldTheBaseball_WildCard_Returns_Description_For_Evaluated_Hand()
    {
        var hand = new HoldTheBaseballHand("5s Ad".ToCards(), "Jh 3h Ac".ToCards());

        var description = HandDescriptionFormatter.GetHandDescription(hand);

        description.Should().Be("Three of a kind, Aces");
    }
}
