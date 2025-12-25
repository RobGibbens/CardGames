# Game History (Hand Outcomes) — Requirements

## 1. Purpose
Provide a durable, queryable history of a poker game’s progression across hands, including:
- A record of how each hand ended.
- Per-player end-of-hand result (folded / reached showdown and won/lost / won without showdown).
- Net chip change per player for the hand.
- The winner(s) for each hand and the win amount.
- A dashboard “flyout” view that shows a running hand-by-hand ledger sorted newest-first, with one row per hand showing:
  - who won,
  - how much they won,
  - the current player’s last action/result for that hand.

This feature is intended for in-game transparency and post-hand review. It is not intended to be a full event-sourcing playback system.

## 2. Scope
### In scope
- Capturing and storing hand summaries within a single game.
- Capturing per-player end-of-hand outcome and net chip change.
- Identifying the hand winner(s) and their winnings.
- Exposing a query API for “hand history summaries” suitable for the dashboard flyout.
- Rendering the table in the Blazor dashboard flyout sorted by time descending.

### Out of scope (explicit)
- Full hand replays (every betting action and card reveal step-by-step).
- Persistence across application restarts (unless the existing architecture already persists games; see §11 for persistence options).
- Multi-table tournament aggregation.
- Advanced analytics (EV, odds, etc.).

## 3. Definitions and Terminology
- **Game**: A match/session with a stable set of players (human + bots) across many hands.
- **Hand**: One deal from blinds/antes through completion (folds or showdown).
- **Hand end**: The terminal state that determines pot distribution (everyone folded to one player, or showdown).
- **Current player**: The player associated with the currently logged-in user / UI context.
- **Last action (for the hand)**: The final meaningful action/result descriptor for a player in that hand (e.g., folded on turn; or reached showdown and lost).
- **Net chips**: Player’s chip delta for that hand (positive = won, negative = lost). This should include any chips invested that were not returned.
- **Winner(s)**: Player(s) who receive pot chips at hand end. Note: split pots may result in multiple winners.

## 4. User Stories
### 4.1 As a player, I want to see what happened recently
- I can open the dashboard flyout and view a list of hands.
- Newest hands appear at the top.
- Each row indicates who won the hand and their win amount.
- Each row indicates what *I* did in that hand (folded, won, lost, etc.) and how many chips I gained/lost.

### 4.2 As a player, I want accurate net chip changes
- For every completed hand, my net chip change is shown.
- Net chip changes match the game engine’s pot distribution results.

### 4.3 As a developer, I want stable identifiers
- Each hand has a unique identifier for linking/retrieval.
- Each hand has a monotonic sequence number within the game.

## 5. Functional Requirements

### 5.1 Capture Hand Summary on Completion
When a hand completes, the system MUST create a new `HandHistory` record containing:
- Hand identity (game id, hand id, hand number).
- Completion timestamp (UTC).
- Termination type: `FoldedToWinner` or `Showdown`.
- Pot distribution summary (total pot size, rake if applicable).
- Winners and their amounts.
- Per-player outcomes and net chip deltas.

**Acceptance criteria**
- For every hand that ends, exactly one history record is created.
- The record is created after pot settlement is finalized (net chip deltas are not estimated).

### 5.2 Record Per-Player End-of-Hand Outcome
For each player in the hand, the system MUST store:
- Player identity (stable player id and display name at hand time).
- End-of-hand status:
  - `Folded`
  - `Won`
  - `Lost`
  - `SplitPotWon` (optional if you want to explicitly mark splits)
  - `AllInLost` / `AllInWon` (optional; see §5.2.2)
- Showdown flag (whether player reached showdown).
- Street at which player folded (if folded): `Preflop`, `Flop`, `Turn`, `River`.
- Net chip delta for the hand.

#### 5.2.1 “Last action” mapping
The UI requirement says “record their last action for the hand.” The system MUST derive a UI-ready “result label” from the final state:
- If player folded: label “Folded” (optionally “Folded (Turn)” depending on UI space).
- If showdown and won: label “Won (Showdown)”.
- If showdown and lost: label “Lost (Showdown)”.
- If won without showdown (everyone folded): label “Won (No Showdown)”.

