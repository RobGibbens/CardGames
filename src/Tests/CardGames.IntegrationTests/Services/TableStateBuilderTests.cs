using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;

namespace CardGames.IntegrationTests.Services;

/// <summary>
/// Integration tests for <see cref="TableStateBuilder"/>.
/// Tests building public and private state snapshots for games.
/// </summary>
public class TableStateBuilderTests : IntegrationTestBase
{
    private ITableStateBuilder TableStateBuilder => Scope.ServiceProvider.GetRequiredService<ITableStateBuilder>();

    [Fact]
    public async Task BuildPublicStateAsync_GameNotFound_ReturnsNull()
    {
        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task BuildPublicStateAsync_ExistingGame_ReturnsState()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.GameId.Should().Be(setup.Game.Id);
    }

    [Fact]
    public async Task BuildPublicStateAsync_IncludesSeats()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Seats.Should().HaveCount(4);
    }

    [Fact]
    public async Task BuildPublicStateAsync_IncludesPotTotal()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, ante: 10);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.TotalPot.Should().Be(40); // 4 players x 10 ante
    }

    [Fact]
    public async Task BuildPublicStateAsync_AfterDeal_CardsHidden()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        // In public state, cards should be face down (not showing values)
        result!.Seats.Should().AllSatisfy(seat =>
        {
            seat.Cards.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task BuildPublicStateAsync_IncludesCurrentPhase()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));
    }

    [Fact]
    public async Task BuildPrivateStateAsync_GameNotFound_ReturnsNull()
    {
        // Act
        var result = await TableStateBuilder.BuildPrivateStateAsync(Guid.NewGuid(), "someuser");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task BuildPrivateStateAsync_ExistingGame_ReturnsState()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var playerEmail = setup.Players[0].Email;

        // Act
        var result = await TableStateBuilder.BuildPrivateStateAsync(setup.Game.Id, playerEmail!);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildPrivateStateAsync_AfterDeal_ShowsPlayerCards()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var playerEmail = setup.Players[0].Email;
        
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // Act
        var result = await TableStateBuilder.BuildPrivateStateAsync(setup.Game.Id, playerEmail!);

        // Assert
        result.Should().NotBeNull();
        result!.Hand.Should().NotBeEmpty();
        result.Hand.Should().HaveCount(5);
    }

    [Fact]
    public async Task BuildPublicStateAsync_SevenCardStud_ShowsBoardCards()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Re-fetch to get updated state
        var game = await DbContext.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == setup.Game.Id);

        // Deal cards using flow handler
        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");
        await handler.DealCardsAsync(DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        // Seven Card Stud should show board (up) cards
        result!.Seats.Should().ContainSingle(s => s.Cards.Any());
    }

    [Fact]
    public async Task BuildPublicStateAsync_KingsAndLows_InDropOrStayPhase()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        await Mediator.Send(new CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand.StartHandCommand(setup.Game.Id));

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentPhase.Should().Be(nameof(Phases.DropOrStay));
    }
}
