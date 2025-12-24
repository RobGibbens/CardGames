using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Service for broadcasting lobby updates to connected SignalR clients.
/// </summary>
public interface ILobbyBroadcaster
{
    /// <summary>
    /// Broadcasts a game created event to all clients in the lobby group.
    /// </summary>
    /// <param name="gameCreated">The game creation details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastGameCreatedAsync(GameCreatedDto gameCreated, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a game deleted/ended event to all clients in the lobby group.
    /// </summary>
    /// <param name="gameId">The unique identifier of the deleted game.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastGameDeletedAsync(Guid gameId, CancellationToken cancellationToken = default);
}
