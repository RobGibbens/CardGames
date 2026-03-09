using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for Nebraska.
/// Evaluates the best 5-card hand from 5 private cards and up to 5 community cards,
/// with Nebraska rules enforced by <see cref="NebraskaHand"/> (exactly 3 private + 2 community).
/// </summary>
[HandEvaluator("NEBRASKA")]
[HandEvaluator("SOUTHDAKOTA")]
public sealed class NebraskaHandEvaluator : IHandEvaluator
{
    /// <inheritdoc />
    public bool SupportsPositionalCards => true;

    /// <inheritdoc />
    public bool HasWildCards => false;

    /// <inheritdoc />
    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        var cardList = cards.ToList();
        var holeCards = cardList.Take(5).ToList();
        var communityCards = cardList.Skip(5).ToList();
        return new NebraskaHand(holeCards, communityCards);
    }

    /// <inheritdoc />
    public HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards)
    {
        return new NebraskaHand(holeCards, boardCards);
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
