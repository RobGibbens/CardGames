using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueOneOffEventsPage;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Queries;

public class GetLeagueOneOffEventsPageQueryHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_ExcludesCompletedOneOffEvents_WhenIncludeCompletedIsFalse()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-oneoff-page-user";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "One-Off Page League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		DbContext.LeagueOneOffEvents.AddRange(
			new LeagueOneOffEvent
			{
				LeagueId = leagueId,
				Name = "Completed Event",
				ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
				EventType = LeagueOneOffEventType.CashGame,
				Status = LeagueOneOffEventStatus.Completed,
				CreatedByUserId = fakeCurrentUser.UserId!,
				CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2)
			},
			new LeagueOneOffEvent
			{
				LeagueId = leagueId,
				Name = "Planned Event",
				ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(1),
				EventType = LeagueOneOffEventType.Tournament,
				Status = LeagueOneOffEventStatus.Planned,
				CreatedByUserId = fakeCurrentUser.UserId!,
				CreatedAtUtc = DateTimeOffset.UtcNow
			});

		await DbContext.SaveChangesAsync();

		var filteredResult = await Mediator.Send(new GetLeagueOneOffEventsPageQuery(leagueId, 10, 1, IncludeCompleted: false));

		filteredResult.IsT0.Should().BeTrue();
		filteredResult.AsT0.TotalCount.Should().Be(1);
		filteredResult.AsT0.Entries.Should().ContainSingle(x => x.Name == "Planned Event");
		filteredResult.AsT0.Entries.Should().NotContain(x => x.Name == "Completed Event");

		var unfilteredResult = await Mediator.Send(new GetLeagueOneOffEventsPageQuery(leagueId, 10, 1));

		unfilteredResult.IsT0.Should().BeTrue();
		unfilteredResult.AsT0.TotalCount.Should().Be(2);
		unfilteredResult.AsT0.Entries.Should().Contain(x => x.Name == "Completed Event");
	}
}