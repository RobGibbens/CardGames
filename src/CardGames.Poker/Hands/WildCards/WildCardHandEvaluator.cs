using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.WildCards;

public static class WildCardHandEvaluator
{
    private static readonly int[] AllValues = Enumerable.Range(2, 13).ToArray();
    private static readonly Suit[] AllSuits = { Suit.Hearts, Suit.Diamonds, Suit.Spades, Suit.Clubs };

    public static (HandType Type, long Strength) EvaluateBestHand(
        IReadOnlyCollection<Card> allCards,
        IReadOnlyCollection<Card> wildCards,
        HandTypeStrengthRanking ranking)
    {
        var fiveCardHands = allCards.SubsetsOfSize(5);

        HandType bestType = HandType.Incomplete;
        long bestStrength = 0;

        foreach (var hand in fiveCardHands)
        {
            var handList = hand.ToList();
            var wildInHand = handList.Where(c => wildCards.Contains(c)).ToList();

            HandType type;
            long strength;

            if (wildInHand.Any())
            {
                (type, strength) = EvaluateWithWildCards(handList, wildInHand, ranking);
            }
            else
            {
                type = HandTypeDetermination.DetermineHandType(handList);
                strength = HandStrength.Calculate(handList, type, ranking);
            }

            if (strength > bestStrength)
            {
                bestType = type;
                bestStrength = strength;
            }
        }

        return (bestType, bestStrength);
    }

    private static (HandType Type, long Strength) EvaluateWithWildCards(
        IReadOnlyCollection<Card> hand,
        IReadOnlyCollection<Card> wildCards,
        HandTypeStrengthRanking ranking)
    {
        var naturalCards = hand.Except(wildCards).ToList();
        var wildCount = wildCards.Count;

        if (wildCount == 5)
        {
            return (HandType.FiveOfAKind, HandStrength.Calculate(
                Enumerable.Repeat(new Card(Suit.Spades, Symbol.Ace), 5).ToList(),
                HandType.FiveOfAKind,
                ranking));
        }

        var possibleSubstitutions = GenerateAllSubstitutions(wildCount);
        
        HandType bestType = HandType.Incomplete;
        long bestStrength = 0;

        foreach (var substitution in possibleSubstitutions)
        {
            var evaluatedHand = naturalCards.Concat(substitution).ToList();
            var type = HandTypeDetermination.DetermineHandType(evaluatedHand);
            var strength = HandStrength.Calculate(evaluatedHand, type, ranking);

            if (strength > bestStrength)
            {
                bestType = type;
                bestStrength = strength;
            }
        }

        return (bestType, bestStrength);
    }

    private static IEnumerable<IEnumerable<Card>> GenerateAllSubstitutions(int count)
    {
        if (count == 0)
        {
            return new[] { Enumerable.Empty<Card>() };
        }

        var allCards = AllValues
            .SelectMany(v => AllSuits.Select(s => new Card(s, v)))
            .ToList();

        return allCards.CartesianPower(count);
    }
}
