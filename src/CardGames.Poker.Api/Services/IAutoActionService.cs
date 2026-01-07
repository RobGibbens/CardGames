namespace CardGames.Poker.Api.Services;

/// <summary>
/// Service for performing automatic actions when a player's turn timer expires.
/// </summary>
public interface IAutoActionService
{
    /// <summary>
    /// Performs the default action for a player when their timer expires.
    /// The action depends on the current game phase:
    /// - Betting phase: Check if possible, otherwise Fold
    /// - Draw phase: Stand pat (discard no cards)
    /// - Drop/Stay phase: Drop
    /// </summary>
    /// <param name="gameId">The game ID.</param>
    /// <param name="playerSeatIndex">The seat index of the player whose timer expired.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PerformAutoActionAsync(Guid gameId, int playerSeatIndex, CancellationToken cancellationToken = default);
}
