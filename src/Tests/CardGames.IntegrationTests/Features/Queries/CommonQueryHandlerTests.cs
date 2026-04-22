using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGames;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetTableSettings;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.IntegrationTests.Features.Queries;

/// <summary>
/// Integration tests for common query handlers.
/// </summary>
public class CommonQueryHandlerTests : IntegrationTestBase
{
    #region GetGameQueryHandler Tests

    [Fact]
    public async Task GetGame_ExistingGame_ReturnsGame()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var query = new GetGameQuery(setup.Game.Id);

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(setup.Game.Id);
        result.GameTypeCode.Should().Be("FIVECARDDRAW");
        result.LeagueId.Should().BeNull();
    }

    [Fact]
    public async Task GetGame_LeagueSeasonEventLaunch_ReturnsLeagueId()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        var league = new League
        {
            Name = "Query Season League",
            CreatedByUserId = "season-owner",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var season = new LeagueSeason
        {
            LeagueId = league.Id,
            Name = "Season One",
            Status = LeagueSeasonStatus.InProgress,
            CreatedByUserId = "season-owner",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var seasonEvent = new LeagueSeasonEvent
        {
            LeagueId = league.Id,
            LeagueSeasonId = season.Id,
            Name = "Season Event",
            Status = LeagueSeasonEventStatus.Planned,
            CreatedByUserId = "season-owner",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LaunchedGameId = setup.Game.Id,
            GameTypeCode = "FIVECARDDRAW"
        };

        DbContext.Leagues.Add(league);
        DbContext.LeagueSeasons.Add(season);
        DbContext.LeagueSeasonEvents.Add(seasonEvent);
        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new GetGameQuery(setup.Game.Id));

        result.Should().NotBeNull();
        result!.LeagueId.Should().Be(league.Id);
    }

    [Fact]
    public async Task GetGame_LeagueOneOffLaunch_ReturnsLeagueId()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        var league = new League
        {
            Name = "Query One-Off League",
            CreatedByUserId = "oneoff-owner",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var oneOffEvent = new LeagueOneOffEvent
        {
            LeagueId = league.Id,
            Name = "One-Off Event",
            ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            EventType = LeagueOneOffEventType.Tournament,
            Status = LeagueOneOffEventStatus.Planned,
            CreatedByUserId = "oneoff-owner",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LaunchedGameId = setup.Game.Id,
            GameTypeCode = "FIVECARDDRAW"
        };

        DbContext.Leagues.Add(league);
        DbContext.LeagueOneOffEvents.Add(oneOffEvent);
        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new GetGameQuery(setup.Game.Id));

        result.Should().NotBeNull();
        result!.LeagueId.Should().Be(league.Id);
    }

    [Fact]
    public async Task GetGame_NonExistentGame_ReturnsNull()
    {
        // Arrange
        var query = new GetGameQuery(Guid.NewGuid());

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGame_DeletedGame_ReturnsNull()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        setup.Game.IsDeleted = true;
        await DbContext.SaveChangesAsync();

        var query = new GetGameQuery(setup.Game.Id);

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("FIVECARDDRAW")]
    [InlineData("SEVENCARDSTUD")]
    [InlineData("KINGSANDLOWS")]
    public async Task GetGame_DifferentGameTypes_ReturnsCorrectType(string gameTypeCode)
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, gameTypeCode, 4);
        var query = new GetGameQuery(setup.Game.Id);

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result!.GameTypeCode.Should().Be(gameTypeCode);
    }

    #endregion

    #region GetGamesQueryHandler Tests

    [Fact]
    public async Task GetGames_MultipleGames_ReturnsAll()
    {
        // Arrange
        await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 2);
        await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 2);

        var query = new GetGamesQuery();

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetGames_NoGames_ReturnsEmptyList()
    {
        // Arrange - No games created
        var query = new GetGamesQuery();

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGames_ExcludesDeletedGames()
    {
        // Arrange
        var setup1 = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var setup2 = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 2);
        
        // Delete one game
        setup1.Game.IsDeleted = true;
        await DbContext.SaveChangesAsync();

        var query = new GetGamesQuery();

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().ContainSingle(g => g.Id == setup2.Game.Id);
        result.Should().NotContain(g => g.Id == setup1.Game.Id);
    }

    #endregion

    #region GetGameRulesQueryHandler Tests

    [Theory]
    [InlineData(PokerGameMetadataRegistry.FiveCardDrawCode)]
    [InlineData(PokerGameMetadataRegistry.SevenCardStudCode)]
    [InlineData(PokerGameMetadataRegistry.KingsAndLowsCode)]
    [InlineData(PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode)]
    [InlineData(PokerGameMetadataRegistry.GoodBadUglyCode)]
    public async Task GetGameRules_ValidGameType_ReturnsRules(string gameTypeCode)
    {
        // Arrange
        var query = new GetGameRulesQuery(gameTypeCode);

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result!.Phases.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetGameRules_NonExistentGameType_ReturnsNull()
    {
        // Arrange
        var query = new GetGameRulesQuery("UNKNOWN_GAME");

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGameRules_FiveCardDraw_HasCorrectPhases()
    {
        // Arrange
        var query = new GetGameRulesQuery(PokerGameMetadataRegistry.FiveCardDrawCode);

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result!.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.FirstBettingRound));
        result.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.DrawPhase));
        result.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.SecondBettingRound));
    }

    #endregion

    #region GetTableSettingsQueryHandler Tests

    [Fact]
    public async Task GetTableSettings_ExistingGame_ReturnsSettings()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var query = new GetTableSettingsQuery(setup.Game.Id);

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result!.GameId.Should().Be(setup.Game.Id);
        result.Ante.Should().Be(setup.Game.Ante);
        result.MinBet.Should().Be(setup.Game.MinBet);
    }

    [Fact]
    public async Task GetTableSettings_NonExistentGame_ReturnsNull()
    {
        // Arrange
        var query = new GetTableSettingsQuery(Guid.NewGuid());

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTableSettings_IncludesRowVersion()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var query = new GetTableSettingsQuery(setup.Game.Id);

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result!.RowVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetTableSettings_GameWithoutGameType_ReturnsFallbackTypeValues()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        setup.Game.GameType = null;
        setup.Game.GameTypeId = null;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var query = new GetTableSettingsQuery(setup.Game.Id);

        // Act
        var result = await Mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result!.GameTypeCode.Should().BeEmpty();
        result.GameTypeName.Should().Be("Unknown Game");
        result.MaxPlayers.Should().Be(0);
        result.MinPlayers.Should().Be(0);
    }

    #endregion
}
