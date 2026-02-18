using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueMembershipHistory;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Queries;

public class GetLeagueMembershipHistoryQueryHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_ReturnsMembershipEventsForActiveMember()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-history-user";
		fakeCurrentUser.IsAuthenticated = true;

		var createResult = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "History Query League"
		}));

		createResult.IsT0.Should().BeTrue();

		var queryResult = await Mediator.Send(new GetLeagueMembershipHistoryQuery(createResult.AsT0.LeagueId));
		queryResult.IsT0.Should().BeTrue();
		queryResult.AsT0.Should().ContainSingle(x =>
			x.UserId == "league-history-user" &&
			x.ActorUserId == "league-history-user" &&
			x.EventType == CardGames.Poker.Api.Contracts.LeagueMembershipHistoryEventType.MemberJoined);
	}

	[Fact]
	public async Task Handle_ReturnsForbiddenForNonMember()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-history-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createResult = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "History Forbidden League"
		}));

		createResult.IsT0.Should().BeTrue();

		fakeCurrentUser.UserId = "league-history-outsider";
		var queryResult = await Mediator.Send(new GetLeagueMembershipHistoryQuery(createResult.AsT0.LeagueId));

		queryResult.IsT1.Should().BeTrue();
		queryResult.AsT1.Code.Should().Be(GetLeagueMembershipHistoryErrorCode.Forbidden);
	}
}
