using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Hands.StudHands;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.StudHands;

public class StudOrderHelperTests
{
    [Fact]
    public void OrderPlayerCards_ThirdStreet_PlacesHoleCardsBeforeBoardCard()
    {
        var cards = new[]
        {
            new TestCard("ThirdStreet", false, 1, "Board"),
            new TestCard("ThirdStreet", true, 2, "SecondHole"),
            new TestCard("ThirdStreet", true, 1, "FirstHole")
        };

        var ordered = StudOrderHelper.OrderPlayerCards(cards, card => card.Phase, card => card.IsHole, card => card.DealOrder);

        ordered.Select(card => card.Name).Should().Equal("FirstHole", "SecondHole", "Board");
    }

    [Fact]
    public void OrderFaceUpCardsInGlobalDealOrder_UsesSeatRotationAcrossVisibleStreets()
    {
        var seats = new[]
        {
            new TestSeat(0, new[]
            {
                new TestCard("ThirdStreet", false, 3, "Seat0Third"),
                new TestCard("FourthStreet", false, 1, "Seat0Fourth")
            }),
            new TestSeat(1, new[]
            {
                new TestCard("ThirdStreet", false, 3, "Seat1Third"),
                new TestCard("FourthStreet", false, 1, "Seat1Fourth")
            }),
            new TestSeat(2, new[]
            {
                new TestCard("ThirdStreet", false, 3, "Seat2Third")
            })
        };

        var ordered = StudOrderHelper.OrderFaceUpCardsInGlobalDealOrder(
            seats,
            dealerSeatIndex: 1,
            seat => seat.SeatIndex,
            seat => seat.Cards,
            _ => true);

        ordered.Select(card => card.Name).Should().Equal(
            "Seat2Third",
            "Seat0Third",
            "Seat1Third",
            "Seat0Fourth",
            "Seat1Fourth");
    }

    private sealed record TestCard(string Phase, bool IsHole, int DealOrder, string Name);

    private sealed record TestSeat(int SeatIndex, IReadOnlyList<TestCard> Cards);
}