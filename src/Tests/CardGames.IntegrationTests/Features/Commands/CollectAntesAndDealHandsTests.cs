using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for <see cref="CollectAntesCommandHandler"/> and <see cref="DealHandsCommandHandler"/>.
/// </summary>
public class CollectAntesAndDealHandsTests : IntegrationTestBase
{
    #region CollectAntes Tests

    [Fact]
    public async Task CollectAntes_GameNotFound_ReturnsError()
    {
        // Arrange
        var command = new CollectAntesCommand(Guid.NewGuid());

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
    }

    [Fact]
    public async Task CollectAntes_WrongPhase_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        // Don't start hand - game is in WaitingToStart phase
        var command = new CollectAntesCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
    }

    [Fact]
    public async Task CollectAntes_AfterStartHand_Succeeds()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, ante: 10);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        var command = new CollectAntesCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
    }

    [Fact]
    public async Task CollectAntes_DeductsFromPlayerChips()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, startingChips: 1000, ante: 10);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Act
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

        // Assert
        var players = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == setup.Game.Id)
            .ToListAsync();

        players.Should().AllSatisfy(p => p.ChipStack.Should().Be(990)); // 1000 - 10 ante
    }

    [Fact]
    public async Task CollectAntes_AddsToPot()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, ante: 10);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Act
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

        // Assert
        var pot = await GetFreshDbContext().Pots
            .Where(p => p.GameId == setup.Game.Id && p.HandNumber == 1)
            .FirstAsync();

        pot.Amount.Should().Be(40); // 4 players x 10 ante
    }

    [Fact]
    public async Task CollectAntes_AdvancesToDealingPhase()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, ante: 10);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Act
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

        // Assert
        var game = await GetFreshDbContext().Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.Dealing));
    }

    #endregion

    #region DealHands Tests

    [Fact]
    public async Task DealHands_GameNotFound_ReturnsError()
    {
        // Arrange
        var command = new DealHandsCommand(Guid.NewGuid());

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
    }

    [Fact]
    public async Task DealHands_WrongPhase_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var command = new DealHandsCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
    }

    [Fact]
    public async Task DealHands_AfterCollectAntes_Succeeds()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

        var command = new DealHandsCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
    }

    [Fact]
    public async Task DealHands_Creates5CardsPerPlayer()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

        // Act
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // Assert
        foreach (var player in setup.GamePlayers)
        {
            var cards = await GetFreshDbContext().GameCards
                .Where(gc => gc.GameId == setup.Game.Id && gc.GamePlayerId == player.Id && !gc.IsDiscarded)
                .ToListAsync();

            cards.Should().HaveCount(5);
        }
    }

    [Fact]
    public async Task DealHands_AllCardsAreUnique()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

        // Act
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // Assert
        var allCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.GamePlayerId != null)
            .ToListAsync();

        var uniqueCards = allCards.Select(c => $"{c.Suit}-{c.Symbol}").Distinct().ToList();
        uniqueCards.Should().HaveCount(allCards.Count);
    }

    [Fact]
    public async Task DealHands_AdvancesToFirstBettingRound()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

        // Act
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // Assert
        var game = await GetFreshDbContext().Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase.Should().Be(nameof(Phases.FirstBettingRound));
    }

    [Fact]
    public async Task DealHands_CreatesBettingRound()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

        // Act
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // Assert
        var bettingRound = await GetFreshDbContext().BettingRounds
            .Where(br => br.GameId == setup.Game.Id)
            .FirstOrDefaultAsync();

        bettingRound.Should().NotBeNull();
        bettingRound!.IsComplete.Should().BeFalse();
    }

    #endregion
}
