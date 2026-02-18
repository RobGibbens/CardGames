using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueInvite;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Commands;

public class CreateLeagueInviteCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_ManagerCanCreateInvite()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Invite League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		var inviteResult = await Mediator.Send(new CreateLeagueInviteCommand(leagueId, new CardGames.Poker.Api.Contracts.CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		}));

		inviteResult.IsT0.Should().BeTrue();
		inviteResult.AsT0.LeagueId.Should().Be(leagueId);
		inviteResult.AsT0.InviteUrl.Should().StartWith("/leagues/join/");
	}
}