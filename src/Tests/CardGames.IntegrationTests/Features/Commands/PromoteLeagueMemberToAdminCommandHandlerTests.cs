using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueInvite;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.JoinLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.PromoteLeagueMemberToAdmin;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class PromoteLeagueMemberToAdminCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_AdminPromotesActiveMemberToAdmin()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();

		fakeCurrentUser.UserId = "league-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Promotion League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;
		var createInvite = await Mediator.Send(new CreateLeagueInviteCommand(leagueId, new CardGames.Poker.Api.Contracts.CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		}));

		var token = createInvite.AsT0.InviteUrl.Split('/').Last();

		fakeCurrentUser.UserId = "league-member";
		await Mediator.Send(new JoinLeagueCommand(new CardGames.Poker.Api.Contracts.JoinLeagueRequest { Token = token }));

		fakeCurrentUser.UserId = "league-admin";
		var promoteResult = await Mediator.Send(new PromoteLeagueMemberToAdminCommand(leagueId, "league-member"));

		promoteResult.IsT0.Should().BeTrue();

		var membership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == leagueId && x.UserId == "league-member");

		membership.Role.Should().Be(LeagueRole.Admin);
	}
}