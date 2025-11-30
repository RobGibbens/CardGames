namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents a pot (main or side) with its amount and eligible players.
/// </summary>
public record PotDto(
    int Amount,
    IReadOnlyList<string> EligiblePlayers,
    bool IsMainPot);

/// <summary>
/// Represents the complete pot breakdown including all pots and player contributions.
/// </summary>
public record PotBreakdownDto(
    int TotalAmount,
    IReadOnlyList<PotDto> Pots,
    IReadOnlyDictionary<string, int> PlayerContributions);
