using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Evaluation.Evaluators;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.StudHands;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class HandEvaluatorFactoryTests
{
    private readonly IHandEvaluatorFactory _factory = new HandEvaluatorFactory();

    [Fact]
    public void GetEvaluator_WithFiveCardDrawCode_ReturnsDrawHandEvaluator()
    {
        var evaluator = _factory.GetEvaluator(HandEvaluatorFactory.FiveCardDrawCode);

        evaluator.Should().BeOfType<DrawHandEvaluator>();
        evaluator.HasWildCards.Should().BeFalse();
        evaluator.SupportsPositionalCards.Should().BeFalse();
    }

    [Fact]
    public void GetEvaluator_WithTwosJacksCode_ReturnsTwosJacksEvaluator()
    {
        var evaluator = _factory.GetEvaluator(HandEvaluatorFactory.TwosJacksManWithTheAxeCode);

        evaluator.Should().BeOfType<TwosJacksManWithTheAxeHandEvaluator>();
        evaluator.HasWildCards.Should().BeTrue();
        evaluator.SupportsPositionalCards.Should().BeFalse();
    }

    [Fact]
    public void GetEvaluator_WithKingsAndLowsCode_ReturnsKingsAndLowsEvaluator()
    {
        var evaluator = _factory.GetEvaluator(HandEvaluatorFactory.KingsAndLowsCode);

        evaluator.Should().BeOfType<KingsAndLowsHandEvaluator>();
        evaluator.HasWildCards.Should().BeTrue();
        evaluator.SupportsPositionalCards.Should().BeFalse();
    }

    [Fact]
    public void GetEvaluator_WithSevenCardStudCode_ReturnsSevenCardStudEvaluator()
    {
        var evaluator = _factory.GetEvaluator(HandEvaluatorFactory.SevenCardStudCode);

        evaluator.Should().BeOfType<SevenCardStudHandEvaluator>();
        evaluator.HasWildCards.Should().BeFalse();
        evaluator.SupportsPositionalCards.Should().BeTrue();
    }

    [Fact]
    public void GetEvaluator_WithUnknownCode_ReturnsDefaultDrawEvaluator()
    {
        var evaluator = _factory.GetEvaluator("UNKNOWNGAME");

        evaluator.Should().BeOfType<DrawHandEvaluator>();
    }

    [Fact]
    public void GetEvaluator_WithNullCode_ReturnsDefaultDrawEvaluator()
    {
        var evaluator = _factory.GetEvaluator(null);

        evaluator.Should().BeOfType<DrawHandEvaluator>();
    }

    [Fact]
    public void GetEvaluator_WithEmptyCode_ReturnsDefaultDrawEvaluator()
    {
        var evaluator = _factory.GetEvaluator("");

        evaluator.Should().BeOfType<DrawHandEvaluator>();
    }

    [Fact]
    public void GetEvaluator_IsCaseInsensitive()
    {
        var lowerEvaluator = _factory.GetEvaluator("fivecarddraw");
        var upperEvaluator = _factory.GetEvaluator("FIVECARDDRAW");
        var mixedEvaluator = _factory.GetEvaluator("FiveCardDraw");

        lowerEvaluator.Should().BeOfType<DrawHandEvaluator>();
        upperEvaluator.Should().BeOfType<DrawHandEvaluator>();
        mixedEvaluator.Should().BeOfType<DrawHandEvaluator>();
    }

    [Fact]
    public void TryGetEvaluator_WithKnownCode_ReturnsTrueAndEvaluator()
    {
        var result = _factory.TryGetEvaluator(HandEvaluatorFactory.SevenCardStudCode, out var evaluator);

        result.Should().BeTrue();
        evaluator.Should().BeOfType<SevenCardStudHandEvaluator>();
    }

    [Fact]
    public void TryGetEvaluator_WithUnknownCode_ReturnsFalseAndDefaultEvaluator()
    {
        var result = _factory.TryGetEvaluator("UNKNOWNGAME", out var evaluator);

        result.Should().BeFalse();
        evaluator.Should().BeOfType<DrawHandEvaluator>();
    }

    [Fact]
    public void HasEvaluator_WithKnownCode_ReturnsTrue()
    {
        HandEvaluatorFactory.HasEvaluator(HandEvaluatorFactory.FiveCardDrawCode).Should().BeTrue();
        HandEvaluatorFactory.HasEvaluator(HandEvaluatorFactory.TwosJacksManWithTheAxeCode).Should().BeTrue();
        HandEvaluatorFactory.HasEvaluator(HandEvaluatorFactory.KingsAndLowsCode).Should().BeTrue();
        HandEvaluatorFactory.HasEvaluator(HandEvaluatorFactory.SevenCardStudCode).Should().BeTrue();
    }

    [Fact]
    public void HasEvaluator_WithUnknownCode_ReturnsFalse()
    {
        HandEvaluatorFactory.HasEvaluator("UNKNOWNGAME").Should().BeFalse();
        HandEvaluatorFactory.HasEvaluator(null).Should().BeFalse();
        HandEvaluatorFactory.HasEvaluator("").Should().BeFalse();
    }

    [Fact]
    public void GetRegisteredGameCodes_ReturnsAllRegisteredCodes()
    {
        var codes = HandEvaluatorFactory.GetRegisteredGameCodes().ToList();

        codes.Should().Contain(HandEvaluatorFactory.FiveCardDrawCode);
        codes.Should().Contain(HandEvaluatorFactory.TwosJacksManWithTheAxeCode);
        codes.Should().Contain(HandEvaluatorFactory.KingsAndLowsCode);
        codes.Should().Contain(HandEvaluatorFactory.SevenCardStudCode);
    }
}

