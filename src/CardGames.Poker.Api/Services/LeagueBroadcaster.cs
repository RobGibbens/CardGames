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
	public async Task BroadcastJoinRequestUpdatedAsync(
		LeagueJoinRequestUpdatedDto joinRequestUpdated,
		string? requesterUserId = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(joinRequestUpdated);

		var groupNames = new HashSet<string>(StringComparer.Ordinal)
		{
			LeagueHub.GetManagedLeagueGroupName(joinRequestUpdated.LeagueId)
		};

		if (!string.IsNullOrWhiteSpace(requesterUserId))
		{
			groupNames.Add(LeagueHub.GetJoinRequesterGroupName(requesterUserId));
		}

		await hubContext.Clients.Groups(groupNames.ToList())
			.SendAsync("LeagueJoinRequestUpdated", joinRequestUpdated, cancellationToken);

		logger.LogInformation(
			"Broadcast LeagueJoinRequestUpdated for league {LeagueId}, request {JoinRequestId} status {Status} to groups {GroupNames}",
			joinRequestUpdated.LeagueId,
			joinRequestUpdated.JoinRequestId,
			joinRequestUpdated.Status,
			groupNames);
	}
}
