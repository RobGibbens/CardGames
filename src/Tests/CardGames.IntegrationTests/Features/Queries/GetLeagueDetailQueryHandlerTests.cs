using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueDetail;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Queries;

public class GetLeagueDetailQueryHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_ReturnsDetailForActiveMember()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-detail-query-user";
		fakeCurrentUser.IsAuthenticated = true;

		var createResult = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Detail Query League"
		}));

		createResult.IsT0.Should().BeTrue();

		var queryResult = await Mediator.Send(new GetLeagueDetailQuery(createResult.AsT0.LeagueId));
		queryResult.IsT0.Should().BeTrue();
		queryResult.AsT0.LeagueId.Should().Be(createResult.AsT0.LeagueId);
		queryResult.AsT0.Name.Should().Be("Detail Query League");
		queryResult.AsT0.CreatedByDisplayName.Should().Be("league-detail-query-user");
	}
}