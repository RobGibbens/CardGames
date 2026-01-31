using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for Twos, Jacks, Man with the Axe variant.
/// Wild cards: All 2s, all Jacks, and the King of Diamonds (Man with the Axe).
/// </summary>
public sealed class TwosJacksManWithTheAxeHandEvaluator : IHandEvaluator
{
    /// <inheritdoc />
    public bool SupportsPositionalCards => false;

    /// <inheritdoc />
    public bool HasWildCards => true;

    /// <inheritdoc />
    public HandBase CreateHand(IReadOnlyCollection<Card> cards)
    {
        return new TwosJacksManWithTheAxeDrawHand(cards);
    }

    /// <inheritdoc />
    public HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards)
    {
        // Draw-style game; combine all cards
        var allCards = holeCards.Concat(boardCards).Concat(downCards).ToList();
        return new TwosJacksManWithTheAxeDrawHand(allCards);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards)
    {
        var cardList = cards.ToList();
        var wildIndexes = new List<int>();

        for (int i = 0; i < cardList.Count; i++)
        {
            if (TwosJacksManWithTheAxeWildCardRules.IsWild(cardList[i]))
            {
                wildIndexes.Add(i);
            }
        }

        return wildIndexes;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand)
    {
        if (hand is TwosJacksManWithTheAxeDrawHand twosJacksHand)
        {
            return twosJacksHand.EvaluatedBestCards.ToList();
        }

        return hand.Cards.ToList();
    }
}
