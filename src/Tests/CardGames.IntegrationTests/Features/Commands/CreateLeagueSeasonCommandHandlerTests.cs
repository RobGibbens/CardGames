using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Commands;

public class CreateLeagueSeasonCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_AdminCanCreateSeason()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Season League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		var result = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CardGames.Poker.Api.Contracts.CreateLeagueSeasonRequest
		{
			Name = "Spring 2026",
			PlannedEventCount = 10
		}));

		result.IsT0.Should().BeTrue();
		result.AsT0.LeagueId.Should().Be(leagueId);
		result.AsT0.Name.Should().Be("Spring 2026");
	}
}