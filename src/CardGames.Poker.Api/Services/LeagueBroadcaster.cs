using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Broadcasts league management updates to connected SignalR clients.
/// </summary>
public sealed class LeagueBroadcaster(
	IHubContext<LeagueHub> hubContext,
	ILogger<LeagueBroadcaster> logger)
	: ILeagueBroadcaster
{
	/// <inheritdoc />
	public async Task BroadcastJoinRequestSubmittedAsync(LeagueJoinRequestSubmittedDto joinRequestSubmitted, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(joinRequestSubmitted);

		var groupName = LeagueHub.GetManagedLeagueGroupName(joinRequestSubmitted.LeagueId);

		await hubContext.Clients.Group(groupName)
			.SendAsync("LeagueJoinRequestSubmitted", joinRequestSubmitted, cancellationToken);

		logger.LogInformation(
			"Broadcast LeagueJoinRequestSubmitted for league {LeagueId}, request {JoinRequestId} to group {GroupName}",
			joinRequestSubmitted.LeagueId,
			joinRequestSubmitted.JoinRequestId,
			groupName);
	}

	/// <inheritdoc />
	public async Task BroadcastJoinRequestUpdatedAsync(LeagueJoinRequestUpdatedDto joinRequestUpdated, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(joinRequestUpdated);

		var groupName = LeagueHub.GetManagedLeagueGroupName(joinRequestUpdated.LeagueId);

		await hubContext.Clients.Group(groupName)
			.SendAsync("LeagueJoinRequestUpdated", joinRequestUpdated, cancellationToken);

		logger.LogInformation(
			"Broadcast LeagueJoinRequestUpdated for league {LeagueId}, request {JoinRequestId} status {Status} to group {GroupName}",
			joinRequestUpdated.LeagueId,
			joinRequestUpdated.JoinRequestId,
			joinRequestUpdated.Status,
			groupName);
	}
}
