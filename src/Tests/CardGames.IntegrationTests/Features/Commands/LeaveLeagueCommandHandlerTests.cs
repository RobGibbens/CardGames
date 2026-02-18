using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.LeaveLeague;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class LeaveLeagueCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_FailsWhenManagerWouldBeLastManager()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-manager";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Manager Safety League"
		}));

		var leaveResult = await Mediator.Send(new LeaveLeagueCommand(createLeague.AsT0.LeagueId));

		leaveResult.IsT1.Should().BeTrue();
		leaveResult.AsT1.Code.Should().Be(LeaveLeagueErrorCode.Conflict);

		var membership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == createLeague.AsT0.LeagueId && x.UserId == "league-manager");

		membership.IsActive.Should().BeTrue();
	}

	[Fact]
	public async Task Handle_FailsWhenAdminWouldLeaveNoGovernanceCapableMember()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "sole-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var league = new League
		{
			Id = Guid.CreateVersion7(),
			Name = "Admin Safety League",
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

		var leaveResult = await Mediator.Send(new LeaveLeagueCommand(league.Id));

		leaveResult.IsT1.Should().BeTrue();
		leaveResult.AsT1.Code.Should().Be(LeaveLeagueErrorCode.Conflict);

		var membership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == league.Id && x.UserId == "sole-admin");

		membership.IsActive.Should().BeTrue();
	}
}
