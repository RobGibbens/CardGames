using CardGames.Core.French.Cards;
using CardGames.Core.Extensions;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.StudHands;

/// <summary>
/// Represents a Baseball poker hand where 3s and 9s are wild cards.
/// Wild cards can substitute for any card to make the best possible hand,
/// including Five of a Kind (the highest possible hand type).
/// </summary>
public class BaseballHand : HandBase
{
    protected override HandTypeStrengthRanking Ranking => HandTypeStrengthRanking.Classic;

    public IReadOnlyCollection<Card> HoleCards { get; }
    public IReadOnlyCollection<Card> OpenCards { get; }
    public IReadOnlyCollection<Card> DownCards { get; }

    /// <summary>
    /// The symbols that are wild in Baseball (3s and 9s).
    /// </summary>
    private static readonly Symbol[] WildSymbols = { Symbol.Three, Symbol.Nine };

    /// <summary>
    /// All possible card values for substitution.
    /// </summary>
    private static readonly int[] AllValues = Enumerable.Range(2, 13).ToArray();

    /// <summary>
    /// All possible suits for substitution.
    /// </summary>
    private static readonly Suit[] AllSuits = { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades };

    public BaseballHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> openCards,
        IReadOnlyCollection<Card> downCards)
        : base(holeCards.Concat(openCards).Concat(downCards).ToList())
    {
        HoleCards = holeCards;
        OpenCards = openCards;
        DownCards = downCards;
    }

    /// <summary>
    /// Determines if a card is wild (3 or 9).
    /// </summary>
    private static bool IsWild(Card card)
        => WildSymbols.Contains(card.Symbol);

    protected override IEnumerable<IReadOnlyCollection<Card>> PossibleHands()
    {
        // Get all 5-card subsets of the player's cards
        var fiveCardSubsets = Cards.SubsetsOfSize(5);

        foreach (var subset in fiveCardSubsets)
        {
            var cardList = subset.ToList();
            var wildIndices = cardList
                .Select((card, index) => new { card, index })
                .Where(x => IsWild(x.card))
                .Select(x => x.index)
                .ToList();

            if (wildIndices.Count == 0)
            {
                // No wild cards, return the hand as is
                yield return cardList;
            }
            else
            {
                // Generate all possible substitutions for wild cards
                foreach (var substitution in GenerateSubstitutions(cardList, wildIndices))
                {
                    yield return substitution;
                }
            }
        }
    }

    /// <summary>
    /// Generates all possible card substitutions for wild cards in the hand.
    /// </summary>
    private IEnumerable<IReadOnlyCollection<Card>> GenerateSubstitutions(
        List<Card> cards, 
        List<int> wildIndices)
    {
        // Get non-wild cards to know what values are already present
        var nonWildCards = cards
            .Select((card, index) => new { card, index })
            .Where(x => !wildIndices.Contains(x.index))
            .Select(x => x.card)
            .ToList();

        // Generate all possible substitution combinations
        var substitutionOptions = GenerateSubstitutionOptions(nonWildCards);
        var allCombinations = GetCartesianProduct(substitutionOptions, wildIndices.Count);

        foreach (var combination in allCombinations)
        {
            var newCards = new Card[5];
            var substitutionIndex = 0;
            
            for (int i = 0; i < 5; i++)
            {
                if (wildIndices.Contains(i))
                {
                    newCards[i] = combination[substitutionIndex++];
                }
                else
                {
                    newCards[i] = cards[i];
                }
            }

            yield return newCards.ToList();
        }
    }

    /// <summary>
    /// Generates all possible cards that a wild card can represent.
    /// </summary>
    private IEnumerable<Card> GenerateSubstitutionOptions(List<Card> nonWildCards)
    {
        // A wild card can be any card not already in the hand
        // This includes duplicate values of different suits for flushes
        foreach (var suit in AllSuits)
        {
            foreach (var value in AllValues)
            {
                var card = new Card(suit, value);
                yield return card;
            }
        }
    }

    /// <summary>
    /// Gets the Cartesian product of substitution options.
    /// </summary>
    private IEnumerable<Card[]> GetCartesianProduct(IEnumerable<Card> options, int count)
    {
        var optionsList = options.ToList();
        
        if (count == 0)
        {
            yield return Array.Empty<Card>();
            yield break;
        }

        if (count == 1)
        {
            foreach (var option in optionsList)
            {
                yield return new[] { option };
            }
            yield break;
        }

        // For multiple wild cards, generate all combinations
        var indices = new int[count];
        var total = (int)Math.Pow(optionsList.Count, count);

        for (int i = 0; i < total; i++)
        {
            var combination = new Card[count];
            var temp = i;
            for (int j = 0; j < count; j++)
            {
                combination[j] = optionsList[temp % optionsList.Count];
                temp /= optionsList.Count;
            }
            yield return combination;
        }
    }

    protected override HandType DetermineType()
    {
        // Check if Five of a Kind is possible (requires wild cards)
        if (CanMakeFiveOfAKind())
        {
            return HandType.FiveOfAKind;
        }

        return base.DetermineType();
    }

    /// <summary>
    /// Determines if Five of a Kind can be made with the available cards.
    /// </summary>
    private bool CanMakeFiveOfAKind()
    {
        var wildCount = Cards.Count(IsWild);
        if (wildCount == 0) return false;

        // Group non-wild cards by value
        var nonWildCards = Cards.Where(c => !IsWild(c)).ToList();
        var valueGroups = nonWildCards.GroupBy(c => c.Value).ToList();

        // Check if we can make 5 of any rank
        foreach (var group in valueGroups)
        {
            var naturalCount = group.Count();
            // We need natural cards + wild cards >= 5, picking from our total cards
            // We can use all wild cards to match this value
            if (naturalCount + wildCount >= 5)
            {
                return true;
            }
        }

        // Check if all cards are wild
        if (Cards.Count >= 5 && wildCount >= 5)
        {
            return true;
        }

        return false;
    }

    protected override long CalculateStrength()
    {
        if (Type == HandType.FiveOfAKind)
        {
            return CalculateFiveOfAKindStrength();
        }

        return base.CalculateStrength();
    }

    /// <summary>
    /// Calculates the strength of a Five of a Kind hand.
    /// </summary>
    private long CalculateFiveOfAKindStrength()
    {
        var wildCount = Cards.Count(IsWild);
        var nonWildCards = Cards.Where(c => !IsWild(c)).ToList();
        
        int bestValue;
        if (nonWildCards.Count > 0)
        {
            // Find the highest value we can make five of
            var valueGroups = nonWildCards.GroupBy(c => c.Value).ToList();
            bestValue = valueGroups
                .Where(g => g.Count() + wildCount >= 5)
                .Select(g => g.Key)
                .DefaultIfEmpty((int)Symbol.Ace) // Default to Aces if all wilds
                .Max();
        }
        else
        {
            // All cards are wild, make five Aces
            bestValue = (int)Symbol.Ace;
        }

        // Five of a Kind strength = hand type prefix + value * 5 kickers
        var handTypeStrength = HandTypeStrength.ByRanking(Ranking, HandType.FiveOfAKind);
        var prefixMultiplier = 10000000000L;
        
        // All 5 cards are the same value
        var cardStrength = bestValue * 100000000 + bestValue * 1000000 + bestValue * 10000 + bestValue * 100 + bestValue;
        
        return prefixMultiplier * handTypeStrength + cardStrength;
    }
}
