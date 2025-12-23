namespace CardGames.Poker.Api.Services;

/// <summary>
/// Service for broadcasting game state updates to connected SignalR clients.
/// </summary>
public interface IGameStateBroadcaster
{
    /// <summary>
    /// Broadcasts the current game state to all players in the game group.
    /// Sends public state to the group and private state to each individual player.
    /// </summary>
    /// <param name="gameId">The unique identifier of the game.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastGameStateAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts the current game state to a specific user.
    /// Useful for sending initial state on connect or targeted updates.
    /// </summary>
    /// <param name="gameId">The unique identifier of the game.</param>
    /// <param name="userId">The user identifier to send state to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastGameStateToUserAsync(Guid gameId, string userId, CancellationToken cancellationToken = default);
}
