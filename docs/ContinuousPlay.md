# Continuous Play (Automatic New Hands) – Requirements

## Overview
The application currently supports playing a single hand at a time. This feature adds **continuous play**: when a hand completes, the system automatically transitions to the next hand after a short results period. The table state must be reset appropriately, the dealer button must rotate, eligible players must ante (or be sat out), the deck must be reshuffled/redealt, and the UI must reflect the transition.

This document focuses on functional requirements and observable behavior (API + domain + UI), aligned with the existing solution structure:
- `CardGames.Poker.Api` hosts the game engine endpoints and server-side orchestration.
- `CardGames.Poker.Web` renders the table state (e.g., `TablePlay.razor`, `TableSeat.razor`) and displays actions/results/overlays.
- `CardGames.Poker.Contracts` defines DTOs/contracts between API and clients.
- CLI tools exist (`CardGames.Poker.CLI`) and should remain compatible.

## Goals
- Keep play moving: automatically start the next hand when the current hand completes.
- Preserve fairness and expected poker flow: rotate dealer/button, deal and bet in correct order, enforce antes.
- Handle player eligibility: sitting-out players do not participate; players unable to ante are forced to sit out.
- Provide consistent UX: show end-of-hand results for a fixed time for all players.

## Non-Goals (initial scope)
- Implementing additional betting rounds or game variants beyond what the current game supports.
- Persisting long-term match history/hand histories unless already supported.
- Adding new authentication/authorization concepts.

## Definitions
- **Hand**: One complete cycle from initial deal through showdown and pot settlement.
- **Results overlay**: The UI panel/overlay that shows the hand outcome(s) and winner(s).
- **Dealer Button / Button**: Seat marker indicating the dealer position.
- **Clockwise**: Seat order increasing by one position to the right (wrapping around).
- **Eligible player**: Occupied seat, not sitting out, and has sufficient chips to pay the ante at hand start.
- **Sitting out**: A player is present at the table but not participating in the next hand.

## Current-state assumptions (to verify)
These are inferred from the existing code layout and naming (e.g., `FiveCardDraw`, `TablePlay`, `GetCurrentPlayerTurn`, odds panel usage). If any assumption is wrong, adjust the requirements accordingly:
1. The backend is authoritative for table/game state, including chip counts, current bets, and card visibility.
2. The frontend regularly fetches or subscribes to current table state (polling or realtime).
3. `SeatInfo` includes at least: `IsOccupied`, `PlayerName`, `Chips`, `CurrentBet`, `IsFolded`, `IsAllIn`, `IsReady`, `IsDisconnected`, `Cards`, and `HandEvaluationDescription`.
4. There is a notion of “dealer seat” already used by the UI (`IsDealer` parameter on `TableSeat.razor`).

## Functional Requirements

### FR1 – Automatic hand restart
1. When a hand reaches a completed state (all actions resolved and pots settled), the system transitions to a **HandComplete** phase.
2. The system **automatically schedules a new hand** to begin after a fixed delay while showing results.
3. The scheduling is server-driven to prevent clients from starting hands independently.

**Acceptance criteria**
- A new hand begins without any explicit user action once the previous hand completes.
- The transition occurs for all clients in a consistent way.

### FR2 – Results overlay duration
1. The results overlay remains visible to all players for **7 seconds** starting when the server marks a hand completed.
2. During the results overlay period, no new cards are dealt and no actions are accepted for the next hand.

**Acceptance criteria**
- Results remain visible for ~7 seconds on every connected client.
- Action UI is disabled or ignored during the results phase.

### FR3 – Dealer/button rotation
1. The dealer position rotates **clockwise by one seat** after each hand.
2. Rotation must select the next eligible dealer seat among **occupied** seats (and potentially excluding empty seats). Behavior with sitting-out dealer must be defined:
   - Default: rotate to the next occupied seat regardless of sitting-out status.
   - Alternative (if desired): rotate among eligible/active seats only.
3. Dealer state is part of the authoritative server state and returned in table snapshots.

**Acceptance criteria**
- The “Dealer” indicator (`D`) moves to the correct next seat after each hand.
- Rotation wraps around at the last seat back to the first.

### FR4 – Dealing order and action order
1. Cards are dealt **clockwise starting from the player to the left of the button**.
2. Betting/actions proceed **clockwise** starting from the appropriate first actor consistent with the game rules in use.

**Acceptance criteria**
- The first dealt/acting player after a restart is the seat immediately left of the dealer button (subject to eligibility).

### FR5 – Antes at hand start
1. At the start of every hand, all eligible players must pay the ante amount.
2. The ante amount is determined by the current table/game configuration (existing config mechanism).
3. Ante payments are recorded as part of the hand’s pot and reflected in chip counts.

**Acceptance criteria**
- When a new hand begins, chip stacks are reduced by ante for participating players.
- Pot/hand state reflects the antes.

### FR6 – Excluding sitting-out players
1. Players marked as sitting out do **not** receive cards for that hand.
2. Sitting-out players are skipped in dealing order and action order.

**Acceptance criteria**
- A sitting-out player’s seat shows no newly dealt cards for that hand.
- The current actor never becomes a sitting-out player.

### FR7 – Auto-sit-out when insufficient chips to ante
1. If a seat’s chip count is **less than the ante** at hand start, the player:
   - does not pay the ante,
   - does not receive cards,
   - is automatically set to sitting out.
