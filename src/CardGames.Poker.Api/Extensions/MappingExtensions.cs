using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Api.Extensions;

/// <summary>
/// Extension methods for mapping domain objects to DTOs.
/// </summary>
public static class MappingExtensions
{
    /// <summary>
    /// Converts a Card to a CardDto.
    /// </summary>
    public static CardDto ToDto(this Card card)
    {
        return new CardDto(
            Rank: card.Symbol.ToString(),
            Suit: card.Suit.ToString(),
            DisplayValue: card.ToShortString()
        );
    }

    /// <summary>
    /// Converts a collection of Cards to CardDtos.
    /// </summary>
    public static IReadOnlyList<CardDto> ToDtos(this IEnumerable<Card> cards)
    {
        return cards.Select(c => c.ToDto()).ToList();
    }

    /// <summary>
    /// Converts a HandBase to a HandDto.
    /// </summary>
    public static HandDto ToDto(this HandBase hand, string description)
    {
        return new HandDto(
            Cards: hand.Cards.ToDtos(),
            HandType: MapHandType(hand.Type),
            Description: description,
            Strength: hand.Strength
        );
    }

    /// <summary>
    /// Maps the domain HandType to a display string.
    /// </summary>
    public static string MapHandType(HandType type)
    {
        return type switch
        {
            HandType.HighCard => "High Card",
            HandType.OnePair => "One Pair",
            HandType.TwoPair => "Two Pair",
            HandType.Trips => "Three of a Kind",
            HandType.Straight => "Straight",
            HandType.Flush => "Flush",
            HandType.FullHouse => "Full House",
            HandType.Quads => "Four of a Kind",
            HandType.StraightFlush => "Straight Flush",
            HandType.FiveOfAKind => "Five of a Kind",
            _ => "Unknown"
        };
    }
}
