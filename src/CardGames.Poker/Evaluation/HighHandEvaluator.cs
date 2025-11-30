using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Evaluation;

/// <summary>
/// Evaluator for standard high-hand poker (best hand wins).
/// </summary>
public sealed class HighHandEvaluator : IHandEvaluator
{
    private readonly HandTypeStrengthRanking _ranking;

    public HighHandEvaluator(HandTypeStrengthRanking ranking = HandTypeStrengthRanking.Classic)
    {
        _ranking = ranking;
    }

    public static HighHandEvaluator Classic => new(HandTypeStrengthRanking.Classic);

    public static HighHandEvaluator ShortDeck => new(HandTypeStrengthRanking.ShortDeck);

    public HandEvaluationResult Evaluate(IReadOnlyCollection<Card> cards)
    {
        if (cards.Count < 5)
        {
            return HandEvaluationResult.Create(HandType.Incomplete, 0, cards);
        }

        var bestHand = FindBestFiveCardHand(cards);
        var type = HandTypeDetermination.DetermineHandType(bestHand);
        var strength = HandStrength.Calculate(bestHand, type, _ranking);

        return HandEvaluationResult.Create(type, strength, bestHand);
    }

    public HandEvaluationResult Evaluate(IReadOnlyCollection<Card> cards, IReadOnlyCollection<Card> wildCards)
    {
        if (cards.Count < 5)
        {
            return HandEvaluationResult.Create(HandType.Incomplete, 0, cards);
        }

        if (!wildCards.Any())
        {
            return Evaluate(cards);
        }

        var (type, strength, evaluatedCards) = WildCardHandEvaluator.EvaluateBestHand(
            cards, wildCards, _ranking);

        return HandEvaluationResult.Create(type, strength, evaluatedCards);
    }

    public int Compare(HandEvaluationResult hand1, HandEvaluationResult hand2)
        => hand1.Strength.CompareTo(hand2.Strength);

    private IReadOnlyCollection<Card> FindBestFiveCardHand(IReadOnlyCollection<Card> cards)
    {
        if (cards.Count == 5)
        {
            return cards;
        }

        var fiveCardHands = cards.SubsetsOfSize(5);
        long bestStrength = 0;
        IReadOnlyCollection<Card> bestHand = cards.Take(5).ToList();

        foreach (var hand in fiveCardHands)
        {
            var handList = hand.ToList();
            var type = HandTypeDetermination.DetermineHandType(handList);
            var strength = HandStrength.Calculate(handList, type, _ranking);

            if (strength > bestStrength)
            {
                bestStrength = strength;
                bestHand = handList;
            }
        }

        return bestHand;
    }
}
