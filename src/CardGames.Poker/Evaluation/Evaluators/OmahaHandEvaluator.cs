using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for Omaha.
/// Evaluates the best 5-card hand from 4 private cards and up to 5 community cards,
/// with Omaha rules enforced by <see cref="OmahaHand"/> (exactly 2 private + 3 community).
/// </summary>
[HandEvaluator("OMAHA")]
public sealed class OmahaHandEvaluator : IHandEvaluator
{
    /// <inheritdoc />
    public bool SupportsPositionalCards => true;

    /// <inheritdoc />
    public bool HasWildCards => false;

    /// <inheritdoc />
    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        var cardList = cards.ToList();
        var holeCards = cardList.Take(4).ToList();
        var communityCards = cardList.Skip(4).ToList();
        return new OmahaHand(holeCards, communityCards);
    }

    /// <inheritdoc />
    public HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards)
    {
        return new OmahaHand(holeCards, boardCards);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards)
    {
        return [];
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand)
    {
        return hand.Cards.ToList();
    }
}
