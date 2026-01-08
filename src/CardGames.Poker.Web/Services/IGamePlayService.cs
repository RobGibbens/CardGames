using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Abstraction for game-specific gameplay operations.
/// Different poker variants implement this interface to handle betting, drawing, and other actions.
/// </summary>
public interface IGamePlayService
{
	/// <summary>
	/// Gets whether this game supports draw actions (discarding and drawing cards).
	/// </summary>
	bool SupportsDraw { get; }

	/// <summary>
	/// Gets the game type code this service handles.
	/// </summary>
	string GameTypeCode { get; }

	/// <summary>
	/// Processes a betting action (check, bet, call, raise, fold, all-in).
	/// </summary>
	/// <param name="gameId">The game identifier.</param>
	/// <param name="request">The betting action request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the betting action.</returns>
	Task<ProcessBettingActionSuccessful> ProcessBettingActionAsync(
		Guid gameId,
		ProcessBettingActionRequest request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Processes a draw action (discarding and drawing cards).
	/// Only supported by games where SupportsDraw is true.
	/// </summary>
	/// <param name="gameId">The game identifier.</param>
	/// <param name="request">The draw request with card indices to discard.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the draw action.</returns>
	/// <exception cref="NotSupportedException">Thrown if the game does not support draw.</exception>
	Task<ProcessDrawSuccessful> ProcessDrawAsync(
		Guid gameId,
		ProcessDrawRequest request,
		CancellationToken cancellationToken = default);
}
