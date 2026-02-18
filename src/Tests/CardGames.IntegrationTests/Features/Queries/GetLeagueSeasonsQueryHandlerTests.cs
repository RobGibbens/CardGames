using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasons;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Queries;

public class GetLeagueSeasonsQueryHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_ReturnsSeasonsForActiveMember()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-query-user";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Season Query League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CardGames.Poker.Api.Contracts.CreateLeagueSeasonRequest
		{
			Name = "Autumn 2026"
		}));

		createSeason.IsT0.Should().BeTrue();

		var queryResult = await Mediator.Send(new GetLeagueSeasonsQuery(leagueId));
		queryResult.IsT0.Should().BeTrue();
		queryResult.AsT0.Should().ContainSingle(x => x.SeasonId == createSeason.AsT0.SeasonId);
	}
}