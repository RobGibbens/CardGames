using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueDetail;
using CardGames.Poker.Api.Infrastructure;
using ContractsLeagueRole = CardGames.Poker.Api.Contracts.LeagueRole;

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
		createResult.AsT0.MyRole.Should().Be(ContractsLeagueRole.Manager);

		var queryResult = await Mediator.Send(new GetLeagueDetailQuery(createResult.AsT0.LeagueId));
		queryResult.IsT0.Should().BeTrue();
		queryResult.AsT0.LeagueId.Should().Be(createResult.AsT0.LeagueId);
		queryResult.AsT0.Name.Should().Be("Detail Query League");
		queryResult.AsT0.CreatedByDisplayName.Should().Be("league-detail-query-user");
		queryResult.AsT0.MyRole.Should().Be(ContractsLeagueRole.Manager);
	}

	[Fact]
	public async Task Handle_ReturnsDetailForCreator_WhenMembershipRowIsMissing()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-detail-creator-fallback-user";
		fakeCurrentUser.IsAuthenticated = true;

		var createResult = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Detail Query Creator Fallback League"
		}));

		createResult.IsT0.Should().BeTrue();

		var membership = await DbContext.LeagueMembersCurrent
			.Where(x => x.LeagueId == createResult.AsT0.LeagueId && x.UserId == fakeCurrentUser.UserId)
			.ToListAsync();

		membership.Should().NotBeEmpty();
		DbContext.LeagueMembersCurrent.RemoveRange(membership);
		await DbContext.SaveChangesAsync();

		var queryResult = await Mediator.Send(new GetLeagueDetailQuery(createResult.AsT0.LeagueId));
		queryResult.IsT0.Should().BeTrue();
		queryResult.AsT0.MyRole.Should().Be(ContractsLeagueRole.Manager);
	}

	[Fact]
	public async Task Handle_ReturnsForbiddenForNonMemberNonCreator()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-detail-creator-user";
		fakeCurrentUser.IsAuthenticated = true;

		var createResult = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Detail Query Forbidden League"
		}));

		createResult.IsT0.Should().BeTrue();

		fakeCurrentUser.UserId = "league-detail-other-user";
		var queryResult = await Mediator.Send(new GetLeagueDetailQuery(createResult.AsT0.LeagueId));

		queryResult.IsT1.Should().BeTrue();
		queryResult.AsT1.Code.Should().Be(GetLeagueDetailErrorCode.Forbidden);
	}
}