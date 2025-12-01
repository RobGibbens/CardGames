using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.Events;
using CardGames.Poker.Web.Services.StateManagement;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Web.Tests.StateManagement;

public class EventReplayServiceTests
{
    private readonly EventReplayService _replayService;
    private readonly ILogger<EventReplayService> _logger;

    public EventReplayServiceTests()
    {
        _logger = Substitute.For<ILogger<EventReplayService>>();
        _replayService = new EventReplayService(_logger);
    }

    [Fact]
    public void RecordEvent_StoresEvent()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var evt = new BettingActionEvent(
            tableId,
            DateTime.UtcNow,
            new BettingActionDto("Player1", BettingActionType.Bet, 100, DateTime.UtcNow),
            100);

        // Act
        _replayService.RecordEvent(evt);

        // Assert
        _replayService.GetEventCount(tableId).Should().Be(1);
    }

    [Fact]
    public void GetAllEvents_ReturnsAllEventsInOrder()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var evt1 = new BettingActionEvent(
            tableId,
            DateTime.UtcNow,
            new BettingActionDto("Player1", BettingActionType.Bet, 100, DateTime.UtcNow),
            100);
        var evt2 = new BettingActionEvent(
            tableId,
            DateTime.UtcNow.AddSeconds(1),
            new BettingActionDto("Player2", BettingActionType.Call, 100, DateTime.UtcNow),
            200);

        _replayService.RecordEvent(evt1);
        _replayService.RecordEvent(evt2);

        // Act
        var events = _replayService.GetAllEvents(tableId);

        // Assert
        events.Should().HaveCount(2);
        events[0].Should().Be(evt1);
        events[1].Should().Be(evt2);
    }

    [Fact]
    public void GetEventsForReplay_ReturnsEventsAfterVersion()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            var evt = new BettingActionEvent(
                tableId,
                DateTime.UtcNow.AddSeconds(i),
                new BettingActionDto($"Player{i}", BettingActionType.Bet, 100, DateTime.UtcNow),
                100 * (i + 1));
            _replayService.RecordEvent(evt);
        }

        // Act
        var events = _replayService.GetEventsForReplay(tableId, 2);

        // Assert
        events.Should().HaveCount(3);
    }

    [Fact]
    public void GetEventsForHand_ReturnsEventsForSpecificHand()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        
        var handStarted = new HandStartedEvent(tableId, DateTime.UtcNow, 1, 0, 25, 50);
        var bettingAction = new BettingActionEvent(
            tableId,
            DateTime.UtcNow.AddSeconds(1),
            new BettingActionDto("Player1", BettingActionType.Bet, 100, DateTime.UtcNow),
            100);
        var handCompleted = new HandCompletedEvent(
            tableId,
            DateTime.UtcNow.AddSeconds(2),
            1,
            new List<string> { "Player1" },
            "Player1 wins with a pair",
            new Dictionary<string, int> { { "Player1", 200 } },
            false);

        _replayService.RecordEvent(handStarted);
        _replayService.RecordEvent(bettingAction);
        _replayService.RecordEvent(handCompleted);

        // Act
        var events = _replayService.GetEventsForHand(tableId, 1);

        // Assert
        events.Should().HaveCount(2); // HandStartedEvent and HandCompletedEvent have HandNumber
    }

    [Fact]
    public void ClearOldEvents_RemovesEventsBeforeThreshold()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var oldTime = DateTime.UtcNow.AddMinutes(-10);
        var newTime = DateTime.UtcNow;

        var oldEvent = new BettingActionEvent(
            tableId,
            oldTime,
            new BettingActionDto("Player1", BettingActionType.Bet, 100, oldTime),
            100);
        var newEvent = new BettingActionEvent(
            tableId,
            newTime,
            new BettingActionDto("Player2", BettingActionType.Call, 100, newTime),
            200);

        _replayService.RecordEvent(oldEvent);
        _replayService.RecordEvent(newEvent);

        // Act
        _replayService.ClearOldEvents(tableId, DateTime.UtcNow.AddMinutes(-5));

        // Assert
        var events = _replayService.GetAllEvents(tableId);
        events.Should().ContainSingle();
        ((BettingActionEvent)events[0]).Action.PlayerName.Should().Be("Player2");
    }

    [Fact]
    public void ClearEvents_RemovesAllEventsForTable()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var evt = new BettingActionEvent(
            tableId,
            DateTime.UtcNow,
            new BettingActionDto("Player1", BettingActionType.Bet, 100, DateTime.UtcNow),
            100);
        _replayService.RecordEvent(evt);

        // Act
        _replayService.ClearEvents(tableId);

        // Assert
        _replayService.GetEventCount(tableId).Should().Be(0);
    }

    [Fact]
    public void GetAllEvents_ReturnsEmptyForUnknownTable()
    {
        // Arrange
        var tableId = Guid.NewGuid();

        // Act
        var events = _replayService.GetAllEvents(tableId);

        // Assert
        events.Should().BeEmpty();
    }

    [Fact]
    public void GetEventsForReplay_ReturnsEmptyForUnknownTable()
    {
        // Arrange
        var tableId = Guid.NewGuid();

        // Act
        var events = _replayService.GetEventsForReplay(tableId, 0);

        // Assert
        events.Should().BeEmpty();
    }
}
