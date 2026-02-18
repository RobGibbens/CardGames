using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.TransferLeagueOwnership;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class TransferLeagueOwnershipCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_ManagerTransfersOwnershipToActiveMember()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-manager";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Ownership Transfer League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "target-member",
			Role = LeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});

		await DbContext.SaveChangesAsync();

		var transferResult = await Mediator.Send(new TransferLeagueOwnershipCommand(leagueId, "target-member"));

		transferResult.IsT0.Should().BeTrue();

		var managerMembership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == leagueId && x.UserId == "league-manager");

		var targetMembership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == leagueId && x.UserId == "target-member");

		managerMembership.Role.Should().Be(LeagueRole.Admin);
		targetMembership.Role.Should().Be(LeagueRole.Manager);

		var transferEvent = await DbContext.LeagueMembershipEvents
			.AsNoTracking()
			.OrderByDescending(x => x.OccurredAtUtc)
			.FirstAsync(x => x.LeagueId == leagueId && x.UserId == "target-member");

		transferEvent.ActorUserId.Should().Be("league-manager");
		transferEvent.EventType.Should().Be((LeagueMembershipEventType)5);
	}
}
