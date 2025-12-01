using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.Events;
using CardGames.Poker.Web.Services.StateManagement;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Web.Tests.StateManagement;

public class ConcurrentActionHandlerTests
{
    private readonly ConcurrentActionHandler _actionHandler;
    private readonly ILogger<ConcurrentActionHandler> _logger;
    private readonly IGameStateManager _stateManager;

    public ConcurrentActionHandlerTests()
    {
        _logger = Substitute.For<ILogger<ConcurrentActionHandler>>();
        _stateManager = Substitute.For<IGameStateManager>();
        _stateManager.StateVersion.Returns(1);
        _actionHandler = new ConcurrentActionHandler(_logger, _stateManager);
    }

    [Fact]
    public void TryQueueAction_SucceedsWhenNoCurrentPlayer()
    {
        // Arrange
        var action = CreatePendingAction("Player1");

        // Act
        var result = _actionHandler.TryQueueAction(action);

        // Assert
        result.Should().BeTrue();
        _actionHandler.HasPendingAction.Should().BeTrue();
        _actionHandler.CurrentPendingAction.Should().Be(action);
    }

    [Fact]
    public void TryQueueAction_SucceedsForCurrentPlayer()
    {
        // Arrange
        _actionHandler.SetCurrentPlayer("Player1");
        var action = CreatePendingAction("Player1");

        // Act
        var result = _actionHandler.TryQueueAction(action);

        // Assert
        result.Should().BeTrue();
        _actionHandler.HasPendingAction.Should().BeTrue();
    }

    [Fact]
    public void TryQueueAction_FailsForWrongPlayer()
    {
        // Arrange
        _actionHandler.SetCurrentPlayer("Player1");
        var action = CreatePendingAction("Player2");
        PendingAction? rejectedAction = null;
        string? rejectionReason = null;
        _actionHandler.OnActionRejected += (a, r) =>
        {
            rejectedAction = a;
            rejectionReason = r;
        };

        // Act
        var result = _actionHandler.TryQueueAction(action);

        // Assert
        result.Should().BeFalse();
        rejectedAction.Should().Be(action);
        rejectionReason.Should().Contain("not your turn");
    }

    [Fact]
    public void TryQueueAction_FailsForStaleStateVersion()
    {
        // Arrange
        _stateManager.StateVersion.Returns(10);
        var action = new PendingAction(
            Guid.NewGuid(),
            "Player1",
            BettingActionType.Bet,
            100,
            DateTime.UtcNow,
            5); // Stale version

        PendingAction? rejectedAction = null;
        _actionHandler.OnActionRejected += (a, _) => rejectedAction = a;

        // Act
        var result = _actionHandler.TryQueueAction(action);

        // Assert
        result.Should().BeFalse();
        rejectedAction.Should().Be(action);
    }

    [Fact]
    public void ConfirmAction_ClearsPendingAction()
    {
        // Arrange
        var action = CreatePendingAction("Player1");
        _actionHandler.TryQueueAction(action);
        ActionResult? result = null;
        _actionHandler.OnActionProcessed += r => result = r;

        // Act
        _actionHandler.ConfirmAction(action.Id);

        // Assert
        _actionHandler.HasPendingAction.Should().BeFalse();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.ActionId.Should().Be(action.Id);
    }

    [Fact]
    public void RejectAction_ClearsPendingAction()
    {
        // Arrange
        var action = CreatePendingAction("Player1");
        _actionHandler.TryQueueAction(action);
        ActionResult? result = null;
        _actionHandler.OnActionProcessed += r => result = r;

        // Act
        _actionHandler.RejectAction(action.Id, "Invalid action");

        // Assert
        _actionHandler.HasPendingAction.Should().BeFalse();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid action");
    }

    [Fact]
    public void CancelAction_ClearsPendingAction()
    {
        // Arrange
        var action = CreatePendingAction("Player1");
        _actionHandler.TryQueueAction(action);

        // Act
        _actionHandler.CancelAction(action.Id);

        // Assert
        _actionHandler.HasPendingAction.Should().BeFalse();
    }

    [Fact]
    public void ClearPendingActions_ClearsAll()
    {
        // Arrange
        var action = CreatePendingAction("Player1");
        _actionHandler.TryQueueAction(action);

        // Act
        _actionHandler.ClearPendingActions();

        // Assert
        _actionHandler.HasPendingAction.Should().BeFalse();
        _actionHandler.CurrentPendingAction.Should().BeNull();
    }

    [Fact]
    public void CanPlayerAct_ReturnsTrueWhenNoCurrentPlayerSet()
    {
        // Act
        var result = _actionHandler.CanPlayerAct("AnyPlayer");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanPlayerAct_ReturnsTrueForCurrentPlayer()
    {
        // Arrange
        _actionHandler.SetCurrentPlayer("Player1");

        // Act
        var result = _actionHandler.CanPlayerAct("Player1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanPlayerAct_ReturnsFalseForOtherPlayer()
    {
        // Arrange
        _actionHandler.SetCurrentPlayer("Player1");

        // Act
        var result = _actionHandler.CanPlayerAct("Player2");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SetCurrentPlayer_ClearsPendingActionFromPreviousPlayer()
    {
        // Arrange
        _actionHandler.SetCurrentPlayer("Player1");
        var action = CreatePendingAction("Player1");
        _actionHandler.TryQueueAction(action);

        // Act
        _actionHandler.SetCurrentPlayer("Player2");

        // Assert
        _actionHandler.HasPendingAction.Should().BeFalse();
    }

    [Fact]
    public void OnActionQueued_IsRaisedWhenActionQueued()
    {
        // Arrange
        PendingAction? queuedAction = null;
        _actionHandler.OnActionQueued += a => queuedAction = a;
        var action = CreatePendingAction("Player1");

        // Act
        _actionHandler.TryQueueAction(action);

        // Assert
        queuedAction.Should().Be(action);
    }

    [Fact]
    public void TryQueueAction_QueuesSecondActionWhenFirstIsPending()
    {
        // Arrange
        var action1 = CreatePendingAction("Player1");
        var action2 = CreatePendingAction("Player1");
        _actionHandler.TryQueueAction(action1);

        // Act
        var result = _actionHandler.TryQueueAction(action2);

        // Assert
        result.Should().BeTrue();
        _actionHandler.CurrentPendingAction.Should().Be(action1);
    }

    private static PendingAction CreatePendingAction(string playerName)
    {
        return new PendingAction(
            Guid.NewGuid(),
            playerName,
            BettingActionType.Bet,
            100,
            DateTime.UtcNow,
            1);
    }
}
