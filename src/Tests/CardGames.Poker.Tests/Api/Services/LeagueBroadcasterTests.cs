#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class LeagueBroadcasterTests
{
	[Fact]
	public async Task BroadcastEventSessionLaunchedAsync_SendsToViewedLeagueGroup()
	{
		var leagueId = Guid.NewGuid();
		var payload = new LeagueEventSessionLaunchedDto
		{
			LeagueId = leagueId,
			EventId = Guid.NewGuid(),
			SourceType = LeagueEventSourceType.Season,
			SeasonId = Guid.NewGuid(),
			GameId = Guid.NewGuid(),
			LaunchedAtUtc = DateTimeOffset.UtcNow
		};

		var clientProxy = Substitute.For<IClientProxy>();
		clientProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		var clients = Substitute.For<IHubClients>();
		clients.Group(LeagueHub.GetViewedLeagueGroupName(leagueId)).Returns(clientProxy);

		var hubContext = Substitute.For<IHubContext<LeagueHub>>();
		hubContext.Clients.Returns(clients);

		var sut = new LeagueBroadcaster(hubContext, Substitute.For<ILogger<LeagueBroadcaster>>());

		await sut.BroadcastEventSessionLaunchedAsync(payload);

		await clientProxy.Received(1).SendCoreAsync(
			"LeagueEventSessionLaunched",
			Arg.Is<object?[]>(args =>
				args.Length == 1 &&
				args[0] != null &&
				((LeagueEventSessionLaunchedDto)args[0]!).LeagueId == leagueId &&
				((LeagueEventSessionLaunchedDto)args[0]!).EventId == payload.EventId &&
				((LeagueEventSessionLaunchedDto)args[0]!).GameId == payload.GameId),
			Arg.Any<CancellationToken>());
	}
}