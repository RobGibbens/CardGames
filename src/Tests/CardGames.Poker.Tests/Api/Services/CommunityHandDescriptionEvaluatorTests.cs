using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class CommunityHandDescriptionEvaluatorTests
{
    [Theory]
    [InlineData("HOLDEM")]
    [InlineData("REDRIVER")]
    public void Evaluate_HoldemStyleVariantsWithTwoHoleCards_UsesHoldemRules(string gameTypeCode)
    {
        var holeCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Ace),
            new Card(Suit.Hearts, Symbol.King)
        };
        var communityCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Queen),
            new Card(Suit.Hearts, Symbol.Jack),
            new Card(Suit.Hearts, Symbol.Ten),
            new Card(Suit.Clubs, Symbol.Deuce),
            new Card(Suit.Diamonds, Symbol.Three)
        };

        var description = CommunityHandDescriptionEvaluator.Evaluate(gameTypeCode, holeCards, communityCards);

        description.Should().Be(HandDescriptionFormatter.GetHandDescription(new HoldemHand(holeCards, communityCards)));
    }

    [Theory]
    [InlineData("IRISHHOLDEM")]
    [InlineData("PHILSMOM")]
    [InlineData("CRAZYPINEAPPLE")]
    public void Evaluate_IrishHoldemFamilyWithThreeHoleCards_UsesHoldemRules(string gameTypeCode)
    {
        var holeCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Ace),
            new Card(Suit.Hearts, Symbol.King),
            new Card(Suit.Spades, Symbol.Queen)
        };
        var communityCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Jack),
            new Card(Suit.Hearts, Symbol.Ten),
            new Card(Suit.Clubs, Symbol.Deuce),
            new Card(Suit.Diamonds, Symbol.Three),
            new Card(Suit.Spades, Symbol.Four)
        };

        var description = CommunityHandDescriptionEvaluator.Evaluate(gameTypeCode, holeCards, communityCards);

        description.Should().Be(HandDescriptionFormatter.GetHandDescription(new HoldemHand(holeCards, communityCards)));
    }

    [Theory]
    [InlineData("IRISHHOLDEM")]
    [InlineData("PHILSMOM")]
    [InlineData("CRAZYPINEAPPLE")]
    public void Evaluate_IrishHoldemFamilyWithFourHoleCards_UsesHoldemRules(string gameTypeCode)
    {
        var holeCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Ace),
            new Card(Suit.Hearts, Symbol.King),
            new Card(Suit.Spades, Symbol.Queen),
            new Card(Suit.Clubs, Symbol.Queen)
        };
        var communityCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Jack),
            new Card(Suit.Hearts, Symbol.Ten),
            new Card(Suit.Clubs, Symbol.Deuce),
            new Card(Suit.Diamonds, Symbol.Three),
            new Card(Suit.Spades, Symbol.Four)
        };

        var description = CommunityHandDescriptionEvaluator.Evaluate(gameTypeCode, holeCards, communityCards);

        description.Should().Be(HandDescriptionFormatter.GetHandDescription(new HoldemHand(holeCards, communityCards)));
    }

    [Fact]
    public void Evaluate_HoldTheBaseball_UsesHoldTheBaseballRules()
    {
        var holeCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Ace),
            new Card(Suit.Hearts, Symbol.King)
        };
        var communityCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Queen),
            new Card(Suit.Hearts, Symbol.Jack),
            new Card(Suit.Hearts, Symbol.Ten),
            new Card(Suit.Clubs, Symbol.Deuce),
            new Card(Suit.Diamonds, Symbol.Three)
        };

        var description = CommunityHandDescriptionEvaluator.Evaluate("HOLDTHEBASEBALL", holeCards, communityCards);

        description.Should().Be(HandDescriptionFormatter.GetHandDescription(new HoldTheBaseballHand(holeCards, communityCards)));
    }

    [Fact]
    public void Evaluate_KlondikeWithKnownCard_UsesKlondikeRules()
    {
        var holeCards = new[]
        {
            new Card(Suit.Hearts, Symbol.King),
            new Card(Suit.Spades, Symbol.King)
        };
        var klondikeCard = new Card(Suit.Clubs, Symbol.Deuce);
        var communityCards = new[]
        {
            klondikeCard,
            new Card(Suit.Diamonds, Symbol.King),
            new Card(Suit.Spades, Symbol.Five),
            new Card(Suit.Diamonds, Symbol.Eight),
            new Card(Suit.Hearts, Symbol.Three),
            new Card(Suit.Hearts, Symbol.Four)
        };

        var description = CommunityHandDescriptionEvaluator.Evaluate("KLONDIKE", holeCards, communityCards, klondikeCard);

        description.Should().Be(HandDescriptionFormatter.GetHandDescription(new KlondikeHand(holeCards, communityCards, klondikeCard)));
    }

    [Fact]
    public void Evaluate_Omaha_UsesOmahaRules()
    {
        var holeCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Ace),
            new Card(Suit.Spades, Symbol.Ace),
            new Card(Suit.Clubs, Symbol.King),
            new Card(Suit.Diamonds, Symbol.King)
        };
        var communityCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Six),
            new Card(Suit.Clubs, Symbol.Ten),
            new Card(Suit.Hearts, Symbol.Four),
            new Card(Suit.Diamonds, Symbol.Deuce),
            new Card(Suit.Clubs, Symbol.Six)
        };

        var description = CommunityHandDescriptionEvaluator.Evaluate("OMAHA", holeCards, communityCards);

        description.Should().Be(HandDescriptionFormatter.GetHandDescription(new OmahaHand(holeCards, communityCards)));
    }

    [Theory]
    [InlineData("NEBRASKA")]
    [InlineData("SOUTHDAKOTA")]
    public void Evaluate_NebraskaStyleVariants_UsesNebraskaRules(string gameTypeCode)
    {
        var holeCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Four),
            new Card(Suit.Diamonds, Symbol.Five),
            new Card(Suit.Diamonds, Symbol.Eight),
            new Card(Suit.Clubs, Symbol.Jack),
            new Card(Suit.Hearts, Symbol.Jack)
        };
        var communityCards = new[]
        {
            new Card(Suit.Spades, Symbol.Nine),
            new Card(Suit.Clubs, Symbol.Seven),
            new Card(Suit.Diamonds, Symbol.Jack),
            new Card(Suit.Diamonds, Symbol.Six),
            new Card(Suit.Clubs, Symbol.Three)
        };

        var description = CommunityHandDescriptionEvaluator.Evaluate(gameTypeCode, holeCards, communityCards);

        description.Should().Be(HandDescriptionFormatter.GetHandDescription(new NebraskaHand(holeCards, communityCards)));
    }

    [Fact]
    public void Evaluate_BobBarker_UsesExactlyTwoHoleCardsAndThreeBoardCards()
    {
        var holeCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Queen),
            new Card(Suit.Clubs, Symbol.Queen),
            new Card(Suit.Diamonds, Symbol.King),
            new Card(Suit.Spades, Symbol.King)
        };
        var communityCards = new[]
        {
            new Card(Suit.Spades, Symbol.Ace),
            new Card(Suit.Hearts, Symbol.Six),
            new Card(Suit.Clubs, Symbol.Ten),
            new Card(Suit.Hearts, Symbol.Four),
            new Card(Suit.Diamonds, Symbol.Deuce),
            new Card(Suit.Clubs, Symbol.Six)
        };

        var description = CommunityHandDescriptionEvaluator.Evaluate("BOBBARKER", holeCards, communityCards);

        description.Should().Be(HandDescriptionFormatter.GetHandDescription(new BobBarkerHand(holeCards, communityCards)));
    }

    [Fact]
    public void Evaluate_BobBarkerWithFiveHoleCards_UsesBestLegalFourCardSubset()
    {
        var holeCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Queen),
            new Card(Suit.Clubs, Symbol.Queen),
            new Card(Suit.Diamonds, Symbol.King),
            new Card(Suit.Spades, Symbol.King),
            new Card(Suit.Clubs, Symbol.Deuce)
        };
        var communityCards = new[]
        {
            new Card(Suit.Spades, Symbol.Ace),
            new Card(Suit.Hearts, Symbol.Six),
            new Card(Suit.Clubs, Symbol.Ten),
            new Card(Suit.Hearts, Symbol.Four),
            new Card(Suit.Diamonds, Symbol.Deuce),
            new Card(Suit.Clubs, Symbol.Six)
        };

        var description = CommunityHandDescriptionEvaluator.Evaluate("BOBBARKER", holeCards, communityCards);

        description.Should().Be(
            HandDescriptionFormatter.GetHandDescription(
                new BobBarkerHand(
                    [
                        new Card(Suit.Hearts, Symbol.Queen),
                        new Card(Suit.Clubs, Symbol.Queen),
                        new Card(Suit.Diamonds, Symbol.King),
                        new Card(Suit.Spades, Symbol.King)
                    ],
                    communityCards)));
    }
}