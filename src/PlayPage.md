# Table / Play Page — Product & Design Document

## 1. Purpose
The **Table / Play Page** is the primary gameplay surface where authenticated users join a table, take a seat, play hands in real time, and review immediate hand outcomes. It must support Five Card Draw first, but be designed to handle multiple poker variants by rendering a common table “shell” plus variant-specific overlays.

This document describes UX, information architecture, real-time contract expectations, state model (client view), responsiveness, and extensibility requirements.

## 2. Success Criteria
- Users can join a table, sit, and see other players join/leave with <250ms typical update latency.
- Host can start the game once minimum players are seated/ready and buy-in rules are satisfied.
- During hands, players see actionable controls only when it is their turn and only for valid actions.
- Reconnect restores the current table state (public + player-private) without manual refresh.
- UI is responsive across desktop/tablet/mobile and matches the site’s styling.

## 3. Non-Goals (MVP)
- Tournament bracket UI.
- Spectator mode (unless already supported by backend).
- Advanced analytics (HUD).
- Real-money or payments.

## 4. Page Entry & Navigation
### 4.1 Route
- `GET /table/{tableId}` (Blazor route: `@page "/table/{TableId:guid}"`)

### 4.2 Auth
- Requires authentication (`[Authorize]`).
- If unauthenticated: redirect to login and return to the table after login.

### 4.3 No Top Nav
- The page **does not** show the global top navigation.
- Primary navigation control is a **“Leave table”** button.

### 4.4 Leave Table
- UI action: `Leave table`.
- Behavior:
  - Sends a leave request to server (SignalR +/or API fallback).
  - Navigates to `/lobby`.
  - If leaving mid-hand, server decides penalties/timeouts (authoritative).

## 5. User Roles & Capabilities
### 5.1 Player
- Join table session.
- Choose/leave a seat.
- Mark ready (pre-start).
- Act on turn when eligible.

### 5.2 Host / Table Owner
- All Player actions.
- Start game once start conditions are met.
- Pause/resume/end game.
- Optional: initiate host handover when host disconnects (phase 2 if needed).

## 6. High-Level UX
### 6.1 Visual Layout (Table “Shell”)
- Overhead poker table background image.
- Seats arranged around the table edge.
- Center “board” area for community cards (if variant uses them) and pot/phase indicators.
- Action controls appear near the bottom (mobile: docked panel).

### 6.2 Core Regions
1. **Table Canvas**
   - Table background.
   - Seat components (N seats, where N varies by table config).
   - Center board area.
   - Pot + bet tokens.
   - Dealer button and turn indicator.

2. **Action Panel**
   - Only active for the current player on their turn.
   - Buttons: fold/check/call/bet/raise/all-in (variant/state dependent).
   - Bet sizing control (slider + quick chips).
   - Turn timer.

3. **Status/Controls Strip**
   - Leave table button.
   - Table name / blinds/ante summary.
   - Connection indicator.
   - Host-only controls (start/pause/resume/end) gated by permissions.

4. **Side Drawer / Overlay Panels** (responsive)
   - Players list / seats list.
   - Hand history summary (last N hands).
   - Game rules summary (variant + configured parameters).

## 7. Game Lifecycle UX
### 7.1 Pre-Start Room State
- State: `WaitingForPlayers` / `Seating`.
- Experiences:
  - Players join and appear in a “standing” list or empty seats.
  - Players can click an empty seat to sit.
  - Optional “Ready” toggle appears for seated players.
  - Host sees start conditions checklist:
    - Minimum players seated
    - Buy-in/stack verified
    - (Optional) all ready

### 7.2 Start Game
- Host presses Start.
- Client transitions to `Dealing`.
- Dealing animation occurs (see §10).

### 7.3 In-Hand Turn-Based Play
- The server is authoritative.
- Client displays:
  - whose turn
  - current bet amount to call
  - legal actions and min/max bet/raise constraints
  - countdown timer
- When action is submitted:
  - UI optimistically disables inputs.
  - Server confirms via event; UI updates.
  - If rejected, UI shows an inline reason and re-enables with updated constraints.