#### 5.2.2 Optional: capture all-in nuance
If the engine exposes this data, optionally store:
- `WentAllIn` boolean.
- `AllInStreet`.

**Acceptance criteria**
- For a player who folds, showdown-related fields are false/empty.
- For a player who reaches showdown, folded-related fields are empty.
- Net chip delta sums across all players to `0 - rake` (if rake exists) or `0` (if no rake).

### 5.3 Record Winner(s)
The system MUST record the winner list for each hand.
- For a standard non-split pot, exactly one winner.
- For a split pot, multiple winners.

The UI flyout is initially specified as “who won” and “how much they won”. This MUST be defined for split pots:
- Display either:
  - Primary winner (highest share) and “+N split” indicator, or
  - A comma-separated list with amounts, or
  - “Split pot” with a details affordance.

**Acceptance criteria**
- Winner amounts equal the settled pot distribution amounts.

### 5.4 Game-level History Retrieval
Provide a query endpoint/service to retrieve history entries for a game.
- Default ordering: `CompletedAtUtc DESC`.
- Support pagination / max count (e.g., last 50 hands) to avoid unbounded growth in UI.

**Acceptance criteria**
- Query returns the same ordering consistently.
- Query can identify the current player’s row for each hand.

### 5.5 Dashboard Flyout Display (Blazor)
Add a “History” section (or extend existing dashboard) to show a table with one row per hand.

Each row MUST show:
- Hand number (or time).
- Winner display name.
- Winner amount.
- Current player action/result in that hand.
- Current player net chip delta.

Sorting MUST be newest-first.

**Behavior**
- When a new hand completes, the table updates.
- Limit display to N entries by default (e.g., 25 or 50) with optional “Show more”.

**Empty state**
- If no hands completed, show “No hands played yet.”

**Acceptance criteria**
- On hand completion, history appears without requiring page refresh (assuming the app already updates play state live).
- Rows are stable even if player display name changes later (history should preserve hand-time name).

## 6. Data Model Requirements

### 6.1 Identifiers
- `GameId`: stable identifier for the game session.
- `HandId`: stable identifier for the hand.
- `HandNumber`: 1-based sequence within the game (monotonic increasing).

### 6.2 Proposed Domain Objects (logical)
These are conceptual requirements; exact types/names should match existing solution conventions.

#### `HandHistory`
- `GameId`
- `HandId`
- `HandNumber`
- `CompletedAtUtc`
- `EndReason` (`FoldedToWinner` | `Showdown`)
- `TotalPot`
- `Rake` (optional)
- `Winners`: list of `HandWinner`
- `PlayerResults`: list of `HandPlayerResult`

#### `HandWinner`
- `PlayerId`
- `PlayerName`
- `AmountWon`

#### `HandPlayerResult`
- `PlayerId`
- `PlayerName`
- `ResultType` (`Folded` | `Won` | `Lost` | `SplitWon` ...)
- `Showdown` (bool)
- `FoldStreet` (nullable)
- `NetChipDelta`

### 6.3 Invariants
- For a given `GameId`, `HandNumber` is unique.
- `CompletedAtUtc` is set once and not modified.
- `PlayerResults` includes every seated/participating player for the hand.

## 7. Event/Integration Requirements

### 7.1 Source of truth
History MUST be derived from the poker engine’s settled state at hand completion (chip stacks after settlement), not from UI events.

### 7.2 When to record
Record creation MUST occur at the exact point where:
- the hand is terminal, and
- chips have been distributed.

If the engine has a “hand completed” domain event (preferred), the history recorder should subscribe to it.

### 7.3 Avoid double-recording
The recorder MUST be idempotent per (`GameId`, `HandId`) or (`GameId`, `HandNumber`).

## 8. UI Requirements (Dashboard Flyout)

