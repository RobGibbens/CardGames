using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.WildCards;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class HandEvaluatorWildCardTests
{
    private readonly HighHandEvaluator _evaluator = HighHandEvaluator.Classic;

    [Fact]
    public void Evaluate_WithWildCards_CreatesFiveOfAKind()
    {
        // 4 Aces + 1 wild card = Five of a Kind
        var cards = "As Ah Ad Ac Kc".ToCards();
        var wildCards = new[] { cards.First(c => c.Symbol == Symbol.King) };

        var result = _evaluator.Evaluate(cards, wildCards);

        result.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Evaluate_WithWildCards_CompletesRoyalFlush()
    {
        // Missing one card for royal flush, wild completes it
        var cards = "As Ks Qs Ts 2c".ToCards();
        var wildCardRules = new WildCardRules(kingRequired: false);
        var wildCards = wildCardRules.DetermineWildCards(cards);

        var result = _evaluator.Evaluate(cards, wildCards);

        result.Type.Should().Be(HandType.StraightFlush);
    }

    [Fact]
    public void Evaluate_WithWildCards_TurnsTripsIntoQuads()
    {
        var cards = "As Ah Ad 2c 3d".ToCards();
        var wildCardRules = new WildCardRules(kingRequired: false);
        var wildCards = wildCardRules.DetermineWildCards(cards);

        var result = _evaluator.Evaluate(cards, wildCards);

        result.Type.Should().Be(HandType.Quads);
    }

    [Fact]
    public void Evaluate_WithMultipleWildCards_CreatesBestPossibleHand()
    {
        // With 2 wild cards (the 2s), we should get five of a kind
        var cards = "As Ah Ad 2c 2d".ToCards();
        var wildCardRules = new WildCardRules(kingRequired: false);
        var wildCards = wildCardRules.DetermineWildCards(cards);

        var result = _evaluator.Evaluate(cards, wildCards);

        result.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Evaluate_WithKingAsWild_KingBecomesWild()
    {
        var cards = "As Ah Ad Kc 5d".ToCards();
        var wildCardRules = new WildCardRules(kingRequired: false);
        var wildCards = wildCardRules.DetermineWildCards(cards);

        // King and 5 should both be wild
        wildCards.Should().HaveCount(2);

        var result = _evaluator.Evaluate(cards, wildCards);

        // With 2 wild cards and 3 Aces, we get Five of a Kind
        result.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Evaluate_EmptyWildCards_EvaluatesNormally()
    {
        var cards = "As Ah Ad Kc Qd".ToCards();
        var emptyWildCards = Enumerable.Empty<Card>().ToList();

        var result = _evaluator.Evaluate(cards, emptyWildCards);

        result.Type.Should().Be(HandType.Trips);
    }

    [Fact]
    public void Evaluate_WithWildCards_StraightUsesWildsForMissingCards()
    {
        // 5-4-3 with 2 wilds should make a straight
        var cards = "5c 4d 3s 2c 2d".ToCards();
        var wildCardRules = new WildCardRules(kingRequired: false);
        var wildCards = wildCardRules.DetermineWildCards(cards);

        var result = _evaluator.Evaluate(cards, wildCards);

        // Best hand could be a straight (A-5 or 2-6 or higher)
        result.Type.Should().BeOneOf(HandType.Straight, HandType.StraightFlush);
    }

    [Fact]
    public void Evaluate_SevenCardsWithWilds_FindsBestHand()
    {
        var cards = "As Ah Kc Kd 5s 2c 2d".ToCards();
        var wildCardRules = new WildCardRules(kingRequired: false);
        var wildCards = wildCardRules.DetermineWildCards(cards);

        var result = _evaluator.Evaluate(cards, wildCards);

        // With Kings and 2s being wild, should make five of a kind with Aces
        result.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Compare_WildCardHandBeatsSameTypeNaturalHand()
    {
        // Natural quads
        var naturalQuads = _evaluator.Evaluate("Ks Kh Kd Kc Qd".ToCards());

        // Quads made with wild card (but Aces)
        var cards = "As Ah Ad 2c 3d".ToCards();
        var wildCardRules = new WildCardRules(kingRequired: false);
        var wildCards = wildCardRules.DetermineWildCards(cards);
        var wildCardQuads = _evaluator.Evaluate(cards, wildCards);

        // Ace quads beat King quads
        var comparison = _evaluator.Compare(wildCardQuads, naturalQuads);

        comparison.Should().BeGreaterThan(0);
    }
}
