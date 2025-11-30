#nullable enable

using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.HandTypes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Evaluation;

/// <summary>
/// Evaluator for low-hand poker (Ace-to-Five lowball).
/// In Ace-to-Five low:
/// - Ace is always low (value 1)
/// - Straights and flushes don't count against you
/// - Best low hand is A-2-3-4-5 (the wheel)
/// - Pairs and other duplicates count against you (disqualify from low)
/// </summary>
public sealed class LowHandEvaluator : IHandEvaluator
{
    private const int MaxQualifyingHighCard = 8; // For 8-or-better qualification
    private readonly bool _requiresEightOrBetter;

    /// <summary>
    /// Creates a low hand evaluator.
    /// </summary>
    /// <param name="requiresEightOrBetter">
    /// When true, hands must have all cards 8 or lower to qualify (used in most hi/lo games).
    /// When false, any unpaired hand qualifies (used in pure lowball).
    /// </param>
    public LowHandEvaluator(bool requiresEightOrBetter = true)
    {
        _requiresEightOrBetter = requiresEightOrBetter;
    }

    public static LowHandEvaluator EightOrBetter => new(true);

    public static LowHandEvaluator AnyLow => new(false);

    public HandEvaluationResult Evaluate(IReadOnlyCollection<Card> cards)
    {
        if (cards.Count < 5)
        {
            return CreateNoQualifyingLow(cards);
        }

        var bestLow = FindBestLowHand(cards);
        if (bestLow == null)
        {
            return CreateNoQualifyingLow(cards);
        }

        var strength = CalculateLowStrength(bestLow);
        return new HandEvaluationResult(
            HandType.HighCard, // Low hands are always "high card" type - just ranked inversely
            strength,
            bestLow,
            new List<Card>(), // No primary cards for low hands
            bestLow.OrderBy(c => GetLowValue(c)).ToList() // All cards are effectively kickers
        );
    }

    public HandEvaluationResult Evaluate(IReadOnlyCollection<Card> cards, IReadOnlyCollection<Card> wildCards)
    {
        if (cards.Count < 5)
        {
            return CreateNoQualifyingLow(cards);
        }

        // For low evaluation with wild cards, wild cards become the lowest available cards
        var naturalCards = cards.Except(wildCards).ToList();
        var wildCount = wildCards.Count(c => cards.Contains(c));

        var bestLow = FindBestLowHandWithWilds(naturalCards, wildCount);
        if (bestLow == null)
        {
            return CreateNoQualifyingLow(cards);
        }

        var strength = CalculateLowStrength(bestLow);
        return new HandEvaluationResult(
            HandType.HighCard,
            strength,
            bestLow,
            new List<Card>(),
            bestLow.OrderBy(c => GetLowValue(c)).ToList()
        );
    }

    public int Compare(HandEvaluationResult hand1, HandEvaluationResult hand2)
    {
        // For low hands, higher strength means BETTER hand (better low = higher strength)
        // No qualifying low (strength 0) always loses
        if (hand1.Strength == 0 && hand2.Strength == 0) return 0;
        if (hand1.Strength == 0) return -1;
        if (hand2.Strength == 0) return 1;

        // Higher strength is better for low hands
        return hand1.Strength.CompareTo(hand2.Strength);
    }

    private IReadOnlyCollection<Card>? FindBestLowHand(IReadOnlyCollection<Card> cards)
    {
        var fiveCardHands = cards.Count == 5
            ? new[] { cards }
            : cards.SubsetsOfSize(5);

        IReadOnlyCollection<Card>? bestLow = null;
        long bestStrength = 0;

        foreach (var hand in fiveCardHands)
        {
            var handList = hand.ToList();
            if (!QualifiesForLow(handList))
            {
                continue;
            }

            var strength = CalculateLowStrength(handList);
            if (bestLow == null || strength > bestStrength)
            {
                bestStrength = strength;
                bestLow = handList;
            }
        }

        return bestLow;
    }

