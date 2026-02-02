using System;
using System.Linq;
using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Core.French.Decks;
using CardGames.Core.Random;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CardGames.Core.French.Tests.Dealers;

public class FrenchDeckDealerTests
{
    [Fact]
    public void Can_Deal_Specific_Value()
    {
        var dealer = FrenchDeckDealer.WithFullDeck();

        var success = dealer.TryDealCardOfValue(7, out var card);

        success.Should().BeTrue();
        card.Symbol.Should().Be(Symbol.Seven);
    }
    
    [Fact]
    public void Can_Deal_Specific_Symbol()
    {
        var dealer = FrenchDeckDealer.WithFullDeck();

        var success = dealer.TryDealCardOfSymbol(Symbol.Ace, out var card);

        success.Should().BeTrue();
        card.Symbol.Should().Be(Symbol.Ace);
    }
    
    [Fact]
    public void Can_Deal_Specific_Suit()
    {
        var dealer = FrenchDeckDealer.WithFullDeck();

        var success = dealer.TryDealCardOfSuit(Suit.Diamonds, out var card);

        success.Should().BeTrue();
        card.Suit.Should().Be(Suit.Diamonds);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(9)]
    [InlineData(14)]
    public void Can_Deal_Exactly_Four_Cards_Of_Given_Value(int value)
    {
        var dealer = FrenchDeckDealer.WithFullDeck();
        Enumerable
            .Repeat(true, 4)
            .ForEach(__ => dealer.TryDealCardOfValue(value, out _).Should().BeTrue());

        var success = dealer.TryDealCardOfValue(value, out _);

        success.Should().BeFalse();
    }
    
    
    [Theory]
    [InlineData(Suit.Diamonds)]
    [InlineData(Suit.Hearts)]
    public void Can_Deal_Exactly_Nine_Cards_Of_Given_Suit_From_A_Short_Deck(Suit suit)
    {
        var dealer = FrenchDeckDealer.WithShortDeck();
        Enumerable
            .Repeat(true, 9)
            .ForEach(__ => dealer.TryDealCardOfSuit(suit, out _).Should().BeTrue());

        var success = dealer.TryDealCardOfSuit(suit, out _);

        success.Should().BeFalse();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Short_Deck_Dealer_Can_Not_Deal_Cards_Missing_From_Full_Deck(int value)
    {
        var dealer = FrenchDeckDealer.WithShortDeck();

        var success = dealer.TryDealCardOfValue(value, out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void Constructor_With_RandomNumberGenerator_Works()
    {
        var deck = new FullFrenchDeck();
        var rng = Substitute.For<IRandomNumberGenerator>();
        var dealer = new FrenchDeckDealer(deck, rng);

        dealer.Should().NotBeNull();
        dealer.TryDealCardOfValue(2, out _).Should().BeTrue();
    }

    [Fact]
    public void DealSpecific_Deals_Specific_Card()
    {
        var dealer = FrenchDeckDealer.WithFullDeck();
        var cardToDeal = new Card(Suit.Hearts, Symbol.Ace);

        var dealtCard = dealer.DealSpecific(cardToDeal);

        dealtCard.Should().Be(cardToDeal);
    }

    [Fact]
    public void DealSpecific_Throws_If_Already_Dealt()
    {
         var dealer = FrenchDeckDealer.WithFullDeck();
         var cardToDeal = new Card(Suit.Hearts, Symbol.Ace);
         dealer.DealSpecific(cardToDeal);
         
         Action act = () => dealer.DealSpecific(cardToDeal);
         act.Should().Throw<ArgumentException>();
    }
}
