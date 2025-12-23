namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
/// Marker interface for commands that affect the lobby state and require
/// SignalR broadcasting to all lobby clients after successful execution.
/// </summary>
/// <remarks>
/// Commands implementing this interface will trigger automatic broadcasting
/// to the lobby via the LobbyStateBroadcastingBehavior MediatR pipeline behavior.
/// This is used for actions like creating or deleting games that should
/// update the lobby game list in real-time.
/// </remarks>
public interface ILobbyStateChangingCommand
{
    /// <summary>
    /// Gets the unique identifier of the game being created or affected.
    /// </summary>
    Guid GameId { get; }
}
