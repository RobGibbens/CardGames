using CardGames.Core.French.Cards;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.WildCards;

/// <summary>
/// Wild card rules for "The Good, the Bad, and the Ugly".
/// Cards matching "The Good" table card's rank are wild.
/// </summary>
public class GoodBadUglyWildCardRules
{
    /// <summary>
    /// Determines the wild cards in a player's hand based on The Good card's rank.
    /// </summary>
    /// <param name="hand">All cards in the player's hand.</param>
    /// <param name="wildRank">The rank (value) of "The Good" table card, or null if not yet revealed.</param>
    /// <returns>The collection of wild cards in the player's hand.</returns>
    public IReadOnlyCollection<Card> DetermineWildCards(
        IReadOnlyCollection<Card> hand,
        int? wildRank)
    {
        if (!wildRank.HasValue)
        {
            return new List<Card>();
        }

        return hand.Where(c => c.Value == wildRank.Value).ToList();
    }
}
