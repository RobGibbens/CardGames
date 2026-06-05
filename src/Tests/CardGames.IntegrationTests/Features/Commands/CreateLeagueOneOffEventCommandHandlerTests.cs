using CardGames.Contracts.SignalR;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueOneOffEvent;
using CardGames.Poker.Api.Infrastructure;
using LeagueOneOffEventType = CardGames.Poker.Api.Contracts.LeagueOneOffEventType;

namespace CardGames.IntegrationTests.Features.Commands;

public class CreateLeagueOneOffEventCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_AdminCanCreateOneOffEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		var fakeLeagueBroadcaster = Scope.ServiceProvider.GetRequiredService<FakeLeagueBroadcaster>();
		fakeCurrentUser.UserId = "league-one-off-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "OneOff League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		var result = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Friday Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3),
			EventType = LeagueOneOffEventType.CashGame
		}));

		result.IsT0.Should().BeTrue();
		result.AsT0.LeagueId.Should().Be(leagueId);
		result.AsT0.Name.Should().Be("Friday Night");
		fakeLeagueBroadcaster.EventChangedNotifications.Should().ContainSingle();
		fakeLeagueBroadcaster.EventChangedNotifications[0].LeagueId.Should().Be(leagueId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].EventId.Should().Be(result.AsT0.EventId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SourceType.Should().Be(LeagueEventSourceType.OneOff);
		fakeLeagueBroadcaster.EventChangedNotifications[0].ChangeKind.Should().Be(LeagueEventChangeKind.Created);
	}

	[Fact]
	public async Task Handle_TournamentPersistsTournamentBuyIn()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-one-off-tournament-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "Tournament OneOff League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		var result = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Championship Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3),
			EventType = LeagueOneOffEventType.Tournament,
			GameTypeCode = "HOLDEM",
			TournamentBuyIn = 2500
		}));

		result.IsT0.Should().BeTrue();
		result.AsT0.TournamentBuyIn.Should().Be(2500);

		var savedEvent = await DbContext.LeagueOneOffEvents.FindAsync(result.AsT0.EventId);
		savedEvent.Should().NotBeNull();
		savedEvent!.TournamentBuyIn.Should().Be(2500);
	}

	[Fact]
	public async Task Handle_PastScheduledDate_ReturnsInvalidRequest()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-one-off-past-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "OneOff Past League"
		}));

		createLeague.IsT0.Should().BeTrue();
		var leagueId = createLeague.AsT0.LeagueId;

		var result = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Friday Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
			EventType = LeagueOneOffEventType.CashGame
		}));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(CreateLeagueOneOffEventErrorCode.InvalidRequest);
		result.AsT1.Message.Should().Be("Scheduled date/time must be in the future.");
	}
}