using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueOneOffEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeasonEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.LaunchLeagueEventSession;
using CardGames.Poker.Api.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CardGames.IntegrationTests.Features.Commands;

public class LaunchLeagueEventSessionCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_SeasonHoldEmTournamentLaunch_PersistsBlindSettingsOnCreatedGame()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-launch-season-holdem-admin";
		fakeCurrentUser.UserName = "league-launch-season-holdem-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season HoldEm Launch League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Spring 2026"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Week 2",
			SequenceNumber = 2,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(2),
			GameTypeCode = "HOLDEM",
			Ante = 0,
			MinBet = 10,
			SmallBlind = 5,
			BigBlind = 10,
			TournamentBuyIn = 250
		}));

		var result = await Mediator.Send(new LaunchLeagueEventSessionCommand(
			leagueId,
			LeagueEventSourceType.Season,
			createEvent.AsT0.EventId,
			createSeason.AsT0.SeasonId,
			new LaunchLeagueEventSessionRequest
			{
				GameCode = "HOLDEM",
				HostStartingChips = 250
			}));

		result.IsT0.Should().BeTrue();

		var createdGame = await DbContext.Games.FindAsync(result.AsT0.GameId);
		createdGame.Should().NotBeNull();
		createdGame!.SmallBlind.Should().Be(5);
		createdGame.BigBlind.Should().Be(10);
		createdGame.Ante.Should().Be(0);
		createdGame.MinBet.Should().Be(10);
		createdGame.TournamentBuyIn.Should().Be(250);
	}

	[Fact]
	public async Task Handle_OneOffHoldEmTournamentLaunch_PersistsBlindSettingsOnCreatedGame()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-launch-oneoff-holdem-admin";
		fakeCurrentUser.UserName = "league-launch-oneoff-holdem-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "One-Off HoldEm Launch League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createEvent = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Saturday Tournament",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3),
			EventType = CardGames.Poker.Api.Contracts.LeagueOneOffEventType.Tournament,
			GameTypeCode = "HOLDEM",
			Ante = 0,
			MinBet = 10,
			SmallBlind = 5,
			BigBlind = 10,
			TournamentBuyIn = 300
		}));

		var result = await Mediator.Send(new LaunchLeagueEventSessionCommand(
			leagueId,
			LeagueEventSourceType.OneOff,
			createEvent.AsT0.EventId,
			null,
			new LaunchLeagueEventSessionRequest
			{
				GameCode = "HOLDEM",
				HostStartingChips = 300
			}));

		result.IsT0.Should().BeTrue();

		var createdGame = await DbContext.Games.FindAsync(result.AsT0.GameId);
		createdGame.Should().NotBeNull();
		createdGame!.SmallBlind.Should().Be(5);
		createdGame.BigBlind.Should().Be(10);
		createdGame.Ante.Should().Be(0);
		createdGame.MinBet.Should().Be(10);
		createdGame.TournamentBuyIn.Should().Be(300);
	}

	[Fact]
	public async Task Handle_SeasonEventLaunch_BroadcastsViewerRefreshNotification()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-launch-broadcast-admin";
		fakeCurrentUser.UserName = "league-launch-broadcast-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var fakeLeagueBroadcaster = Scope.ServiceProvider.GetRequiredService<FakeLeagueBroadcaster>();

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "League Launch Broadcast"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Spring 2026"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Week 1",
			SequenceNumber = 1,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(2),
			TournamentBuyIn = 250
		}));

		var result = await Mediator.Send(new LaunchLeagueEventSessionCommand(
			leagueId,
			CardGames.Poker.Api.Features.Leagues.v1.Commands.LaunchLeagueEventSession.LeagueEventSourceType.Season,
			createEvent.AsT0.EventId,
			createSeason.AsT0.SeasonId,
			new LaunchLeagueEventSessionRequest
			{
				GameCode = "FIVECARDDRAW",
				GameName = "Week 1 Live",
				Ante = 10,
				MinBet = 20,
				HostStartingChips = 250
			}));

		result.IsT0.Should().BeTrue();
		fakeLeagueBroadcaster.SessionLaunchNotifications.Should().ContainSingle();

		var notification = fakeLeagueBroadcaster.SessionLaunchNotifications.Single();
		notification.LeagueId.Should().Be(leagueId);
		notification.EventId.Should().Be(createEvent.AsT0.EventId);
		notification.SourceType.Should().Be(CardGames.Contracts.SignalR.LeagueEventSourceType.Season);
		notification.SeasonId.Should().Be(createSeason.AsT0.SeasonId);
		notification.GameId.Should().Be(result.AsT0.GameId);
	}

	[Fact]
	public async Task Handle_OneOffEventLaunch_BroadcastsViewerRefreshNotification()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-launch-oneoff-admin";
		fakeCurrentUser.UserName = "league-launch-oneoff-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var fakeLeagueBroadcaster = Scope.ServiceProvider.GetRequiredService<FakeLeagueBroadcaster>();

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "League One-Off Launch Broadcast"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createEvent = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Saturday Cash",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3),
			EventType = CardGames.Poker.Api.Contracts.LeagueOneOffEventType.CashGame,
			GameTypeCode = "FIVECARDDRAW",
			Ante = 10,
			MinBet = 20
		}));

		var result = await Mediator.Send(new LaunchLeagueEventSessionCommand(
			leagueId,
			CardGames.Poker.Api.Features.Leagues.v1.Commands.LaunchLeagueEventSession.LeagueEventSourceType.OneOff,
			createEvent.AsT0.EventId,
			null,
			new LaunchLeagueEventSessionRequest
			{
				GameCode = "FIVECARDDRAW",
				GameName = "Saturday Cash Table",
				Ante = 10,
				MinBet = 20,
				HostStartingChips = 500
			}));

		result.IsT0.Should().BeTrue();
		fakeLeagueBroadcaster.SessionLaunchNotifications.Should().ContainSingle();

		var notification = fakeLeagueBroadcaster.SessionLaunchNotifications.Single();
		notification.LeagueId.Should().Be(leagueId);
		notification.EventId.Should().Be(createEvent.AsT0.EventId);
		notification.SourceType.Should().Be(CardGames.Contracts.SignalR.LeagueEventSourceType.OneOff);
		notification.SeasonId.Should().BeNull();
		notification.GameId.Should().Be(result.AsT0.GameId);
	}
}