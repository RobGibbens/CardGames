namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents a player waiting for a seat at a table.
/// </summary>
public record WaitingListEntryDto(
    Guid TableId,
    string PlayerName,
    DateTime JoinedAt,
    int Position);
