# Product Requirements Document: SignalR Real-Time Table State (Poker)

## 1. Overview

### 1.1 Problem
`CardGames.Poker.Web/Components/Pages/TablePlay.razor` currently polls the API every ~1 second to refresh game state. Polling is wasteful, increases latency for other clients, and complicates reconnection/resync.

### 1.2 Goal
Replace client polling with SignalR hosted in `CardGames.Poker.Api`.

- Clients join a per-game SignalR group.
- Any state-changing command is applied by the API (authoritative).
- The API broadcasts updated state to all clients in the same game.
- The Blazor page updates its local UI state from pushed updates.

### 1.3 Non-goals (initial phase)
- Lobby real-time updates (table list, lobby chat) unless explicitly added later.
- Client-side optimistic UI.
- Delta/patch-based state sync (use snapshots first).

### 1.4 Success criteria
- No continuous polling loop remains in `TablePlay.razor`.
- Two clients in the same `GameId` reflect actions (seat/ready/betting/draw/showdown/pause/end) without manual refresh.
- Reconnect restores state reliably.
- Private cards are not broadcast to other players.

---

## 2. Current State (TablePlay)

`TablePlay.razor` relies on:
- `StartPollingIfNeeded()` + `PollGameStateAsync()`
- `RefreshGameStateAsync()` which calls:
  - `GetGameAsync(GameId)`
  - `GetGamePlayersAsync(GameId)`
  - `GetCurrentBettingRoundAsync(GameId)`
  - `GetCurrentPlayerTurnAsync(GameId)`
  - `GetCurrentDrawPlayerAsync(GameId)` (during draw)
  - `PerformShowdownAsync(GameId)` (during showdown/complete)

The page also infers:
- `isHost` from `_loggedInUserEmail` compared to `_gameResponse.CreatedByName`
- which seat is current user via `_loggedInUserEmail` matching seat `PlayerName`

---

## 3. Architecture Decision

### 3.1 Server authoritative state
The API owns game state. The client only renders state.

### 3.2 Command transport
**Phase 1 recommendation:** keep commands over existing HTTP endpoints (`IFiveCardDrawApi`) to minimize changes.
- After each successful command, the API broadcasts updated state via SignalR.

**Phase 2 option:** move commands into SignalR hub methods for a fully duplex protocol.

### 3.3 State sync strategy
**Snapshot-first:** broadcast complete table snapshots (public) plus personalized private state (hands).
- This avoids complex client reconciliation.

---

## 4. Hub Inventory and Order of Implementation

### 4.1 Required hub (MVP)
1) `GameHub` (table gameplay)
- Purpose: per-game subscriptions and game state pushes
- Endpoint: `/hubs/game`
- Auth required
- Grouping: `game:{GameId}`

### 4.2 Optional/next hub (not required for TablePlay)
2) `LobbyHub`
- Purpose: lobby table list/player counts/etc.
- Endpoint: `/hubs/lobby`

**Implementation order**
1. Implement `GameHub` + pipeline mapping
2. Implement broadcaster service hooked into HTTP commands
3. Implement Blazor client connection + handlers
4. Remove polling from `TablePlay.razor`
5. Add optional `LobbyHub` later

---

## 5. Server Requirements (`CardGames.Poker.Api`)

### 5.1 Add SignalR hosting
- Register SignalR in DI: `services.AddSignalR()`
- Map hubs: `app.MapHub<GameHub>("/hubs/game")`
- Ensure authentication/authorization is enabled for hubs.

### 5.2 Hub: `GameHub`

#### 5.2.1 Authorization
- Hub must require authentication: `[Authorize]`.
- `UserIdentifier` must be stable and match existing identity usage (email/username claim).
  - Client already uses `ClaimTypes.Email`, `email`, `preferred_username`, `Identity.Name`.
  - Configure `IUserIdProvider` if needed so `Clients.User(...)` targets the same identifier.

#### 5.2.2 Group model
- Group name convention: `game:{gameId}`

#### 5.2.3 Hub methods (minimum)
- `Task JoinGame(Guid gameId)`
  - Add caller to group `game:{gameId}`.
  - Send initial state snapshot to caller.
  - Send private state to caller (their cards) if applicable.

- `Task LeaveGame(Guid gameId)`
  - Remove caller from group.

> Note: if the game “leave” is a domain action, keep that as HTTP command in Phase 1.

### 5.3 Broadcast model

