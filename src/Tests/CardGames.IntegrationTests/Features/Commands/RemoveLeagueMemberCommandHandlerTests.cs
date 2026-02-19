using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.RemoveLeagueMember;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class RemoveLeagueMemberCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_RemovesActiveMember_AndWritesAuditEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-manager";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Remove Member League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "league-member",
			Role = LeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});

		await DbContext.SaveChangesAsync();

		var removeResult = await Mediator.Send(new RemoveLeagueMemberCommand(leagueId, "league-member"));

		removeResult.IsT0.Should().BeTrue();

		var membership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == leagueId && x.UserId == "league-member");

		membership.IsActive.Should().BeFalse();
		membership.LeftAtUtc.Should().NotBeNull();

		var removeEvent = await DbContext.LeagueMembershipEvents
			.AsNoTracking()
			.OrderByDescending(x => x.OccurredAtUtc)
			.FirstAsync(x => x.LeagueId == leagueId && x.UserId == "league-member");

		removeEvent.ActorUserId.Should().Be("league-manager");
		removeEvent.EventType.Should().Be(LeagueMembershipEventType.MemberLeft);
	}

	[Fact]
	public async Task Handle_FailsWhenRemovingLastManager()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-manager";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Last Manager Safety League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "league-admin",
			Role = LeagueRole.Admin,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});

		await DbContext.SaveChangesAsync();

		fakeCurrentUser.UserId = "league-admin";
		var removeResult = await Mediator.Send(new RemoveLeagueMemberCommand(leagueId, "league-manager"));

		removeResult.IsT1.Should().BeTrue();
		removeResult.AsT1.Code.Should().Be(RemoveLeagueMemberErrorCode.Conflict);

		var managerMembership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == leagueId && x.UserId == "league-manager");

		managerMembership.IsActive.Should().BeTrue();
		managerMembership.Role.Should().Be(LeagueRole.Owner);
	}
}
