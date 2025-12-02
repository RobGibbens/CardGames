using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Variants;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Common.Mapping;

/// <summary>
/// Mapperly-based mapper for converting domain objects to DTOs.
/// </summary>
[Mapper]
public static partial class ApiMapper
{
    /// <summary>
    /// Converts a Card to a CardDto.
    /// </summary>
    public static CardDto ToCardDto(Card card)
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
    public static IReadOnlyList<CardDto> ToCardDtos(IEnumerable<Card> cards)
    {
        return cards.Select(ToCardDto).ToList();
    }

    /// <summary>
    /// Converts a HandBase to a HandDto.
    /// </summary>
    public static HandDto ToHandDto(HandBase hand, string description)
    {
        return new HandDto(
            Cards: ToCardDtos(hand.Cards),
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

    /// <summary>
    /// Converts a GameVariantInfo to a VariantDto.
    /// </summary>
    public static VariantDto ToVariantDto(GameVariantInfo variant)
    {
        return new VariantDto(
            variant.Id,
            variant.Name,
            variant.Description,
            variant.MinPlayers,
            variant.MaxPlayers
        );
    }

    /// <summary>
    /// Converts a collection of GameVariantInfo to VariantDtos.
    /// </summary>
    public static IReadOnlyList<VariantDto> ToVariantDtos(IEnumerable<GameVariantInfo> variants)
    {
        return variants.Select(ToVariantDto).ToList();
    }
}

/// <summary>
/// Data transfer object for variant information.
/// </summary>
public record VariantDto(
    string Id,
    string Name,
    string? Description,
    int MinPlayers,
    int MaxPlayers);
