using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for Klondike Hold'em.
/// Evaluates the best 5-card hand from 2 hole cards, up to 5 regular community cards,
/// and 1 Klondike Card (wild). The Klondike Card is the first community card passed in
/// (dealt before the flop, lowest DealOrder) and is always treated as a wild card.
/// </summary>
[HandEvaluator("KLONDIKE")]
public sealed class KlondikeHandEvaluator : IHandEvaluator
{
    /// <inheritdoc />
    public bool SupportsPositionalCards => true;

    /// <inheritdoc />
    public bool HasWildCards => true;

    /// <inheritdoc />
    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        // Assume first 2 cards are hole cards, first community card is the Klondike Card
        var cardList = cards.ToList();
        var holeCards = cardList.Take(2).ToList();
        var communityCards = cardList.Skip(2).ToList();
        var klondikeCard = communityCards.First();
        return new KlondikeHand(holeCards, communityCards, klondikeCard);
    }

    /// <inheritdoc />
    public HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards)
    {
        // Board cards include both regular community cards and the Klondike Card.
        // The Klondike Card is dealt before the flop and has the lowest DealOrder,
        // so it is always the first board card when sorted by DealOrder.
        var boardList = boardCards.ToList();
        var klondikeCard = boardList.First();
        return new KlondikeHand(holeCards, boardList, klondikeCard);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards)
    {
        // The Klondike Card is the first community card (index 2 after 2 hole cards)
        if (cards.Count < 3) return [];
        return [2];
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand)
    {
        return hand is KlondikeHand klondikeHand
            ? klondikeHand.EvaluatedBestCards.ToList()
            : hand.Cards.ToList();
    }
}
