using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CorrectLeagueSeasonEventResults;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeasonEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using DataLeagueRole = CardGames.Poker.Api.Data.Entities.LeagueRole;
using DataLeagueSeasonEventStatus = CardGames.Poker.Api.Data.Entities.LeagueSeasonEventStatus;

namespace CardGames.IntegrationTests.Features.Commands;

public class LeagueSeasonEventResultsCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_IngestedResults_BroadcastsLeagueEventChanged()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		var fakeLeagueBroadcaster = Scope.ServiceProvider.GetRequiredService<FakeLeagueBroadcaster>();
		fakeCurrentUser.UserId = "league-results-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var (leagueId, seasonId, eventId) = await CreateSeasonEventAsync("league-results-member");
		fakeLeagueBroadcaster.EventChangedNotifications.Clear();

		var result = await Mediator.Send(new IngestLeagueSeasonEventResultsCommand(leagueId, seasonId, eventId, new IngestLeagueSeasonEventResultsRequest
		{
			Results =
			[
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "league-results-admin",
					Placement = 1,
					Points = 10,
					ChipsDelta = 120
				},
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "league-results-member",
					Placement = 2,
					Points = 6,
					ChipsDelta = -120
				}
			]
		}));

		result.IsT0.Should().BeTrue();
		fakeLeagueBroadcaster.EventChangedNotifications.Should().ContainSingle();
		fakeLeagueBroadcaster.EventChangedNotifications[0].LeagueId.Should().Be(leagueId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].EventId.Should().Be(eventId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SourceType.Should().Be(CardGames.Contracts.SignalR.LeagueEventSourceType.Season);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SeasonId.Should().Be(seasonId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].ChangeKind.Should().Be(CardGames.Contracts.SignalR.LeagueEventChangeKind.ResultsRecorded);

		var seasonEvent = await DbContext.LeagueSeasonEvents.AsNoTracking().SingleAsync(x => x.Id == eventId);
		seasonEvent.Status.Should().Be(DataLeagueSeasonEventStatus.Completed);
	}

	[Fact]
	public async Task Handle_CorrectedResults_BroadcastsLeagueEventChanged()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		var fakeLeagueBroadcaster = Scope.ServiceProvider.GetRequiredService<FakeLeagueBroadcaster>();
		fakeCurrentUser.UserId = "league-results-correction-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var (leagueId, seasonId, eventId) = await CreateSeasonEventAsync("league-results-correction-member");

		var ingestResult = await Mediator.Send(new IngestLeagueSeasonEventResultsCommand(leagueId, seasonId, eventId, new IngestLeagueSeasonEventResultsRequest
		{
			Results =
			[
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "league-results-correction-admin",
					Placement = 1,
					Points = 10,
					ChipsDelta = 100
				},
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "league-results-correction-member",
					Placement = 2,
					Points = 6,
					ChipsDelta = -100
				}
			]
		}));

		ingestResult.IsT0.Should().BeTrue();
		fakeLeagueBroadcaster.EventChangedNotifications.Clear();

		var correctionResult = await Mediator.Send(new CorrectLeagueSeasonEventResultsCommand(leagueId, seasonId, eventId, new CorrectLeagueSeasonEventResultsRequest
		{
			Reason = "Corrected placements",
			Results =
			[
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "league-results-correction-admin",
					Placement = 2,
					Points = 6,
					ChipsDelta = -100
				},
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "league-results-correction-member",
					Placement = 1,
					Points = 10,
					ChipsDelta = 100
				}
			]
		}));

		correctionResult.IsT0.Should().BeTrue();
		fakeLeagueBroadcaster.EventChangedNotifications.Should().ContainSingle();
		fakeLeagueBroadcaster.EventChangedNotifications[0].LeagueId.Should().Be(leagueId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].EventId.Should().Be(eventId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SourceType.Should().Be(CardGames.Contracts.SignalR.LeagueEventSourceType.Season);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SeasonId.Should().Be(seasonId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].ChangeKind.Should().Be(CardGames.Contracts.SignalR.LeagueEventChangeKind.ResultsRecorded);

		var auditRows = await DbContext.LeagueSeasonEventResultCorrectionAudits
			.AsNoTracking()
			.Where(x => x.LeagueSeasonEventId == eventId)
			.ToListAsync();

		auditRows.Should().HaveCount(1);
	}

	private async Task<(Guid LeagueId, Guid SeasonId, Guid EventId)> CreateSeasonEventAsync(string memberUserId)
	{
		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Results Broadcast League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = memberUserId,
			Role = DataLeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});

		await DbContext.SaveChangesAsync();

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Results Season"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Results Week 1",
			SequenceNumber = 1,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3)
		}));

		return (leagueId, createSeason.AsT0.SeasonId, createEvent.AsT0.EventId);
	}
}