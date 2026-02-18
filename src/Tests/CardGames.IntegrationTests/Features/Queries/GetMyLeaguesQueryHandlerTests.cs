using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetMyLeagues;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CardGames.IntegrationTests.Features.Queries;

public class GetMyLeaguesQueryHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_IncludesLeagueCreatedByCurrentUser()
	{
		var currentUser = Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		currentUser.Should().BeOfType<FakeCurrentUserService>();

		var fakeCurrentUser = (FakeCurrentUserService)currentUser;
		fakeCurrentUser.UserId = "league-user-2";
		fakeCurrentUser.UserName = "league-user-2@example.com";
		fakeCurrentUser.UserEmail = "league-user-2@example.com";
		fakeCurrentUser.IsAuthenticated = true;

		var createResult = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Home Game League",
			Description = "Sunday league"
		}));

		createResult.IsT0.Should().BeTrue();

		var queryResult = await Mediator.Send(new GetMyLeaguesQuery());

		queryResult.IsT0.Should().BeTrue();
		var leagues = queryResult.AsT0;
		leagues.Should().ContainSingle(x => x.Name == "Home Game League");
	}
}