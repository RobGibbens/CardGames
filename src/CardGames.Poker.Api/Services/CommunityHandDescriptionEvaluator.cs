using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Api.Services;

internal static class CommunityHandDescriptionEvaluator
{
    public static string? Evaluate(
        string? gameTypeCode,
        IReadOnlyCollection<Card> playerCards,
        IReadOnlyCollection<Card> communityCards,
        Card? klondikeCard = null)
    {
        if (playerCards.Count == 0)
        {
            return null;
        }

        if (IsHoldTheBaseballGame(gameTypeCode) && playerCards.Count == 2)
        {
            return HandDescriptionFormatter.GetHandDescription(new HoldTheBaseballHand(playerCards, communityCards));
        }

        if (IsKlondikeGame(gameTypeCode) && playerCards.Count == 2)
        {
            var hand = klondikeCard is null
                ? new KlondikeHand(playerCards, communityCards)
                : new KlondikeHand(playerCards, communityCards, klondikeCard);

            return HandDescriptionFormatter.GetHandDescription(hand);
        }

        if (playerCards.Count == 2 && IsHoldEmGame(gameTypeCode))
        {
            return HandDescriptionFormatter.GetHandDescription(new HoldemHand(playerCards, communityCards));
        }

        if (playerCards.Count >= 2 && playerCards.Count <= 4 && IsIrishHoldEmFamilyGame(gameTypeCode))
        {
            return HandDescriptionFormatter.GetHandDescription(new HoldemHand(playerCards, communityCards));
        }

        if (playerCards.Count == 4 && IsBobBarkerGame(gameTypeCode))
        {
            return HandDescriptionFormatter.GetHandDescription(new BobBarkerHand(playerCards, communityCards));
        }

        if (playerCards.Count == 5 && IsBobBarkerGame(gameTypeCode))
        {
            BobBarkerHand? bestHand = null;

            foreach (var candidateCards in playerCards.SubsetsOfSize(4))
            {
                var candidateHand = new BobBarkerHand(candidateCards.ToList(), communityCards);
                if (bestHand is null || candidateHand > bestHand)
                {
                    bestHand = candidateHand;
                }
            }

            return bestHand is null
                ? null
                : HandDescriptionFormatter.GetHandDescription(bestHand);
        }

        if (playerCards.Count == 4 && IsOmahaGame(gameTypeCode))
        {
            return HandDescriptionFormatter.GetHandDescription(new OmahaHand(playerCards, communityCards));
        }

        if (playerCards.Count == 5 && (IsNebraskaGame(gameTypeCode) || IsSouthDakotaGame(gameTypeCode)))
        {
            return HandDescriptionFormatter.GetHandDescription(new NebraskaHand(playerCards, communityCards));
        }

        return null;
    }

    private static bool IsHoldEmGame(string? gameTypeCode)
        => IsGameType(gameTypeCode, PokerGameMetadataRegistry.HoldEmCode)
           || IsGameType(gameTypeCode, PokerGameMetadataRegistry.RedRiverCode)
           || IsGameType(gameTypeCode, PokerGameMetadataRegistry.KlondikeCode);

    private static bool IsHoldTheBaseballGame(string? gameTypeCode)
        => IsGameType(gameTypeCode, PokerGameMetadataRegistry.HoldTheBaseballCode);

    private static bool IsKlondikeGame(string? gameTypeCode)
        => IsGameType(gameTypeCode, PokerGameMetadataRegistry.KlondikeCode);

    private static bool IsOmahaGame(string? gameTypeCode)
        => IsGameType(gameTypeCode, PokerGameMetadataRegistry.OmahaCode);

    private static bool IsNebraskaGame(string? gameTypeCode)
        => IsGameType(gameTypeCode, PokerGameMetadataRegistry.NebraskaCode);

    private static bool IsSouthDakotaGame(string? gameTypeCode)
        => IsGameType(gameTypeCode, PokerGameMetadataRegistry.SouthDakotaCode);

    private static bool IsIrishHoldEmFamilyGame(string? gameTypeCode)
        => IsGameType(gameTypeCode, PokerGameMetadataRegistry.IrishHoldEmCode)
           || IsGameType(gameTypeCode, PokerGameMetadataRegistry.PhilsMomCode)
           || IsGameType(gameTypeCode, PokerGameMetadataRegistry.CrazyPineappleCode);

    private static bool IsBobBarkerGame(string? gameTypeCode)
        => IsGameType(gameTypeCode, PokerGameMetadataRegistry.BobBarkerCode);

    private static bool IsGameType(string? gameTypeCode, string expectedCode)
        => !string.IsNullOrWhiteSpace(gameTypeCode)
           && string.Equals(gameTypeCode, expectedCode, StringComparison.OrdinalIgnoreCase);
}