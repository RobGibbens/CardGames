using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueInvite;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.JoinLeague;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class JoinLeagueCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_JoinsLeagueAndIsIdempotentForActiveMember()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();

		fakeCurrentUser.UserId = "league-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Join League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;
		var createInvite = await Mediator.Send(new CreateLeagueInviteCommand(leagueId, new CardGames.Poker.Api.Contracts.CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		}));

		var token = createInvite.AsT0.InviteUrl.Split('/').Last();

		fakeCurrentUser.UserId = "league-member";

		var firstJoin = await Mediator.Send(new JoinLeagueCommand(new CardGames.Poker.Api.Contracts.JoinLeagueRequest { Token = token }));
		var secondJoin = await Mediator.Send(new JoinLeagueCommand(new CardGames.Poker.Api.Contracts.JoinLeagueRequest { Token = token }));

		firstJoin.IsT0.Should().BeTrue();
		firstJoin.AsT0.Joined.Should().BeTrue();

		secondJoin.IsT0.Should().BeTrue();
		secondJoin.AsT0.AlreadyMember.Should().BeTrue();

		var activeMemberships = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == leagueId && x.UserId == "league-member" && x.IsActive)
			.ToListAsync();

		activeMemberships.Should().HaveCount(1);
		activeMemberships[0].Role.Should().Be(LeagueRole.Member);
	}
}