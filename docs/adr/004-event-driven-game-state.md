# ADR-004: Event-Driven Game State Management

## Status

Accepted

## Context

Poker games require managing complex state that must be:

1. **Synchronized** across multiple clients in real-time
2. **Auditable** for hand history and replay
3. **Recoverable** after disconnections/reconnections
4. **Consistent** even with concurrent actions
5. **Trackable** for animations and UI transitions

Traditional request-response patterns are insufficient because:
- Multiple clients need updates simultaneously
- State changes must be ordered correctly
- Clients need to animate transitions (card dealing, chips moving)
- Audit logging requires event history

## Decision

We will use an **event-driven architecture** for game state management where:

1. **All state changes are represented as events**
2. **Events are broadcast to relevant clients via SignalR**
3. **Events are stored for audit and replay**
4. **Client state is rebuilt from events on reconnection**

### Event Types

```csharp
// Base event type
public abstract record GameEvent(Guid GameId, DateTime Timestamp);

// Game flow events
public record HandStartedEvent(...) : GameEvent;
public record BettingActionEvent(...) : GameEvent;
public record CommunityCardsDealtEvent(...) : GameEvent;
public record ShowdownCompletedEvent(...) : GameEvent;

// Connection events
public record PlayerConnectedEvent(...) : GameEvent;
public record PlayerReconnectedEvent(...) : GameEvent;

// UI-focused events
public record DealCardEvent(...) : GameEvent;
public record TurnTimerStartedEvent(...) : GameEvent;
```

### Event Flow

```
Player Action → Command → Handler → Game Logic → Event(s) → SignalR Hub → Clients
                                         ↓
                                   Audit Logger
                                         ↓
                                   Event Store
```

### State Synchronization

```csharp
// Full state snapshot for reconnection
public record TableStateSyncEvent(
    Guid TableId,
    DateTime Timestamp,
    TableStateSnapshot Snapshot) : GameEvent(TableId, Timestamp);

// Clients request sync on reconnect
await hubConnection.invoke("RequestTableState", tableId);
```

## Consequences

### Positive

- **Real-time sync** - All clients receive updates simultaneously
- **Audit trail** - Complete history of game actions
- **Replay capability** - Can reconstruct game state from events
- **Animation support** - Events contain timing/sequence info
- **Disconnection recovery** - Full state sync on reconnect
- **Loose coupling** - Components communicate via events
- **Testability** - Can test by replaying event sequences

### Negative

- **Eventual consistency** - Brief inconsistency window possible
- **Event versioning** - Schema changes require migration strategy
- **Increased complexity** - More moving parts than direct state updates
- **Storage requirements** - Event history consumes storage

### Neutral

- **Learning curve** - Event-driven thinking required
- **Debugging** - Need tools to visualize event flow

## Event Categories

### 1. Game State Events

Core gameplay events that change game state:

| Event | Purpose |
|-------|---------|
| `HandStartedEvent` | New hand begins |
| `BettingActionEvent` | Player bet/fold/check/etc. |
| `CommunityCardsDealtEvent` | Flop/Turn/River dealt |
| `HoleCardsDealtEvent` | Private cards to player |
| `ShowdownCompletedEvent` | Hand finished, winners determined |

### 2. Animation Events

Events for client-side animation:

| Event | Purpose |
|-------|---------|
| `DealCardEvent` | Single card dealt (with sequence) |
| `DealingStartedEvent` | Dealing phase begins |
| `ShowdownAnimationReadyEvent` | Animation steps queued |

### 3. Timer Events

Turn timer management:

| Event | Purpose |
|-------|---------|
| `TurnTimerStartedEvent` | Player's timer begins |
| `TurnTimerTickEvent` | Timer update (sent periodically) |
| `TurnTimerExpiredEvent` | Timer ran out, default action |

### 4. Connection Events

Player connection state:

| Event | Purpose |
|-------|---------|
| `PlayerConnectedEvent` | Player joined table |
| `PlayerDisconnectedEvent` | Player left/disconnected |
| `PlayerReconnectedEvent` | Player reconnected |

## Implementation Guidelines

### Event Design Principles

1. **Immutable** - Events are read-only records
2. **Self-contained** - Include all data needed to process
3. **Ordered** - Timestamp and sequence for ordering
4. **Typed** - Strongly typed for compile-time safety

### Example Event Flow

```csharp
// 1. Player action received
await _hub.Call(tableId, 50);

// 2. Game logic processes action
var action = new BettingActionDto("Alice", BettingActionType.Call, 50, DateTime.UtcNow);
var evt = new BettingActionEvent(tableId, DateTime.UtcNow, action, pot);

// 3. Event broadcast to all players at table
await Clients.Group(tableId).SendAsync("PlayerAction", evt);

// 4. Event logged for audit
await _auditLogger.LogActionAsync(evt);

// 5. Clients update their state
connection.on("PlayerAction", (event) => {
    updateGameState(event);
    animateChipMovement(event.action);
});
```

### State Reconstruction

```csharp
// On reconnection, request full state
connection.on("TableStateSync", (event) => {
    rebuildFullState(event.snapshot);
    resumeGamePlay();
});

// Server builds snapshot from current state
public async Task RequestTableState(string tableId)
{
    var snapshot = await _gameService.GetTableSnapshot(tableId);
    await Clients.Caller.SendAsync("TableStateSync", new TableStateSyncEvent(
        Guid.Parse(tableId),
        DateTime.UtcNow,
        snapshot));
}
```

## Alternatives Considered

### 1. Direct State Updates

**Rejected because:**
- No audit trail
- Difficult to sync multiple clients
- No animation/transition support
- Hard to recover from disconnections

### 2. Polling-Based Updates

**Rejected because:**
- Inefficient bandwidth usage
- Latency between state changes
- Poor user experience
- Scales poorly

### 3. CQRS with Event Sourcing

**Partially adopted:**
- We use events for communication and audit
- But don't rebuild all state from events (too slow)
- Hybrid: current state + event log

### 4. Server-Sent Events (SSE)

**Rejected because:**
- One-way communication only
- Would need separate REST for player actions
- SignalR already provides bi-directional

## References

- [Event-Driven Architecture - Martin Fowler](https://martinfowler.com/articles/201701-event-driven.html)
- [Event Sourcing - Microsoft](https://docs.microsoft.com/en-us/azure/architecture/patterns/event-sourcing)
- [Real-Time Web - SignalR Patterns](https://docs.microsoft.com/en-us/aspnet/core/signalr/introduction)
