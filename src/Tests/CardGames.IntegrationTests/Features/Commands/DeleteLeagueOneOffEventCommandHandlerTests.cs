using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueOneOffEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.DeleteLeagueOneOffEvent;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using ContractLeagueOneOffEventType = CardGames.Poker.Api.Contracts.LeagueOneOffEventType;

namespace CardGames.IntegrationTests.Features.Commands;

public class DeleteLeagueOneOffEventCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_AdminCanDeleteOneOffEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		var fakeLeagueBroadcaster = Scope.ServiceProvider.GetRequiredService<FakeLeagueBroadcaster>();
		fakeCurrentUser.UserId = "league-oneoff-delete-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "One-Off Delete League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createEvent = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Friday Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM"
		}));

		fakeLeagueBroadcaster.EventChangedNotifications.Clear();
		var result = await Mediator.Send(new DeleteLeagueOneOffEventCommand(leagueId, createEvent.AsT0.EventId));

		result.IsT0.Should().BeTrue();
		var exists = await DbContext.LeagueOneOffEvents.AsNoTracking().AnyAsync(x => x.Id == createEvent.AsT0.EventId);
		exists.Should().BeFalse();
		fakeLeagueBroadcaster.EventChangedNotifications.Should().ContainSingle();
		fakeLeagueBroadcaster.EventChangedNotifications[0].LeagueId.Should().Be(leagueId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].EventId.Should().Be(createEvent.AsT0.EventId);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SourceType.Should().Be(CardGames.Contracts.SignalR.LeagueEventSourceType.OneOff);
		fakeLeagueBroadcaster.EventChangedNotifications[0].SeasonId.Should().BeNull();
		fakeLeagueBroadcaster.EventChangedNotifications[0].ChangeKind.Should().Be(CardGames.Contracts.SignalR.LeagueEventChangeKind.Deleted);
	}

	[Fact]
	public async Task Handle_NonGovernanceMemberCannotDeleteOneOffEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-oneoff-delete-owner";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "One-Off Delete Forbidden League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createEvent = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Friday Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM"
		}));

		fakeCurrentUser.UserId = "league-oneoff-delete-outsider";

		var result = await Mediator.Send(new DeleteLeagueOneOffEventCommand(leagueId, createEvent.AsT0.EventId));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DeleteLeagueOneOffEventErrorCode.Forbidden);
	}

	[Fact]
	public async Task Handle_LaunchedOneOffEventCannotBeDeleted()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-oneoff-delete-launched-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "One-Off Delete Launch Guard League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createEvent = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Friday Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM"
		}));

		var oneOffEvent = await DbContext.LeagueOneOffEvents.SingleAsync(x => x.Id == createEvent.AsT0.EventId);
		oneOffEvent.LaunchedGameId = Guid.CreateVersion7();
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new DeleteLeagueOneOffEventCommand(leagueId, createEvent.AsT0.EventId));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(DeleteLeagueOneOffEventErrorCode.Conflict);
	}
}