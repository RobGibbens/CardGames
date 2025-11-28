namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents a player in the game.
/// </summary>
public record PlayerDto(
    string Name,
    IReadOnlyList<CardDto>? HoleCards = null,
    HandDto? Hand = null,
    int ChipStack = 0,
    bool HasFolded = false,
    bool IsAllIn = false);
