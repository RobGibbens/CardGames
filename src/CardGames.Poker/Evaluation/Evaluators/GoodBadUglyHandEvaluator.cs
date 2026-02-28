using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for "The Good, the Bad, and the Ugly" variant.
/// Uses positional cards (hole cards, board cards, down cards) with wild card support.
/// Wild cards are determined dynamically by "The Good" table card.
/// </summary>
[HandEvaluator("GOODBADUGLY")]
public sealed class GoodBadUglyHandEvaluator : IHandEvaluator
{
    private readonly GoodBadUglyWildCardRules _wildCardRules = new();

    /// <inheritdoc />
    public bool SupportsPositionalCards => true;

    /// <inheritdoc />
    public bool HasWildCards => true;

    /// <summary>
    /// The wild rank determined by "The Good" table card. Set externally during game flow.
    /// </summary>
    public int? WildRank { get; set; }

    /// <inheritdoc />
    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        var cardList = cards.ToList();
        var holeCards = cardList.Take(2).ToList();
        var openCards = cardList.Skip(2).ToList();

        return new GoodBadUglyHand(holeCards, openCards, [], WildRank, _wildCardRules);
    }

    /// <inheritdoc />
    public HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards)
    {
        var combinedHoleCards = holeCards.Concat(downCards).ToList();
        return new GoodBadUglyHand(combinedHoleCards, boardCards.ToList(), [], WildRank, _wildCardRules);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards)
    {
        if (!WildRank.HasValue)
        {
            return [];
        }

        var cardList = cards.ToList();
        var wildIndexes = new List<int>();

        for (var i = 0; i < cardList.Count; i++)
        {
            if (cardList[i].Value == WildRank.Value)
            {
                wildIndexes.Add(i);
            }
        }

        return wildIndexes;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand)
    {
        return hand is GoodBadUglyHand gbuHand
            ? gbuHand.EvaluatedBestCards.ToList()
            : hand.Cards.ToList();
    }
}
