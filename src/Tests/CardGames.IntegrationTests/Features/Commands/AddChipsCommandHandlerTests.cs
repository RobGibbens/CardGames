using CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for <see cref="AddChipsCommandHandler"/>.
/// Tests chip management including immediate and pending additions.
/// </summary>
public class AddChipsCommandHandlerTests : IntegrationTestBase
{
    [Fact]
    public async Task Handle_ValidAmount_AddsChipsToPlayer()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, startingChips: 500);
        var player = setup.GamePlayers[0];
        var command = new AddChipsCommand(setup.Game.Id, player.PlayerId, 200);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        // During WaitingToStart, chips should be added immediately or queued based on game phase
    }

    [Fact]
    public async Task Handle_ZeroAmount_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var player = setup.GamePlayers[0];
        var command = new AddChipsCommand(setup.Game.Id, player.PlayerId, 0);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(AddChipsErrorCode.InvalidAmount);
    }

    [Fact]
    public async Task Handle_NegativeAmount_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var player = setup.GamePlayers[0];
        var command = new AddChipsCommand(setup.Game.Id, player.PlayerId, -100);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(AddChipsErrorCode.InvalidAmount);
    }

    [Fact]
    public async Task Handle_GameNotFound_ReturnsError()
    {
        // Arrange
        var command = new AddChipsCommand(Guid.NewGuid(), Guid.NewGuid(), 100);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(AddChipsErrorCode.GameNotFound);
    }

    [Fact]
    public async Task Handle_PlayerNotInGame_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var command = new AddChipsCommand(setup.Game.Id, Guid.NewGuid(), 100);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(AddChipsErrorCode.PlayerNotInGame);
    }

    [Fact]
    public async Task Handle_CompletedGame_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        setup.Game.Status = GameStatus.Completed;
        await DbContext.SaveChangesAsync();

        var player = setup.GamePlayers[0];
        var command = new AddChipsCommand(setup.Game.Id, player.PlayerId, 100);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(AddChipsErrorCode.GameEnded);
    }

    [Fact]
    public async Task Handle_CancelledGame_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        setup.Game.Status = GameStatus.Cancelled;
        await DbContext.SaveChangesAsync();

        var player = setup.GamePlayers[0];
        var command = new AddChipsCommand(setup.Game.Id, player.PlayerId, 100);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(AddChipsErrorCode.GameEnded);
    }

    [Fact]
    public async Task Handle_KingsAndLows_AlwaysAppliesImmediately()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 2, startingChips: 500);
        setup.Game.Status = GameStatus.InProgress;
        setup.Game.CurrentPhase = nameof(Phases.FirstBettingRound);
        await DbContext.SaveChangesAsync();

        var player = setup.GamePlayers[0];
        var initialChips = player.ChipStack;
        var command = new AddChipsCommand(setup.Game.Id, player.PlayerId, 200);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.AppliedImmediately.Should().BeTrue();
        success.NewChipStack.Should().Be(initialChips + 200);
    }

    [Fact]
    public async Task Handle_BetweenHands_AppliesImmediately()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, startingChips: 500);
        setup.Game.CurrentPhase = "BetweenHands";
        await DbContext.SaveChangesAsync();

        var player = setup.GamePlayers[0];
        var initialChips = player.ChipStack;
        var command = new AddChipsCommand(setup.Game.Id, player.PlayerId, 200);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.AppliedImmediately.Should().BeTrue();
        success.NewChipStack.Should().Be(initialChips + 200);
    }

    [Fact]
    public async Task Handle_DuringHand_QueuesChips()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, startingChips: 500);
        setup.Game.Status = GameStatus.InProgress;
        setup.Game.CurrentPhase = nameof(Phases.FirstBettingRound);
        await DbContext.SaveChangesAsync();

        var player = setup.GamePlayers[0];
        var initialChips = player.ChipStack;
        var command = new AddChipsCommand(setup.Game.Id, player.PlayerId, 200);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.AppliedImmediately.Should().BeFalse();
        success.NewChipStack.Should().Be(initialChips); // Chips not added yet
        success.PendingChipsToAdd.Should().Be(200);
    }

    [Fact]
    public async Task Handle_MultipleQueuedAdditions_Accumulate()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, startingChips: 500);
        setup.Game.Status = GameStatus.InProgress;
        setup.Game.CurrentPhase = nameof(Phases.FirstBettingRound);
        await DbContext.SaveChangesAsync();

        var player = setup.GamePlayers[0];
        var command1 = new AddChipsCommand(setup.Game.Id, player.PlayerId, 100);
        var command2 = new AddChipsCommand(setup.Game.Id, player.PlayerId, 150);

        // Act
        await Mediator.Send(command1);
        var result = await Mediator.Send(command2);

        // Assert
        result.IsT0.Should().BeTrue();
        result.AsT0.PendingChipsToAdd.Should().Be(250);
    }

    [Fact]
    public async Task Handle_ResponseIncludesMessage()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var player = setup.GamePlayers[0];
        var command = new AddChipsCommand(setup.Game.Id, player.PlayerId, 100);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        result.AsT0.Message.Should().NotBeNullOrEmpty();
        result.AsT0.Message.Should().Contain("100");
    }
}
