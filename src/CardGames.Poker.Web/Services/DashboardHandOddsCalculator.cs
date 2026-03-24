using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Poker.Web.Services;

public static class DashboardHandOddsCalculator
{
    public static OddsCalculator.OddsResult? Calculate(
        string? gameTypeCode,
        IReadOnlyCollection<Card> playerCards,
        IReadOnlyCollection<Card> communityCards,
        IReadOnlyCollection<Card>? deadCards = null,
        IReadOnlyCollection<Card>? faceUpCardsInOrder = null,
        IReadOnlyCollection<OddsCalculator.PairPressurePlayerSnapshot>? pairPressurePlayers = null,
        int? heroSeatIndex = null,
        int? dealerSeatIndex = null)
    {
        if (playerCards.Count == 0)
        {
            return null;
        }

        deadCards ??= Array.Empty<Card>();
        faceUpCardsInOrder ??= Array.Empty<Card>();

        if (string.Equals(gameTypeCode, "TWOSJACKSMANWITHTHEAXE", StringComparison.OrdinalIgnoreCase))
        {
            return OddsCalculator.CalculateTwosJacksManWithTheAxeDrawOdds(playerCards, deadCards);
        }

        if (string.Equals(gameTypeCode, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase))
        {
            return OddsCalculator.CalculateStudOdds(playerCards, Array.Empty<Card>(), deadCards: deadCards);
        }

        if (string.Equals(gameTypeCode, "PAIRPRESSURE", StringComparison.OrdinalIgnoreCase))
        {
            return CalculatePairPressureOdds(
                playerCards,
                deadCards,
                faceUpCardsInOrder,
                pairPressurePlayers,
                heroSeatIndex,
                dealerSeatIndex);
        }

        if (string.Equals(gameTypeCode, "RAZZ", StringComparison.OrdinalIgnoreCase))
        {
            return OddsCalculator.CalculateRazzOdds(playerCards, Array.Empty<Card>(), deadCards: deadCards);
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

        if (string.Equals(gameTypeCode, "NEBRASKA", StringComparison.OrdinalIgnoreCase))
        {
            var holeCards = playerCards.Take(5).ToList();
            if (holeCards.Count < 3)
            {
                return null;
            }

            return OddsCalculator.CalculateNebraskaOdds(holeCards, communityCards, deadCards);
        }

        if (string.Equals(gameTypeCode, "SOUTHDAKOTA", StringComparison.OrdinalIgnoreCase))
        {
            var holeCards = playerCards.Take(5).ToList();
            if (holeCards.Count < 3)
            {
                return null;
            }

            return OddsCalculator.CalculateSouthDakotaOdds(holeCards, communityCards, deadCards);
        }

        if (string.Equals(gameTypeCode, "IRISHHOLDEM", StringComparison.OrdinalIgnoreCase))
        {
            // Pre-discard (4 hole cards): use Omaha-style odds calculation
            // Post-discard (2 hole cards): use Hold'Em-style odds calculation
            if (playerCards.Count >= 4)
            {
                return OddsCalculator.CalculateOmahaOdds(playerCards.Take(4).ToList(), communityCards, deadCards);
            }

            if (playerCards.Count >= 2)
            {
                return OddsCalculator.CalculateHoldemOdds(playerCards.Take(2).ToList(), communityCards, deadCards);
            }

            return null;
        }

        if (string.Equals(gameTypeCode, "GOODBADUGLY", StringComparison.OrdinalIgnoreCase))
        {
            return OddsCalculator.CalculateStudOdds(playerCards, communityCards, totalCardsPerPlayer: 7, deadCards: deadCards);
        }

        return OddsCalculator.CalculateDrawOdds(playerCards, deadCards);
    }

    private static OddsCalculator.OddsResult? CalculatePairPressureOdds(
        IReadOnlyCollection<Card> playerCards,
        IReadOnlyCollection<Card> deadCards,
        IReadOnlyCollection<Card> faceUpCardsInOrder,
        IReadOnlyCollection<OddsCalculator.PairPressurePlayerSnapshot>? pairPressurePlayers,
        int? heroSeatIndex,
        int? dealerSeatIndex)
    {
        var orderedCards = playerCards.ToList();
        if (orderedCards.Count < 2)
        {
            return null;
        }

        if (pairPressurePlayers is { Count: > 0 } && heroSeatIndex.HasValue && dealerSeatIndex.HasValue)
        {
            return OddsCalculator.CalculatePairPressureTableOdds(
                orderedCards,
                heroSeatIndex.Value,
                dealerSeatIndex.Value,
                pairPressurePlayers,
                deadCards: deadCards);
        }

        if (orderedCards.Count >= 7)
        {
            var hand = new PairPressureHand(
                orderedCards.Take(2).ToList(),
                orderedCards.Skip(2).Take(4).ToList(),
                orderedCards[6],
                faceUpCardsInOrder);

            return CreateCertainResult(hand.Type);
        }

        return OddsCalculator.CalculatePairPressureOdds(
            orderedCards.Take(2).ToList(),
            orderedCards.Skip(2).ToList(),
            faceUpCardsInOrder,
            deadCards: deadCards);
    }

    private static OddsCalculator.OddsResult CreateCertainResult(HandType handType)
    {
        return new OddsCalculator.OddsResult
        {
            HandTypeProbabilities = new Dictionary<HandType, decimal>
            {
                [handType] = 1m
            }
        };
    }
}
