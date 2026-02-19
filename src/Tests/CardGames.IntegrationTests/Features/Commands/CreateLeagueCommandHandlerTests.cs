using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class CreateLeagueCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_CreatesLeagueCreatorMembershipAndJoinedEvent()
	{
		var currentUser = Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		currentUser.Should().BeOfType<FakeCurrentUserService>();

		var fakeCurrentUser = (FakeCurrentUserService)currentUser;
		fakeCurrentUser.UserId = "league-user-1";
		fakeCurrentUser.UserName = "league-user-1@example.com";
		fakeCurrentUser.UserEmail = "league-user-1@example.com";
		fakeCurrentUser.IsAuthenticated = true;

		var command = new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Weekend League",
			Description = "Saturday games"
		});

		var result = await Mediator.Send(command);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;

		var league = await DbContext.Leagues
			.AsNoTracking()
			.SingleAsync(x => x.Id == success.LeagueId);

		league.Name.Should().Be("Weekend League");
		league.CreatedByUserId.Should().Be("league-user-1");

		var member = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == success.LeagueId && x.UserId == "league-user-1");

		member.IsActive.Should().BeTrue();
		member.Role.Should().Be(LeagueRole.Owner);

		var events = await DbContext.LeagueMembershipEvents
			.AsNoTracking()
			.Where(x => x.LeagueId == success.LeagueId)
			.ToListAsync();

		events.Should().HaveCount(1);
		events[0].UserId.Should().Be("league-user-1");
		events[0].ActorUserId.Should().Be("league-user-1");
		events[0].EventType.Should().Be(LeagueMembershipEventType.MemberJoined);
	}
}
