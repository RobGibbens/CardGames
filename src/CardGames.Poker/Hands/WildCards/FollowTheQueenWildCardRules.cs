using CardGames.Core.French.Cards;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.WildCards;

/// <summary>
/// Follow the Queen wild card rules.
/// - Queens are always wild.
/// - When a Queen is dealt face up, the next face-up card's rank becomes wild.
/// - If another Queen is dealt face up later, the next card after that Queen
///   becomes the new wild rank (replacing the previous one).
/// - If a Queen is the last face-up card dealt, only Queens are wild.
/// </summary>
public class FollowTheQueenWildCardRules
{
    /// <summary>
    /// Determines the wild cards based on the face-up cards dealt in order.
    /// </summary>
    /// <param name="hand">All cards in the player's hand.</param>
    /// <param name="faceUpCardsInOrder">The face-up cards dealt to all players in the order they were dealt.</param>
    /// <returns>The collection of wild cards in the player's hand.</returns>
    public IReadOnlyCollection<Card> DetermineWildCards(
        IReadOnlyCollection<Card> hand,
        IReadOnlyCollection<Card> faceUpCardsInOrder)
    {
        var wildRanks = DetermineWildRanks(faceUpCardsInOrder);
        return hand.Where(c => wildRanks.Contains(c.Value)).ToList();
    }

    /// <summary>
    /// Determines which card ranks are wild based on the face-up cards dealt.
    /// Queens (value 12) are always wild. Additionally, the rank following the last
    /// Queen dealt face-up is also wild.
    /// </summary>
    /// <param name="faceUpCardsInOrder">The face-up cards dealt in order.</param>
    /// <returns>The set of wild card ranks (values).</returns>
    public IReadOnlyCollection<int> DetermineWildRanks(IReadOnlyCollection<Card> faceUpCardsInOrder)
    {
        var wildRanks = new HashSet<int> { (int)Symbol.Queen }; // Queens are always wild

        int? followingWildRank = null;
        var cardsList = faceUpCardsInOrder.ToList();

        for (int i = 0; i < cardsList.Count; i++)
        {
            if (cardsList[i].Symbol == Symbol.Queen)
            {
                // Check if there is a next card
                if (i + 1 < cardsList.Count)
                {
                    // The next card's rank becomes wild, replacing any previous following wild
                    followingWildRank = cardsList[i + 1].Value;
                }
                else
                {
                    // Queen is the last face-up card, no following wild rank
                    followingWildRank = null;
                }
            }
        }

        if (followingWildRank.HasValue)
        {
            wildRanks.Add(followingWildRank.Value);
        }

        return wildRanks.ToList();
    }
}