### 8.1 Layout
- Section title: “History” (or “Hand History”).
- Table columns (minimum):
  1. Hand (Number or time)
  2. Winner
  3. Won
  4. You
  5. ? Chips

### 8.2 Formatting
- Winner amount: numeric with chip symbol or consistent formatting.
- Current player delta: show + / - and colorize if UI convention exists (green for +, red for -).
- “You” column examples:
  - “Folded (Turn)”
  - “Won (Showdown)”
  - “Lost (Showdown)”
  - “Won (No Showdown)”

### 8.3 Sorting
- Default: newest at top (`CompletedAtUtc DESC`).
- If two hands have same timestamp (unlikely), secondary sort by `HandNumber DESC`.

### 8.4 Accessibility
- Table MUST be keyboard navigable.
- Sufficient contrast for delta colors.
- Use semantic table markup (`<table>`, `<thead>`, `<tbody>`) if consistent with existing components.

## 9. Non-Functional Requirements

### 9.1 Performance
- Recording a hand history MUST be O(P) where P = number of players.
- Retrieving history for the dashboard MUST be efficient; limit results.

### 9.2 Reliability
- History creation MUST not crash the game loop.
- If history cannot be recorded (e.g., storage failure), the system MUST:
  - log the failure with context (GameId, HandNumber), and
  - continue the game (best-effort), unless the product requirement mandates strict auditing.

### 9.3 Data consistency
- NetChipDelta MUST match the post-settlement chip stacks.

### 9.4 Security
- Players should only access their own game history (if games are private).
- Ensure API endpoints enforce authorization consistent with existing auth.

## 10. Edge Cases
- **Split pot**: multiple winners, each with their won amount.
- **Side pots**: winners per side pot; resulting per-player net delta still computed; winner list should reflect final settlements.
- **Player disconnects**: still record their outcome.
- **Player leaves table mid-hand**: define whether allowed; if allowed, treat as folded/forfeited and record accordingly.
- **Rake/fees**: if applicable, sum of net deltas may be negative; represent rake explicitly.

## 11. Persistence Strategy (options)
Choose based on existing architecture.

### 11.1 In-memory (MVP)
- Keep a ring buffer of last N hands per game.
- Pros: simplest.
- Cons: lost on restart; not shareable across replicas.

### 11.2 Durable storage (recommended if games persist)
- Store `HandHistory` records in the existing game database.
- Index by `GameId`, `HandNumber`.

### 11.3 Event stream (future)
- If the solution already uses events, store a hand-completed event and project to `HandHistory` for queries.

## 12. API / Service Contract Requirements

### 12.1 Query model for dashboard
Return a compact projection for the UI:
- `HandNumber`
- `CompletedAtUtc`
- `WinnerName` (or multiple)
- `WinnerAmount` (or aggregate)
- `CurrentPlayerResultLabel`
- `CurrentPlayerNetDelta`

### 12.2 Pagination
- Input: `GameId`, `take`, `skip` (or cursor).
- Output: sorted list.

## 13. Testing Requirements

### 13.1 Unit tests
- Given a settled hand with known chip deltas, the history record matches expected deltas.
- Given a fold-to-winner hand, result labels are “Won (No Showdown)” for winner and “Folded” for others.
- Given a showdown hand, showdown flags are set and labels correspond.
- Split pot: multiple winners and correct amounts.
- Idempotency: recording same hand twice does not create duplicate entries.

### 13.2 Integration tests
- Simulate completing multiple hands and verify retrieval order is newest-first.
- Verify dashboard projection highlights the current player correctly.

## 14. Telemetry / Logging
- On hand record creation: log at Debug/Information with `GameId`, `HandNumber`, `HandId`, end reason.
- On failure: log at Error with exception and same identifiers.

## 15. Open Questions
- Do we need full per-street action history later, or only end-of-hand status?
- How should split pots be displayed in the flyout initially?
- What is the source of “current player id” in Blazor (auth user id mapping vs table seat id)?
- Should history be persisted beyond the life of a single in-memory session?
