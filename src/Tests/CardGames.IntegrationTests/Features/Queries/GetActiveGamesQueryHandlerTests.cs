using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Queries;

public class GetActiveGamesQueryHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_WhitespaceVariant_IgnoresFilter_AndExcludesCompleteGames()
	{
		var fiveCardDraw = await DatabaseSeeder.CreateGameAsync(DbContext, PokerGameMetadataRegistry.FiveCardDrawCode, name: "Five Card Draw Table");
		var holdEm = await DatabaseSeeder.CreateGameAsync(DbContext, PokerGameMetadataRegistry.HoldEmCode, name: "Hold 'Em Table");
		var completed = await DatabaseSeeder.CreateGameAsync(DbContext, PokerGameMetadataRegistry.OmahaCode, name: "Completed Table");
		completed.CurrentPhase = "Complete";
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new GetActiveGamesQuery("   "));

		result.Select(x => x.Id).Should().BeEquivalentTo([fiveCardDraw.Id, holdEm.Id]);
		result.Should().NotContain(x => x.Id == completed.Id);
	}

	[Fact]
	public async Task Handle_UnknownVariant_ReturnsEmptyList()
	{
		await DatabaseSeeder.CreateGameAsync(DbContext, PokerGameMetadataRegistry.FiveCardDrawCode, name: "Known Table");

		var result = await Mediator.Send(new GetActiveGamesQuery("NOT_A_REAL_VARIANT"));

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task Handle_MissingMetadata_FallsBackToMappedGameTypeData()
	{
		var customGameType = new GameType
		{
			Id = Guid.CreateVersion7(),
			Code = "CUSTOMBROKEN",
			Name = "Custom Broken Variant",
			Description = "Custom description",
			MinPlayers = 2,
			MaxPlayers = 6,
			InitialHoleCards = 5,
			InitialBoardCards = 0,
			MaxCommunityCards = 0,
			MaxPlayerCards = 5,
			BettingStructure = BettingStructure.Ante
		};

		DbContext.GameTypes.Add(customGameType);
		await DbContext.SaveChangesAsync();

		var game = await DatabaseSeeder.CreateGameAsync(DbContext, customGameType.Code, name: "Broken Metadata Table");

		var result = await Mediator.Send(new GetActiveGamesQuery());

		var table = result.Should().ContainSingle(x => x.Id == game.Id).Subject;
		table.GameTypeCode.Should().Be(customGameType.Code);
		table.GameTypeName.Should().Be(customGameType.Name);
		table.GameTypeDescription.Should().Be(customGameType.Description);
		table.GameTypeMetadataName.Should().BeNull();
		table.GameTypeImageName.Should().BeNull();
		table.CurrentPhaseDescription.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Handle_UnauthenticatedUser_HidesLeagueGames_ButStillReturnsPublicGames()
	{
		var publicGame = await DatabaseSeeder.CreateGameAsync(DbContext, PokerGameMetadataRegistry.FiveCardDrawCode, name: "Public Table");
		var league = await CreateLeagueAsync("league-owner", "Members Only League");
		var leagueGame = await CreateLinkedLeagueGameAsync(league.Id, "league-owner", "League Table");
		var currentUser = GetCurrentUser();
		currentUser.IsAuthenticated = false;
		currentUser.UserId = null;

		var result = await Mediator.Send(new GetActiveGamesQuery());

		result.Select(x => x.Id).Should().BeEquivalentTo([publicGame.Id]);
		result.Should().NotContain(x => x.Id == leagueGame.Id);
	}

	[Fact]
	public async Task Handle_LeagueOwnerWithoutMembershipRow_StillSeesLeagueGames()
	{
		const string ownerUserId = "league-owner-without-membership";
		var currentUser = GetCurrentUser();
		currentUser.UserId = ownerUserId;
		currentUser.IsAuthenticated = true;

		var league = await CreateLeagueAsync(ownerUserId, "Owner Visibility League");
		var leagueGame = await CreateLinkedLeagueGameAsync(league.Id, ownerUserId, "Owner League Table");

		DbContext.LeagueMembersCurrent
			.Where(x => x.LeagueId == league.Id && x.UserId == ownerUserId)
			.Should().BeEmpty();

		var result = await Mediator.Send(new GetActiveGamesQuery());

		var table = result.Should().ContainSingle(x => x.Id == leagueGame.Id).Subject;
		table.LeagueId.Should().Be(league.Id);
	}

	[Fact]
	public async Task Handle_ConcurrentRequests_KeepLeagueVisibilityScopedPerUser()
	{
		var publicGame = await DatabaseSeeder.CreateGameAsync(DbContext, PokerGameMetadataRegistry.FiveCardDrawCode, name: "Shared Public Table");
		var league = await CreateLeagueAsync("league-owner", "Concurrent League");
		var leagueGame = await CreateLinkedLeagueGameAsync(league.Id, "league-owner", "Shared League Table");

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = league.Id,
			UserId = "league-member",
			Role = LeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		using var memberScope = CreateNewScope();
		using var outsiderScope = CreateNewScope();

		var memberCurrentUser = GetCurrentUser(memberScope.ServiceProvider);
		memberCurrentUser.UserId = "league-member";
		memberCurrentUser.IsAuthenticated = true;

		var outsiderCurrentUser = GetCurrentUser(outsiderScope.ServiceProvider);
		outsiderCurrentUser.UserId = "league-outsider";
		outsiderCurrentUser.IsAuthenticated = true;

		var memberMediator = memberScope.ServiceProvider.GetRequiredService<IMediator>();
		var outsiderMediator = outsiderScope.ServiceProvider.GetRequiredService<IMediator>();

		var memberTask = memberMediator.Send(new GetActiveGamesQuery());
		var outsiderTask = outsiderMediator.Send(new GetActiveGamesQuery());

		await Task.WhenAll(memberTask, outsiderTask);

		memberTask.Result.Select(x => x.Id).Should().BeEquivalentTo([publicGame.Id, leagueGame.Id]);
		outsiderTask.Result.Select(x => x.Id).Should().BeEquivalentTo([publicGame.Id]);
	}

	private FakeCurrentUserService GetCurrentUser(IServiceProvider? serviceProvider = null)
	{
		return (FakeCurrentUserService)(serviceProvider ?? Scope.ServiceProvider).GetRequiredService<ICurrentUserService>();
	}

	private async Task<League> CreateLeagueAsync(string ownerUserId, string name)
	{
		var league = new League
		{
			Name = name,
			CreatedByUserId = ownerUserId,
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		DbContext.Leagues.Add(league);
		await DbContext.SaveChangesAsync();
		return league;
	}

	private async Task<Game> CreateLinkedLeagueGameAsync(Guid leagueId, string ownerUserId, string gameName)
	{
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, PokerGameMetadataRegistry.FiveCardDrawCode, name: gameName);

		DbContext.LeagueOneOffEvents.Add(new LeagueOneOffEvent
		{
			LeagueId = leagueId,
			Name = gameName,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddHours(1),
			EventType = LeagueOneOffEventType.CashGame,
			Status = LeagueOneOffEventStatus.Planned,
			CreatedByUserId = ownerUserId,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			LaunchedGameId = game.Id
		});

		await DbContext.SaveChangesAsync();
		return game;
	}
}