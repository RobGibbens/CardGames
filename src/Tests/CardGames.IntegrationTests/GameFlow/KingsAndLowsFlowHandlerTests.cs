using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using DropOrStayDecision = CardGames.Poker.Api.Data.Entities.DropOrStayDecision;

namespace CardGames.IntegrationTests.GameFlow;

/// <summary>
/// Integration tests for <see cref="KingsAndLowsFlowHandler"/>.
/// Tests unique phase transitions including DropOrStay and PotMatching.
/// </summary>
public class KingsAndLowsFlowHandlerTests : IntegrationTestBase
{
    [Fact]
    public void GameTypeCode_ReturnsKingsAndLows()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();

        // Assert
        handler.GameTypeCode.Should().Be("KINGSANDLOWS");
    }

    [Fact]
    public void GetGameRules_ReturnsValidRules()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();

        // Act
        var rules = handler.GetGameRules();

        // Assert
        rules.Should().NotBeNull();
        rules.Phases.Should().NotBeEmpty();
    }

    [Fact]
    public void GetDealingConfiguration_ReturnsAllAtOnceWith5Cards()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();

        // Act
        var config = handler.GetDealingConfiguration();

        // Assert
        config.PatternType.Should().Be(DealingPatternType.AllAtOnce);
        config.InitialCardsPerPlayer.Should().Be(5);
        config.AllFaceDown.Should().BeTrue();
    }

    [Fact]
    public async Task GetInitialPhase_ReturnsDealing()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Act
        var initialPhase = handler.GetInitialPhase(setup.Game);

        // Assert - Kings and Lows starts with Dealing, not CollectingAntes
        initialPhase.Should().Be(nameof(Phases.Dealing));
    }

    [Fact]
    public void SkipsAnteCollection_ReturnsTrue()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();

        // Assert - Kings and Lows skips ante collection (pot comes from losers matching)
        handler.SkipsAnteCollection.Should().BeTrue();
    }

    [Fact]
    public async Task GetNextPhase_FromDealing_ReturnsDropOrStay()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.Dealing));

        // Assert
        nextPhase.Should().Be(nameof(Phases.DropOrStay));
    }

    [Fact]
    public async Task GetNextPhase_FromDropOrStay_WithMultiplePlayers_ReturnsDrawPhase()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Simulate multiple players staying
        foreach (var gp in setup.GamePlayers)
        {
            gp.DropOrStayDecision = DropOrStayDecision.Stay;
        }
        await DbContext.SaveChangesAsync();

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.DropOrStay));

        // Assert
        nextPhase.Should().Be(nameof(Phases.DrawPhase));
    }

    [Fact]
    public async Task GetNextPhase_FromDropOrStay_WithSinglePlayer_ReturnsPlayerVsDeck()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Simulate only one player staying
        setup.GamePlayers[0].DropOrStayDecision = DropOrStayDecision.Stay;
        setup.GamePlayers[0].HasFolded = false;
        for (var i = 1; i < setup.GamePlayers.Count; i++)
        {
            setup.GamePlayers[i].DropOrStayDecision = DropOrStayDecision.Drop;
            setup.GamePlayers[i].HasFolded = true;
        }
        await DbContext.SaveChangesAsync();

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.DropOrStay));

        // Assert
        nextPhase.Should().Be(nameof(Phases.PlayerVsDeck));
    }

    [Fact]
    public async Task GetNextPhase_FromDropOrStay_WithNoPlayers_ReturnsComplete()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Simulate all players dropping
        foreach (var gp in setup.GamePlayers)
        {
            gp.DropOrStayDecision = DropOrStayDecision.Drop;
            gp.HasFolded = true;
        }
        await DbContext.SaveChangesAsync();

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.DropOrStay));

        // Assert
        nextPhase.Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public async Task GetNextPhase_FromDrawPhase_ReturnsDrawComplete()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.DrawPhase));

        // Assert
        nextPhase.Should().Be(nameof(Phases.DrawComplete));
    }

    [Fact]
    public async Task GetNextPhase_FromDrawComplete_ReturnsShowdown()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.DrawComplete));

        // Assert
        nextPhase.Should().Be(nameof(Phases.Showdown));
    }

    [Fact]
    public async Task GetNextPhase_FromShowdown_ReturnsPotMatching()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.Showdown));

        // Assert
        nextPhase.Should().Be(nameof(Phases.PotMatching));
    }

    [Fact]
    public async Task GetNextPhase_FromPlayerVsDeck_ReturnsComplete()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.PlayerVsDeck));

        // Assert
        nextPhase.Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public void SpecialPhases_ContainsDropOrStayPotMatchingAndPlayerVsDeck()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();

        // Assert
        handler.SpecialPhases.Should().HaveCount(3);
        handler.SpecialPhases.Should().Contain(nameof(Phases.DropOrStay));
        handler.SpecialPhases.Should().Contain(nameof(Phases.PotMatching));
        handler.SpecialPhases.Should().Contain(nameof(Phases.PlayerVsDeck));
    }

    [Fact]
    public void RequiresChipCoverageCheck_ReturnsTrue()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();

        // Assert
        handler.RequiresChipCoverageCheck.Should().BeTrue();
    }

    [Fact]
    public void GetChipCheckConfiguration_ReturnsKingsAndLowsDefaults()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();

        // Act
        var config = handler.GetChipCheckConfiguration();

        // Assert
        config.IsEnabled.Should().BeTrue();
        config.PauseDuration.Should().Be(TimeSpan.FromMinutes(2));
        config.ShortageAction.Should().Be(ChipShortageAction.AutoDrop);
    }

    [Fact]
    public void SupportsInlineShowdown_ReturnsTrue()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();

        // Assert
        handler.SupportsInlineShowdown.Should().BeTrue();
    }

    [Fact]
    public async Task OnHandStartingAsync_ResetsDropOrStayDecisions()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Set some existing decisions
        foreach (var gp in setup.GamePlayers)
        {
            gp.DropOrStayDecision = DropOrStayDecision.Stay;
        }
        await DbContext.SaveChangesAsync();

        // Act
        await handler.OnHandStartingAsync(setup.Game);

        // Assert - Decisions should be reset
        foreach (var gp in setup.GamePlayers)
        {
            gp.DropOrStayDecision.Should().BeNull();
        }
    }

    [Fact]
    public async Task DealCardsAsync_Creates5CardsPerPlayer()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
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
        var cards = DbContext.GameCards.Where(gc => gc.GameId == setup.Game.Id).ToList();
        var playerCards = cards.Where(c => c.GamePlayerId != null && c.Location == CardLocation.Hand).ToList();

        playerCards.Should().HaveCount(20); // 5 cards * 4 players
    }

    [Fact]
    public async Task DealCardsAsync_TransitionsToDropOrStay()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert - After dealing in Kings and Lows, phase should transition to DropOrStay
        setup.Game.CurrentPhase.Should().Be(nameof(Phases.DropOrStay));
    }
}