### 7.4 Showdown / Settlement
- Reveal cards as allowed by variant rules.
- Animate pot moving to winner(s).
- Record hand summary into hand history.
- Transition to next hand.

### 7.5 Pause / Resume / End
- Host-only.
- Pause freezes turn timer and disables actions.
- End returns to a terminal state with summary and “Leave table”.

## 8. Responsiveness Requirements
### 8.1 Breakpoints (guidance)
- Desktop: ≥ 1024px
- Tablet: 768px–1023px
- Mobile: ≤ 767px

### 8.2 Responsive Behavior
- Table canvas scales to fit viewport.
- Seats remain positioned around table using percentage-based layout.
- Action panel:
  - Desktop/tablet: centered bottom panel above safe area.
  - Mobile: fixed bottom sheet with collapsible advanced controls (bet sizing).
- Side information (hand history, players list) becomes a drawer on smaller screens.

### 8.3 Safe Areas
- For mobile, respect safe area insets for docked panels.

## 9. Variant Support & Extensibility
### 9.1 Design Principle
Separate:
- **Table shell**: seats, pot, turn indicator, generic action panel, connection UX.
- **Variant overlays**: visual rules for cards on table, special selections (draw, stud streets), and action availability.

### 9.2 Common Abstractions (Frontend)
- `TableViewState` (client view model): table metadata, seats, pot, phase, current actor, timers.
- `PlayerViewState`: display name, chips, seat index, status (sitting/standing/folded/all-in/disconnected), their visible cards.
- `VariantUiDefinition`:
  - `BoardSlots` (0..N)
  - `HoleCardSlots` per player
  - `SupportsDrawSelection` / `SupportsUpCards`
  - `ActionSet` mapping by phase

The UI should not hardcode Five Card Draw rules beyond the smallest necessary “first implementation”.

### 9.3 Variant Overlay Examples
- Five Card Draw:
  - No board cards.
  - Player has 5 hole cards.
  - “Draw selection” interaction between betting rounds.

- Hold’em (future):
  - Board slots: 5 (flop/turn/river).
  - Player has 2 hole cards.

- Stud (future):
  - Up cards rendered differently.

## 10. Card Dealing & Animations
### 10.1 Goals
- Visually pleasing and clear; not sluggish.
- Deterministic: driven by server events.
- Avoid desync: animations are a presentation of already-known game events.

### 10.2 Animation Model
- Use CSS animations for movement/flip when possible; optionally enhance with Web Animations API for sequencing.
- Animate card “flying” from a deck position (top-center or dealer area) to:
  - seat card positions
  - board positions
- Flip animation:
  - opponent cards remain face-down unless revealed
  - the current user’s hole cards can flip face-up on arrival

### 10.3 Sequencing
- Dealing events are received from SignalR as ordered events.
- Client queues animations per hand:
  1. Clear previous hand visuals.
  2. Deal each card with a small stagger (e.g., 70–150ms).
  3. Reveal board cards per street (for community variants).

### 10.4 Accessibility
- Provide reduced motion support (`prefers-reduced-motion`).
- When reduced motion enabled, skip movement/flip and show immediate state.

## 11. Real-Time Communication (SignalR)
### 11.1 Connection
- Use a dedicated hub for table gameplay, e.g. `/hubs/table`.
- Client joins a group by `tableId`.
- Maintain reconnection with exponential backoff.

### 11.2 Transport Rules
- Server is authoritative.
- Client requests are commands; server responds with events.
- All events are ordered per table (sequence number) to prevent out-of-order UI.

### 11.3 Client → Server Commands (logical)
- `JoinTable(tableId)`
- `LeaveTable(tableId)`
- `TakeSeat(tableId, seatIndex)`
- `LeaveSeat(tableId)`
- `SetReady(tableId, isReady)`
- `StartGame(tableId)` (host only)
- `PauseGame(tableId)` / `ResumeGame(tableId)` / `EndGame(tableId)` (host only)
- `SubmitAction(tableId, actionType, amount?)`
- `SubmitDrawSelection(tableId, discardCardIds[])` (variant-specific)

