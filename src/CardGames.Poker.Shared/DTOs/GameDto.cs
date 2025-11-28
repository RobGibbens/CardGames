using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents the current state of a poker game table.
/// </summary>
public record GameDto(
    Guid Id,
    string Name,
    PokerVariant Variant,
    GameState State,
    IReadOnlyList<PlayerDto> Players,
    IReadOnlyList<CardDto>? CommunityCards,
    int DealerPosition,
    int CurrentPlayerPosition,
    int SmallBlind,
    int BigBlind,
    int Pot,
    int CurrentBet,
    string? CurrentStreet,
    DateTime CreatedAt,
    DateTime? LastActivityAt);
