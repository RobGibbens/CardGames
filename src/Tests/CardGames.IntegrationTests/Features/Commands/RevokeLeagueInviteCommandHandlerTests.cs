using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueInvite;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.RevokeLeagueInvite;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class RevokeLeagueInviteCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_AdminCanRevokeInvite()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-revoke-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Revoke League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		var createInvite = await Mediator.Send(new CreateLeagueInviteCommand(leagueId, new CardGames.Poker.Api.Contracts.CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(4)
		}));

		createInvite.IsT0.Should().BeTrue();

		var revokeResult = await Mediator.Send(new RevokeLeagueInviteCommand(leagueId, createInvite.AsT0.InviteId));
		revokeResult.IsT0.Should().BeTrue();

		var invite = await DbContext.LeagueInvites
			.AsNoTracking()
			.SingleAsync(x => x.Id == createInvite.AsT0.InviteId);

		invite.Status.Should().Be(LeagueInviteStatus.Revoked);
		invite.RevokedByUserId.Should().Be("league-revoke-admin");
		invite.RevokedAtUtc.Should().NotBeNull();
	}
}