### 11.4 Server → Client Events (logical)
- `TableSnapshot(state, sequence)` (full state)
- `TableEvent(event, sequence)` (incremental)
- `PlayerJoined/Left`
- `SeatChanged`
- `PhaseChanged`
- `CardsDealt` / `BoardUpdated` / `CardsRevealed`
- `ActionRequested(playerId, legalActions, constraints, expiresAt)`
- `ActionApplied(playerId, action, resultingStateDelta)`
- `HandCompleted(summary)`
- `Error(message, code?)`

### 11.5 Reconnect Flow
- On reconnect:
  - client re-joins table group
  - server sends `TableSnapshot`
  - client resets local sequence baseline

## 12. Client State Management
### 12.1 Deterministic Updates
- Prefer event sourcing on the client:
  - Keep `sequence` and ignore older events.
  - Apply deltas to the `TableViewState`.

### 12.2 UI-Only State
- Card animation queue.
- Local bet slider value.
- Drawer open/closed.
- Toast/inline error surfaces.

### 12.3 Privacy Rules
- Client must only receive private info for the authenticated user (hole cards).
- Opponent cards:
  - face-down placeholders unless revealed by showdown rules.

## 13. Error Handling & Edge Cases
- Connection lost:
  - show banner and disable actions.
  - auto-reconnect.
- Host disconnect mid-game:
  - show banner “Host disconnected; waiting…”
  - if host handover is supported, update host indicator.
- Invalid action:
  - show error reason; refresh legal actions.
- Table ended:
  - show end-of-game overlay and offer Leave.

## 14. Security Considerations
- Authorize all hub methods.
- Validate table membership and seat ownership on server.
- Rate-limit action submissions.
- CSRF: not applicable to SignalR WebSockets in same way as cookies, but still ensure authentication tokens/cookies are secure.

## 15. Theming & Visual Style
- Reuse existing site typography, button styles, inputs, and iconography.
- Use existing CSS variables/theme colors from `wwwroot/app.css`.
- Ensure consistent button classes (`btn`, `btn-primary`, `btn-secondary`, `btn-lg`, etc.).
- Provide a cohesive table color palette aligned with the rest of the site.

## 16. Component Breakdown (Blazor)
Proposed component structure (names indicative):
- `Pages/TablePlay.razor` (route + orchestration)
- `Shared/TableCanvas.razor` (table background + child positioning)
- `Shared/TableSeat.razor` (seat UI)
- `Shared/CardStack.razor` (hand/board rendering)
- `Shared/ActionPanel.razor` (turn actions + bet sizing)
- `Shared/HandHistoryPanel.razor` (summary list)
- `Shared/ConnectionBanner.razor` (reconnect UX)

Variant overlays:
- `Variants/FiveCardDrawOverlay.razor`
- `Variants/HoldemOverlay.razor` (future)

## 17. Telemetry & Observability
Client-side:
- Log connection changes.
- Track action submit latency (client submit -> server ack).
- Track reconnect success rate.

Server-side (supporting requirement):
- Correlate events per table and hand.

## 18. Acceptance Criteria (Checklist)
### MVP
- [ ] Authenticated user can open `/table/{id}` and receive a snapshot.
- [ ] Seats render around table and update in real time.
- [ ] Player can take/leave a seat.
- [ ] Host can start game when minimum players met.
- [ ] Cards deal with animation and prefer-reduced-motion fallback.
- [ ] Turn timer visible; actions gated by server’s legal action set.
- [ ] “Leave table” navigates to `/lobby` and informs server.
- [ ] Reconnect restores accurate state.

### Post-MVP
- [ ] Hand history expandable with action log.
- [ ] Host handover behavior.
- [ ] Additional variants implemented with overlays.

## 19. Open Questions
- How many seats are supported per variant/table config in MVP?
- Should “Ready” be required for start, or only minimum players?
- Where does bet sizing UI appear for non-betting variants?
- Do we want chat on the play page in MVP?
- Should leaving table during a hand auto-fold vs sit-out?
