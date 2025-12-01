using CardGames.Poker.Shared.Events;

namespace CardGames.Poker.Web.Services.StateManagement;

/// <summary>
/// Service for managing event history and replaying events to rebuild state.
/// </summary>
public interface IEventReplayService
{
    /// <summary>
    /// Gets the number of events stored for a table.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    int GetEventCount(Guid tableId);

    /// <summary>
    /// Records an event for later replay.
    /// </summary>
    /// <param name="gameEvent">The event to record.</param>
    void RecordEvent(GameEvent gameEvent);

    /// <summary>
    /// Gets events for replay from a specific version.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="fromVersion">The version to start replaying from (exclusive).</param>
    /// <returns>Events to replay in order.</returns>
    IReadOnlyList<GameEvent> GetEventsForReplay(Guid tableId, long fromVersion);

    /// <summary>
    /// Gets all events for a table.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <returns>All recorded events in order.</returns>
    IReadOnlyList<GameEvent> GetAllEvents(Guid tableId);

    /// <summary>
    /// Gets events for a specific hand.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="handNumber">The hand number.</param>
    /// <returns>Events for the specified hand.</returns>
    IReadOnlyList<GameEvent> GetEventsForHand(Guid tableId, int handNumber);

    /// <summary>
    /// Clears events older than a specific threshold.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="olderThan">Clear events older than this time.</param>
    void ClearOldEvents(Guid tableId, DateTime olderThan);

    /// <summary>
    /// Clears all events for a table.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    void ClearEvents(Guid tableId);
}

/// <summary>
/// Implementation of event replay service for managing event history.
/// </summary>
public class EventReplayService : IEventReplayService
{
    private readonly ILogger<EventReplayService> _logger;
    private readonly Dictionary<Guid, List<RecordedEvent>> _eventHistory = new();
    private readonly object _lock = new();
    private readonly int _maxEventsPerTable = 1000;

    public EventReplayService(ILogger<EventReplayService> logger)
    {
        _logger = logger;
    }

    public int GetEventCount(Guid tableId)
    {
        lock (_lock)
        {
            return _eventHistory.TryGetValue(tableId, out var events) ? events.Count : 0;
        }
    }

    public void RecordEvent(GameEvent gameEvent)
    {
        lock (_lock)
        {
            if (!_eventHistory.TryGetValue(gameEvent.GameId, out var events))
            {
                events = new List<RecordedEvent>();
                _eventHistory[gameEvent.GameId] = events;
            }

            var recordedEvent = new RecordedEvent(
                events.Count + 1,
                gameEvent,
                GetHandNumberFromEvent(gameEvent));

            events.Add(recordedEvent);

            // Trim old events if necessary
            if (events.Count > _maxEventsPerTable)
            {
                var removeCount = events.Count - _maxEventsPerTable;
                events.RemoveRange(0, removeCount);

                _logger.LogDebug(
                    "Trimmed {Count} old events for table {TableId}",
                    removeCount, gameEvent.GameId);
            }

            _logger.LogTrace(
                "Recorded event {EventType} for table {TableId}, sequence {Sequence}",
                gameEvent.GetType().Name, gameEvent.GameId, recordedEvent.Sequence);
        }
    }

    public IReadOnlyList<GameEvent> GetEventsForReplay(Guid tableId, long fromVersion)
    {
        lock (_lock)
        {
            if (!_eventHistory.TryGetValue(tableId, out var events))
            {
                return [];
            }

            var eventsToReplay = events
                .Where(e => e.Sequence > fromVersion)
                .Select(e => e.Event)
                .ToList();

            _logger.LogDebug(
                "Retrieved {Count} events for replay from version {Version} for table {TableId}",
                eventsToReplay.Count, fromVersion, tableId);

            return eventsToReplay;
        }
    }

    public IReadOnlyList<GameEvent> GetAllEvents(Guid tableId)
    {
        lock (_lock)
        {
            if (!_eventHistory.TryGetValue(tableId, out var events))
            {
                return [];
            }

            return events.Select(e => e.Event).ToList();
        }
    }

    public IReadOnlyList<GameEvent> GetEventsForHand(Guid tableId, int handNumber)
    {
        lock (_lock)
        {
            if (!_eventHistory.TryGetValue(tableId, out var events))
            {
                return [];
            }

            return events
                .Where(e => e.HandNumber == handNumber)
                .Select(e => e.Event)
                .ToList();
        }
    }

    public void ClearOldEvents(Guid tableId, DateTime olderThan)
    {
        lock (_lock)
        {
            if (!_eventHistory.TryGetValue(tableId, out var events))
            {
                return;
            }

            var removeCount = events.RemoveAll(e => e.Event.Timestamp < olderThan);

            if (removeCount > 0)
            {
                _logger.LogDebug(
                    "Cleared {Count} old events for table {TableId}",
                    removeCount, tableId);
            }
        }
    }

    public void ClearEvents(Guid tableId)
    {
        lock (_lock)
        {
            if (_eventHistory.Remove(tableId))
            {
                _logger.LogDebug("Cleared all events for table {TableId}", tableId);
            }
        }
    }

    private static int GetHandNumberFromEvent(GameEvent gameEvent)
    {
        return gameEvent switch
        {
            HandStartedEvent e => e.HandNumber,
            HandCompletedEvent e => e.HandNumber,
            ShowdownStartedEvent e => e.HandNumber,
            ShowdownCompletedEvent e => e.HandNumber,
            DealerButtonMovedEvent e => e.HandNumber,
            MissedBlindRecordedEvent e => e.HandNumber,
            _ => 0
        };
    }

    private record RecordedEvent(
        long Sequence,
        GameEvent Event,
        int HandNumber);
}
