using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeasonEvent;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Commands;

public class CreateLeagueSeasonEventCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_AdminCanCreateSeasonEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		var fakeLeagueBroadcaster = Scope.ServiceProvider.GetRequiredService<FakeLeagueBroadcaster>();
		fakeCurrentUser.UserId = "league-season-event-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Season Event League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CardGames.Poker.Api.Contracts.CreateLeagueSeasonRequest
		{
			Name = "Summer 2026"
		}));

		createSeason.IsT0.Should().BeTrue();

		var result = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CardGames.Poker.Api.Contracts.CreateLeagueSeasonEventRequest
		{
			Name = "Week 1",
			SequenceNumber = 1,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(7)
		}));

		result.IsT0.Should().BeTrue();
		result.AsT0.SeasonId.Should().Be(createSeason.AsT0.SeasonId);
		result.AsT0.SequenceNumber.Should().Be(1);
		fakeLeagueBroadcaster.EventChangedNotifications.Should().ContainSingle();
		fakeLeagueBroadcaster.EventChangedNotifications[0].LeagueId.Should().Be(leagueId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].EventId.Should().Be(result.AsT0.EventId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SourceType.Should().Be(CardGames.Contracts.SignalR.LeagueEventSourceType.Season);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SeasonId.Should().Be(createSeason.AsT0.SeasonId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].ChangeKind.Should().Be(CardGames.Contracts.SignalR.LeagueEventChangeKind.Created);
	}

	[Fact]
	public async Task Handle_WithTournamentBuyIn_PersistsTournamentBuyIn()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-event-tournament-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CardGames.Poker.Api.Contracts.CreateLeagueRequest
		{
			Name = "Season Tournament League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CardGames.Poker.Api.Contracts.CreateLeagueSeasonRequest
		{
			Name = "Autumn 2026"
		}));

		createSeason.IsT0.Should().BeTrue();

		var result = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CardGames.Poker.Api.Contracts.CreateLeagueSeasonEventRequest
		{
			Name = "Week 2",
			SequenceNumber = 2,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(7),
			TournamentBuyIn = 1800
		}));

		result.IsT0.Should().BeTrue();
		result.AsT0.TournamentBuyIn.Should().Be(1800);

		var savedEvent = await DbContext.LeagueSeasonEvents.FindAsync(result.AsT0.EventId);
		savedEvent.Should().NotBeNull();
		savedEvent!.TournamentBuyIn.Should().Be(1800);
	}

	[Fact]
	public async Task Handle_PastScheduledDate_ReturnsInvalidRequest()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-season-event-past-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Season Event Past League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		var createSeason = await Mediator.Send(new CreateLeagueSeasonCommand(leagueId, new CreateLeagueSeasonRequest
		{
			Name = "Summer 2026"
		}));

		createSeason.IsT0.Should().BeTrue();

		var result = await Mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, createSeason.AsT0.SeasonId, new CreateLeagueSeasonEventRequest
		{
			Name = "Week 1",
			SequenceNumber = 1,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
		}));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(CreateLeagueSeasonEventErrorCode.InvalidRequest);
		result.AsT1.Message.Should().Be("Scheduled date/time must be in the future.");
	}
}