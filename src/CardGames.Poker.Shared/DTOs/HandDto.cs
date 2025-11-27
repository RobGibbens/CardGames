namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents a player's poker hand.
/// </summary>
public record HandDto(
    IReadOnlyList<CardDto> Cards,
    string HandType,
    string Description,
    long Strength);
