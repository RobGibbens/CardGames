using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using FluentAssertions;
using Xunit;

namespace CardGames.Core.French.Tests.Cards.Extensions;

public class CardsExtensionsTests
{
    private static readonly Card TwoOfHearts = new(Suit.Hearts, Symbol.Deuce);
    private static readonly Card ThreeOfSpades = new(Suit.Spades, Symbol.Three);
    private static readonly Card TwoOfClubs = new(Suit.Clubs, Symbol.Deuce);
    private static readonly Card AceOfDiamonds = new(Suit.Diamonds, Symbol.Ace);

    [Fact]
    public void Suits_Returns_All_Suits()
    {
        var cards = new[] { TwoOfHearts, ThreeOfSpades, TwoOfClubs };
        cards.Suits().Should().BeEquivalentTo(new[] { Suit.Hearts, Suit.Spades, Suit.Clubs });
    }

    [Fact]
    public void DistinctSuits_Returns_Distinct_Suits()
    {
         var cards = new[] { TwoOfHearts, ThreeOfSpades, TwoOfClubs, new Card(Suit.Hearts, Symbol.Five) };
         cards.DistinctSuits().Should().BeEquivalentTo(new[] { Suit.Hearts, Suit.Spades, Suit.Clubs });
    }

    [Fact]
    public void Values_Returns_All_Values()
    {
        var cards = new[] { TwoOfHearts, ThreeOfSpades, TwoOfClubs };
        cards.Values().Should().BeEquivalentTo(new[] { (int)Symbol.Deuce, (int)Symbol.Three, (int)Symbol.Deuce });
    }

    [Fact]
    public void DistinctValues_Returns_Distinct_Values()
    {
        var cards = new[] { TwoOfHearts, ThreeOfSpades, TwoOfClubs };
        cards.DistinctValues().Should().BeEquivalentTo(new[] { (int)Symbol.Deuce, (int)Symbol.Three });
    }
    
    [Fact]
    public void DescendingValues_Returns_Values_Descending()
    {
        var cards = new[] { TwoOfHearts, ThreeOfSpades, AceOfDiamonds };
        // Ace (14), Three (3), Two (2)
        cards.DescendingValues().Should().ContainInOrder((int)Symbol.Ace, (int)Symbol.Three, (int)Symbol.Deuce);
    }

    [Fact]
    public void DistinctDescendingValues_Returns_Distinct_Values_Descending()
    {
        var cards = new[] { TwoOfHearts, ThreeOfSpades, TwoOfClubs, AceOfDiamonds };
        // Ace (14), Three (3), Deuce (2)
        cards.DistinctDescendingValues().Should().ContainInOrder((int)Symbol.Ace, (int)Symbol.Three, (int)Symbol.Deuce);
    }

    [Fact]
    public void ByDescendingValue_Returns_Cards_Descending()
    {
         var cards = new[] { TwoOfHearts, AceOfDiamonds, ThreeOfSpades };
         var result = cards.ByDescendingValue();
         result.Should().HaveCount(3);
         result.First().Should().Be(AceOfDiamonds);
         result.Last().Should().Be(TwoOfHearts);
    }

    [Fact]
    public void HighestValue_Returns_Max_Value()
    {
        var cards = new[] { TwoOfHearts, AceOfDiamonds, ThreeOfSpades };
        cards.HighestValue().Should().Be((int)Symbol.Ace);
    }

    [Fact]
    public void ValueOfBiggestPair_Returns_Pair_Value()
    {
        var cards = new[] { TwoOfHearts, TwoOfClubs, ThreeOfSpades, AceOfDiamonds };
        cards.ValueOfBiggestPair().Should().Be((int)Symbol.Deuce);
    }

    [Fact]
    public void ValueOfBiggestPair_Returns_Zero_If_No_Pair()
    {
        var cards = new[] { TwoOfHearts, ThreeOfSpades, AceOfDiamonds };
        cards.ValueOfBiggestPair().Should().Be(0);
    }
    
    [Fact]
    public void ValueOfBiggestTrips_Returns_Trips_Value()
    {
        var cards = new[] { TwoOfHearts, TwoOfClubs, new Card(Suit.Diamonds, Symbol.Deuce), AceOfDiamonds };
        cards.ValueOfBiggestTrips().Should().Be((int)Symbol.Deuce);
    }

    [Fact]
    public void ValueOfBiggestTrips_Returns_Zero_If_No_Trips()
    {
        var cards = new[] { TwoOfHearts, TwoOfClubs, AceOfDiamonds };
        cards.ValueOfBiggestTrips().Should().Be(0);
    }

    [Fact]
    public void ValueOfBiggestQuads_Returns_Quads_Value()
    {
        var cards = new[] 
        { 
            TwoOfHearts, 
            TwoOfClubs, 
            new Card(Suit.Diamonds, Symbol.Deuce), 
            new Card(Suit.Spades, Symbol.Deuce),
            AceOfDiamonds 
        };
        cards.ValueOfBiggestQuads().Should().Be((int)Symbol.Deuce);
    }

    [Fact]
    public void ValueOfBiggestQuads_Returns_Zero_If_No_Quads()
    {
        var cards = new[] 
        { 
            TwoOfHearts, 
            TwoOfClubs, 
            new Card(Suit.Diamonds, Symbol.Deuce), 
            AceOfDiamonds 
        };
        
        cards.ValueOfBiggestQuads().Should().Be(0);
    }
    
    [Fact]
    public void ContainsValues_Returns_True_When_Contains()
    {
        var cards = new[] { TwoOfHearts, ThreeOfSpades };
        cards.ContainsValues(new[] { (int)Symbol.Deuce, (int)Symbol.Three }).Should().BeTrue();
    }
    
    [Fact]
    public void ContainsValues_Returns_False_When_Missing()
    {
        var cards = new[] { TwoOfHearts, ThreeOfSpades };
        cards.ContainsValues(new[] { (int)Symbol.Deuce, (int)Symbol.Ace }).Should().BeFalse();
    }

    [Fact]
    public void ContainsValue_Returns_True_When_Contains()
    {
        var cards = new[] { TwoOfHearts };
        cards.ContainsValue((int)Symbol.Deuce).Should().BeTrue();
    }
    
     [Fact]
    public void ContainsValue_Returns_False_When_Missing()
    {
        var cards = new[] { TwoOfHearts };
        cards.ContainsValue((int)Symbol.Ace).Should().BeFalse();
    }

}
