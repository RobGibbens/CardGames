using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Service for broadcasting league management updates to connected SignalR clients.
/// </summary>
public interface ILeagueBroadcaster
{
	/// <summary>
	/// Broadcasts that a league event changed and viewers should refresh.
	/// </summary>
	/// <param name="eventChanged">The league event change details.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task BroadcastLeagueEventChangedAsync(LeagueEventChangedDto eventChanged, CancellationToken cancellationToken = default);

	/// <summary>
	/// Broadcasts that a league event launched a new game session.
	/// </summary>
	/// <param name="sessionLaunched">The launched session details.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task BroadcastEventSessionLaunchedAsync(LeagueEventSessionLaunchedDto sessionLaunched, CancellationToken cancellationToken = default);

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
	/// <param name="requesterUserId">The requester user identifier to notify directly.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task BroadcastJoinRequestUpdatedAsync(
		LeagueJoinRequestUpdatedDto joinRequestUpdated,
		string? requesterUserId = null,
		CancellationToken cancellationToken = default);
}
