using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for Crazy Pineapple.
/// Post-discard (2 hole cards remaining), evaluates identically to Texas Hold 'Em.
/// </summary>
[HandEvaluator("CRAZYPINEAPPLE")]
public sealed class CrazyPineappleHandEvaluator : IHandEvaluator
{
    /// <inheritdoc />
    public bool SupportsPositionalCards => true;

    /// <inheritdoc />
    public bool HasWildCards => false;

    /// <inheritdoc />
    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        var cardList = cards.ToList();
        var holeCards = cardList.Take(2).ToList();
        var communityCards = cardList.Skip(2).ToList();
        return new HoldemHand(holeCards, communityCards);
    }

    /// <inheritdoc />
    public HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards)
    {
        return new HoldemHand(holeCards, boardCards);
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
