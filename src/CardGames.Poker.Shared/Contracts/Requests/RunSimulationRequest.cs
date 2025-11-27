using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Contracts.Requests;

/// <summary>
/// Request to run a poker simulation.
/// </summary>
public record RunSimulationRequest(
    PokerVariant Variant,
    int NumberOfHands,
    IReadOnlyList<SimulationPlayerRequest> Players,
    IReadOnlyList<string>? FlopCards = null,
    string? TurnCard = null,
    string? RiverCard = null);

/// <summary>
/// Represents a player in a simulation request.
/// </summary>
public record SimulationPlayerRequest(
    string Name,
    IReadOnlyList<string>? HoleCards = null);
