using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Responses;

/// <summary>
/// Response containing evaluated hand information.
/// </summary>
public record EvaluateHandResponse(
    IReadOnlyList<CardDto> Cards,
    string HandType,
    string Description,
    long Strength);