    private IReadOnlyCollection<Card>? FindBestLowHandWithWilds(
        IReadOnlyCollection<Card> naturalCards,
        int wildCount)
    {
        if (naturalCards.Count + wildCount < 5)
        {
            return null;
        }

        // Try all combinations of natural cards with wilds filling in
        var neededNatural = 5 - wildCount;
        var naturalCombinations = naturalCards.Count >= neededNatural
            ? naturalCards.SubsetsOfSize(neededNatural)
            : new[] { naturalCards };

        IReadOnlyCollection<Card>? bestLow = null;
        long bestStrength = 0;

        foreach (var naturalCombo in naturalCombinations)
        {
            var naturalList = naturalCombo.ToList();
            var usedValues = naturalList.Select(c => GetLowValue(c)).ToHashSet();

            // Check if natural cards have duplicates
            if (usedValues.Count != naturalList.Count)
            {
                continue;
            }

            // Find best values for wild cards (lowest unused values)
            var wildValues = new List<int>();
            for (int v = 1; v <= 14 && wildValues.Count < wildCount; v++)
            {
                if (!usedValues.Contains(v) && (!_requiresEightOrBetter || v <= MaxQualifyingHighCard))
                {
                    wildValues.Add(v);
                    usedValues.Add(v);
                }
            }

            if (wildValues.Count < wildCount)
            {
                continue;
            }

            // Create complete hand with wild card substitutions
            // Wild values are stored as low values (Ace=1), but Card needs standard values (Ace=14)
            var completeHand = naturalList
                .Concat(wildValues.Select(v => CreateCardFromLowValue(v)))
                .ToList();

            if (!QualifiesForLow(completeHand))
            {
                continue;
            }

            var strength = CalculateLowStrength(completeHand);
            if (bestLow == null || strength > bestStrength)
            {
                bestStrength = strength;
                bestLow = completeHand;
            }
        }

        return bestLow;
    }

    private bool QualifiesForLow(IReadOnlyCollection<Card> cards)
    {
        // Must have 5 different values (no pairs)
        var lowValues = cards.Select(c => GetLowValue(c)).ToList();
        if (lowValues.Distinct().Count() != 5)
        {
            return false;
        }

        // For 8-or-better, highest card must be 8 or lower
        if (_requiresEightOrBetter && lowValues.Max() > MaxQualifyingHighCard)
        {
            return false;
        }

        return true;
    }

    private long CalculateLowStrength(IReadOnlyCollection<Card> cards)
    {
        // For low hands, we calculate strength where HIGHER is BETTER
        // (better low = higher strength)
        // A-2-3-4-5 should have highest strength
        // 8-7-6-5-4 should have lowest qualifying strength

        var sortedValues = cards
            .Select(c => GetLowValue(c))
            .OrderBy(v => v)
            .ToList();

        // Calculate inverse strength - wheel (1,2,3,4,5) should be highest
        // We use a base that ensures lower cards give higher scores
        long strength = 0;
        for (int i = 0; i < sortedValues.Count; i++)
        {
            // Higher position = more significant
            // Lower value = better (higher contribution)
            var contribution = (long)Math.Pow(16, 4 - i) * (15 - sortedValues[i]);
            strength += contribution;
        }

        return strength;
    }

    private static int GetLowValue(Card card)
    {
        // Ace is low (value 1) in Ace-to-Five lowball
        return card.Value == 14 ? 1 : card.Value;
    }

    /// <summary>
    /// Creates a card from a low value (where Ace=1).
    /// Converts back to standard card value (where Ace=14).
    /// </summary>
    private static Card CreateCardFromLowValue(int lowValue)
    {
        // Convert low value (Ace=1) back to standard card value (Ace=14)
        var standardValue = lowValue == 1 ? 14 : lowValue;
        return new Card(Suit.Spades, standardValue);
    }

    private static HandEvaluationResult CreateNoQualifyingLow(IReadOnlyCollection<Card> cards)
    {
        return new HandEvaluationResult(
            HandType.Incomplete,
            0, // No qualifying low gets strength 0
            cards,
            new List<Card>(),
            new List<Card>()
        );
    }
}
