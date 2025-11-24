using CardGames.Core.French.Cards;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.WildCards;

/// <summary>
/// Baseball wild card rules: all 3s and 9s are wild.
/// These numbers are chosen because of their significance in the American sport:
/// three strikes, three outs, nine innings.
/// </summary>
public class BaseballWildCardRules
{
    public IReadOnlyCollection<Card> DetermineWildCards(IReadOnlyCollection<Card> hand)
    {
        return hand
            .Where(c => c.Symbol == Symbol.Three || c.Symbol == Symbol.Nine)
            .Distinct()
            .ToList();
    }
}
