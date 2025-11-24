using CardGames.Core.French.Cards;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.WildCards;

public class WildCardRules
{
    public bool KingRequired { get; }

    public WildCardRules(bool kingRequired = false)
    {
        KingRequired = kingRequired;
    }

    public IReadOnlyCollection<Card> DetermineWildCards(IReadOnlyCollection<Card> hand)
    {
        var wildCards = new List<Card>();

        var kings = hand.Where(c => c.Symbol == Symbol.King).ToList();
        wildCards.AddRange(kings);

        var hasKing = kings.Any();
        if (!KingRequired || hasKing)
        {
            var nonKingCards = hand.Where(c => c.Symbol != Symbol.King).ToList();
            if (nonKingCards.Any())
            {
                var minValue = nonKingCards.Min(c => c.Value);
                wildCards.AddRange(hand.Where(c => c.Value == minValue && c.Symbol != Symbol.King));
            }
        }

        return wildCards.Distinct().ToList();
    }
}
