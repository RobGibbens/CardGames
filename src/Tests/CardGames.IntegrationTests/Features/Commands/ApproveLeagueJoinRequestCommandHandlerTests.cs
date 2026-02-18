using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.ApproveLeagueJoinRequest;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueInvite;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.JoinLeague;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class ApproveLeagueJoinRequestCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_MemberSubmitsJoinRequest_AdminApproves_MemberBecomesActive()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();

		fakeCurrentUser.UserId = "league-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Approval League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;
		var createInvite = await Mediator.Send(new CreateLeagueInviteCommand(leagueId, new CardGames.Poker.Api.Contracts.CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		}));

		var token = createInvite.AsT0.InviteUrl.Split('/').Last();

		fakeCurrentUser.UserId = "league-member";
		var join = await Mediator.Send(new JoinLeagueCommand(new CardGames.Poker.Api.Contracts.JoinLeagueRequest { Token = token }));

		join.IsT0.Should().BeTrue();
		join.AsT0.RequestSubmitted.Should().BeTrue();
		join.AsT0.JoinRequestStatus.Should().Be(CardGames.Poker.Api.Contracts.LeagueJoinRequestStatus.Pending);
		join.AsT0.JoinRequestId.Should().NotBeNull();

		var joinRequestId = join.AsT0.JoinRequestId!.Value;

		var joinRequestBefore = await DbContext.LeagueJoinRequests
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == leagueId && x.Id == joinRequestId);

		joinRequestBefore.Status.Should().Be(LeagueJoinRequestStatus.Pending);

		fakeCurrentUser.UserId = "league-admin";
		var approve = await Mediator.Send(new ApproveLeagueJoinRequestCommand(
			leagueId,
			joinRequestId,
			new CardGames.Poker.Api.Contracts.ModerateLeagueJoinRequestRequest { Reason = "Welcome" }));

		approve.IsT0.Should().BeTrue();

		var membership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == leagueId && x.UserId == "league-member");

		membership.IsActive.Should().BeTrue();
		membership.Role.Should().Be(LeagueRole.Member);

		var joinRequestAfter = await DbContext.LeagueJoinRequests
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == leagueId && x.Id == joinRequestId);

		joinRequestAfter.Status.Should().Be(LeagueJoinRequestStatus.Approved);

		fakeCurrentUser.UserId = "league-member";
		var joinAgain = await Mediator.Send(new JoinLeagueCommand(new CardGames.Poker.Api.Contracts.JoinLeagueRequest { Token = token }));

		joinAgain.IsT0.Should().BeTrue();
		joinAgain.AsT0.AlreadyMember.Should().BeTrue();
		joinAgain.AsT0.JoinRequestStatus.Should().Be(CardGames.Poker.Api.Contracts.LeagueJoinRequestStatus.Approved);
		joinAgain.AsT0.RequestSubmitted.Should().BeFalse();
	}
}
