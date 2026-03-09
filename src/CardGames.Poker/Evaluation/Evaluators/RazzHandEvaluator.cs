using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Poker.Evaluation.Evaluators;

[HandEvaluator("RAZZ")]
public sealed class RazzHandEvaluator : IHandEvaluator
{
    public bool SupportsPositionalCards => true;

    public bool HasWildCards => false;

    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        var cardList = cards.ToList();

        var hole = cardList.Take(2).ToList();
        var open = cardList.Skip(2).Take(4).ToList();
        var down = cardList.Skip(6).Take(1).ToList();

        if (hole.Count == 2 && down.Count == 1)
        {
            return new RazzHand(hole, open, down);
        }

        return new RazzHand(hole, open, down);
    }

    public HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards)
    {
        var holeList = holeCards.Take(2).ToList();
        var boardList = boardCards.ToList();
        var downList = downCards.Take(1).ToList();

        return new RazzHand(holeList, boardList, downList);
    }

    public IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards)
    {
        return [];
    }

    public IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand)
    {
        return hand is RazzHand razzHand
            ? razzHand.GetBestLowHand()
            : hand.Cards;
    }
}
