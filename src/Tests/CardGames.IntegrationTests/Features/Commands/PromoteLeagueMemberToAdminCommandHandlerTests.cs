using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.PromoteLeagueMemberToAdmin;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class PromoteLeagueMemberToAdminCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_ManagerPromotesActiveMemberToAdmin()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();

		fakeCurrentUser.UserId = "league-manager";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Promotion League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "league-member",
			Role = LeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			LeftAtUtc = null,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});

		await DbContext.SaveChangesAsync();

		fakeCurrentUser.UserId = "league-manager";
		var promoteResult = await Mediator.Send(new PromoteLeagueMemberToAdminCommand(leagueId, "league-member"));

		promoteResult.IsT0.Should().BeTrue();

		var membership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == leagueId && x.UserId == "league-member");

		membership.Role.Should().Be(LeagueRole.Admin);
	}

	[Fact]
	public async Task Handle_AdminPromotesActiveMemberToAdmin()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();

		fakeCurrentUser.UserId = "league-manager";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Admin Promotion League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		DbContext.LeagueMembersCurrent.AddRange(
			new LeagueMemberCurrent
			{
				LeagueId = leagueId,
				UserId = "league-admin",
				Role = LeagueRole.Admin,
				IsActive = true,
				JoinedAtUtc = DateTimeOffset.UtcNow,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			},
			new LeagueMemberCurrent
			{
				LeagueId = leagueId,
				UserId = "league-member",
				Role = LeagueRole.Member,
				IsActive = true,
				JoinedAtUtc = DateTimeOffset.UtcNow,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			});

		await DbContext.SaveChangesAsync();

		fakeCurrentUser.UserId = "league-admin";
		var promoteResult = await Mediator.Send(new PromoteLeagueMemberToAdminCommand(leagueId, "league-member"));

		promoteResult.IsT0.Should().BeTrue();

		var membership = await DbContext.LeagueMembersCurrent
			.AsNoTracking()
			.SingleAsync(x => x.LeagueId == leagueId && x.UserId == "league-member");

		membership.Role.Should().Be(LeagueRole.Admin);
	}

	[Fact]
	public async Task Handle_FailsWhenActorIsMember()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();

		fakeCurrentUser.UserId = "league-manager";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Member Forbidden Promotion League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		DbContext.LeagueMembersCurrent.AddRange(
			new LeagueMemberCurrent
			{
				LeagueId = leagueId,
				UserId = "league-member-actor",
				Role = LeagueRole.Member,
				IsActive = true,
				JoinedAtUtc = DateTimeOffset.UtcNow,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			},
			new LeagueMemberCurrent
			{
				LeagueId = leagueId,
				UserId = "league-member-target",
				Role = LeagueRole.Member,
				IsActive = true,
				JoinedAtUtc = DateTimeOffset.UtcNow,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			});

		await DbContext.SaveChangesAsync();

		fakeCurrentUser.UserId = "league-member-actor";
		var promoteResult = await Mediator.Send(new PromoteLeagueMemberToAdminCommand(leagueId, "league-member-target"));

		promoteResult.IsT1.Should().BeTrue();
		promoteResult.AsT1.Code.Should().Be(PromoteLeagueMemberToAdminErrorCode.Forbidden);
	}
}
