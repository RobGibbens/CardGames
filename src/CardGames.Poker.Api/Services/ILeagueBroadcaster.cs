using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Service for broadcasting league management updates to connected SignalR clients.
/// </summary>
public interface ILeagueBroadcaster
{
	/// <summary>
	/// Broadcasts that a league join request was submitted.
	/// </summary>
	/// <param name="joinRequestSubmitted">The submitted join request details.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task BroadcastJoinRequestSubmittedAsync(LeagueJoinRequestSubmittedDto joinRequestSubmitted, CancellationToken cancellationToken = default);

	/// <summary>
	/// Broadcasts that a league join request status was updated.
	/// </summary>
	/// <param name="joinRequestUpdated">The updated join request details.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task BroadcastJoinRequestUpdatedAsync(LeagueJoinRequestUpdatedDto joinRequestUpdated, CancellationToken cancellationToken = default);
}
