using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data.Entities;
using FluentAssertions;
using DataLeagueRole = CardGames.Poker.Api.Data.Entities.LeagueRole;

namespace CardGames.IntegrationTests.Api;

public class LeaguesApiStandingsScaffoldTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters =
		{
			new JsonStringEnumConverter()
		}
	};

	[Fact]
	[Trait("QualityGate", "LeaguesP0")]
	public async Task Journey_ResultIngestion_ToStandings_IsCovered_ByQualityGateTests()
	{
		SetUser("standings-manager");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "Standings League"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = league!.LeagueId,
			UserId = "standings-member",
			Role = DataLeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		var createSeasonResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons", new CreateLeagueSeasonRequest
		{
			Name = "Season 1"
		});
		createSeasonResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var season = await createSeasonResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonResponse>(JsonOptions);
		season.Should().NotBeNull();

		var createEventResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{season!.SeasonId}/events", new CreateLeagueSeasonEventRequest
		{
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3)
		});
		createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var seasonEvent = await createEventResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonEventResponse>(JsonOptions);
		seasonEvent.Should().NotBeNull();

		var ingestResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{season.SeasonId}/events/{seasonEvent!.EventId}/results", new IngestLeagueSeasonEventResultsRequest
		{
			Results =
			[
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-manager",
					Placement = 1,
					Points = 10,
					ChipsDelta = 120
				},
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-member",
					Placement = 2,
					Points = 6,
					ChipsDelta = -120
				}
			]
		});

		ingestResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var idempotentReplayResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{season.SeasonId}/events/{seasonEvent.EventId}/results", new IngestLeagueSeasonEventResultsRequest
		{
			Results =
			[
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-manager",
					Placement = 1,
					Points = 10,
					ChipsDelta = 120
				},
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-member",
					Placement = 2,
					Points = 6,
					ChipsDelta = -120
				}
			]
		});

		idempotentReplayResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var standingsResponse = await Client.GetAsync($"/api/v1/leagues/{league.LeagueId}/standings");
		standingsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var standings = await standingsResponse.Content.ReadFromJsonAsync<IReadOnlyList<LeagueStandingEntryDto>>(JsonOptions);
		standings.Should().NotBeNull();
		standings!.Count.Should().Be(2);
		standings[0].UserId.Should().Be("standings-manager");
		standings[0].Rank.Should().Be(1);
		standings[0].TotalPoints.Should().Be(10);
		standings[1].UserId.Should().Be("standings-member");
		standings[1].Rank.Should().Be(2);
		standings[1].TotalPoints.Should().Be(6);

		var duplicateIngestResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{season.SeasonId}/events/{seasonEvent.EventId}/results", new IngestLeagueSeasonEventResultsRequest
		{
			Results =
			[
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-manager",
					Placement = 1,
					Points = 8,
					ChipsDelta = 50
				}
			]
		});

		duplicateIngestResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task Journey_ResultCorrection_ReplacesEventResults_UpdatesStandings_AndWritesAudit()
	{
		SetUser("standings-manager");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "Standings Correction League"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = league!.LeagueId,
			UserId = "standings-member",
			Role = DataLeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		var createSeasonResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons", new CreateLeagueSeasonRequest
		{
			Name = "Season 1"
		});
		createSeasonResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var season = await createSeasonResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonResponse>(JsonOptions);
		season.Should().NotBeNull();

		var createEventResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{season!.SeasonId}/events", new CreateLeagueSeasonEventRequest
		{
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3)
		});
		createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var seasonEvent = await createEventResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonEventResponse>(JsonOptions);
		seasonEvent.Should().NotBeNull();

		var ingestResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{season.SeasonId}/events/{seasonEvent!.EventId}/results", new IngestLeagueSeasonEventResultsRequest
		{
			Results =
			[
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-manager",
					Placement = 1,
					Points = 10,
					ChipsDelta = 100
				},
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-member",
					Placement = 2,
					Points = 6,
					ChipsDelta = -100
				}
			]
		});

		ingestResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var correctionResponse = await Client.PutAsJsonAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{season.SeasonId}/events/{seasonEvent.EventId}/results/corrections", new CorrectLeagueSeasonEventResultsRequest
		{
			Reason = "Scoring spreadsheet correction",
			Results =
			[
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-manager",
					Placement = 2,
					Points = 6,
					ChipsDelta = -100
				},
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-member",
					Placement = 1,
					Points = 10,
					ChipsDelta = 100
				}
			]
		}, JsonOptions);

		correctionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var standingsResponse = await Client.GetAsync($"/api/v1/leagues/{league.LeagueId}/standings");
		standingsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var standings = await standingsResponse.Content.ReadFromJsonAsync<IReadOnlyList<LeagueStandingEntryDto>>(JsonOptions);
		standings.Should().NotBeNull();
		standings!.Count.Should().Be(2);
		standings[0].UserId.Should().Be("standings-member");
		standings[0].Rank.Should().Be(1);
		standings[0].TotalPoints.Should().Be(10);
		standings[1].UserId.Should().Be("standings-manager");
		standings[1].Rank.Should().Be(2);
		standings[1].TotalPoints.Should().Be(6);

		var auditRows = DbContext.LeagueSeasonEventResultCorrectionAudits
			.Where(x => x.LeagueSeasonEventId == seasonEvent.EventId)
			.ToList();

		auditRows.Should().HaveCount(1);
		auditRows[0].CorrectedByUserId.Should().Be("standings-manager");
		auditRows[0].Reason.Should().Be("Scoring spreadsheet correction");
		auditRows[0].PreviousResultsSnapshotJson.Should().Contain("standings-manager");
		auditRows[0].NewResultsSnapshotJson.Should().Contain("standings-member");
	}

	[Fact]
	public async Task GetStandings_CanBeScopedToSeason()
	{
		SetUser("standings-manager");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "Season Scoped Standings League"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = league!.LeagueId,
			UserId = "standings-member",
			Role = DataLeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		var createSeasonOneResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons", new CreateLeagueSeasonRequest
		{
			Name = "Season 1"
		});
		createSeasonOneResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var seasonOne = await createSeasonOneResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonResponse>(JsonOptions);
		seasonOne.Should().NotBeNull();

		var createSeasonTwoResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons", new CreateLeagueSeasonRequest
		{
			Name = "Season 2"
		});
		createSeasonTwoResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var seasonTwo = await createSeasonTwoResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonResponse>(JsonOptions);
		seasonTwo.Should().NotBeNull();

		var seasonOneEventResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{seasonOne!.SeasonId}/events", new CreateLeagueSeasonEventRequest
		{
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3)
		});
		seasonOneEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var seasonOneEvent = await seasonOneEventResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonEventResponse>(JsonOptions);
		seasonOneEvent.Should().NotBeNull();

		var seasonTwoEventResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{seasonTwo!.SeasonId}/events", new CreateLeagueSeasonEventRequest
		{
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(10)
		});
		seasonTwoEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var seasonTwoEvent = await seasonTwoEventResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonEventResponse>(JsonOptions);
		seasonTwoEvent.Should().NotBeNull();

		var seasonOneIngestResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{seasonOne.SeasonId}/events/{seasonOneEvent!.EventId}/results", new IngestLeagueSeasonEventResultsRequest
		{
			Results =
			[
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-manager",
					Placement = 1,
					Points = 10,
					ChipsDelta = 100
				},
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-member",
					Placement = 2,
					Points = 6,
					ChipsDelta = -100
				}
			]
		});
		seasonOneIngestResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var seasonTwoIngestResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{seasonTwo.SeasonId}/events/{seasonTwoEvent!.EventId}/results", new IngestLeagueSeasonEventResultsRequest
		{
			Results =
			[
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-manager",
					Placement = 2,
					Points = 4,
					ChipsDelta = -50
				},
				new LeagueSeasonEventResultEntryRequest
				{
					MemberUserId = "standings-member",
					Placement = 1,
					Points = 12,
					ChipsDelta = 50
				}
			]
		});
		seasonTwoIngestResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var seasonOneStandingsResponse = await Client.GetAsync($"/api/v1/leagues/{league.LeagueId}/standings?seasonId={seasonOne.SeasonId}");
		seasonOneStandingsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var seasonOneStandings = await seasonOneStandingsResponse.Content.ReadFromJsonAsync<IReadOnlyList<LeagueStandingEntryDto>>(JsonOptions);
		seasonOneStandings.Should().NotBeNull();
		seasonOneStandings![0].UserId.Should().Be("standings-manager");
		seasonOneStandings[0].TotalPoints.Should().Be(10);
		seasonOneStandings[1].UserId.Should().Be("standings-member");
		seasonOneStandings[1].TotalPoints.Should().Be(6);

		var seasonTwoStandingsResponse = await Client.GetAsync($"/api/v1/leagues/{league.LeagueId}/standings?seasonId={seasonTwo.SeasonId}");
		seasonTwoStandingsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var seasonTwoStandings = await seasonTwoStandingsResponse.Content.ReadFromJsonAsync<IReadOnlyList<LeagueStandingEntryDto>>(JsonOptions);
		seasonTwoStandings.Should().NotBeNull();
		seasonTwoStandings![0].UserId.Should().Be("standings-member");
		seasonTwoStandings[0].TotalPoints.Should().Be(12);
		seasonTwoStandings[1].UserId.Should().Be("standings-manager");
		seasonTwoStandings[1].TotalPoints.Should().Be(4);
	}

	private void SetUser(string userId)
	{
		Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
		Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
	}
}
