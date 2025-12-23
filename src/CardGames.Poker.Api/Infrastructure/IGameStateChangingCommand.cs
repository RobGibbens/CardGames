namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
/// Marker interface for commands that modify game state and require
/// SignalR broadcasting after successful execution.
/// </summary>
/// <remarks>
/// Commands implementing this interface will trigger automatic broadcasting
/// of game state to all connected clients via the GameStateBroadcastingBehavior
/// MediatR pipeline behavior.
/// </remarks>
public interface IGameStateChangingCommand
{
    /// <summary>
    /// Gets the unique identifier of the game being modified.
    /// </summary>
    Guid GameId { get; }
}
