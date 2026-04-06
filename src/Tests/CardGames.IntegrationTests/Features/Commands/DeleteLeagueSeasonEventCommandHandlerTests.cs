using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeasonEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.DeleteLeagueSeasonEvent;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Features.Commands;

public class DeleteLeagueSeasonEventCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_AdminCanDeleteSeasonEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-delete-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season Delete League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Summer 2027"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Week 1",
			SequenceNumber = 1
		}));

		var result = await Mediator.Send(new DeleteLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, createEvent.AsT0.EventId));

		result.IsT0.Should().BeTrue();
		var exists = await DbContext.LeagueSeasonEvents.AsNoTracking().AnyAsync(x => x.Id == createEvent.AsT0.EventId);
		exists.Should().BeFalse();
	}

	[Fact]
	public async Task Handle_NonGovernanceMemberCannotDeleteSeasonEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-delete-owner";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season Delete Forbidden League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Fall 2027"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Week 1"
		}));

		fakeCurrentUser.UserId = "league-season-delete-outsider";

		var result = await Mediator.Send(new DeleteLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, createEvent.AsT0.EventId));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DeleteLeagueSeasonEventErrorCode.Forbidden);
	}

	[Fact]
	public async Task Handle_LaunchedSeasonEventCannotBeDeleted()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-delete-launched-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season Delete Launch Guard League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Winter 2027"
		}));

		var createEvent = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Week 1"
		}));

		var seasonEvent = await DbContext.LeagueSeasonEvents.SingleAsync(x => x.Id == createEvent.AsT0.EventId);
		seasonEvent.LaunchedGameId = Guid.CreateVersion7();
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new DeleteLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, createEvent.AsT0.EventId));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DeleteLeagueSeasonEventErrorCode.Conflict);
	}
}