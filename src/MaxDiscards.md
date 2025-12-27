# Five Card Draw: Conditional Max Discards (Ace Bonus)

## 1. Summary
Five Card Draw currently limits discards (and therefore replacement draws) to **3** cards. Change the rule to:

- **MaxDiscards = 4** *if and only if* the player’s **current hand (pre-discard)** contains **at least one Ace**.
- Otherwise, **MaxDiscards = 3**.

This must be consistently enforced in:
- The **Blazor UI** (`CardGames.Poker.Web/Components/Shared/DrawPanel.razor`) to guide the player and prevent invalid submissions.
- The **API** (`CardGames.Poker.Api`) to prevent bypass via modified clients.
- The **domain/game logic** (`CardGames.Poker`) where applicable so rules remain consistent across entry points.

## 2. Definitions

### 2.1 “Has an Ace”
A player “has an Ace” if their current 5-card hand (before discarding) contains any Ace.

Representation differs by layer:
- **Web UI**: `CardInfo.Rank` is a string; Ace may appear as `"A"` or `"ACE"` (possibly other spellings).
- **API/DB**: `GameCard.Symbol` is an enum (`CardSymbol.Ace`).
- **Domain**: uses `Symbol.Ace`.

Eligibility MUST be computed server-side from authoritative state.

### 2.2 “Pre-discard”
Eligibility for discarding 4 cards is evaluated against the player’s hand as it exists at the moment the draw action is requested, prior to applying discards.

## 3. Functional Requirements

### FR-1: Conditional discard limit
- If the player has an Ace in their current hand: allow discarding **0..4** cards.
- If the player does not have an Ace: allow discarding **0..3** cards.
- Discarding **5** cards is never allowed.

### FR-2: Stand pat unchanged
A discard count of **0** (“stand pat”) is always valid.

### FR-3: Index validity unchanged
Discard indices remain:
- Zero-based
- Must be within `[0..4]`

### FR-4: Validation error contract
When too many discards are submitted:
- API must reject using the existing error mechanics (currently `ProcessDrawErrorCode.TooManyDiscards`).
- Error message must reflect the computed max (3 or 4) for that particular request.

## 4. UX / UI Requirements (Blazor)

### UI-1: Dynamic instructional text
In `CardGames.Poker.Web/Components/Shared/DrawPanel.razor`, update any static “0-3” language to reflect the current max:
- If eligible: display **0–4** and indicate it’s due to having an Ace.
- Otherwise: display **0–3**.

### UI-2: Button enable/disable rules
The discard button must be disabled if:
- submitting/animating (existing behavior), OR
- `SelectedCount == 0`, OR
- `SelectedCount > AllowedMaxDiscards` where `AllowedMaxDiscards` is computed from the hand or (preferably) from server state.

### UI-3: Warning message
The warning currently triggers at `SelectedCount > 3`.

Update so it triggers at `SelectedCount > AllowedMaxDiscards` and displays the correct max:
- “Maximum 3 cards can be discarded” (no Ace), OR
- “Maximum 4 cards can be discarded when you hold an Ace” (Ace eligible).

### UI-4: Single source of truth in UI
All UI behaviors that depend on the limit (subtitle, warnings, button state) must use the same computed value.

### UI-5: Prefer server-provided max
If the player’s private state provides `DrawPrivateDto.MaxDiscards`, the UI should use that value as the authoritative limit instead of duplicating the rule.

## 5. API Requirements

### API-1: Validate discard count using per-player computed max
In `CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/ProcessDraw/ProcessDrawCommandHandler.cs`:
- Replace the constant `MaxDiscards = 3` enforcement with computed `maxDiscards`.
- `maxDiscards` is 4 only if the current draw player’s current hand contains an Ace (per DB card symbol).
- Validation must occur after the player’s current hand is available, or must compute from already-loaded cards.

### API-2: Accurate error message
When rejecting too many discards, message must reflect the computed `maxDiscards`:
- “Cannot discard more than 3 cards.” or
- “Cannot discard more than 4 cards.”

### API-3: Do not trust client-derived eligibility
Do not accept any client-supplied “hasAce” or “maxDiscards” indicator. Eligibility is derived from server state only.

