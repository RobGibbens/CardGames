using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data.Entities;
using FluentAssertions;
using ContractsLeagueRole = CardGames.Poker.Api.Contracts.LeagueRole;
using DataLeagueRole = CardGames.Poker.Api.Data.Entities.LeagueRole;

namespace CardGames.IntegrationTests.Api;

public class LeaguesApiGovernanceTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters =
		{
			new JsonStringEnumConverter()
		}
	};

	[Fact]
	public async Task TransferOwnership_Manager_Succeeds_AndAuditsHistory()
	{
		SetUser("league-owner");
		var leagueId = await CreateLeagueAsync("Governance Transfer League");

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "league-target-member",
			Role = DataLeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		var transferResponse = await Client.PostAsync($"/api/v1/leagues/{leagueId}/members/league-target-member/transfer-ownership", null);
		transferResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var membersResponse = await Client.GetFromJsonAsync<IReadOnlyList<LeagueMemberDto>>($"/api/v1/leagues/{leagueId}/members", JsonOptions);
		membersResponse.Should().NotBeNull();
		membersResponse!.Single(x => x.UserId == "league-owner").Role.Should().Be(ContractsLeagueRole.Manager);
		membersResponse.Single(x => x.UserId == "league-target-member").Role.Should().Be(ContractsLeagueRole.Owner);

		var historyResponse = await Client.GetFromJsonAsync<IReadOnlyList<LeagueMembershipHistoryItemDto>>($"/api/v1/leagues/{leagueId}/members/history", JsonOptions);
		historyResponse.Should().NotBeNull();
		historyResponse!.Should().Contain(x =>
			x.UserId == "league-target-member" &&
			x.ActorUserId == "league-owner" &&
			x.EventType == LeagueMembershipHistoryEventType.LeagueOwnershipTransferred);
	}

	[Fact]
	public async Task TransferOwnership_AdminButNotManager_IsForbidden()
	{
		SetUser("league-owner-a");
		var leagueId = await CreateLeagueAsync("Governance Forbidden Transfer League");

		DbContext.LeagueMembersCurrent.AddRange(
			new LeagueMemberCurrent
			{
				LeagueId = leagueId,
				UserId = "league-admin",
				Role = DataLeagueRole.Admin,
				IsActive = true,
				JoinedAtUtc = DateTimeOffset.UtcNow,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			},
			new LeagueMemberCurrent
			{
				LeagueId = leagueId,
				UserId = "league-member",
				Role = DataLeagueRole.Member,
				IsActive = true,
				JoinedAtUtc = DateTimeOffset.UtcNow,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			});
		await DbContext.SaveChangesAsync();

		SetUser("league-admin");
		var transferResponse = await Client.PostAsync($"/api/v1/leagues/{leagueId}/members/league-member/transfer-ownership", null);
		transferResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task DemoteAdmin_WhenOnlyGovernanceCapableMember_Remains_ReturnsConflict()
	{
		SetUser("league-admin-only");

		var league = new League
		{
			Id = Guid.CreateVersion7(),
			Name = "Demote Conflict League",
			CreatedByUserId = "league-admin-only",
			CreatedAtUtc = DateTimeOffset.UtcNow,
			IsArchived = false
		};

		DbContext.Leagues.Add(league);
		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = league.Id,
			UserId = "league-admin-only",
			Role = DataLeagueRole.Admin,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		var demoteResponse = await Client.PostAsync($"/api/v1/leagues/{league.Id}/members/league-admin-only/demote-admin", null);
		demoteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task RemoveMember_NonGovernanceMember_IsForbidden()
	{
		SetUser("league-owner-b");
		var leagueId = await CreateLeagueAsync("Governance Remove Forbidden League");

		DbContext.LeagueMembersCurrent.AddRange(
			new LeagueMemberCurrent
			{
				LeagueId = leagueId,
				UserId = "league-member-actor",
				Role = DataLeagueRole.Member,
				IsActive = true,
				JoinedAtUtc = DateTimeOffset.UtcNow,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			},
			new LeagueMemberCurrent
			{
				LeagueId = leagueId,
				UserId = "league-member-target",
				Role = DataLeagueRole.Member,
				IsActive = true,
				JoinedAtUtc = DateTimeOffset.UtcNow,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			});
		await DbContext.SaveChangesAsync();

		SetUser("league-member-actor");
		var removeResponse = await Client.PostAsync($"/api/v1/leagues/{leagueId}/members/league-member-target/remove", null);
		removeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task PromoteMember_AdminActor_Succeeds_AndAuditsHistory()
	{
		SetUser("league-owner-c");
		var leagueId = await CreateLeagueAsync("Governance Promote League");

		DbContext.LeagueMembersCurrent.AddRange(
			new LeagueMemberCurrent
			{
				LeagueId = leagueId,
				UserId = "league-admin-actor",
				Role = DataLeagueRole.Admin,
				IsActive = true,
				JoinedAtUtc = DateTimeOffset.UtcNow,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			},
			new LeagueMemberCurrent
			{
				LeagueId = leagueId,
				UserId = "league-member-promote-target",
				Role = DataLeagueRole.Member,
				IsActive = true,
				JoinedAtUtc = DateTimeOffset.UtcNow,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			});
		await DbContext.SaveChangesAsync();

		SetUser("league-admin-actor");
		var promoteResponse = await Client.PostAsync($"/api/v1/leagues/{leagueId}/members/league-member-promote-target/promote-admin", null);
		promoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var membersResponse = await Client.GetFromJsonAsync<IReadOnlyList<LeagueMemberDto>>($"/api/v1/leagues/{leagueId}/members", JsonOptions);
		membersResponse.Should().NotBeNull();
		membersResponse!.Single(x => x.UserId == "league-member-promote-target").Role.Should().Be(ContractsLeagueRole.Admin);

		var historyResponse = await Client.GetFromJsonAsync<IReadOnlyList<LeagueMembershipHistoryItemDto>>($"/api/v1/leagues/{leagueId}/members/history", JsonOptions);
		historyResponse.Should().NotBeNull();
		historyResponse!.Should().Contain(x =>
			x.UserId == "league-member-promote-target" &&
			x.ActorUserId == "league-admin-actor" &&
			x.EventType == LeagueMembershipHistoryEventType.MemberPromotedToAdmin);
	}

	private async Task<Guid> CreateLeagueAsync(string leagueName)
	{
		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = leagueName
		});

		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();
		return league!.LeagueId;
	}

	private void SetUser(string userId)
	{
		Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
		Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
	}
}
