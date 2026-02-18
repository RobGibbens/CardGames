using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.DemoteLeagueAdminToMember;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class DemoteLeagueAdminToMemberCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_DemotesAdminToMember_WhenGovernanceStillRetained()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-manager";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Demote Admin League"
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

		var demoteResult = await Mediator.Send(new DemoteLeagueAdminToMemberCommand(leagueId, "league-admin"));

		demoteResult.IsT0.Should().BeTrue();

		var membership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == leagueId && x.UserId == "league-admin");

		membership.Role.Should().Be(LeagueRole.Member);

		var demoteEvent = await DbContext.LeagueMembershipEvents
			.AsNoTracking()
			.OrderByDescending(x => x.OccurredAtUtc)
			.FirstAsync(x => x.LeagueId == leagueId && x.UserId == "league-admin");

		demoteEvent.ActorUserId.Should().Be("league-manager");
		demoteEvent.EventType.Should().Be((LeagueMembershipEventType)4);
	}

	[Fact]
	public async Task Handle_FailsWhenDemotionWouldLeaveNoGovernanceCapableMember()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "sole-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var league = new League
		{
			Id = Guid.CreateVersion7(),
			Name = "Sole Admin League",
			CreatedByUserId = "sole-admin",
			CreatedAtUtc = DateTimeOffset.UtcNow,
			IsArchived = false
		};

		DbContext.Leagues.Add(league);
		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = league.Id,
			UserId = "sole-admin",
			Role = LeagueRole.Admin,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});

		await DbContext.SaveChangesAsync();

		var demoteResult = await Mediator.Send(new DemoteLeagueAdminToMemberCommand(league.Id, "sole-admin"));

		demoteResult.IsT1.Should().BeTrue();
		demoteResult.AsT1.Code.Should().Be(DemoteLeagueAdminToMemberErrorCode.Conflict);

		var membership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == league.Id && x.UserId == "sole-admin");

		membership.Role.Should().Be(LeagueRole.Admin);
	}
}