public class DrawHandEvaluatorTests
{
    private readonly DrawHandEvaluator _evaluator = new();

    [Fact]
    public void CreateHand_ReturnsDrawHand()
    {
        var cards = "Ah Kh Qh Jh 9c".ToCards();

        var hand = _evaluator.CreateHand(cards);

        hand.Should().BeOfType<DrawHand>();
        hand.Type.Should().Be(HandType.HighCard);
    }

    [Fact]
    public void GetWildCardIndexes_ReturnsEmpty()
    {
        var cards = "2h 2d Jh Kd Ah".ToCards();

        var indexes = _evaluator.GetWildCardIndexes(cards);

        indexes.Should().BeEmpty();
    }

    [Fact]
    public void GetEvaluatedBestCards_ReturnsOriginalCards()
    {
        var cards = "Ah Kh Qh Jh 9c".ToCards();
        var hand = _evaluator.CreateHand(cards);

        var bestCards = _evaluator.GetEvaluatedBestCards(hand);

        bestCards.Should().BeEquivalentTo(cards);
    }
}

public class TwosJacksManWithTheAxeHandEvaluatorTests
{
    private readonly TwosJacksManWithTheAxeHandEvaluator _evaluator = new();

    [Fact]
    public void CreateHand_ReturnsTwosJacksHand()
    {
        var cards = "Ah Kh Qh Jh 9c".ToCards();

        var hand = _evaluator.CreateHand(cards);

        hand.Should().BeOfType<TwosJacksManWithTheAxeDrawHand>();
    }

    [Fact]
    public void GetWildCardIndexes_IdentifiesTwosJacksAndKingOfDiamonds()
    {
        var cards = "2h Jc Kd Ah 9c".ToCards();

        var indexes = _evaluator.GetWildCardIndexes(cards);

        indexes.Should().HaveCount(3);
        indexes.Should().Contain(0); // 2h
        indexes.Should().Contain(1); // Jc
        indexes.Should().Contain(2); // Kd (Man with the Axe)
    }

    [Fact]
    public void GetWildCardIndexes_DoesNotIncludeNonWildCards()
    {
        var cards = "Ah Kh Qh 9c 8c".ToCards();

        var indexes = _evaluator.GetWildCardIndexes(cards);

        indexes.Should().BeEmpty();
    }

    [Fact]
    public void GetEvaluatedBestCards_ReturnsSubstitutedCards()
    {
        var cards = "2h Ah Qh 9c 8c".ToCards();
        var hand = _evaluator.CreateHand(cards);

        var bestCards = _evaluator.GetEvaluatedBestCards(hand);

        // Wild 2h should be substituted to pair with Ace
        bestCards.Should().HaveCount(5);
    }
}

public class KingsAndLowsHandEvaluatorTests
{
    private readonly KingsAndLowsHandEvaluator _evaluator = new();

    [Fact]
    public void CreateHand_ReturnsKingsAndLowsHand()
    {
        var cards = "Kh 5h 5c 9d Tc".ToCards();

        var hand = _evaluator.CreateHand(cards);

        hand.Should().BeOfType<KingsAndLowsDrawHand>();
    }

    [Fact]
    public void GetWildCardIndexes_IdentifiesKingsAndLowestCards()
    {
        var cards = "Kh 3h 3c 9d Tc".ToCards();

        var indexes = _evaluator.GetWildCardIndexes(cards);

        // King is wild, plus all 3s (the lowest value)
        indexes.Should().Contain(0); // Kh
        indexes.Should().Contain(1); // 3h
        indexes.Should().Contain(2); // 3c
    }

    [Fact]
    public void GetEvaluatedBestCards_ReturnsSubstitutedCards()
    {
        var cards = "Kh 5h 5c 9d Tc".ToCards();
        var hand = _evaluator.CreateHand(cards);

        var bestCards = _evaluator.GetEvaluatedBestCards(hand);

        bestCards.Should().HaveCount(5);
    }
}

public class SevenCardStudHandEvaluatorTests
{
    private readonly SevenCardStudHandEvaluator _evaluator = new();

    [Fact]
    public void CreateHand_WithPositionalCards_ReturnsSevenCardStudHand()
    {
        var holeCards = "Ah Kh".ToCards();
        var boardCards = "Qh Jh Th 9h".ToCards();
        var downCards = "8h".ToCards();

        var hand = _evaluator.CreateHand(holeCards, boardCards, downCards);

        hand.Should().BeOfType<SevenCardStudHand>();
    }

    [Fact]
    public void CreateHand_WithSevenCardList_ReturnsStudHand()
    {
        var cards = "Ah Kh Qh Jh Th 9h 8h".ToCards();

        var hand = _evaluator.CreateHand(cards);

        // Should create a valid hand from the card list
        hand.Should().BeAssignableTo<StudHand>();
    }

    [Fact]
    public void GetWildCardIndexes_ReturnsEmpty()
    {
        var cards = "2h 2d Jh Kd Ah Qh 9c".ToCards();

        var indexes = _evaluator.GetWildCardIndexes(cards);

        indexes.Should().BeEmpty();
    }

    [Fact]
    public void GetEvaluatedBestCards_ReturnsBestFiveCardHand()
    {
        var holeCards = "Ah Kh".ToCards();
        var boardCards = "Qh Jh Th 9c".ToCards();
        var downCards = "8c".ToCards();
        var hand = _evaluator.CreateHand(holeCards, boardCards, downCards);

        var bestCards = _evaluator.GetEvaluatedBestCards(hand);

        // Best hand should be the royal flush: Ah Kh Qh Jh Th
        bestCards.Should().HaveCount(5);
    }
}
