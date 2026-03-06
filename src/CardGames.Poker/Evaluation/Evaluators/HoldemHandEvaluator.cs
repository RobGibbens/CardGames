using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for Texas Hold 'Em.
/// Evaluates the best 5-card hand from 2 hole cards and up to 5 community cards.
/// </summary>
[HandEvaluator("HOLDEM")]
public sealed class HoldemHandEvaluator : IHandEvaluator
{
    /// <inheritdoc />
    public bool SupportsPositionalCards => true;

    /// <inheritdoc />
    public bool HasWildCards => false;

    /// <inheritdoc />
    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        // Assume first 2 cards are hole cards, rest are community cards
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
        // Hold 'Em uses hole cards + community (board) cards
        return new HoldemHand(holeCards, boardCards);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards)
    {
        // Hold 'Em has no wild cards
        return [];
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand)
    {
        return hand.Cards.ToList();
    }
}
