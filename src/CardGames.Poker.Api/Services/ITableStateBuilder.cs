using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Service for building table state snapshots for SignalR broadcasts.
/// </summary>
public interface ITableStateBuilder
{
    /// <summary>
    /// Builds the public table state visible to all players in the game.
    /// Private card information is redacted (shown as face-down).
    /// </summary>
    /// <param name="gameId">The unique identifier of the game.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The public table state, or null if the game is not found.</returns>
    Task<TableStatePublicDto?> BuildPublicStateAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the private state for a specific player, including their face-up cards
    /// and available actions.
    /// </summary>
    /// <param name="gameId">The unique identifier of the game.</param>
    /// <param name="userId">The user identifier (typically email) of the requesting player.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The private state for the player, or null if the game or player is not found.</returns>
    Task<PrivateStateDto?> BuildPrivateStateAsync(Guid gameId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of connected user identifiers for a game.
    /// Used by the broadcaster to send private state updates.
    /// </summary>
    /// <param name="gameId">The unique identifier of the game.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of user identifiers (typically emails) for players in the game.</returns>
    Task<IReadOnlyList<string>> GetPlayerUserIdsAsync(Guid gameId, CancellationToken cancellationToken = default);
}
