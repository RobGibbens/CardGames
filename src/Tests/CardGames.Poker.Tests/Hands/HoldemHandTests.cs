using CardGames.Poker.Hands.HandTypes;
using CardGames.Core.French.Cards.Extensions;
using Xunit;
using FluentAssertions;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Tests.Hands;

public class HoldemHandTests
{
    [Theory]
    [InlineData("2s 5d", "8d Js Kc 5c 5h", HandType.Trips)]
    [InlineData("2s 6d", "5d Js Kc 5c 5h", HandType.Trips)]
    [InlineData("2s 2d", "8d Js Kc 5c 5h", HandType.TwoPair)]
    [InlineData("Qs 2d", "8d Js Qc 5c 5h", HandType.TwoPair)]
    [InlineData("Qs 5d", "8d Js Qc 2c 5h", HandType.TwoPair)]
    [InlineData("2s 2d", "5d Js Kc 5c 5h", HandType.FullHouse)]
    [InlineData("2s 2h", "5d Jh Kh 6h 5h", HandType.Flush)]
    [InlineData("2s 2c", "7h Jh Kh 6h 5h", HandType.Flush)]
    [InlineData("2s 3h", "8d Jh Td Qh 9c", HandType.Straight)]
    [InlineData("2s 3h", "8d Jh 4d Qh 9c", HandType.HighCard)]
    public void Determines_Hand_Type(string holeCards, string boardCards, HandType expectedHandType)
    {
        var hand = new HoldemHand(holeCards.ToCards(), boardCards.ToCards());

        hand.Type.Should().Be(expectedHandType);
    }

    [Fact]
    public void Higher_Pair_Beats_Lower_Pair()
    {
        // This test verifies the bug fix where pair value is correctly prioritized
        // over high card kickers when comparing hands of the same type
        var board = "Qc Td 4s 3c 2h".ToCards();
        
        var pairOfFives = new HoldemHand("5s 5d".ToCards(), board);
        var pairOfThrees = new HoldemHand("7s 3h".ToCards(), board);
        var pairOfDeuces = new HoldemHand("Js 2d".ToCards(), board);
        
        pairOfFives.Type.Should().Be(HandType.OnePair);
        pairOfThrees.Type.Should().Be(HandType.OnePair);
        pairOfDeuces.Type.Should().Be(HandType.OnePair);
        
        // Pair of Fives should beat Pair of Threes
        pairOfFives.Strength.Should().BeGreaterThan(pairOfThrees.Strength);
        
        // Pair of Threes should beat Pair of Deuces
        pairOfThrees.Strength.Should().BeGreaterThan(pairOfDeuces.Strength);
        
        // Pair of Fives should beat Pair of Deuces
        pairOfFives.Strength.Should().BeGreaterThan(pairOfDeuces.Strength);
    }

    [Fact]
    public void Same_Pair_Higher_Kicker_Wins()
    {
        // Both players have pair of Kings, but different kickers
        var board = "Kc 7d 5s 3c 2h".ToCards();
        
        var pairWithAceKicker = new HoldemHand("Kd As".ToCards(), board);
        var pairWithQueenKicker = new HoldemHand("Kh Qs".ToCards(), board);
        
        pairWithAceKicker.Type.Should().Be(HandType.OnePair);
        pairWithQueenKicker.Type.Should().Be(HandType.OnePair);
        
        // Same pair, but Ace kicker beats Queen kicker
        pairWithAceKicker.Strength.Should().BeGreaterThan(pairWithQueenKicker.Strength);
    }

    [Fact]
    public void Two_Pair_Higher_Top_Pair_Wins()
    {
        var board = "Ks 7d 5s 3c 2h".ToCards();
        
        var kingsAndFives = new HoldemHand("Kd 5c".ToCards(), board);
        var kingsAndThrees = new HoldemHand("Kh 3d".ToCards(), board);
        
        kingsAndFives.Type.Should().Be(HandType.TwoPair);
        kingsAndThrees.Type.Should().Be(HandType.TwoPair);
        
        // Kings and Fives beats Kings and Threes (second pair is higher)
        kingsAndFives.Strength.Should().BeGreaterThan(kingsAndThrees.Strength);
    }

    [Fact]
    public void Trips_Higher_Value_Wins()
    {
        var board = "Qs 7s 5s 3c 2h".ToCards();
        
        var tripQueens = new HoldemHand("Qd Qc".ToCards(), board);
        var tripSevens = new HoldemHand("7d 7c".ToCards(), board);
        
        tripQueens.Type.Should().Be(HandType.Trips);
        tripSevens.Type.Should().Be(HandType.Trips);
        
        tripQueens.Strength.Should().BeGreaterThan(tripSevens.Strength);
    }
}
