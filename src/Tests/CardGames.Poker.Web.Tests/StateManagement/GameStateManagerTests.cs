using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.Events;
using CardGames.Poker.Web.Services.StateManagement;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Web.Tests.StateManagement;

public class GameStateManagerTests
{
    private readonly GameStateManager _stateManager;
    private readonly ILogger<GameStateManager> _logger;

    public GameStateManagerTests()
    {
        _logger = Substitute.For<ILogger<GameStateManager>>();
        _stateManager = new GameStateManager(_logger);
    }

    [Fact]
    public void Initialize_SetsCurrentState()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();

        // Act
        _stateManager.Initialize(tableId, snapshot);

        // Assert
        _stateManager.CurrentState.Should().NotBeNull();
        _stateManager.CurrentState!.TableId.Should().Be(tableId);
        _stateManager.StateVersion.Should().Be(1);
    }

    [Fact]
    public void Initialize_RaisesStateChangedEvent()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        GameStateSnapshot? receivedState = null;
        _stateManager.OnStateChanged += state => receivedState = state;

        // Act
        _stateManager.Initialize(tableId, snapshot);

        // Assert
        receivedState.Should().NotBeNull();
        receivedState!.TableId.Should().Be(tableId);
    }

    [Fact]
    public void ApplyServerEvent_UpdatesStateForBettingAction()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        var bettingAction = new BettingActionEvent(
            tableId,
            DateTime.UtcNow,
            new BettingActionDto("Player1", BettingActionType.Bet, 100, DateTime.UtcNow),
            100);

        // Act
        _stateManager.ApplyServerEvent(bettingAction);

        // Assert
        _stateManager.CurrentState.Should().NotBeNull();
        _stateManager.CurrentState!.Pot.Should().Be(100);
        _stateManager.StateVersion.Should().Be(2);
    }

    [Fact]
    public void ApplyServerEvent_UpdatesPlayerFoldStatus()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        var foldAction = new BettingActionEvent(
            tableId,
            DateTime.UtcNow,
            new BettingActionDto("Player1", BettingActionType.Fold, 0, DateTime.UtcNow),
            0);

        // Act
        _stateManager.ApplyServerEvent(foldAction);

        // Assert
        var player1 = _stateManager.CurrentState!.Players.First(p => p.Name == "Player1");
        player1.HasFolded.Should().BeTrue();
    }

    [Fact]
    public void ApplyOptimisticUpdate_CreatesPendingUpdate()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        var update = new OptimisticUpdate(
            OptimisticUpdateType.PlayerAction,
            "Player1",
            BettingActionType.Bet,
            100);

        // Act
        var updateId = _stateManager.ApplyOptimisticUpdate(update);

        // Assert
        _stateManager.HasPendingUpdates.Should().BeTrue();
        updateId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ConfirmOptimisticUpdate_RemovesPendingUpdate()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        var update = new OptimisticUpdate(
            OptimisticUpdateType.PlayerAction,
            "Player1",
            BettingActionType.Bet,
            100);

        var updateId = _stateManager.ApplyOptimisticUpdate(update);

        // Act
        _stateManager.ConfirmOptimisticUpdate(updateId);

        // Assert
        _stateManager.HasPendingUpdates.Should().BeFalse();
    }

    [Fact]
    public void RejectOptimisticUpdate_RollsBackState()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        var initialVersion = _stateManager.StateVersion;
        var initialPlayer = _stateManager.CurrentState!.Players.First(p => p.Name == "Player1");
        var initialChips = initialPlayer.ChipStack;

        var update = new OptimisticUpdate(
            OptimisticUpdateType.PlayerAction,
            "Player1",
            BettingActionType.Bet,
            100);

        var updateId = _stateManager.ApplyOptimisticUpdate(update);

        // Act
        _stateManager.RejectOptimisticUpdate(updateId, "Invalid action");

        // Assert
        _stateManager.HasPendingUpdates.Should().BeFalse();
        _stateManager.StateVersion.Should().Be(initialVersion);
        _stateManager.CurrentState!.Players.First(p => p.Name == "Player1").ChipStack.Should().Be(initialChips);
    }

    [Fact]
    public void CreateSnapshot_CapturesCurrentState()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        // Act
        var stateSnapshot = _stateManager.CreateSnapshot();

        // Assert
        stateSnapshot.Should().NotBeNull();
        stateSnapshot.Version.Should().Be(_stateManager.StateVersion);
        stateSnapshot.State.Should().NotBeNull();
        stateSnapshot.State.TableId.Should().Be(tableId);
    }

    [Fact]
    public void RestoreSnapshot_RestoresState()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        var stateSnapshot = _stateManager.CreateSnapshot();

        // Modify state
        var bettingAction = new BettingActionEvent(
            tableId,
            DateTime.UtcNow,
            new BettingActionDto("Player1", BettingActionType.Bet, 100, DateTime.UtcNow),
            100);
        _stateManager.ApplyServerEvent(bettingAction);

        // Act
        _stateManager.RestoreSnapshot(stateSnapshot);

        // Assert
        _stateManager.StateVersion.Should().Be(stateSnapshot.Version);
        _stateManager.CurrentState!.Pot.Should().Be(0);
    }

    [Fact]
    public void ValidateAgainstServer_ReturnsValidWhenStatesMatch()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        // Act
        var result = _stateManager.ValidateAgainstServer(snapshot);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAgainstServer_DetectsPotMismatch()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        // Create a server snapshot with different pot
        var serverSnapshot = snapshot with { Pot = 500 };

        // Act
        var result = _stateManager.ValidateAgainstServer(serverSnapshot);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Conflicts.Should().ContainSingle(c => c.PropertyName == "Pot");
    }

    [Fact]
    public void ReconcileWithServer_ReplacesLocalState()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        var serverSnapshot = snapshot with { Pot = 500, CurrentBet = 100 };

        // Act
        _stateManager.ReconcileWithServer(serverSnapshot);

        // Assert
        _stateManager.CurrentState!.Pot.Should().Be(500);
        _stateManager.CurrentState.CurrentBet.Should().Be(100);
    }

    [Fact]
    public void ReplayEvents_AppliesEventsInOrder()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        var events = new List<GameEvent>
        {
            new BettingActionEvent(tableId, DateTime.UtcNow.AddSeconds(1),
                new BettingActionDto("Player1", BettingActionType.Bet, 50, DateTime.UtcNow), 50),
            new BettingActionEvent(tableId, DateTime.UtcNow.AddSeconds(2),
                new BettingActionDto("Player2", BettingActionType.Call, 50, DateTime.UtcNow), 100),
        };

        // Act
        _stateManager.ReplayEvents(events);

        // Assert
        _stateManager.CurrentState!.Pot.Should().Be(100);
        _stateManager.StateVersion.Should().Be(3);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        // Act
        _stateManager.Reset();

        // Assert
        _stateManager.CurrentState.Should().BeNull();
        _stateManager.StateVersion.Should().Be(0);
        _stateManager.HasPendingUpdates.Should().BeFalse();
    }

    [Fact]
    public void ApplyServerEvent_HandlesPlayerTurnEvent()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        var playerTurnEvent = new PlayerTurnEvent(
            tableId,
            DateTime.UtcNow,
            "Player2",
            new AvailableActionsDto("Player2", true, true, true, true, true, true, 10, 1000, 50, 100, 1000));

        // Act
        _stateManager.ApplyServerEvent(playerTurnEvent);

        // Assert
        _stateManager.CurrentState!.CurrentPlayerName.Should().Be("Player2");
        var player2 = _stateManager.CurrentState.Players.First(p => p.Name == "Player2");
        player2.IsCurrentPlayer.Should().BeTrue();
    }

    [Fact]
    public void ApplyServerEvent_HandlesCommunityCardsDealt()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        var cards = new List<CardDto>
        {
            new CardDto("A", "s", "As"),
            new CardDto("K", "h", "Kh"),
            new CardDto("Q", "d", "Qd"),
        };

        var communityCardsEvent = new CommunityCardsDealtEvent(
            tableId,
            DateTime.UtcNow,
            "Flop",
            cards,
            cards);

        // Act
        _stateManager.ApplyServerEvent(communityCardsEvent);

        // Assert
        _stateManager.CurrentState!.CommunityCards.Should().NotBeNull();
        _stateManager.CurrentState.CommunityCards!.Should().HaveCount(3);
        _stateManager.CurrentState.CurrentStreet.Should().Be("Flop");
    }

    [Fact]
    public void ApplyServerEvent_HandlesHandStarted()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var snapshot = CreateTestSnapshot();
        _stateManager.Initialize(tableId, snapshot);

        var handStartedEvent = new HandStartedEvent(
            tableId,
            DateTime.UtcNow,
            2,
            1,
            25,
            50);

        // Act
        _stateManager.ApplyServerEvent(handStartedEvent);

        // Assert
        _stateManager.CurrentState!.HandNumber.Should().Be(2);
        _stateManager.CurrentState.DealerPosition.Should().Be(1);
        _stateManager.CurrentState.SmallBlind.Should().Be(25);
        _stateManager.CurrentState.BigBlind.Should().Be(50);
        _stateManager.CurrentState.Pot.Should().Be(0);
        _stateManager.CurrentState.CommunityCards.Should().BeNull();
    }

    private static TableStateSnapshot CreateTestSnapshot()
    {
        return new TableStateSnapshot(
            Guid.NewGuid(),
            "Test Table",
            PokerVariant.TexasHoldem,
            GameState.BettingRound,
            new List<PlayerStateSnapshot>
            {
                new PlayerStateSnapshot("Player1", 0, 1000, 0, false, false, true, false, null),
                new PlayerStateSnapshot("Player2", 1, 1000, 0, false, false, true, false, null),
            },
            null,
            0,
            0,
            "Player1",
            25,
            50,
            0,
            0,
            null,
            1,
            null);
    }
}
