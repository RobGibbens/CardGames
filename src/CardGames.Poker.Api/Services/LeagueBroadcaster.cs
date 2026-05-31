using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Api.Infrastructure.Telemetry;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Broadcasts league management updates to connected SignalR clients.
/// </summary>
public sealed class LeagueBroadcaster(
	IHubContext<LeagueHub> hubContext,
	ILogger<LeagueBroadcaster> logger,
	BroadcastTelemetry telemetry)
	: ILeagueBroadcaster
{
	/// <inheritdoc />
	public async Task BroadcastLeagueEventChangedAsync(LeagueEventChangedDto eventChanged, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(eventChanged);
		using var scope = CreateScope(eventChanged.LeagueId);

		var groupName = LeagueHub.GetViewedLeagueGroupName(eventChanged.LeagueId);

		await SendLeagueBroadcastAsync(
			hubContext.Clients.Group(groupName),
			"LeagueEventChanged",
			eventChanged,
			eventChanged.LeagueId,
			null,
			cancellationToken);

		logger.LogInformation(
			"Broadcast LeagueEventChanged for league {LeagueId}, event {EventId}, change {ChangeKind} to group {GroupName}",
			eventChanged.LeagueId,
			eventChanged.EventId,
			eventChanged.ChangeKind,
			groupName);
	}

	/// <inheritdoc />
	public async Task BroadcastEventSessionLaunchedAsync(LeagueEventSessionLaunchedDto sessionLaunched, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(sessionLaunched);
		using var scope = CreateScope(sessionLaunched.LeagueId, sessionLaunched.GameId);

		var groupName = LeagueHub.GetViewedLeagueGroupName(sessionLaunched.LeagueId);

		await SendLeagueBroadcastAsync(
			hubContext.Clients.Group(groupName),
			"LeagueEventSessionLaunched",
			sessionLaunched,
			sessionLaunched.LeagueId,
			sessionLaunched.GameId,
			cancellationToken);

		logger.LogInformation(
			"Broadcast LeagueEventSessionLaunched for league {LeagueId}, event {EventId}, game {GameId} to group {GroupName}",
			sessionLaunched.LeagueId,
			sessionLaunched.EventId,
			sessionLaunched.GameId,
			groupName);
	}

	/// <inheritdoc />
	public async Task BroadcastJoinRequestSubmittedAsync(LeagueJoinRequestSubmittedDto joinRequestSubmitted, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(joinRequestSubmitted);
		using var scope = CreateScope(joinRequestSubmitted.LeagueId);

		var groupName = LeagueHub.GetManagedLeagueGroupName(joinRequestSubmitted.LeagueId);

		await SendLeagueBroadcastAsync(
			hubContext.Clients.Group(groupName),
			"LeagueJoinRequestSubmitted",
			joinRequestSubmitted,
			joinRequestSubmitted.LeagueId,
			null,
			cancellationToken);

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
		using var scope = CreateScope(joinRequestUpdated.LeagueId);

		var groupNames = new HashSet<string>(StringComparer.Ordinal)
		{
			LeagueHub.GetManagedLeagueGroupName(joinRequestUpdated.LeagueId)
		};

		if (!string.IsNullOrWhiteSpace(requesterUserId))
		{
			groupNames.Add(LeagueHub.GetJoinRequesterGroupName(requesterUserId));
		}

		await SendLeagueBroadcastAsync(
			hubContext.Clients.Groups(groupNames.ToList()),
			"LeagueJoinRequestUpdated",
			joinRequestUpdated,
			joinRequestUpdated.LeagueId,
			null,
			cancellationToken);

		logger.LogInformation(
			"Broadcast LeagueJoinRequestUpdated for league {LeagueId}, request {JoinRequestId} status {Status} to groups {GroupNames}",
			joinRequestUpdated.LeagueId,
			joinRequestUpdated.JoinRequestId,
			joinRequestUpdated.Status,
			groupNames);
	}

	private async Task SendLeagueBroadcastAsync(
		IClientProxy clientProxy,
		string eventName,
		object payload,
		Guid leagueId,
		Guid? gameId,
		CancellationToken cancellationToken)
	{
		using var scope = CreateScope(leagueId, gameId);
		using var activity = PokerActivitySource.Source.StartActivity("realtime.broadcast");
		activity?.SetTag("hub", "league");
		activity?.SetTag("event", eventName);
		activity?.SetTag("league.id", leagueId);
		if (gameId.HasValue)
		{
			activity?.SetTag("game.id", gameId.Value);
		}

		var stopwatch = Stopwatch.StartNew();
		try
		{
			await clientProxy.SendAsync(eventName, payload, cancellationToken);
			activity?.SetStatus(ActivityStatusCode.Ok);
			telemetry.RecordBroadcast("league", eventName, "ok", stopwatch.Elapsed.TotalMilliseconds);
		}
		catch (Exception ex)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			telemetry.RecordBroadcast("league", eventName, "failed", stopwatch.Elapsed.TotalMilliseconds);
			logger.LogError(ex, "Error broadcasting {EventName} for league {LeagueId}", eventName, leagueId);
			throw;
		}
	}

	private IDisposable? CreateScope(Guid leagueId, Guid? gameId = null)
	{
		var values = new Dictionary<string, object>
		{
			["LeagueId"] = leagueId
		};

		if (gameId.HasValue)
		{
			values["GameId"] = gameId.Value;
		}

		return logger.BeginScope(values);
	}
}
