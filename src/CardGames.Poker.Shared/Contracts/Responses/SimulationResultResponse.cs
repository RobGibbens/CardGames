using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Contracts.Responses;

/// <summary>
/// Response containing simulation results.
/// </summary>
public record SimulationResultResponse(
    PokerVariant Variant,
    int TotalHands,
    IReadOnlyList<PlayerSimulationResult> PlayerResults,
    IReadOnlyList<HandTypeDistribution> HandDistributions);

/// <summary>
/// Represents a player's simulation results.
/// </summary>
public record PlayerSimulationResult(
    string Name,
    int Wins,
    double WinPercentage,
    int Ties,
    double TiePercentage);

/// <summary>
/// Represents distribution of hand types.
/// </summary>
public record HandTypeDistribution(
    string HandType,
    int Count,
    double Percentage);
