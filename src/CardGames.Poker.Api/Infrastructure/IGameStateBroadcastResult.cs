namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
/// Interface for successful command results that can suppress the automatic
/// post-handler game-state broadcast when no state mutation occurred.
/// </summary>
public interface IGameStateBroadcastResult
{
    /// <summary>
    /// Gets a value indicating whether the MediatR broadcast pipeline should
    /// emit a game-state update after this response.
    /// </summary>
    bool ShouldBroadcastGameState { get; }
}