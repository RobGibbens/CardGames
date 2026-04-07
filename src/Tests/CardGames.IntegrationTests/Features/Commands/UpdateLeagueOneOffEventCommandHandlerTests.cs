using CardGames.IntegrationTests.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueOneOffEvent;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueOneOffEvent;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using ContractLeagueOneOffEventType = CardGames.Poker.Api.Contracts.LeagueOneOffEventType;

namespace CardGames.IntegrationTests.Features.Commands;

public class UpdateLeagueOneOffEventCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_AdminCanUpdateOneOffEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-one-off-update-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "One-Off Update League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createEvent = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Friday Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(2),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM",
			Ante = 10,
			MinBet = 20
		}));

		var scheduledAtUtc = DateTimeOffset.UtcNow.AddDays(7);
		var result = await Mediator.Send(new UpdateLeagueOneOffEventCommand(leagueId, createEvent.AsT0.EventId, new UpdateLeagueOneOffEventRequest
		{
			Name = "High Stakes",
			ScheduledAtUtc = scheduledAtUtc,
			EventType = ContractLeagueOneOffEventType.CashGame,
			Notes = "Updated notes",
			GameTypeCode = "OMAHA",
			Ante = 25,
			MinBet = 50
		}));

		result.IsT0.Should().BeTrue();

		var savedEvent = await DbContext.LeagueOneOffEvents
			.AsNoTracking()
			.SingleAsync(x => x.Id == createEvent.AsT0.EventId);

		savedEvent.Name.Should().Be("High Stakes");
		savedEvent.ScheduledAtUtc.Should().Be(scheduledAtUtc);
		savedEvent.GameTypeCode.Should().Be("OMAHA");
		savedEvent.Ante.Should().Be(25);
		savedEvent.MinBet.Should().Be(50);
		savedEvent.Notes.Should().Be("Updated notes");
	}

	[Fact]
	public async Task Handle_TournamentUpdatePersistsTournamentBuyIn()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-one-off-tournament-update-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "One-Off Tournament Update League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createEvent = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Friday Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(2),
			EventType = ContractLeagueOneOffEventType.Tournament,
			GameTypeCode = "HOLDEM",
			TournamentBuyIn = 1200
		}));

		var result = await Mediator.Send(new UpdateLeagueOneOffEventCommand(leagueId, createEvent.AsT0.EventId, new UpdateLeagueOneOffEventRequest
		{
			Name = "Championship",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(7),
			EventType = ContractLeagueOneOffEventType.Tournament,
			GameTypeCode = "OMAHA",
			TournamentBuyIn = 2400
		}));

		result.IsT0.Should().BeTrue();

		var savedEvent = await DbContext.LeagueOneOffEvents.AsNoTracking().SingleAsync(x => x.Id == createEvent.AsT0.EventId);
		savedEvent.TournamentBuyIn.Should().Be(2400);
	}

	[Fact]
	public async Task Handle_NonGovernanceMemberCannotUpdateOneOffEvent()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-one-off-owner";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "One-Off Forbidden League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createEvent = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Friday Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(2),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM"
		}));

		fakeCurrentUser.UserId = "league-one-off-outsider";

		var result = await Mediator.Send(new UpdateLeagueOneOffEventCommand(leagueId, createEvent.AsT0.EventId, new UpdateLeagueOneOffEventRequest
		{
			Name = "Outsider Update",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(10),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM"
		}));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(UpdateLeagueOneOffEventErrorCode.Forbidden);
	}

	[Fact]
	public async Task Handle_LaunchedOneOffEventCannotBeUpdated()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-one-off-launched-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "One-Off Launch Guard League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createEvent = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Friday Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(2),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM"
		}));

		var oneOffEvent = await DbContext.LeagueOneOffEvents.SingleAsync(x => x.Id == createEvent.AsT0.EventId);
		oneOffEvent.LaunchedGameId = Guid.CreateVersion7();
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new UpdateLeagueOneOffEventCommand(leagueId, createEvent.AsT0.EventId, new UpdateLeagueOneOffEventRequest
		{
			Name = "Friday Night Updated",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(10),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM"
		}));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(UpdateLeagueOneOffEventErrorCode.Conflict);
	}

	[Fact]
	public async Task Handle_PastScheduledDate_ReturnsInvalidRequest()
	{
		var fakeCurrentUser = (FakeCurrentUserService)Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		fakeCurrentUser.UserId = "league-one-off-update-past-admin";
		fakeCurrentUser.IsAuthenticated = true;

		var createLeague = await Mediator.Send(new CreateLeagueCommand(new CreateLeagueRequest
		{
			Name = "One-Off Update Past League"
		}));

		var leagueId = createLeague.AsT0.LeagueId;

		var createEvent = await Mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, new CreateLeagueOneOffEventRequest
		{
			Name = "Friday Night",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(2),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM"
		}));

		var result = await Mediator.Send(new UpdateLeagueOneOffEventCommand(leagueId, createEvent.AsT0.EventId, new UpdateLeagueOneOffEventRequest
		{
			Name = "Friday Night Updated",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM"
		}));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(UpdateLeagueOneOffEventErrorCode.InvalidRequest);
		result.AsT1.Message.Should().Be("Scheduled date/time must be in the future.");
	}
}