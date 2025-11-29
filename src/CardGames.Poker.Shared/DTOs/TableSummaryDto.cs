using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents a summary of a poker table for lobby display.
/// </summary>
public record TableSummaryDto(
    Guid Id,
    string Name,
    PokerVariant Variant,
    int SmallBlind,
    int BigBlind,
    int MinBuyIn,
    int MaxBuyIn,
    int MaxSeats,
    int OccupiedSeats,
    GameState State,
    TablePrivacy Privacy,
    DateTime CreatedAt,
    int WaitingListCount = 0);
