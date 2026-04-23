using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeasonEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.DeleteLeagueSeasonEvent;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class DeleteLeagueSeasonEventCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_AdminCanDeleteSeasonEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		var fakeLeagueBroadcaster = Scope.ServiceProvider.GetRequiredService<FakeLeagueBroadcaster>();
		fakeCurrentUser.UserId = "league-season-delete-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season Delete League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Summer 2027"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3)
		}));

		fakeLeagueBroadcaster.EventChangedNotifications.Clear();
		var result = await Mediator.Send(new DeleteLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, createEvent.AsT0.EventId));

		result.IsT0.Should().BeTrue();
		var exists = await DbContext.LeagueSeasonEvents.AsNoTracking().AnyAsync(x => x.Id == createEvent.AsT0.EventId);
		exists.Should().BeFalse();
		fakeLeagueBroadcaster.EventChangedNotifications.Should().ContainSingle();
		fakeLeagueBroadcaster.EventChangedNotifications[0].LeagueId.Should().Be(leagueId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].EventId.Should().Be(createEvent.AsT0.EventId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SourceType.Should().Be(CardGames.Contracts.SignalR.LeagueEventSourceType.Season);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SeasonId.Should().Be(createSeason.AsT0.SeasonId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].ChangeKind.Should().Be(CardGames.Contracts.SignalR.LeagueEventChangeKind.Deleted);
	}

	[Fact]
	public async Task Handle_NonGovernanceMemberCannotDeleteSeasonEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-delete-owner";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season Delete Forbidden League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Fall 2027"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3)
		}));

		fakeCurrentUser.UserId = "league-season-delete-outsider";

		var result = await Mediator.Send(new DeleteLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, createEvent.AsT0.EventId));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DeleteLeagueSeasonEventErrorCode.Forbidden);
	}

	[Fact]
	public async Task Handle_LaunchedSeasonEventCannotBeDeleted()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-delete-launched-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season Delete Launch Guard League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Winter 2027"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3)
		}));

		var seasonEvent = await DbContext.LeagueSeasonEvents.SingleAsync(x => x.Id == createEvent.AsT0.EventId);
		seasonEvent.LaunchedGameId = Guid.CreateVersion7();
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new DeleteLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, createEvent.AsT0.EventId));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DeleteLeagueSeasonEventErrorCode.Conflict);
	}
}