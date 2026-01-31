using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.DrawHands;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for standard draw poker games (e.g., Five Card Draw).
/// Uses standard poker hand rankings without wild cards.
/// </summary>
public sealed class DrawHandEvaluator : IHandEvaluator
{
    /// <inheritdoc />
    public bool SupportsPositionalCards => false;

    /// <inheritdoc />
    public bool HasWildCards => false;

    /// <inheritdoc />
    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        return new DrawHand(cards);
    }

    /// <inheritdoc />
    public HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards)
    {
        // Draw games don't use positional cards; combine all cards
        var allCards = holeCards.Concat(boardCards).Concat(downCards).ToList();
        return new DrawHand(allCards);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards)
    {
        // Standard draw has no wild cards
        return [];
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand)
    {
        // Standard draw hand returns the original cards
        return hand.Cards.ToList();
    }
}
