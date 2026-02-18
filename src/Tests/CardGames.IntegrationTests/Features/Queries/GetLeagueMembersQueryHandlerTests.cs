using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueMembers;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Queries;

public class GetLeagueMembersQueryHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_ReturnsMembersForActiveMember()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-member-query-user";
		fakeCurrentUser.IsAuthenticated = true;

		var createResult = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Member Query League"
		}));

		createResult.IsT0.Should().BeTrue();

		var queryResult = await Mediator.Send(new GetLeagueMembersQuery(createResult.AsT0.LeagueId));
		queryResult.IsT0.Should().BeTrue();
		queryResult.AsT0.Should().ContainSingle(x => x.UserId == "league-member-query-user");
	}
}