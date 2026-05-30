#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.LaunchLeagueEventSession;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;
using EntityLeagueRole = CardGames.Poker.Api.Data.Entities.LeagueRole;
using EntityLeagueSeasonEventStatus = CardGames.Poker.Api.Data.Entities.LeagueSeasonEventStatus;
using EntityLeagueSeasonStatus = CardGames.Poker.Api.Data.Entities.LeagueSeasonStatus;
using LaunchCreateGameCommand = CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame.CreateGameCommand;

namespace CardGames.Poker.Tests.Api.Features.Leagues.v1.Commands.LaunchLeagueEventSession;

public class LaunchLeagueEventSessionCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenUserIsUnauthenticated_ReturnsUnauthorized()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context, currentUserService: CreateCurrentUserService(isAuthenticated: false, userId: null));

		var result = await sut.Handle(CreateSeasonCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(LaunchLeagueEventSessionErrorCode.Unauthorized);
	}

	[Fact]
	public async Task Handle_WhenGameCodeIsMissing_ReturnsInvalidRequest()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context);

		var result = await sut.Handle(CreateSeasonCommand(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			request: new LaunchLeagueEventSessionRequest
			{
				GameCode = " ",
				HostStartingChips = 300
			}), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(LaunchLeagueEventSessionErrorCode.InvalidRequest);
		result.AsT1.Message.Should().Be("Game code is required.");
	}

	[Fact]
	public async Task Handle_WhenLeagueIsMissing_ReturnsLeagueNotFound()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context);

		var result = await sut.Handle(CreateSeasonCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(LaunchLeagueEventSessionErrorCode.LeagueNotFound);
	}

	[Fact]
	public async Task Handle_WhenUserCannotManageLeague_ReturnsForbidden()
	{
		await using var context = CreateContext();
		var leagueId = await SeedLeagueAsync(context);
		var sut = CreateSut(context);

		var result = await sut.Handle(CreateSeasonCommand(leagueId, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(LaunchLeagueEventSessionErrorCode.Forbidden);
	}

	[Fact]
	public async Task Handle_WhenSeasonIdIsMissing_ReturnsInvalidRequest()
	{
		await using var context = CreateContext();
		var leagueId = await SeedLeagueAsync(context);
		await SeedManagerMembershipAsync(context, leagueId, "manager-user");
		var sut = CreateSut(context);

		var result = await sut.Handle(new LaunchLeagueEventSessionCommand(
			leagueId,
			LeagueEventSourceType.Season,
			Guid.NewGuid(),
			null,
			CreateRequest()), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(LaunchLeagueEventSessionErrorCode.InvalidRequest);
		result.AsT1.Message.Should().Be("Season id is required for season events.");
	}

	[Fact]
	public async Task Handle_WhenSeasonEventIsMissing_ReturnsEventNotFound()
	{
		await using var context = CreateContext();
		var leagueId = await SeedLeagueAsync(context);
		var seasonId = await SeedSeasonAsync(context, leagueId);
		await SeedManagerMembershipAsync(context, leagueId, "manager-user");
		var sut = CreateSut(context);

		var result = await sut.Handle(CreateSeasonCommand(leagueId, seasonId, Guid.NewGuid()), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(LaunchLeagueEventSessionErrorCode.EventNotFound);
	}

	[Fact]
	public async Task Handle_WhenSeasonEventBelongsToDifferentLeagueOrSeason_ReturnsMismatchedLeagueOrSeason()
	{
		await using var context = CreateContext();
		var leagueId = await SeedLeagueAsync(context);
		var wrongLeagueId = await SeedLeagueAsync(context, createdByUserId: "other-owner", name: "Other League");
		var seasonId = await SeedSeasonAsync(context, leagueId);
		var wrongSeasonId = await SeedSeasonAsync(context, wrongLeagueId, name: "Other Season");
		var seasonEventId = await SeedSeasonEventAsync(context, wrongLeagueId, wrongSeasonId, tournamentBuyIn: 500);
		await SeedManagerMembershipAsync(context, leagueId, "manager-user");
		var sut = CreateSut(context);

		var result = await sut.Handle(CreateSeasonCommand(leagueId, seasonId, seasonEventId), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(LaunchLeagueEventSessionErrorCode.MismatchedLeagueOrSeason);
	}

	[Fact]
	public async Task Handle_WhenSeasonEventMissingTournamentBuyIn_ReturnsInvalidRequest()
	{
		await using var context = CreateContext();
		var leagueId = await SeedLeagueAsync(context);
		var seasonId = await SeedSeasonAsync(context, leagueId);
		var seasonEventId = await SeedSeasonEventAsync(context, leagueId, seasonId, tournamentBuyIn: null);
		await SeedManagerMembershipAsync(context, leagueId, "manager-user");
		var sut = CreateSut(context);

		var result = await sut.Handle(CreateSeasonCommand(leagueId, seasonId, seasonEventId), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(LaunchLeagueEventSessionErrorCode.InvalidRequest);
		result.AsT1.Message.Should().Be("Season events must define a tournament buy-in before launch.");
	}

	[Fact]
	public async Task Handle_WhenSeasonEventUsesUnknownGameCode_ReturnsInvalidRequest()
	{
		await using var context = CreateContext();
		var leagueId = await SeedLeagueAsync(context);
		var seasonId = await SeedSeasonAsync(context, leagueId);
		var seasonEventId = await SeedSeasonEventAsync(context, leagueId, seasonId, gameTypeCode: "NOPE", tournamentBuyIn: 500);
		await SeedManagerMembershipAsync(context, leagueId, "manager-user");
		var sut = CreateSut(context);

		var result = await sut.Handle(CreateSeasonCommand(leagueId, seasonId, seasonEventId), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(LaunchLeagueEventSessionErrorCode.InvalidRequest);
		result.AsT1.Message.Should().Be("Unknown game code 'NOPE'.");
	}

	[Fact]
	public async Task Handle_WhenSeasonEventHasAlreadyLaunched_ReturnsAlreadyLaunched()
	{
		await using var context = CreateContext();
		var leagueId = await SeedLeagueAsync(context);
		var seasonId = await SeedSeasonAsync(context, leagueId);
		var seasonEventId = await SeedSeasonEventAsync(context, leagueId, seasonId, tournamentBuyIn: 500, launchedGameId: Guid.NewGuid());
		await SeedManagerMembershipAsync(context, leagueId, "manager-user");
		var sut = CreateSut(context);

		var result = await sut.Handle(CreateSeasonCommand(leagueId, seasonId, seasonEventId), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(LaunchLeagueEventSessionErrorCode.AlreadyLaunched);
	}

	[Fact]
	public async Task Handle_WhenCreateGameConflicts_ReturnsCreateGameConflict()
	{
		await using var context = CreateContext();
		var leagueId = await SeedLeagueAsync(context);
		var seasonId = await SeedSeasonAsync(context, leagueId);
		var seasonEventId = await SeedSeasonEventAsync(context, leagueId, seasonId, tournamentBuyIn: 500);
		await SeedManagerMembershipAsync(context, leagueId, "manager-user");
		var mediator = Substitute.For<IMediator>();
		mediator
			.Send(Arg.Any<LaunchCreateGameCommand>(), Arg.Any<CancellationToken>())
			.Returns(new CreateGameConflict
			{
				GameId = Guid.NewGuid(),
				Reason = "Concurrent launch detected."
			});
		var sut = CreateSut(context, mediator: mediator);

		var result = await sut.Handle(CreateSeasonCommand(leagueId, seasonId, seasonEventId), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(LaunchLeagueEventSessionErrorCode.CreateGameConflict);
		result.AsT1.Message.Should().Be("Concurrent launch detected.");
	}

	[Fact]
	public async Task Handle_WhenSeasonLaunchSucceeds_PersistsLinkageAndBroadcasts()
	{
		await using var context = CreateContext();
		var leagueId = await SeedLeagueAsync(context);
		var seasonId = await SeedSeasonAsync(context, leagueId);
		var seasonEventId = await SeedSeasonEventAsync(context, leagueId, seasonId, tournamentBuyIn: 500);
		await SeedManagerMembershipAsync(context, leagueId, "manager-user");
		var mediator = Substitute.For<IMediator>();
		mediator
			.Send(Arg.Any<LaunchCreateGameCommand>(), Arg.Any<CancellationToken>())
			.Returns(new CreateGameSuccessful
			{
				GameId = Guid.NewGuid(),
				GameTypeCode = "FIVECARDDRAW",
				PlayerCount = 1
			});
		var leagueBroadcaster = Substitute.For<ILeagueBroadcaster>();
		var sut = CreateSut(context, mediator: mediator, leagueBroadcaster: leagueBroadcaster);

		var result = await sut.Handle(CreateSeasonCommand(leagueId, seasonId, seasonEventId), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.LeagueId.Should().Be(leagueId);
		result.AsT0.EventId.Should().Be(seasonEventId);
		result.AsT0.GameCode.Should().Be("FIVECARDDRAW");
		result.AsT0.GameId.Should().NotBe(Guid.Empty);
		result.AsT0.TablePath.Should().Be($"/table/{result.AsT0.GameId}");

		var persistedEvent = await context.LeagueSeasonEvents.SingleAsync(x => x.Id == seasonEventId);
		persistedEvent.LaunchedGameId.Should().Be(result.AsT0.GameId);
		persistedEvent.LaunchedByUserId.Should().Be("manager-user");
		persistedEvent.LaunchedAtUtc.Should().NotBeNull();

		await leagueBroadcaster.Received(1).BroadcastEventSessionLaunchedAsync(
			Arg.Is<CardGames.Contracts.SignalR.LeagueEventSessionLaunchedDto>(dto =>
				dto.LeagueId == leagueId &&
				dto.EventId == seasonEventId &&
				dto.SeasonId == seasonId &&
				dto.GameId == result.AsT0.GameId),
			Arg.Any<CancellationToken>());
	}

	private static LaunchLeagueEventSessionCommandHandler CreateSut(
		CardsDbContext context,
		ICurrentUserService? currentUserService = null,
		IMediator? mediator = null,
		ILeagueBroadcaster? leagueBroadcaster = null)
	{
		var shouldConfigureDefaultMediatorResponse = mediator is null;
		mediator ??= Substitute.For<IMediator>();
		leagueBroadcaster ??= Substitute.For<ILeagueBroadcaster>();
		currentUserService ??= CreateCurrentUserService(isAuthenticated: true, userId: "manager-user", userName: "Manager User");

		if (shouldConfigureDefaultMediatorResponse)
		{
			mediator
				.Send(Arg.Any<LaunchCreateGameCommand>(), Arg.Any<CancellationToken>())
				.Returns(callInfo => new CreateGameSuccessful
				{
					GameId = ((LaunchCreateGameCommand)callInfo[0]).GameId,
					GameTypeCode = ((LaunchCreateGameCommand)callInfo[0]).GameCode,
					PlayerCount = ((LaunchCreateGameCommand)callInfo[0]).Players.Count
				});
		}

		return new LaunchLeagueEventSessionCommandHandler(context, currentUserService, mediator, leagueBroadcaster);
	}

	private static CardsDbContext CreateContext()
	{
		return CreateContext(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot());
	}

	private static CardsDbContext CreateContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot)
			.Options;

		return new CardsDbContext(options);
	}

	private static ICurrentUserService CreateCurrentUserService(bool isAuthenticated, string? userId, string? userName = null)
	{
		var currentUserService = Substitute.For<ICurrentUserService>();
		currentUserService.IsAuthenticated.Returns(isAuthenticated);
		currentUserService.UserId.Returns(userId);
		currentUserService.UserName.Returns(userName);
		return currentUserService;
	}

	private static LaunchLeagueEventSessionCommand CreateSeasonCommand(
		Guid leagueId,
		Guid seasonId,
		Guid seasonEventId,
		LaunchLeagueEventSessionRequest? request = null)
	{
		return new LaunchLeagueEventSessionCommand(
			leagueId,
			LeagueEventSourceType.Season,
			seasonEventId,
			seasonId,
			request ?? CreateRequest());
	}

	private static LaunchLeagueEventSessionRequest CreateRequest()
	{
		return new LaunchLeagueEventSessionRequest
		{
			GameCode = "FIVECARDDRAW",
			GameName = "League Night",
			Ante = 10,
			MinBet = 20,
			HostStartingChips = 300
		};
	}

	private static async Task<Guid> SeedLeagueAsync(CardsDbContext context, string createdByUserId = "owner-user", string name = "Test League")
	{
		var league = new League
		{
			Id = Guid.NewGuid(),
			Name = name,
			CreatedByUserId = createdByUserId,
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		context.Leagues.Add(league);
		await context.SaveChangesAsync();
		return league.Id;
	}

	private static async Task<Guid> SeedSeasonAsync(CardsDbContext context, Guid leagueId, string name = "Season 1")
	{
		var season = new LeagueSeason
		{
			Id = Guid.NewGuid(),
			LeagueId = leagueId,
			Name = name,
			Status = EntityLeagueSeasonStatus.Planned,
			CreatedByUserId = "owner-user",
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		context.LeagueSeasons.Add(season);
		await context.SaveChangesAsync();
		return season.Id;
	}

	private static async Task SeedManagerMembershipAsync(CardsDbContext context, Guid leagueId, string userId)
	{
		context.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = userId,
			Role = EntityLeagueRole.Manager,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});

		await context.SaveChangesAsync();
	}

	private static async Task<Guid> SeedSeasonEventAsync(
		CardsDbContext context,
		Guid leagueId,
		Guid seasonId,
		string gameTypeCode = "FIVECARDDRAW",
		int? tournamentBuyIn = 500,
		Guid? launchedGameId = null)
	{
		var seasonEvent = new LeagueSeasonEvent
		{
			Id = Guid.NewGuid(),
			LeagueId = leagueId,
			LeagueSeasonId = seasonId,
			Name = "Week 1",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(1),
			Status = EntityLeagueSeasonEventStatus.Planned,
			GameTypeCode = gameTypeCode,
			Ante = 10,
			MinBet = 20,
			TournamentBuyIn = tournamentBuyIn,
			CreatedByUserId = "owner-user",
			CreatedAtUtc = DateTimeOffset.UtcNow,
			LaunchedGameId = launchedGameId
		};

		context.LeagueSeasonEvents.Add(seasonEvent);
		await context.SaveChangesAsync();
		return seasonEvent.Id;
	}
}