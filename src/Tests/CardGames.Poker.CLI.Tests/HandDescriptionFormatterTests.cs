using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.StudHands;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.CLI.Tests;

public class HandDescriptionFormatterTests
{
    [Fact]
    public void GetHandDescription_HighCard_ReturnsCorrectDescription()
    {
        // Arrange - High card Ace
        var hand = new HoldemHand("As Kd".ToCards(), "2c 4h 7s 9d Jc".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        description.Should().Contain("High Card");
        description.Should().Contain("Ace");
    }

    [Fact]
    public void GetHandDescription_OnePair_ReturnsCorrectDescription()
    {
        // Arrange - Pair of Kings
        var hand = new HoldemHand("Ks Kd".ToCards(), "2c 4h 7s 9d Jc".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        description.Should().Be("Pair of Kings");
    }

    [Fact]
    public void GetHandDescription_TwoPair_ReturnsCorrectDescription()
    {
        // Arrange - Two pair: Aces and Kings
        var hand = new HoldemHand("As Ad".ToCards(), "Kc Kh 7s 9d Jc".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        description.Should().Contain("Two Pair");
        description.Should().Contain("Aces");
        description.Should().Contain("Kings");
    }

    [Fact]
    public void GetHandDescription_Trips_ReturnsCorrectDescription()
    {
        // Arrange - Three of a kind: Queens
        var hand = new HoldemHand("Qs Qd".ToCards(), "Qc 4h 7s 9d Jc".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        description.Should().Be("Three of a Kind (Queens)");
    }

    [Fact]
    public void GetHandDescription_Straight_ReturnsCorrectDescription()
    {
        // Arrange - Straight to King
        var hand = new HoldemHand("9s Tc".ToCards(), "Jh Qd Ks 2c 3h".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        description.Should().Contain("Straight");
        description.Should().Contain("King");
    }

    [Fact]
    public void GetHandDescription_Straight_IgnoresHigherCardsNotInStraight()
    {
        // Arrange - Straight 4-5-6-7-8 with higher cards (Jack, Ace) not part of the straight
        // This reproduces the bug where "Ah 5h" with board "Jd 8c 7s 6s 4d" was showing "Ace-high" instead of "Eight-high"
        var hand = new HoldemHand("Ah 5h".ToCards(), "Jd 8c 7s 6s 4d".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert - Should be 8-high straight (4-5-6-7-8), not Ace-high
        description.Should().Be("Straight (Eight-high)");
    }

    [Fact]
    public void GetHandDescription_Straight_IgnoresLowerCardsNotInStraight()
    {
        // Arrange - Straight 9-10-J-Q-K with lower cards (4, 5) not part of the straight
        var hand = new HoldemHand("5d 4s".ToCards(), "Jd Tc Qc Kh 9s".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert - Should be King-high straight (9-T-J-Q-K)
        description.Should().Be("Straight (King-high)");
    }

    [Fact]
    public void GetHandDescription_Flush_ReturnsCorrectDescription()
    {
        // Arrange - Flush in Hearts, Ace high
        var hand = new HoldemHand("Ah 2h".ToCards(), "5h 7h 9h Jc Qd".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        description.Should().Contain("Flush");
        description.Should().Contain("Hearts");
        description.Should().Contain("Ace");
    }

    [Fact]
    public void GetHandDescription_FullHouse_ReturnsCorrectDescription()
    {
        // Arrange - Full house: Tens full of Kings
        var hand = new HoldemHand("Ts Td".ToCards(), "Tc Kh Ks 2d 3c".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        description.Should().Contain("Full House");
        description.Should().Contain("Tens");
        description.Should().Contain("Kings");
    }

    [Fact]
    public void GetHandDescription_Quads_ReturnsCorrectDescription()
    {
        // Arrange - Four of a kind: Jacks
        var hand = new HoldemHand("Js Jd".ToCards(), "Jc Jh 2s 3d 4c".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        description.Should().Be("Four of a Kind (Jacks)");
    }

    [Fact]
    public void GetHandDescription_StraightFlush_ReturnsCorrectDescription()
    {
        // Arrange - Straight flush in Spades, 9-high
        var hand = new HoldemHand("5s 6s".ToCards(), "7s 8s 9s 2d 3c".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        description.Should().Contain("Straight Flush");
        description.Should().Contain("Spades");
    }

    [Fact]
    public void GetHandDescription_RoyalFlush_ReturnsCorrectDescription()
    {
        // Arrange - Royal flush in Hearts
        var hand = new HoldemHand("Ah Kh".ToCards(), "Qh Jh Th 2d 3c".ToCards());
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        description.Should().Contain("Royal Flush");
        description.Should().Contain("Hearts");
    }

    [Fact]
    public void GetHandDescription_StudHand_ReturnsCorrectDescription()
    {
        // Arrange - Full house with SevenCardStudHand
        var holeCards = "Ks Kd".ToCards();
        var openCards = "Kc Qh Qs 7d".ToCards();
        var downCard = "2c".ToCard();
        
        var hand = new SevenCardStudHand(holeCards, openCards, downCard);
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        description.Should().Contain("Full House");
        description.Should().Contain("Kings");
        description.Should().Contain("Queens");
    }

    [Fact]
    public void GetHandDescription_OmahaHand_ReturnsCorrectDescription()
    {
        // Arrange - Trips with OmahaHand (must use exactly 2 hole cards)
        var holeCards = "As Ac Ks Kd".ToCards();
        var communityCards = "Ad 2h 7s 9c Jd".ToCards();
        
        var hand = new OmahaHand(holeCards, communityCards);
        
        // Act
        var description = HandDescriptionFormatter.GetHandDescription(hand);
        
        // Assert
        // With Omaha rules, using As Ac from hole and Ad from board gives trips
        description.Should().Contain("Three of a Kind");
    }

    [Fact]
    public void GetHandDescription_BaseballHand_FullHouseWithWildCards_DoesNotThrow()
    {
        // Arrange - Baseball hand where wild cards create a full house 
        // but the actual cards don't have the expected pair pattern
        // This tests the fix for the "'0' is not a valid card value" error
        var holeCards = "Ah 9d".ToCards();  // 9 is wild in Baseball
        var openCards = "As Kh Qh 5c".ToCards();
        var downCards = "2d".ToCards();

        var hand = new BaseballHand(holeCards, openCards, downCards);
        
        // Act & Assert - Should not throw
        var act = () => HandDescriptionFormatter.GetHandDescription(hand);
        act.Should().NotThrow();
    }

    [Fact]
    public void GetHandDescription_BaseballHand_TripsWithWildCards_DoesNotThrow()
    {
        // Arrange - Baseball hand where wild cards create trips
        var holeCards = "3h Kd".ToCards();  // 3 is wild in Baseball
        var openCards = "Ks 7h 5c 2d".ToCards();
        var downCards = "8s".ToCards();

        var hand = new BaseballHand(holeCards, openCards, downCards);
        
        // Act & Assert - Should not throw
        var act = () => HandDescriptionFormatter.GetHandDescription(hand);
        act.Should().NotThrow();
    }
}
