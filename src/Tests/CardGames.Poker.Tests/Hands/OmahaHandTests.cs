using CardGames.Poker.Hands.HandTypes;
using CardGames.Core.French.Cards.Extensions;
using Xunit;
using FluentAssertions;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Tests.Hands;

public class OmahaHandTests
{
    [Theory]
    [InlineData("2s 5d 6d Qh", "8d Js Kc 5c 5h", HandType.Trips)]
    [InlineData("2s 5d 5h Qh", "8d Js Kc 7c 6h", HandType.OnePair)]
    public void Determines_Hand_Type(string holeCards, string boardCards, HandType expectedHandType)
    {
        var hand = new OmahaHand(holeCards.ToCards(), boardCards.ToCards());

        hand.Type.Should().Be(expectedHandType);
    }

    [Theory]
    [InlineData("2s 5d 6d Qh", "8d Js Kc 5c")]
    [InlineData("2s 5d Qd As", "8d Qs Kc Ah")]
    public void Determines_Winner(string holeCardsOne, string holeCardsTwo)
    {
        var board = "Ac Ad Jd 6c 3s".ToCards();
        var handOne = new OmahaHand(holeCardsOne.ToCards(), board);
        var handTwo = new OmahaHand(holeCardsTwo.ToCards(), board);

        (handOne < handTwo).Should().BeTrue();
    }

    [Fact]
    public void Cannot_Use_Board_Straight_Without_Two_Connecting_Hole_Cards()
    {
        // Board has a straight: 6-7-8-9-T
        // Player has no connecting cards
        // In Omaha, player MUST use exactly 2 cards from hand
        // Since no hole cards connect to the straight, player cannot make a straight
        var holeCards = "As 2h 3c 4d".ToCards();
        var board = "6c 7d 8h 9s Tc".ToCards();

        var hand = new OmahaHand(holeCards, board);

        hand.Type.Should().NotBe(HandType.Straight);
        hand.Type.Should().Be(HandType.HighCard);
    }

    [Fact]
    public void Cannot_Use_Board_Flush_When_Hole_Cards_Are_Different_Suit()
    {
        // Board has a flush: 5 hearts
        // Player has all diamonds - no hearts
        // In Omaha, player MUST use exactly 2 cards from hand
        // Since player has no hearts, they cannot make a flush
        var holeCards = "Ad Kd Qd Jd".ToCards();
        var board = "2h 3h 4h 5h 6h".ToCards();

        var hand = new OmahaHand(holeCards, board);

        hand.Type.Should().NotBe(HandType.Flush);
        hand.Type.Should().NotBe(HandType.StraightFlush);
    }

    [Fact]
    public void Cannot_Use_Board_Quads_Must_Use_Only_Three_From_Board()
    {
        // Board has quads: KKKK + 5
        // Player has pair of aces
        // In Omaha, player MUST use exactly 2 from hand + 3 from board
        // So the best hand is KKK (3 from board) + AA (2 from hand) = Full House
        var holeCards = "As Ad 2h 3c".ToCards();
        var board = "Ks Kd Kh Kc 5h".ToCards();

        var hand = new OmahaHand(holeCards, board);

        hand.Type.Should().NotBe(HandType.Quads);
        hand.Type.Should().Be(HandType.FullHouse);
    }

    [Fact]
    public void Can_Make_Flush_With_Two_Hole_Cards_And_Three_Board_Cards()
    {
        // Player has 2 hearts in hand
        // Board has 3 hearts
        // 2 from hand + 3 from board = valid Omaha flush
        var holeCards = "Ah Kh Qc Jc".ToCards();
        var board = "2h 3h 4h 5s 6c".ToCards();

        var hand = new OmahaHand(holeCards, board);

        hand.Type.Should().Be(HandType.Flush);
    }

    [Fact]
    public void Can_Make_Straight_With_Two_Hole_Cards_And_Three_Board_Cards()
    {
        // Player has 9-T in hand
        // Board has 6-7-8
        // 2 from hand (9-T) + 3 from board (6-7-8) = valid Omaha straight
        var holeCards = "9d Tc 2h 3c".ToCards();
        var board = "6c 7d 8h As Ks".ToCards();

        var hand = new OmahaHand(holeCards, board);

        hand.Type.Should().Be(HandType.Straight);
    }

    [Fact]
    public void Four_Hearts_In_Hand_With_Only_Two_On_Board_Is_Not_A_Flush()
    {
        // Player has 4 hearts in hand
        // Board has only 2 hearts
        // In Omaha: 2 hearts from hand + 3 from board (only 2 of which are hearts)
        // = max 4 hearts, which is not a flush
        var holeCards = "Ah Kh Qh Jh".ToCards();
        var board = "2h 3h 4s 5s 6c".ToCards();

        var hand = new OmahaHand(holeCards, board);

        hand.Type.Should().NotBe(HandType.Flush);
    }
}
