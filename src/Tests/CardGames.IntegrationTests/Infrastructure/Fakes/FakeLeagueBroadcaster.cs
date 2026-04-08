using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Services;

namespace CardGames.IntegrationTests.Infrastructure.Fakes;

public sealed class FakeLeagueBroadcaster : ILeagueBroadcaster
{
	public List<LeagueEventChangedDto> EventChangedNotifications { get; } = [];
	public List<LeagueEventSessionLaunchedDto> SessionLaunchNotifications { get; } = [];
	public List<LeagueJoinRequestSubmittedDto> JoinRequestSubmittedNotifications { get; } = [];
	public List<(LeagueJoinRequestUpdatedDto Payload, string? RequesterUserId)> JoinRequestUpdatedNotifications { get; } = [];

	public Task BroadcastLeagueEventChangedAsync(LeagueEventChangedDto eventChanged, CancellationToken cancellationToken = default)
	{
		EventChangedNotifications.Add(eventChanged);
		return Task.CompletedTask;
	}

	public Task BroadcastEventSessionLaunchedAsync(LeagueEventSessionLaunchedDto sessionLaunched, CancellationToken cancellationToken = default)
	{
		SessionLaunchNotifications.Add(sessionLaunched);
		return Task.CompletedTask;
	}

	public Task BroadcastJoinRequestSubmittedAsync(LeagueJoinRequestSubmittedDto joinRequestSubmitted, CancellationToken cancellationToken = default)
	{
		JoinRequestSubmittedNotifications.Add(joinRequestSubmitted);
		return Task.CompletedTask;
	}

	public Task BroadcastJoinRequestUpdatedAsync(LeagueJoinRequestUpdatedDto joinRequestUpdated, string? requesterUserId = null, CancellationToken cancellationToken = default)
	{
		JoinRequestUpdatedNotifications.Add((joinRequestUpdated, requesterUserId));
		return Task.CompletedTask;
	}
}