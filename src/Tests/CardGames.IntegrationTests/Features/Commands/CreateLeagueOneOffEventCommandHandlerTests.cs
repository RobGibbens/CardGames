using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueOneOffEvent;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Commands;

public class CreateLeagueOneOffEventCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_AdminCanCreateOneOffEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-one-off-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "OneOff League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		var result = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CardGames.Poker.Api.Contracts.CreateLeagueOneOffEventRequest
		{
			Name = "Friday Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3),
			EventType = CardGames.Poker.Api.Contracts.LeagueOneOffEventType.CashGame
		}));

		result.IsT0.Should().BeTrue();
		result.AsT0.LeagueId.Should().Be(leagueId);
		result.AsT0.Name.Should().Be("Friday Night");
	}
}