using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.WildCards;

public class WildCardHandEvaluatorTests
{
    [Fact]
    public void Straight_With_WildCards_Should_Not_Create_StraightFlush()
    {
        // Hand: 5c 4d 3s 2c 2d with wilds 2c, 2d
        // This should evaluate to a Straight, NOT a Straight Flush
        var cards = "5c 4d 3s 2c 2d".ToCards();
        var wildCardRules = new WildCardRules(kingRequired: false);
        var wildCards = wildCardRules.DetermineWildCards(cards);

        var (type, strength, evaluatedCards) = WildCardHandEvaluator.EvaluateBestHand(
            cards, wildCards, HandTypeStrengthRanking.Classic);

        // The best possible hand here is a Straight (A-5 or 2-6)
        // We should NOT get a Straight Flush since the natural cards are different suits
        type.Should().NotBe(HandType.StraightFlush);
        
        // Also verify that when a DrawHand is created with the evaluated cards,
        // it doesn't incorrectly evaluate as a straight flush
        var hand = new DrawHand(evaluatedCards);
        hand.Type.Should().NotBe(HandType.StraightFlush);
    }

    [Fact]
    public void Straight_Evaluated_Cards_Should_Have_Mixed_Suits()
    {
        // Hand: 5c 4d 3s 2c 2d with wilds 2c, 2d
        var cards = "5c 4d 3s 2c 2d".ToCards();
        var wildCardRules = new WildCardRules(kingRequired: false);
        var wildCards = wildCardRules.DetermineWildCards(cards);

        var (type, strength, evaluatedCards) = WildCardHandEvaluator.EvaluateBestHand(
            cards, wildCards, HandTypeStrengthRanking.Classic);

        // If it's a straight, the evaluated cards should NOT all be the same suit
        if (type == HandType.Straight)
        {
            var suits = evaluatedCards.Select(c => c.Suit).Distinct().Count();
            suits.Should().BeGreaterThan(1, "Straight should not have all cards of the same suit");
        }
    }

    [Fact]
    public void StraightFlush_Requires_Same_Suit_Natural_Cards()
    {
        // Hand: 5s 4s 3s 2s 2c with wilds 2s, 2c (both are wild as lowest)
        // This CAN be a straight flush since natural cards 5s, 4s, 3s are all spades
        var cards = "5s 4s 3s 2s 2c".ToCards();
        var wildCardRules = new WildCardRules(kingRequired: false);
        var wildCards = wildCardRules.DetermineWildCards(cards);

        var (type, strength, evaluatedCards) = WildCardHandEvaluator.EvaluateBestHand(
            cards, wildCards, HandTypeStrengthRanking.Classic);

        // This hand has natural cards 5s, 4s, 3s which are all spades
        // With 2 wild cards, it can become A-5 straight flush in spades
        // So this SHOULD be a straight flush
        type.Should().Be(HandType.StraightFlush);
    }
}
