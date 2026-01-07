using CardGames.Contracts.SignalR;

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

    /// <summary>
    /// Broadcasts a player joined notification to all players in the game except the joining player.
    /// </summary>
    /// <param name="gameId">The unique identifier of the game.</param>
    /// <param name="playerName">The name of the player who joined.</param>
    /// <param name="seatIndex">The seat index the player joined at.</param>
    /// <param name="canPlayCurrentHand">Whether the player can participate in the current hand.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastPlayerJoinedAsync(
        Guid gameId,
        string playerName,
        int seatIndex,
        bool canPlayCurrentHand,
                CancellationToken cancellationToken = default);

            /// <summary>
            /// Broadcasts a table settings updated notification to all players in the game.
            /// </summary>
            /// <param name="notification">The notification containing updated settings.</param>
            /// <param name="cancellationToken">Cancellation token.</param>
            Task BroadcastTableSettingsUpdatedAsync(
                TableSettingsUpdatedDto notification,
                CancellationToken cancellationToken = default);

            /// <summary>
            /// Broadcasts a player action notification to all players in the game.
            /// The action will be displayed temporarily in the player's seat pill.
            /// </summary>
            /// <param name="gameId">The unique identifier of the game.</param>
            /// <param name="seatIndex">The seat index of the player who performed the action.</param>
            /// <param name="playerName">The name of the player who performed the action.</param>
            /// <param name="actionDescription">The display description of the action (e.g., "Checked", "Raised 50").</param>
            /// <param name="cancellationToken">Cancellation token.</param>
            Task BroadcastPlayerActionAsync(
                Guid gameId,
                int seatIndex,
                string? playerName,
                string actionDescription,
                CancellationToken cancellationToken = default);
        }