## 6. Private State / Contract Requirements (SignalR / DTO)

### C-1: Provide correct `MaxDiscards` per player
`CardGames.Contracts/SignalR/PrivateStateDto.cs` already defines `DrawPrivateDto.MaxDiscards`.

In `CardGames.Poker.Api/Services/TableStateBuilder.cs`, `BuildDrawPrivateDto(...)` currently sets:
- `MaxDiscards = 3`.

Update to compute per-player `MaxDiscards` using the Ace rule from that player’s current hand.

This ensures clients can render correct limits without duplicating game rules.

## 7. Domain/Game Logic Requirements

### D-1: Keep `FiveCardDrawGame.ProcessDraw` consistent
In `CardGames.Poker/Games/FiveCardDraw/FiveCardDrawGame.cs`, `ProcessDraw(...)` currently enforces:
- `discardIndices.Count > 3` invalid.

Update to:
- Allow 4 only if the player has an Ace in their current hand (domain card symbol).

Even if the web app primarily uses the API handler, domain rule consistency prevents divergence and supports tests.

## 8. CLI Requirements (if applicable)
The workspace includes CLI implementations that reference draw/discard logic:
- `CardGames.Poker.CLI/Play/FiveCardDrawPlayCommand.cs`
- `CardGames.Poker.CLI/Play/Api/ApiFiveCardDrawPlayCommand.cs`

Any CLI-side validation/prompting that assumes a hard max of 3 must be updated:
- Allow selecting 4 discards only when the hand contains an Ace, OR
- Prefer using server-provided `MaxDiscards` if the CLI is API-driven.

## 9. Documentation Requirements
Update documentation strings that explicitly state max discards is 3:

- `CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/ProcessDraw/ProcessDrawCommand.cs`
  - XML docs and request docs mention “Maximum of 3”. Update to conditional description.

- `CardGames.Poker/Games/FiveCardDraw/FiveCardDrawGame.cs`
  - method docs and remarks mention “0-3 cards”. Update to conditional:
    - “0–3 (or 0–4 if the player holds at least one Ace)”.

- `CardGames.Poker.Web/Components/Shared/DrawPanel.razor`
  - static subtitle/warning text must reflect conditional max.

## 10. Testing Requirements

### T-1: Update existing tests
In `Tests/CardGames.Poker.Tests/Games/FiveCardDrawGameTests.cs`:
- Existing test `ProcessDraw_CannotDiscardMoreThanThree` asserts that discarding 4 fails.

Update/split test coverage:
1. Without an Ace: discarding 4 fails.
2. With an Ace: discarding 4 succeeds.

### T-2: Edge cases
Add/ensure tests for:
- Player may discard 4 even if one of those discarded cards is the Ace (eligibility is pre-discard).
- Discarding 5 always fails.

### T-3: API-level validation (recommended)
If API/integration tests exist for draw processing, add tests verifying:
- 4 discards without Ace returns `TooManyDiscards`.
- 4 discards with Ace succeeds.

## 11. Inventory: Locations found in this workspace requiring changes

### Blazor UI
- `CardGames.Poker.Web/Components/Shared/DrawPanel.razor`

### Web orchestration (indirect)
- `CardGames.Poker.Web/Components/Pages/TablePlay.razor` (submits draw requests; should remain consistent with server-provided max)

### API command / docs
- `CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/ProcessDraw/ProcessDrawCommand.cs`

### API validation
- `CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/ProcessDraw/ProcessDrawCommandHandler.cs`

### Private state builder
- `CardGames.Poker.Api/Services/TableStateBuilder.cs` (sets `DrawPrivateDto.MaxDiscards = 3` today)

### Contract
- `CardGames.Contracts/SignalR/PrivateStateDto.cs` (`DrawPrivateDto.MaxDiscards` already exists)

### Domain rules
- `CardGames.Poker/Games/FiveCardDraw/FiveCardDrawGame.cs`

### Tests
- `Tests/CardGames.Poker.Tests/Games/FiveCardDrawGameTests.cs`

### CLI
- `CardGames.Poker.CLI/Play/FiveCardDrawPlayCommand.cs`
- `CardGames.Poker.CLI/Play/Api/ApiFiveCardDrawPlayCommand.cs`
