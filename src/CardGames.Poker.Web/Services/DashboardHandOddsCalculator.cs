using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;

namespace CardGames.Poker.Web.Services;

public static class DashboardHandOddsCalculator
{
    public static OddsCalculator.OddsResult? Calculate(
        string? gameTypeCode,
        IReadOnlyCollection<Card> playerCards,
        IReadOnlyCollection<Card> communityCards,
        IReadOnlyCollection<Card>? deadCards = null)
    {
        if (playerCards.Count == 0)
        {
            return null;
        }

        deadCards ??= Array.Empty<Card>();

        if (string.Equals(gameTypeCode, "TWOSJACKSMANWITHTHEAXE", StringComparison.OrdinalIgnoreCase))
        {
            return OddsCalculator.CalculateTwosJacksManWithTheAxeDrawOdds(playerCards, deadCards);
        }

        if (string.Equals(gameTypeCode, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase))
        {
            return OddsCalculator.CalculateStudOdds(playerCards, deadCards);
        }

        if (string.Equals(gameTypeCode, "BASEBALL", StringComparison.OrdinalIgnoreCase))
        {
            var holeCards = playerCards.Take(2).ToList();
            var boardCards = playerCards.Skip(2).ToList();
            var totalCards = Math.Max(7, playerCards.Count);
            return OddsCalculator.CalculateBaseballOdds(holeCards, boardCards, totalCards, deadCards);
        }

        if (string.Equals(gameTypeCode, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
        {
            return OddsCalculator.CalculateKingsAndLowsDrawOdds(playerCards, deadCards);
        }

        if (string.Equals(gameTypeCode, "HOLDEM", StringComparison.OrdinalIgnoreCase))
        {
            var holeCards = playerCards.Take(2).ToList();
            if (holeCards.Count < 2)
            {
                return null;
            }

            return OddsCalculator.CalculateHoldemOdds(holeCards, communityCards, deadCards);
        }

        if (string.Equals(gameTypeCode, "OMAHA", StringComparison.OrdinalIgnoreCase))
        {
            var holeCards = playerCards.Take(4).ToList();
            if (holeCards.Count < 4)
            {
                return null;
            }

            return OddsCalculator.CalculateOmahaOdds(holeCards, communityCards, deadCards);
        }

        if (string.Equals(gameTypeCode, "GOODBADUGLY", StringComparison.OrdinalIgnoreCase))
        {
            return OddsCalculator.CalculateStudOdds(playerCards, communityCards, totalCardsPerPlayer: 7, deadCards: deadCards);
        }

        return OddsCalculator.CalculateDrawOdds(playerCards, deadCards);
    }
}