2. The UI should reflect the updated status (e.g., “Sitting out”, “Out of chips”, or equivalent).
3. This change is server-authoritative and persists into subsequent hands until the user sits back in (if supported).

**Acceptance criteria**
- Low-stack players are excluded from the next hand and are marked sitting out.

### FR8 – New deck and reshuffle
1. Every new hand must start with a **freshly shuffled full deck**.
2. Any previously dealt cards are discarded; no card state leaks between hands.

**Acceptance criteria**
- The card distribution in successive hands is independent.
- No duplicate/impossible card states are observed.

### FR9 – Reset per-hand state
At new hand start, per-hand fields must reset for all seats:
- `IsFolded` cleared.
- `IsAllIn` cleared (unless the rules define carry-over; default is reset).
- `CurrentBet` reset to 0.
- Seat cards cleared before redeal.
- Hand evaluation text cleared until computed for the new hand.
- Any “ready” flags should be reset to the appropriate initial state for a new hand.

**Acceptance criteria**
- New hand starts with a clean table state (except chip stacks and sitting-out flags).

### FR10 – Minimum participants / hand start gating
1. A new hand should only start if there is a minimum number of participating/eligible seats.
   - Default minimum: **2** eligible players.
2. If fewer than the minimum are eligible:
   - Continuous play pauses in a waiting state.
   - Dealer/button position should remain stable (or defined behavior).

**Acceptance criteria**
- The system does not start a hand with fewer than the minimum players.

### FR11 – Disconnections and reconnects
1. Disconnected players may remain occupied but should behave according to existing rules. For continuous play:
   - If disconnected but not sitting out and has chips to ante, they can be treated as eligible unless current rules prevent it.
   - If current behavior forces disconnect to sit out, apply it consistently.
2. Reconnecting during the results period should show the results overlay if still within the 7-second window.

**Acceptance criteria**
- Reconnecting clients receive the correct phase and state snapshot.

### FR12 – Authority, idempotency, and concurrency
1. Only the server can transition from `HandComplete` ? `StartingNextHand` ? `InHand`.
2. Transition must be idempotent (retries or duplicate triggers must not start multiple hands).
3. Any client commands received during the results period must be rejected gracefully or ignored.

**Acceptance criteria**
- No duplicate hand starts occur.
- Client actions during results phase do not corrupt state.

## UI / UX Requirements (Blazor)

### UX1 – Results overlay behavior
- Overlay displays winners, hand types, pot distribution (as currently implemented) while the hand is complete.
- Overlay remains visible for 7 seconds.
- A visible countdown is optional.

### UX2 – Transition to next hand
- When the next hand starts, UI updates to:
  - move dealer marker,
  - clear old cards/bets/statuses,
  - show new cards (respecting visibility rules),
  - update current actor indicator.

### UX3 – Sitting out representation
- Seats sitting out should clearly indicate non-participation.
- If a player is auto-sat-out due to insufficient chips, show a distinct badge/message if supported.

## API / Contract Requirements

### API1 – Table state includes phase and timestamps
Table snapshots should include (either already present or to be added):
- Current phase/state: e.g., `InHand`, `HandComplete`, `StartingNextHand`, `WaitingForPlayers`.
- Timestamp or duration info to support consistent UI (e.g., `HandCompletedAtUtc` or `NextHandStartsAtUtc`).

### API2 – Server schedules next hand
- The API must surface the scheduled start time (or remaining seconds) so clients can present consistent overlays.

### API3 – Backward compatibility
- CLI and existing web UI should continue to function without breaking changes.
- If contract changes are required, versioned endpoints or additive properties are preferred.

## Domain Requirements

### D1 – Dealer rotation rules
- Rotation should skip empty seats.
- Rotation should define whether sitting-out seats can hold the button.

### D2 – Eligibility computation
Eligibility is computed at hand start based on:
- Seat occupied
- Not sitting out
- Chips >= ante

### D3 – State machine
Define explicit states and transitions:
- `InHand` ? `HandComplete` (when hand ends)
- `HandComplete` (results overlay window) ? `StartingNextHand`
- `StartingNextHand` ? `InHand` (after ante+deal)
- Any state ? `WaitingForPlayers` when eligible players < minimum

## Telemetry / Observability
- Log a single structured event per hand transition: hand completed, dealer moved, number of eligible players, next-start time.
- Warn when continuous play pauses due to insufficient eligible players.

## Testing Requirements

### T1 – Unit tests (domain)
Add/extend tests to verify:
- Dealer rotation with gaps (empty seats).
- Eligibility filtering (sitting out, insufficient chips).
- Auto-sit-out behavior.
- Reset of per-hand fields.

### T2 – Integration tests (API)
If existing patterns exist:
- Completing a hand causes `HandComplete` state.
- `NextHandStartsAtUtc` is computed correctly.
- After ~7 seconds, a new hand begins and dealer rotates.

### T3 – UI behavior tests (optional)
If UI test infra exists:
- Overlay remains visible per server schedule.
- Dealer marker moves.

## Open Questions (to resolve during implementation)
1. What is the current authoritative source of hand completion? (API handler, domain engine event, etc.)
2. How is the table state delivered to clients (polling vs SignalR)?
3. Should a sitting-out player be allowed to hold the dealer button?
4. What is the exact ante value and where is it configured?
5. What happens when a player joins/leaves during the 7-second results window?
6. Are blinds supported in this game mode (Five Card Draw), and if so, how do blinds interact with ante and dealer rotation?
