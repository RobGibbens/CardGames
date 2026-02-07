using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGames;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetTableSettings;
using CardGames.Poker.Api.Games;

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

    #endregion
}
