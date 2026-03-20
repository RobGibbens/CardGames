using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for Bob Barker.
/// Evaluates the remaining four active hole cards using Omaha rules: exactly 2 hole cards and 3 community cards.
/// </summary>
[HandEvaluator("BOBBARKER")]
public sealed class BobBarkerHandEvaluator : IHandEvaluator
{
    public bool SupportsPositionalCards => true;

    public bool HasWildCards => false;

    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        var cardList = cards.ToList();
        var holeCards = cardList.Take(4).ToList();
        var communityCards = cardList.Skip(4).ToList();
        return new BobBarkerHand(holeCards, communityCards);
    }

    public HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards)
    {
        return new BobBarkerHand(holeCards, boardCards);
    }

    public IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards)
    {
        return [];
    }

    public IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand)
    {
        return hand.Cards.ToList();
    }
}