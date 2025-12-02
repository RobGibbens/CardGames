# SignalR API Documentation

This document provides comprehensive documentation for the CardGames SignalR real-time API. The API is implemented via the `GameHub` class and provides real-time communication for poker game operations.

## Table of Contents

- [Connection Lifecycle](#connection-lifecycle)
- [Hub Methods (Client → Server)](#hub-methods-client--server)
  - [Connection Management](#connection-management)
  - [Table Actions](#table-actions)
  - [Betting Actions](#betting-actions)
  - [Timer Management](#timer-management)
- [Client Events (Server → Client)](#client-events-server--client)
  - [Connection Events](#connection-events)
  - [Game Events](#game-events)
  - [Showdown Events](#showdown-events)
  - [Timer Events](#timer-events)
  - [Chat Events](#chat-events)
  - [Dealer Button and Blinds Events](#dealer-button-and-blinds-events)
- [Event Payloads](#event-payloads)
- [Error Handling](#error-handling)
- [Best Practices](#best-practices)

---

## Connection Lifecycle

### Connecting to the Hub

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/gamehub")
    .withAutomaticReconnect()
    .build();

await connection.start();
```

### Connection States

| State | Description |
|-------|-------------|
| `Connected` | Active connection to the hub |
| `Disconnected` | Connection has been terminated |
| `Reconnecting` | Attempting to restore a lost connection |

### Connection Events

When a client connects, the server sends a `Connected` event with the connection ID:

```javascript
connection.on("Connected", (connectionId) => {
    console.log("Connected with ID:", connectionId);
});
```

---

## Hub Methods (Client → Server)

### Connection Management

#### `OnConnectedAsync`
Called automatically when a client connects.

**Response Event:** `Connected`
```typescript
// Event payload
{
    connectionId: string
}
```

#### `OnDisconnectedAsync`
Called automatically when a client disconnects. Notifies other players at the same table.

**Response Event (to group):** `PlayerDisconnected`

#### `JoinGame(gameId: string)`
Joins a specific game room/group for receiving game updates.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `gameId` | `string` | The game identifier to join |

**Response Event (to group):** `PlayerJoined`
```typescript
{
    connectionId: string,
    gameId: string
}
```

#### `LeaveGame(gameId: string)`
Leaves a specific game room/group.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `gameId` | `string` | The game identifier to leave |

**Response Event (to group):** `PlayerLeft`

#### `JoinLobby()`
Joins the lobby group to receive table updates and notifications.

**Response Event (to caller):** `JoinedLobby`
```typescript
{
    connectionId: string
}
```

#### `LeaveLobby()`
Leaves the lobby group.

#### `JoinWaitingList(tableId: string)`
Joins a table's waiting list notification group.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `tableId` | `string` | The table identifier |

**Response Event (to caller):** `JoinedWaitingList`

#### `LeaveWaitingListGroup(tableId: string)`
Leaves a table's waiting list notification group.

### Table Actions

#### `JoinTable(tableId: string, playerName: string)`
Joins a table as a seated player with connection tracking.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `tableId` | `string` | The table identifier |
| `playerName` | `string` | The player's display name |

**Response Events:**
- **New connection:** `PlayerConnected` (to group)
- **Reconnection:** `PlayerReconnected` (to group)

```typescript
// PlayerConnectedEvent
{
    tableId: Guid,
    timestamp: DateTime,
    playerName: string,
    connectionId: string
}

// PlayerReconnectedEvent
{
    tableId: Guid,
    timestamp: DateTime,
    playerName: string,
    disconnectedDuration: TimeSpan
}
```

#### `LeaveTable(tableId: string)`
Leaves a table as a seated player.

**Response Event (to group):** `PlayerDisconnected`

#### `RequestTableState(tableId: string)`
Requests full table state synchronization (useful after reconnection).

**Response Event (to caller):** `TableStateRequested`

#### `SendMessage(message: string)`
Sends a message to all connected clients.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `message` | `string` | The message to broadcast |

**Response Event (to all):** `ReceiveMessage`
```typescript
{
    message: string,
    timestamp: DateTime
}
```

#### `Heartbeat()`
Sends a heartbeat to keep the connection alive and update activity timestamp.

**Response Event (to caller):** `HeartbeatAck`
```typescript
{
    timestamp: DateTime
}
```

#### `GetConnectionInfo()`
Gets connection information for the current connection.

**Response Event (to caller):** `ConnectionInfo`
```typescript
{
    connectionId: string,
    playerName: string | null,
    tableId: string | null,
    isConnected: boolean,
    connectedAt: DateTime | null,
    lastActivity: DateTime | null
}
```

### Betting Actions

All betting actions are validated to ensure the player is seated at the correct table.

#### `Fold(tableId: string)`
Performs a fold action, forfeiting the current hand.

**Response Event (to group):** `PlayerAction`

#### `Check(tableId: string)`
Performs a check action (when no bet to call).

**Response Event (to group):** `PlayerAction`

#### `Call(tableId: string, amount: number)`
Calls the current bet.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `tableId` | `string` | The table identifier |
| `amount` | `number` | The amount to call |

**Response Event (to group):** `PlayerAction`

#### `Bet(tableId: string, amount: number)`
Places a bet.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `tableId` | `string` | The table identifier |
| `amount` | `number` | The amount to bet |

**Response Event (to group):** `PlayerAction`

#### `Raise(tableId: string, amount: number)`
Raises the current bet.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `tableId` | `string` | The table identifier |
| `amount` | `number` | The total amount to raise to |

**Response Event (to group):** `PlayerAction`

#### `AllIn(tableId: string, amount: number)`
Goes all-in with remaining chip stack.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `tableId` | `string` | The table identifier |
| `amount` | `number` | The player's remaining chip stack |

**Response Event (to group):** `PlayerAction`

**Error Response (to caller):** `ActionRejected`
```typescript
{
    reason: string
}
```

### Timer Management

#### `UseTimeBank(tableId: string)`
Requests to use time bank for additional decision time.

**Response Events:**
- **Success:** `TimeBankRequested` (to caller)
- **Error:** `TimeBankRejected` (to caller)

---

## Client Events (Server → Client)

### Connection Events

| Event | Description | Payload |
|-------|-------------|---------|
| `Connected` | Initial connection established | `connectionId: string` |
| `PlayerConnected` | Player joined the table | `PlayerConnectedEvent` |
| `PlayerDisconnected` | Player left the table | `PlayerDisconnectedEvent` |
| `PlayerReconnected` | Player reconnected | `PlayerReconnectedEvent` |
| `TableStateSync` | Full table state snapshot | `TableStateSyncEvent` |
| `ConnectionInfo` | Connection details response | Connection info object |
| `HeartbeatAck` | Heartbeat acknowledgment | `timestamp: DateTime` |

### Game Events

| Event | Description | Payload |
|-------|-------------|---------|
| `PlayerJoined` | Player joined game group | `connectionId, gameId` |
| `PlayerLeft` | Player left game group | `connectionId, gameId` |
| `PlayerAction` | Betting action performed | `BettingActionEvent` |
| `JoinedLobby` | Joined lobby successfully | `connectionId` |
| `JoinedWaitingList` | Joined waiting list | `tableId` |
| `TableStateRequested` | Table state request acknowledged | `tableId` |
| `PrivateData` | Private player data (hole cards) | `PrivatePlayerDataEvent` |
| `ReceiveMessage` | Broadcast message received | `message, timestamp` |
| `ActionRejected` | Player action rejected | `reason: string` |

### Showdown Events

| Event | Description | Payload |
|-------|-------------|---------|
| `ShowdownStarted` | Showdown phase begins | `ShowdownStartedEvent` |
| `PlayerRevealedCards` | Player shows cards | `PlayerRevealedCardsEvent` |
| `PlayerMuckedCards` | Player mucks cards | `PlayerMuckedCardsEvent` |
| `ShowdownTurn` | Player's turn at showdown | `ShowdownTurnEvent` |
| `ShowdownCompleted` | Showdown finished | `ShowdownCompletedEvent` |
| `PrivateHoleCards` | Private hole cards to player | `playerName, cards[]` |
| `WinnerAnnouncement` | Winner declaration | `WinnerAnnouncementEvent` |
| `AllInBoardRunOutStarted` | All-in run-out begins | `AllInBoardRunOutStartedEvent` |
| `AllInBoardCardRevealed` | Board card revealed | `AllInBoardCardRevealedEvent` |
| `AutoReveal` | Automatic card reveal | `AutoRevealEvent` |
| `ShowdownAnimationReady` | Animation sequence ready | `ShowdownAnimationReadyEvent` |

### Timer Events

| Event | Description | Payload |
|-------|-------------|---------|
| `TurnTimerStarted` | Player's timer started | `TurnTimerStartedEvent` |
| `TurnTimerTick` | Timer tick (seconds remaining) | `TurnTimerTickEvent` |
| `TurnTimerWarning` | Timer about to expire | `TurnTimerWarningEvent` |
| `TurnTimerExpired` | Timer expired, default action | `TurnTimerExpiredEvent` |
| `TimeBankActivated` | Time bank used | `TimeBankActivatedEvent` |
| `TurnTimerStopped` | Timer stopped (action taken) | `TurnTimerStoppedEvent` |
| `TimeBankRequested` | Time bank request ack | `tableId, playerName` |
| `TimeBankRejected` | Time bank request denied | `reason: string` |

### Chat Events

| Event | Description | Payload |
|-------|-------------|---------|
| `ChatMessageReceived` | Chat message broadcast | `ChatMessageSentEvent` |
| `ChatMessageRejected` | Message rejected | `ChatMessageRejectedEvent` |
| `SystemAnnouncement` | System announcement | `SystemAnnouncementEvent` |
| `TableChatStatusChanged` | Chat enabled/disabled | `TableChatStatusChangedEvent` |
| `PlayerMuted` | Player muted notification | `PlayerMutedEvent` |
| `PlayerUnmuted` | Player unmuted notification | `PlayerUnmutedEvent` |

### Dealer Button and Blinds Events

| Event | Description | Payload |
|-------|-------------|---------|
| `DealerButtonMoved` | Button position changed | `DealerButtonMovedEvent` |
| `BlindPosted` | Blind posted by player | `BlindPostedEvent` |
| `AntePosted` | Ante posted by player | `AntePostedEvent` |
| `AntesCollected` | All antes collected | `AntesCollectedEvent` |
| `MissedBlindRecorded` | Missed blind tracked | `MissedBlindRecordedEvent` |
| `MissedBlindsPosted` | Missed blinds paid | `MissedBlindsPostedEvent` |
| `DeadButton` | Dead button situation | `DeadButtonEvent` |
| `BlindLevelInfo` | Current blind level info | `BlindLevelInfoEvent` |

---

## Event Payloads

### BettingActionEvent

```typescript
{
    gameId: Guid,
    timestamp: DateTime,
    action: {
        playerName: string,
        actionType: BettingActionType, // Fold, Check, Call, Bet, Raise, AllIn
        amount: number,
        timestamp: DateTime
    },
    potAfterAction: number
}
```

### ShowdownStartedEvent

```typescript
{
    gameId: Guid,
    timestamp: DateTime,
    showdownId: Guid,
    handNumber: number,
    eligiblePlayers: string[],
    firstToReveal: string | null,
    hadAllInAction: boolean
}
```

### PlayerRevealedCardsEvent

```typescript
{
    gameId: Guid,
    timestamp: DateTime,
    showdownId: Guid,
    playerName: string,
    cards: CardDto[] | null,
    hand: HandDto | null,
    wasForcedReveal: boolean,
    revealOrder: number
}
```

### TableStateSyncEvent

```typescript
{
    tableId: Guid,
    timestamp: DateTime,
    snapshot: {
        tableId: Guid,
        tableName: string,
        variant: PokerVariant,
        state: GameState,
        players: PlayerStateSnapshot[],
        communityCards: string[] | null,
        dealerPosition: number,
        currentPlayerPosition: number,
        currentPlayerName: string | null,
        smallBlind: number,
        bigBlind: number,
        pot: number,
        currentBet: number,
        currentStreet: string | null,
        handNumber: number,
        availableActions: AvailableActionsSnapshot | null
    }
}
```

### TurnTimerStartedEvent

```typescript
{
    gameId: Guid,
    timestamp: DateTime,
    playerName: string,
    durationSeconds: number,
    timeBankRemaining: number
}
```

---

## Error Handling

### ActionRejected Event

Sent when a player action cannot be processed:

```typescript
{
    reason: string // e.g., "You are not seated at this table."
}
```

### Common Rejection Reasons

| Reason | Description |
|--------|-------------|
| `"You are not seated at this table."` | Player not registered at the specified table |
| `"Not your turn."` | Player tried to act out of turn |
| `"Invalid action."` | Action not available in current state |
| `"Insufficient chips."` | Not enough chips for the action |

---

## Best Practices

### Connection Management

1. **Use automatic reconnection:**
   ```javascript
   .withAutomaticReconnect([0, 2000, 10000, 30000])
   ```

2. **Handle reconnection events:**
   ```javascript
   connection.onreconnecting(() => {
       showReconnectingUI();
   });
   
   connection.onreconnected((connectionId) => {
       hideReconnectingUI();
       requestTableState(currentTableId);
   });
   ```

3. **Implement heartbeat:**
   ```javascript
   setInterval(() => {
       connection.invoke("Heartbeat").catch(console.error);
   }, 30000);
   ```

### Event Handling

1. **Subscribe to all relevant events before connecting:**
   ```javascript
   connection.on("PlayerAction", handlePlayerAction);
   connection.on("ShowdownStarted", handleShowdownStarted);
   // ... more handlers
   
   await connection.start();
   ```

2. **Request state sync after reconnection:**
   ```javascript
   await connection.invoke("RequestTableState", tableId);
   ```

### Betting Actions

1. **Validate actions client-side first** to provide immediate feedback
2. **Handle rejection events** to show user-friendly error messages
3. **Update UI optimistically** but be prepared to rollback on rejection

### Performance

1. **Unsubscribe from events when leaving tables:**
   ```javascript
   await connection.invoke("LeaveTable", tableId);
   await connection.invoke("LeaveGame", gameId);
   ```

2. **Use groups efficiently** - only join the groups you need (lobby, table, waiting list)

---

## Example: Complete Game Flow

```javascript
// 1. Connect to hub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/gamehub")
    .withAutomaticReconnect()
    .build();

// 2. Set up event handlers
connection.on("PlayerAction", (event) => {
    updateGameState(event);
});

connection.on("ShowdownCompleted", (event) => {
    displayWinners(event.winners, event.payouts);
});

connection.on("ActionRejected", (event) => {
    showError(event.reason);
});

// 3. Start connection
await connection.start();

// 4. Join lobby to see available tables
await connection.invoke("JoinLobby");

// 5. Join a table
await connection.invoke("JoinTable", tableId, playerName);

// 6. Perform betting actions
await connection.invoke("Call", tableId, 50);

// 7. Leave when done
await connection.invoke("LeaveTable", tableId);
await connection.invoke("LeaveLobby");
```