#### 5.3.1 Public vs private payload
- Public snapshot broadcast to the group must not include other players’ private cards.
- Private snapshot/hand must be sent only to the owning user/connection.

#### 5.3.2 Required push events (server → client)
At minimum, support one primary message:
- `TableStateUpdated(TableStatePublicDto state)` broadcast to `game:{gameId}`

And one private message:
- `PrivateStateUpdated(TableStatePrivateDto state)` or `PrivateHandUpdated(PrivateHandDto hand)` sent to the acting player.

This keeps client logic simple:
- update public state for everyone
- update private state only for current player

### 5.4 State builder requirements
Implement a state builder that composes current table render state (similar to what `RefreshGameStateAsync` constructs today).

- Inputs:
  - `gameId`
  - requesting user identifier (for private view)
- Outputs:
  - `TableStatePublicDto`
  - `TableStatePrivateDto` (or smaller private DTO)

#### 5.4.1 Public snapshot contents (must include)
- Current phase (string)
- Game name
- Ante/min bet
- Pot/total pot
- Seats list:
  - occupancy
  - player name
  - chips
  - readiness
  - folded/all-in/disconnected/current bet
  - cards **face-down** for non-owner (e.g., placeholders)
- Dealer seat index
- Current actor seat index
- Pause state

#### 5.4.2 Private view contents (must include)
For the requesting player:
- Their cards face-up
- Their available actions (betting) when it is their turn
- Call amount/min/max bet
- Draw hand details during draw

### 5.5 Broadcaster service

#### 5.5.1 Create a broadcaster abstraction
- `IGameStateBroadcaster`
  - `Task BroadcastGameStateAsync(Guid gameId)`
  - `Task BroadcastGameStateToUserAsync(Guid gameId, string userId)` (optional)

Implementation loads the latest state snapshot and pushes via `IHubContext<GameHub>`:
- `Clients.Group(gameGroup).SendAsync("TableStateUpdated", publicState)`
- For each connected user (or at least current actor), send private state:
  - `Clients.User(userId).SendAsync("PrivateStateUpdated", privateState)`

#### 5.5.2 Where broadcasts happen
For Phase 1 (HTTP commands), broadcast after any state mutation succeeds. Minimum endpoints/commands include:
- take seat / join table
- set ready
- start hand
- collect antes
- deal hands
- process betting action
- process draw
- perform showdown
- pause/resume/end

If these are handlers/services rather than controllers, integrate at the application layer after the command commits.

### 5.6 Reconnect/resync
- Group membership is per-connection.
- On reconnect, client calls `JoinGame(gameId)` again.
- `JoinGame` always sends a fresh snapshot.

### 5.7 Security requirements
- Hub requires auth.
- Validate that user can join the requested game.
- Never include private cards in group broadcast.

---

## 6. Client Requirements (`CardGames.Poker.Web`)

### 6.1 SignalR client package
- Add reference to `Microsoft.AspNetCore.SignalR.Client` in `CardGames.Poker.Web`.

### 6.2 Hub client service
Create a scoped service (per Blazor circuit) e.g. `GameHubClient`:
- Manages `HubConnection`
- Exposes:
  - `Task ConnectAsync()`
  - `Task JoinGameAsync(Guid gameId)`
  - `Task LeaveGameAsync(Guid gameId)`
  - `Task DisconnectAsync()`
  - connection state events (connected/reconnecting/disconnected)

Connection configuration:
- URL: `https://{api-host}/hubs/game`
- Enable automatic reconnect.

### 6.3 Event handlers (hub → UI)
`TablePlay.razor` subscribes to:
- `TableStateUpdated(TableStatePublicDto state)`
  - Update local fields that drive UI:
    - `_gameResponse` equivalent
    - `seats`, `pot`, `currentActorSeatIndex`, `dealerSeatIndex`, `isPaused`, `isDrawPhase`, etc.
    - Compute `currentPlayerSeatIndex` by matching `_loggedInUserEmail` with seat player name.

- `PrivateStateUpdated(...)` / `PrivateHandUpdated(...)`
  - Update current player’s face-up cards and available actions.

### 6.4 Remove polling
Delete/disable:
- `_pollingStarted`
- `StartPollingIfNeeded()`
- `PollGameStateAsync()`

Keep a one-time initial load only if needed, but preferred flow:
- On successful `JoinGame`, rely on the initial snapshot.

