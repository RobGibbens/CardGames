using CardGames.Core.French.Cards;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.WildCards;

/// <summary>
/// Wild card rules for Twos, Jacks, Man with the Axe:
/// - All 2s are wild
/// - All Jacks are wild  
/// - The King of Diamonds ("Man with the Axe") is wild
/// </summary>
public class TwosJacksManWithTheAxeWildCardRules
{
    /// <summary>
    /// Determines which cards in the hand are wild cards.
    /// </summary>
    /// <param name="hand">The cards to evaluate.</param>
    /// <returns>Collection of cards that are wild.</returns>
    public IReadOnlyCollection<Card> DetermineWildCards(IReadOnlyCollection<Card> hand)
    {
        return hand
            .Where(c => IsWild(c))
            .ToList();
    }

    /// <summary>
    /// Determines if a specific card is wild in this variant.
    /// </summary>
    /// <param name="card">The card to check.</param>
    /// <returns>True if the card is wild.</returns>
    public static bool IsWild(Card card)
    {
        // All 2s are wild
        if (card.Symbol == Symbol.Deuce)
        {
            return true;
        }

        // All Jacks are wild
        if (card.Symbol == Symbol.Jack)
        {
            return true;
        }

        // King of Diamonds ("Man with the Axe") is wild
        if (card.Symbol == Symbol.King && card.Suit == Suit.Diamonds)
        {
            return true;
        }

        return false;
    }
}
