using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;

namespace CardGames.IntegrationTests.GameFlow;

/// <summary>
/// Integration tests for <see cref="SevenCardStudFlowHandler"/>.
/// Tests street-based phase transitions and dealing configuration.
/// </summary>
public class SevenCardStudFlowHandlerTests : IntegrationTestBase
{
    [Fact]
    public void GameTypeCode_ReturnsSevenCardStud()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();

        // Assert
        handler.GameTypeCode.Should().Be("SEVENCARDSTUD");
    }

    [Fact]
    public void GetGameRules_ReturnsValidRules()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();

        // Act
        var rules = handler.GetGameRules();

        // Assert
        rules.Should().NotBeNull();
        rules.Phases.Should().NotBeEmpty();
    }

    [Fact]
    public void GetDealingConfiguration_ReturnsStreetBased()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();

        // Act
        var config = handler.GetDealingConfiguration();

        // Assert
        config.PatternType.Should().Be(DealingPatternType.StreetBased);
        config.DealingRounds.Should().NotBeNull();
        config.DealingRounds.Should().HaveCount(5); // Third through Seventh Street
    }

    [Fact]
    public void GetDealingConfiguration_ThirdStreet_Has2Hole1Board()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();

        // Act
        var config = handler.GetDealingConfiguration();
        var thirdStreet = config.DealingRounds!.First(r => r.PhaseName == nameof(Phases.ThirdStreet));

        // Assert
        thirdStreet.HoleCards.Should().Be(2);
        thirdStreet.BoardCards.Should().Be(1);
        thirdStreet.HasBettingAfter.Should().BeTrue();
    }

    [Fact]
    public void GetDealingConfiguration_SeventhStreet_Has1Hole0Board()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();

        // Act
        var config = handler.GetDealingConfiguration();
        var seventhStreet = config.DealingRounds!.First(r => r.PhaseName == nameof(Phases.SeventhStreet));

        // Assert
        seventhStreet.HoleCards.Should().Be(1);
        seventhStreet.BoardCards.Should().Be(0);
        seventhStreet.HasBettingAfter.Should().BeTrue();
    }

    [Fact]
    public async Task GetInitialPhase_ReturnsCollectingAntes()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);

        // Act
        var initialPhase = handler.GetInitialPhase(setup.Game);

        // Assert
        initialPhase.Should().Be(nameof(Phases.CollectingAntes));
    }

    [Fact]
    public async Task GetNextPhase_FromCollectingAntes_ReturnsThirdStreet()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.CollectingAntes));

        // Assert
        nextPhase.Should().Be(nameof(Phases.ThirdStreet));
    }

    [Fact]
    public async Task GetNextPhase_ThroughAllStreets_FollowsCorrectSequence()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);

        // Act & Assert - Verify full street sequence
        handler.GetNextPhase(setup.Game, nameof(Phases.ThirdStreet)).Should().Be(nameof(Phases.FourthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.FourthStreet)).Should().Be(nameof(Phases.FifthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.FifthStreet)).Should().Be(nameof(Phases.SixthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.SixthStreet)).Should().Be(nameof(Phases.SeventhStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.SeventhStreet)).Should().Be(nameof(Phases.Showdown));
        handler.GetNextPhase(setup.Game, nameof(Phases.Showdown)).Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public async Task GetNextPhase_SinglePlayerRemaining_SkipsToShowdown()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);

        // Simulate all but one player folding
        foreach (var gp in setup.GamePlayers.Skip(1))
        {
            gp.HasFolded = true;
        }
        await DbContext.SaveChangesAsync();

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.FourthStreet));

        // Assert
        nextPhase.Should().Be(nameof(Phases.Showdown));
    }

    [Fact]
    public void IsStreetPhase_ReturnsTrue_ForValidStreets()
    {
        // Assert
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.ThirdStreet)).Should().BeTrue();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.FourthStreet)).Should().BeTrue();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.FifthStreet)).Should().BeTrue();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.SixthStreet)).Should().BeTrue();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.SeventhStreet)).Should().BeTrue();
    }

    [Fact]
    public void IsStreetPhase_ReturnsFalse_ForNonStreetPhases()
    {
        // Assert
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.CollectingAntes)).Should().BeFalse();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.Showdown)).Should().BeFalse();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.Complete)).Should().BeFalse();
    }

    [Fact]
    public void Streets_ContainsAllFiveStreets()
    {
        // Assert
        SevenCardStudFlowHandler.Streets.Should().HaveCount(5);
        SevenCardStudFlowHandler.Streets.Should().Contain(nameof(Phases.ThirdStreet));
        SevenCardStudFlowHandler.Streets.Should().Contain(nameof(Phases.FourthStreet));
        SevenCardStudFlowHandler.Streets.Should().Contain(nameof(Phases.FifthStreet));
        SevenCardStudFlowHandler.Streets.Should().Contain(nameof(Phases.SixthStreet));
        SevenCardStudFlowHandler.Streets.Should().Contain(nameof(Phases.SeventhStreet));
    }

    [Fact]
    public void SkipsAnteCollection_ReturnsFalse()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();

        // Assert
        handler.SkipsAnteCollection.Should().BeFalse();
    }

    [Fact]
    public void SpecialPhases_IsEmpty()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();

        // Assert
        handler.SpecialPhases.Should().BeEmpty();
    }

    [Fact]
    public void SupportsInlineShowdown_ReturnsFalse()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();

        // Assert
        handler.SupportsInlineShowdown.Should().BeFalse();
    }

    [Fact]
    public async Task DealCardsAsync_CreatesCorrectCardLayout()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert - Third Street deals 2 hole + 1 board per player
        var cards = DbContext.GameCards.Where(gc => gc.GameId == setup.Game.Id).ToList();
        var playerCards = cards.Where(c => c.GamePlayerId != null && c.Location != CardLocation.Deck).ToList();

        playerCards.Should().HaveCount(12); // 3 cards * 4 players
        
        var holeCards = playerCards.Count(c => c.Location == CardLocation.Hole);
        var boardCards = playerCards.Count(c => c.Location == CardLocation.Board);

        holeCards.Should().Be(8);  // 2 hole cards * 4 players
        boardCards.Should().Be(4); // 1 board card * 4 players
    }

    [Fact]
    public async Task DealCardsAsync_BoardCardsAreVisible()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        var boardCards = DbContext.GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.Location == CardLocation.Board)
            .ToList();

        boardCards.Should().AllSatisfy(c => c.IsVisible.Should().BeTrue());
    }

    [Fact]
    public async Task DealCardsAsync_HoleCardsAreNotVisible()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        var holeCards = DbContext.GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.Location == CardLocation.Hole)
            .ToList();

        holeCards.Should().AllSatisfy(c => c.IsVisible.Should().BeFalse());
    }

    [Fact]
    public async Task DealCardsAsync_SetsPhaseToThirdStreet()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        setup.Game.CurrentPhase.Should().Be(nameof(Phases.ThirdStreet));
    }
}
