using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Evaluation;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for Seven Card Stud variant.
/// Uses positional cards (hole cards, board cards, and down card).
/// </summary>
[HandEvaluator("SEVENCARDSTUD")]
public sealed class SevenCardStudHandEvaluator : IHandEvaluator
{
    /// <inheritdoc />
    public bool SupportsPositionalCards => true;

    /// <inheritdoc />
    public bool HasWildCards => false;

    /// <inheritdoc />
    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        // For simple card list, treat first 2 as hole cards, next up to 4 as open, rest as down
        var cardList = cards.ToList();

        if (cardList.Count < 5)
        {
            // Not enough cards; use StudHand with what we have
            var holeCards = cardList.Take(2).ToList();
            var remaining = cardList.Skip(2).ToList();
            return new StudHand(holeCards, remaining, []);
        }

        // Standard 7-card stud: 2 hole + 4 open + 1 down
        var hole = cardList.Take(2).ToList();
        var open = cardList.Skip(2).Take(4).ToList();
        var down = cardList.Skip(6).Take(1).ToList();

        if (hole.Count == 2 && open.Count <= 4 && down.Count == 1)
        {
            return new SevenCardStudHand(hole, open, down[0]);
        }

        return new StudHand(hole, open, down);
    }

    /// <inheritdoc />
    public HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards)
    {
        var holeList = holeCards.ToList();
        var boardList = boardCards.ToList();
        var downList = downCards.ToList();

        // SevenCardStudHand requires exactly 2 initial hole cards and 1 down card
        if (holeList.Count >= 2 && boardList.Count <= 4 && downList.Count >= 1)
        {
            var initialHoleCards = holeList.Take(2).ToList();
            return new SevenCardStudHand(initialHoleCards, boardList, downList[0]);
        }

        // Fall back to base StudHand for incomplete hands
        return new StudHand(holeList, boardList, downList);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards)
    {
        // Standard Seven Card Stud has no wild cards
        return [];
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand)
    {
        if (hand is StudHand studHand)
        {
            return studHand.GetBestHand();
        }

        return hand.Cards.ToList();
    }
}