### 6.5 Commands from UI
Phase 1:
- Keep calling `_FiveCardDrawApiClient_` endpoints as-is.
- **Remove** `await RefreshGameStateAsync()` after successful commands.
  - The server will broadcast the new snapshot.

Phase 2:
- Replace REST calls with hub `InvokeAsync` command methods.

### 6.6 Connection status UI
Wire hub connection lifecycle to existing UI flags:
- `isConnected`
- `isReconnecting`

Use:
- `HubConnection.Reconnecting`
- `HubConnection.Reconnected`
- `HubConnection.Closed`

### 6.7 Disposal
When leaving the page:
- Call `LeaveGameAsync(GameId)`
- Dispose connection if not reused

---

## 7. Message Contract Requirements

### 7.1 Shared DTO location
Create DTOs in a shared project referenced by both:
- `CardGames.Poker.Api`
- `CardGames.Poker.Web`

Candidate projects:
- Prefer adding to an existing shared contracts project if appropriate (e.g., `CardGames.Contracts`), or create a new one dedicated to real-time (`CardGames.Poker.Events` may also be appropriate depending on existing usage).

### 7.2 Minimum DTO definitions
- `TableStatePublicDto`
  - `Guid GameId`
  - `string Name`
  - `string CurrentPhase`
  - `int Ante`
  - `int MinBet`
  - `int TotalPot`
  - `int DealerSeatIndex`
  - `int CurrentActorSeatIndex`
  - `bool IsPaused`
  - `IReadOnlyList<SeatPublicDto> Seats`
  - `ShowdownPublicDto? Showdown`

- `SeatPublicDto`
  - `int SeatIndex`
  - `bool IsOccupied`
  - `string? PlayerName`
  - `int Chips`
  - `bool IsReady`
  - `bool IsFolded`
  - `bool IsAllIn`
  - `bool IsDisconnected`
  - `int CurrentBet`
  - `IReadOnlyList<CardPublicDto> Cards` (face-down unless card is public)

- `CardPublicDto`
  - `bool IsFaceUp`
  - `string? Rank` (null/empty when face-down)
  - `string? Suit` (null/empty when face-down)

- `PrivateStateDto` (or smaller `PrivateHandDto`)
  - `Guid GameId`
  - `string PlayerName`
  - `IReadOnlyList<CardPrivateDto> Hand`
  - `AvailableActionsDto? AvailableActions`
  - `DrawPrivateDto? Draw`

- `AvailableActionsDto`
  - `bool CanFold/CanCheck/CanCall/CanBet/CanRaise/CanAllIn`
  - `int MinBet`
  - `int MaxBet`
  - `int CallAmount`

- `ShowdownPublicDto` (if you want showdown overlay driven entirely from hub)
  - payouts
  - hands
  - flags

> Note: This mirrors what `TablePlay.razor` currently assembles from multiple endpoints.

---

## 8. CORS / Hosting Requirements

If `CardGames.Poker.Web` and `CardGames.Poker.Api` are on different origins:
- Enable CORS for SignalR:
  - allow the web app origin
  - allow credentials if using cookies
  - allow WebSockets/LongPolling

Authentication must be compatible with SignalR negotiate:
- cookie auth: ensure cookie flows to API
- bearer token: configure `AccessTokenProvider` in the SignalR client

---

## 9. Implementation Checklist (MVP)

### API
1. [x] Add SignalR services + map `GameHub`.
2. [x] Implement `GameHub.JoinGame/LeaveGame` with group membership.
3. [x] Create DTOs for public/private table state.
4. [x] Create a table state builder service.
5. [x] Create `IGameStateBroadcaster` using `IHubContext<GameHub>`.
6. [x] Broadcast after each state-changing HTTP endpoint completes.

### Web
1. [x] Add SignalR client package.
2. [x] Implement `GameHubClient` (scoped).
3. [x] In `TablePlay.razor`, connect on init, call join.
4. [x] Subscribe to `TableStateUpdated` + `PrivateStateUpdated`.
5. [x] Remove polling loop.
6. [x] Keep HTTP commands but remove post-command refresh.

---

## 10. Risks and Edge Cases
- **Private data leakage:** ensure only the owner receives face-up hand data.
- **Reconnect:** group rejoin required after reconnect.
- **State races:** avoid broadcasting before state commits.
- **Identity mapping:** ensure API `Clients.User(userId)` matches the claim used in web.
- **Showdown idempotency:** `PerformShowdownAsync` is currently called by clients; consider whether showdown should be initiated once server-side and then broadcast.
