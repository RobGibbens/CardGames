namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents a playing card.
/// </summary>
public record CardDto(string Rank, string Suit, string DisplayValue);
