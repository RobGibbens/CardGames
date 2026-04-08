using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeasonEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueSeasonEvent;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class UpdateLeagueSeasonEventCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_AdminCanUpdateSeasonEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		var fakeLeagueBroadcaster = Scope.ServiceProvider.GetRequiredService<FakeLeagueBroadcaster>();
		fakeCurrentUser.UserId = "league-season-update-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season Update League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Summer 2026"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Week 1",
			SequenceNumber = 1,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3),
			Notes = "Original notes",
			TournamentBuyIn = 1000
		}));

		var scheduledAtUtc = DateTimeOffset.UtcNow.AddDays(5);
		fakeLeagueBroadcaster.EventChangedNotifications.Clear();
		var result = await Mediator.Send(new UpdateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, createEvent.AsT0.EventId, new UpdateLeagueSeasonEventRequest
		{
			Name = "Week 1 Updated",
			SequenceNumber = 3,
			ScheduledAtUtc = scheduledAtUtc,
			Notes = "Updated notes",
			TournamentBuyIn = 2200
		}));

		result.IsT0.Should().BeTrue();

		var savedEvent = await DbContext.LeagueSeasonEvents
			.AsNoTracking()
			.SingleAsync(x => x.Id == createEvent.AsT0.EventId);

		savedEvent.Name.Should().Be("Week 1 Updated");
		savedEvent.SequenceNumber.Should().Be(3);
		savedEvent.ScheduledAtUtc.Should().Be(scheduledAtUtc);
		savedEvent.Notes.Should().Be("Updated notes");
		savedEvent.TournamentBuyIn.Should().Be(2200);
		fakeLeagueBroadcaster.EventChangedNotifications.Should().ContainSingle();
		fakeLeagueBroadcaster.EventChangedNotifications[0].LeagueId.Should().Be(leagueId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].EventId.Should().Be(createEvent.AsT0.EventId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SourceType.Should().Be(CardGames.Contracts.SignalR.LeagueEventSourceType.Season);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SeasonId.Should().Be(createSeason.AsT0.SeasonId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].ChangeKind.Should().Be(CardGames.Contracts.SignalR.LeagueEventChangeKind.Updated);
	}

	[Fact]
	public async Task Handle_WhenSequenceNumberAlreadyExists_ReturnsInvalidRequest()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-update-sequence-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season Sequence League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Fall 2026"
		}));

		var firstEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Week 1",
			SequenceNumber = 1,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3)
		}));

		await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Week 2",
			SequenceNumber = 2,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(10)
		}));

		var result = await Mediator.Send(new UpdateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, firstEvent.AsT0.EventId, new UpdateLeagueSeasonEventRequest
		{
			Name = "Week 1",
			SequenceNumber = 2,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(5)
		}));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(UpdateLeagueSeasonEventErrorCode.InvalidRequest);
	}

	[Fact]
	public async Task Handle_LaunchedSeasonEventCannotBeUpdated()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-update-launched-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season Launch Guard League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Spring 2027"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Week 1",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3)
		}));

		var seasonEvent = await DbContext.LeagueSeasonEvents.SingleAsync(x => x.Id == createEvent.AsT0.EventId);
		seasonEvent.LaunchedGameId = Guid.CreateVersion7();
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new UpdateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, createEvent.AsT0.EventId, new UpdateLeagueSeasonEventRequest
		{
			Name = "Week 1 Updated",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(5)
		}));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(UpdateLeagueSeasonEventErrorCode.Conflict);
	}

	[Fact]
	public async Task Handle_PastScheduledDate_ReturnsInvalidRequest()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-update-past-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season Update Past League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Spring 2027"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Week 1",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3)
		}));

		var result = await Mediator.Send(new UpdateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, createEvent.AsT0.EventId, new UpdateLeagueSeasonEventRequest
		{
			Name = "Week 1 Updated",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
		}));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(UpdateLeagueSeasonEventErrorCode.InvalidRequest);
		result.AsT1.Message.Should().Be("Scheduled date/time must be in the future.");
	}
}