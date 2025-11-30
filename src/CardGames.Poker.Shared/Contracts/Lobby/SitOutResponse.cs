namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Response to a sit out request.
/// </summary>
public record SitOutResponse(
    bool Success,
    Guid? TableId = null,
    string? PlayerName = null,
    bool? IsSittingOut = null,
    string? Error = null);
