using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.DrawHands;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for Kings and Lows variant.
/// Wild cards: All Kings and all cards with the lowest value in the hand (excluding Kings).
/// </summary>
public sealed class KingsAndLowsHandEvaluator : IHandEvaluator
{
    /// <inheritdoc />
    public bool SupportsPositionalCards => false;

    /// <inheritdoc />
    public bool HasWildCards => true;

    /// <inheritdoc />
    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        return new KingsAndLowsDrawHand(cards);
    }

    /// <inheritdoc />
    public HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards)
    {
        // Draw-style game; combine all cards
        var allCards = holeCards.Concat(boardCards).Concat(downCards).ToList();
        return new KingsAndLowsDrawHand(allCards);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards)
    {
        // Create a temporary hand to determine wild cards
        var hand = new KingsAndLowsDrawHand(cards);
        var wildCards = hand.WildCards;

        var cardList = cards.ToList();
        var wildIndexes = new List<int>();

        for (int i = 0; i < cardList.Count; i++)
        {
            if (wildCards.Contains(cardList[i]))
            {
                wildIndexes.Add(i);
            }
        }

        return wildIndexes;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand)
    {
        if (hand is KingsAndLowsDrawHand kingsAndLowsHand)
        {
            return kingsAndLowsHand.EvaluatedBestCards.ToList();
        }

        return hand.Cards.ToList();
    }
}
