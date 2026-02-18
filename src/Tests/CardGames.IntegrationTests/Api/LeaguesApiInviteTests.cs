using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Contracts;
using FluentAssertions;

namespace CardGames.IntegrationTests.Api;

public class LeaguesApiInviteTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters =
		{
			new JsonStringEnumConverter()
		}
	};

	private const string InvalidInviteMessage = "Invite token is invalid.";
	private const string ExpiredInviteMessage = "Invite has expired.";
	private const string RevokedInviteMessage = "Invite has been revoked.";
	private const string JoinRequestNotFoundMessage = "Join request not found.";
	private const string AlreadyApprovedMessage = "Join request has already been approved.";
	private const string AlreadyDeniedMessage = "Join request has already been denied.";

	[Fact]
	public async Task RevokeInvite_Endpoint_RevokesActiveInvite()
	{
		SetUser("league-api-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API Revoke League"
		});

		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});

		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var revokeResponse = await Client.PostAsync($"/api/v1/leagues/{league.LeagueId}/invites/{invite!.InviteId}/revoke", content: null);
		revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var invitesResponse = await Client.GetFromJsonAsync<IReadOnlyList<LeagueInviteSummaryDto>>($"/api/v1/leagues/{league.LeagueId}/invites", JsonOptions);
		invitesResponse.Should().NotBeNull();
		invitesResponse!.Should().ContainSingle(x => x.InviteId == invite.InviteId && x.Status == CardGames.Poker.Api.Contracts.LeagueInviteStatus.Revoked);
	}

	[Fact]
	public async Task JoinByInviteAlias_Endpoint_JoinsLeague()
	{
		SetUser("league-api-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API Join Alias League"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var token = invite!.InviteUrl.Split('/').Last();
		SetUser("league-api-member");

		var joinResponse = await PostAsync("/api/v1/leagues/join-by-invite", new JoinLeagueRequest
		{
			Token = token
		});

		joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var joinResult = await joinResponse.Content.ReadFromJsonAsync<JoinLeagueResponse>(JsonOptions);
		joinResult.Should().NotBeNull();
		joinResult!.LeagueId.Should().Be(league.LeagueId);
		joinResult.Joined.Should().BeFalse();
		joinResult.RequestSubmitted.Should().BeTrue();
		joinResult.JoinRequestStatus.Should().Be(CardGames.Poker.Api.Contracts.LeagueJoinRequestStatus.Pending);
	}

	[Fact]
	public async Task JoinPreview_Endpoint_ReturnsTrustPreview_ForValidInvite()
	{
		SetUser("league-api-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API Join Preview League"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var token = invite!.InviteUrl.Split('/').Last();
		SetUser("league-api-member");

		var previewResponse = await Client.GetAsync($"/api/v1/leagues/join-preview?token={Uri.EscapeDataString(token)}");
		previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

		var preview = await previewResponse.Content.ReadFromJsonAsync<LeagueJoinPreviewDto>(JsonOptions);
		preview.Should().NotBeNull();
		preview!.LeagueId.Should().Be(league.LeagueId);
		preview.LeagueName.Should().Be("API Join Preview League");
		preview.ActiveMemberCount.Should().BeGreaterThanOrEqualTo(1);
		preview.ManagerDisplayName.Should().NotBeNullOrWhiteSpace();
		preview.JoinPolicy.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task JoinPreview_Endpoint_ReturnsBadRequest_ForInvalidInvite()
	{
		SetUser("league-api-member");

		var response = await Client.GetAsync("/api/v1/leagues/join-preview?token=invalid-token");

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Join_Endpoint_IsIdempotent_ForDuplicateSubmissions()
	{
		SetUser("league-api-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API Join Idempotent League"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var token = invite!.InviteUrl.Split('/').Last();
		SetUser("league-api-member");

		var firstResponse = await PostAsync("/api/v1/leagues/join", new JoinLeagueRequest
		{
			Token = token
		});

		var secondResponse = await PostAsync("/api/v1/leagues/join", new JoinLeagueRequest
		{
			Token = token
		});

		firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

		var firstResult = await firstResponse.Content.ReadFromJsonAsync<JoinLeagueResponse>(JsonOptions);
		var secondResult = await secondResponse.Content.ReadFromJsonAsync<JoinLeagueResponse>(JsonOptions);

		firstResult.Should().NotBeNull();
		secondResult.Should().NotBeNull();

		firstResult!.Joined.Should().BeFalse();
		firstResult.AlreadyMember.Should().BeFalse();
		firstResult.RequestSubmitted.Should().BeTrue();
		firstResult.JoinRequestStatus.Should().Be(CardGames.Poker.Api.Contracts.LeagueJoinRequestStatus.Pending);
		secondResult!.Joined.Should().BeFalse();
		secondResult.AlreadyMember.Should().BeFalse();
		secondResult.RequestSubmitted.Should().BeTrue();
		secondResult.JoinRequestStatus.Should().Be(CardGames.Poker.Api.Contracts.LeagueJoinRequestStatus.Pending);
		secondResult.JoinRequestId.Should().Be(firstResult.JoinRequestId);

		var activeMembershipCount = DbContext.LeagueMembersCurrent
			.Count(x => x.LeagueId == league.LeagueId && x.UserId == "league-api-member" && x.IsActive);

		activeMembershipCount.Should().Be(0);
	}

	[Fact]
	public async Task RevokeInvite_Endpoint_ReturnsForbidden_ForNonAdminMember()
	{
		SetUser("league-api-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API Revoke Auth League"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var token = invite!.InviteUrl.Split('/').Last();
		SetUser("league-api-member");
		var joinResponse = await PostAsync("/api/v1/leagues/join", new JoinLeagueRequest
		{
			Token = token
		});
		joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

		var revokeResponse = await Client.PostAsync($"/api/v1/leagues/{league.LeagueId}/invites/{invite.InviteId}/revoke", content: null);
		revokeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task RevokeInvite_Endpoint_IsResourceScoped_ForAdminInDifferentLeague()
	{
		SetUser("league-api-admin-a");
		var createLeagueAResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API League A"
		});
		createLeagueAResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var leagueA = await createLeagueAResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		leagueA.Should().NotBeNull();

		var createInviteAResponse = await PostAsync($"/api/v1/leagues/{leagueA!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteAResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var inviteA = await createInviteAResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		inviteA.Should().NotBeNull();

		SetUser("league-api-admin-b");
		var createLeagueBResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API League B"
		});
		createLeagueBResponse.StatusCode.Should().Be(HttpStatusCode.Created);

		var revokeResponse = await Client.PostAsync($"/api/v1/leagues/{leagueA.LeagueId}/invites/{inviteA!.InviteId}/revoke", content: null);

		revokeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Join_Endpoint_ReturnsBadRequest_ForInvalidToken()
	{
		SetUser("league-api-member");

		var response = await PostAsync("/api/v1/leagues/join", new JoinLeagueRequest
		{
			Token = "not-a-real-token"
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await ReadErrorMessageAsync(response)).Should().Be(InvalidInviteMessage);
	}

	[Fact]
	public async Task Join_Endpoint_ReturnsBadRequest_ForExpiredInvite()
	{
		SetUser("league-api-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API Expired Invite League"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var storedInvite = await DbContext.LeagueInvites.FindAsync(invite!.InviteId);
		storedInvite.Should().NotBeNull();
		storedInvite!.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
		await DbContext.SaveChangesAsync();

		var token = invite.InviteUrl.Split('/').Last();
		SetUser("league-api-member");

		var response = await PostAsync("/api/v1/leagues/join", new JoinLeagueRequest
		{
			Token = token
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await ReadErrorMessageAsync(response)).Should().Be(ExpiredInviteMessage);
	}

	[Fact]
	public async Task Join_Endpoint_ReturnsBadRequest_ForRevokedInvite()
	{
		SetUser("league-api-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API Revoked Invite League"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var revokeResponse = await Client.PostAsync($"/api/v1/leagues/{league.LeagueId}/invites/{invite!.InviteId}/revoke", content: null);
		revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var token = invite.InviteUrl.Split('/').Last();
		SetUser("league-api-member");

		var response = await PostAsync("/api/v1/leagues/join", new JoinLeagueRequest
		{
			Token = token
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await ReadErrorMessageAsync(response)).Should().Be(RevokedInviteMessage);
	}

	[Fact]
	public async Task Journey_JoinByCode_WithPreview_ThenSubmit_ThenAdminApprove_ActivatesMembership()
	{
		SetUser("league-journey-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "League Journey Approve"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var token = invite!.InviteUrl.Split('/').Last();

		SetUser("league-journey-member");
		var previewResponse = await Client.GetAsync($"/api/v1/leagues/join-preview?token={Uri.EscapeDataString(token)}");
		previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var preview = await previewResponse.Content.ReadFromJsonAsync<LeagueJoinPreviewDto>(JsonOptions);
		preview.Should().NotBeNull();
		preview!.LeagueId.Should().Be(league.LeagueId);
		preview.LeagueName.Should().Be("League Journey Approve");
		preview.ManagerDisplayName.Should().NotBeNullOrWhiteSpace();

		var joinResponse = await PostAsync("/api/v1/leagues/join-by-invite", new JoinLeagueRequest
		{
			Token = token
		});
		joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var join = await joinResponse.Content.ReadFromJsonAsync<JoinLeagueResponse>(JsonOptions);
		join.Should().NotBeNull();
		join!.RequestSubmitted.Should().BeTrue();
		join.JoinRequestStatus.Should().Be(CardGames.Poker.Api.Contracts.LeagueJoinRequestStatus.Pending);
		join.JoinRequestId.Should().NotBeNull();

		SetUser("league-journey-admin");
		var pendingResponse = await Client.GetAsync($"/api/v1/leagues/{league.LeagueId}/join-requests/pending");
		pendingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var pending = await pendingResponse.Content.ReadFromJsonAsync<IReadOnlyList<LeagueJoinRequestQueueItemDto>>(JsonOptions);
		pending.Should().NotBeNull();
		pending!.Should().ContainSingle(x => x.JoinRequestId == join.JoinRequestId!.Value && x.RequesterUserId == "league-journey-member");

		var approveResponse = await PostAsync(
			$"/api/v1/leagues/{league.LeagueId}/join-requests/{join.JoinRequestId.Value}/approve",
			new ModerateLeagueJoinRequestRequest { Reason = "Approved for league play" });
		approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var membership = await DbContext.LeagueMembersCurrent.FindAsync(league.LeagueId, "league-journey-member");
		membership.Should().NotBeNull();
		membership!.IsActive.Should().BeTrue();

		SetUser("league-journey-member");
		var joinAgainResponse = await PostAsync("/api/v1/leagues/join", new JoinLeagueRequest
		{
			Token = token
		});
		joinAgainResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var joinAgain = await joinAgainResponse.Content.ReadFromJsonAsync<JoinLeagueResponse>(JsonOptions);
		joinAgain.Should().NotBeNull();
		joinAgain!.AlreadyMember.Should().BeTrue();
		joinAgain.RequestSubmitted.Should().BeFalse();
		joinAgain.JoinRequestStatus.Should().Be(CardGames.Poker.Api.Contracts.LeagueJoinRequestStatus.Approved);
	}

	[Fact]
	public async Task Journey_AdminDeny_IsIdempotent_AndDoesNotCreateMembership()
	{
		SetUser("league-deny-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "League Journey Deny"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var token = invite!.InviteUrl.Split('/').Last();

		SetUser("league-deny-member");
		var joinResponse = await PostAsync("/api/v1/leagues/join", new JoinLeagueRequest
		{
			Token = token
		});
		joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var join = await joinResponse.Content.ReadFromJsonAsync<JoinLeagueResponse>(JsonOptions);
		join.Should().NotBeNull();
		join!.JoinRequestId.Should().NotBeNull();

		SetUser("league-deny-admin");
		var denyResponse = await PostAsync(
			$"/api/v1/leagues/{league.LeagueId}/join-requests/{join.JoinRequestId!.Value}/deny",
			new ModerateLeagueJoinRequestRequest { Reason = "Denied for testing" });
		denyResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var denyAgainResponse = await PostAsync(
			$"/api/v1/leagues/{league.LeagueId}/join-requests/{join.JoinRequestId.Value}/deny",
			new ModerateLeagueJoinRequestRequest { Reason = "Duplicate deny" });
		denyAgainResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var membership = await DbContext.LeagueMembersCurrent.FindAsync(league.LeagueId, "league-deny-member");
		membership.Should().BeNull();

		var joinRequest = await DbContext.LeagueJoinRequests.FindAsync(join.JoinRequestId.Value);
		joinRequest.Should().NotBeNull();
		joinRequest!.Status.Should().Be(CardGames.Poker.Api.Data.Entities.LeagueJoinRequestStatus.Denied);
	}

	[Fact]
	public async Task Moderation_Endpoints_EnforceAuthorization_ForNonGovernanceMembers()
	{
		SetUser("league-authz-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "League Authz"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var token = invite!.InviteUrl.Split('/').Last();

		SetUser("league-authz-member");
		var joinResponse = await PostAsync("/api/v1/leagues/join", new JoinLeagueRequest
		{
			Token = token
		});
		joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var join = await joinResponse.Content.ReadFromJsonAsync<JoinLeagueResponse>(JsonOptions);
		join.Should().NotBeNull();
		join!.JoinRequestId.Should().NotBeNull();

		var pendingAsRequester = await Client.GetAsync($"/api/v1/leagues/{league.LeagueId}/join-requests/pending");
		pendingAsRequester.StatusCode.Should().Be(HttpStatusCode.Forbidden);

		var approveAsRequester = await PostAsync(
			$"/api/v1/leagues/{league.LeagueId}/join-requests/{join.JoinRequestId!.Value}/approve",
			new ModerateLeagueJoinRequestRequest { Reason = "Should fail" });
		approveAsRequester.StatusCode.Should().Be(HttpStatusCode.Forbidden);

		var denyAsRequester = await PostAsync(
			$"/api/v1/leagues/{league.LeagueId}/join-requests/{join.JoinRequestId.Value}/deny",
			new ModerateLeagueJoinRequestRequest { Reason = "Should fail" });
		denyAsRequester.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Moderation_Endpoints_ReturnExpectedErrorSemantics_ForMissingAndInvalidStates()
	{
		SetUser("league-errors-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "League Error Semantics"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var missingApproveResponse = await PostAsync(
			$"/api/v1/leagues/{league!.LeagueId}/join-requests/{Guid.NewGuid()}/approve",
			new ModerateLeagueJoinRequestRequest { Reason = "Missing request" });
		missingApproveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await ReadErrorMessageAsync(missingApproveResponse)).Should().Be(JoinRequestNotFoundMessage);

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();
		var token = invite!.InviteUrl.Split('/').Last();

		SetUser("league-errors-member");
		var joinResponse = await PostAsync("/api/v1/leagues/join", new JoinLeagueRequest
		{
			Token = token
		});
		joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var join = await joinResponse.Content.ReadFromJsonAsync<JoinLeagueResponse>(JsonOptions);
		join.Should().NotBeNull();
		join!.JoinRequestId.Should().NotBeNull();

		SetUser("league-errors-admin");
		var approveResponse = await PostAsync(
			$"/api/v1/leagues/{league.LeagueId}/join-requests/{join.JoinRequestId!.Value}/approve",
			new ModerateLeagueJoinRequestRequest { Reason = "Approve first" });
		approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var denyApprovedResponse = await PostAsync(
			$"/api/v1/leagues/{league.LeagueId}/join-requests/{join.JoinRequestId.Value}/deny",
			new ModerateLeagueJoinRequestRequest { Reason = "Now invalid" });
		denyApprovedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await ReadErrorMessageAsync(denyApprovedResponse)).Should().Be(AlreadyApprovedMessage);

		SetUser("league-errors-member-2");
		var joinResponse2 = await PostAsync("/api/v1/leagues/join", new JoinLeagueRequest
		{
			Token = token
		});
		joinResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
		var join2 = await joinResponse2.Content.ReadFromJsonAsync<JoinLeagueResponse>(JsonOptions);
		join2.Should().NotBeNull();
		join2!.JoinRequestId.Should().NotBeNull();

		SetUser("league-errors-admin");
		var denyResponse = await PostAsync(
			$"/api/v1/leagues/{league.LeagueId}/join-requests/{join2.JoinRequestId!.Value}/deny",
			new ModerateLeagueJoinRequestRequest { Reason = "Deny first" });
		denyResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var approveDeniedResponse = await PostAsync(
			$"/api/v1/leagues/{league.LeagueId}/join-requests/{join2.JoinRequestId.Value}/approve",
			new ModerateLeagueJoinRequestRequest { Reason = "Now invalid" });
		approveDeniedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await ReadErrorMessageAsync(approveDeniedResponse)).Should().Be(AlreadyDeniedMessage);
	}

	private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response)
	{
		var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

		if (body.ValueKind == JsonValueKind.Object)
		{
			if (body.TryGetProperty("message", out var message))
			{
				return message.GetString();
			}

			if (body.TryGetProperty("Message", out var messagePascalCase))
			{
				return messagePascalCase.GetString();
			}
		}

		return null;
	}

	private void SetUser(string userId)
	{
		Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
		Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
	}
}
