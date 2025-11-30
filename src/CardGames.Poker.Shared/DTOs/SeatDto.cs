namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents the status of a seat at a poker table.
/// </summary>
public enum SeatStatus
{
    /// <summary>Seat is empty and available for selection.</summary>
    Available,

    /// <summary>Seat is occupied by an active player.</summary>
    Occupied,

    /// <summary>Seat is reserved for a player (awaiting buy-in confirmation).</summary>
    Reserved,

    /// <summary>Player is sitting out (away from the table).</summary>
    SittingOut
}

/// <summary>
/// Represents a seat at a poker table.
/// </summary>
public record SeatDto(
    int SeatNumber,
    SeatStatus Status,
    string? PlayerName = null,
    int ChipStack = 0,
    DateTime? ReservedUntil = null);
