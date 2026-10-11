# Fix plan: do not reveal uncontested winner’s hole cards publicly

**Audit finding:** “Hole-card leak: an uncontested winner’s cards are shown to everyone.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** Information visibility on **win-by-fold** (everyone else folded). Do not change payout logic, betting, or contested showdown behavior.

---

## 1. Problem summary

When only one player remains because all others folded, the system must **not** expose that player’s **private** cards to other players or to anonymous/public table state. The winner may still see their own cards via **private** state (`BuildPrivateStateAsync`).

Today, hole cards leak through **three layers**:

| Layer | Mechanism | Severity |
|-------|-----------|----------|
| **A. Public seat state (SignalR)** | `TableStateBuilder.BuildSeatPublicDto` treats Showdown/Complete as “reveal all non-folded players’ cards face-up.” | Certain — every connected client sees winner’s hole cards on the table UI. |
| **B. Public showdown DTO (SignalR)** | `TableStateBuilder.BuildShowdownPublicDtoAsync` builds `ShowdownPlayerResultDto.Cards` with `IsFaceUp = true` for the sole remaining player even when no hand was shown down. | Certain — `TableStatePublicDto.Showdown` exposes cards without calling PerformShowdown. |
| **C. PerformShowdown API response** | Win-by-fold branches return `PlayerHands` with full `Cards` for the winner. | Certain — any client that calls showdown (or caches the response) gets hole cards; `TablePlay` can feed `ShowdownOverlay`. |

**Already correct (do not break):**

- **Hand history:** `RecordHandHistoryAsync` in Hold’em sets `reachedShowdown = !gp.HasFolded && !wonByFold` and only populates `ShowdownCards` when `reachedShowdown` — fold wins do **not** persist hole cards to history (`GetHandHistoryQueryHandler` only deserializes `ShowdownCards` when `ReachedShowdown`).
- **Kings and Lows** fold-win path already returns `PlayerHands = []` in one branch (`KingsAndLows/.../PerformShowdownCommandHandler.cs` ~417) — use as a pattern.

---

## 2. Intended behavior (spec)

### 2.1 Definitions

- **Win-by-fold hand:** Exactly **one** player is still “in the hand” (not folded), and the hand ended because others folded — not because cards were tabled at showdown.
- **Player still in hand** (match existing showdown handlers):  
  `!gp.HasFolded && (gp.Status == GamePlayerStatus.Active || gp.IsAllIn)`  
  (Use the same filter `PerformShowdown` uses for `playersInHand`.)

- **Private card:** A card that was **not** public information during betting. Practically:
  - Hold’em / Omaha / draw: hole cards (`Location` `Hole` or `Hand`) with `IsVisible == false`.
  - Stud: down cards (`Hole` with `IsVisible == false`, including seventh-street river).
  - **Not** private: stud upcards (`Board` with `IsVisible == true`), community cards, cards already marked `IsVisible == true` during play (e.g. Screw Your Neighbor when applicable).

### 2.2 Rules

1. **Public table state** (`TableStatePublicDto` seats + `Showdown`):
   - On win-by-fold, **never** set `IsFaceUp = true` (or rank/suit) on private cards for the winner.
   - Cards that were already visible during the hand (`IsVisible == true`) **may** remain face-up (no new information).
   - Winner identity, pot amount, and “won by fold” messaging are fine; **card ranks/suits of hidden cards are not**.

2. **PerformShowdown HTTP response** when `WonByFold == true`:
   - `PlayerHands` may list the winner for payouts/UI, but **`Cards` must be empty** (or omitted if the contract allows — prefer empty list for stable clients).
   - `HandType`, `HandStrength`, `HandDescription` remain null as today.

3. **Private state** (`PrivateStateDto`):
   - Unchanged: the winner still receives their own hole cards and descriptions in `BuildPrivateStateAsync`.

4. **Contested showdown** (2+ players not folded at showdown, or `WonByFold == false`):
   - Unchanged: reveal shown hands per current game rules.

5. **Phase window:** Leak occurs from the moment betting sets `CurrentPhase = "Showdown"` (e.g. Hold’em `ProcessBettingActionCommandHandler.AdvanceToNextPhase` ~665) **until** hand is complete — often **before** any client calls `PerformShowdown`. Fixing **only** PerformShowdown is **insufficient**; **TableStateBuilder** must be fixed.

---

## 3. Detecting win-by-fold in `TableStateBuilder`

Add a single internal helper used by both seat and showdown builders:

**Suggested location:** `src/CardGames.Poker.Api/Services/TableStateBuilder.cs` (private static methods) or `src/CardGames.Poker.Api/Services/HandVisibilityHelper.cs`.

```csharp
/// <summary>
/// True when exactly one player remains in the hand and the hand is (or will be) awarded
/// without a multi-player showdown.
/// </summary>
internal static bool IsWinByFoldHand(
    Game game,
    IReadOnlyList<GamePlayer> gamePlayers,
    IReadOnlyList<Pot> currentHandPots)
{
    var playersInHand = gamePlayers
        .Count(gp => !gp.HasFolded
            && (gp.Status == GamePlayerStatus.Active || gp.IsAllIn));

    if (playersInHand != 1)
        return false;

    // Strong signal after award:
    if (currentHandPots.Any(p => p.IsAwarded
        && string.Equals(p.WinReason, "All others folded", StringComparison.OrdinalIgnoreCase)))
        return true;

    // Before PerformShowdown: only one non-folded player and phase is Showdown/Complete
    // (betting handlers set Showdown on fold-win; award may not exist yet).
    if (string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase)
        || string.Equals(game.CurrentPhase, "Complete", StringComparison.OrdinalIgnoreCase))
    {
        // Avoid treating a lone active player at a broken table as fold-win if others never acted —
        // optional: require at least one folded opponent:
        var someoneFolded = gamePlayers.Any(gp => gp.HasFolded);
        return someoneFolded;
    }

    return false;
}
```

**Wire-up:** In `BuildPublicStateAsync`, after loading `gamePlayers` and pots for `game.CurrentHandNumber`, compute:

```csharp
var currentHandPots = game.Pots.Where(p => p.HandNumber == game.CurrentHandNumber).ToList();
var isWinByFold = IsWinByFoldHand(game, gamePlayers, currentHandPots);
```

Pass `isWinByFold` into `BuildSeatPublicDto` and `BuildShowdownPublicDtoAsync` (add parameters).

**Do not** use only `game.CurrentPhase == "Showdown"` without the single-player + folded-opponent checks — that could mis-classify edge cases.

---

## 4. Fix layer A — `BuildSeatPublicDto`

**File:** `src/CardGames.Poker.Api/Services/TableStateBuilder.cs` (~597–674)

**Current logic (bug):**

```csharp
var shouldShowCardsForShowdown = isShowdownPhase && !gamePlayer.HasFolded;
// ...
var shouldShowCard =
    (isSevenCardStud && card.IsVisible)
    || (isScrewYourNeighbor && card.IsVisible)
    || (isInBetween && card.IsVisible)
    || shouldShowCardsForShowdown;
```

Any non-folded player in Showdown/Complete gets **all** cards face-up, including hidden hole cards.

**Required change:**

1. Add parameter `bool isWinByFoldHand` to `BuildSeatPublicDto`.
2. Replace showdown reveal rule:

```csharp
var shouldShowCardsForContestedShowdown =
    isShowdownPhase && !gamePlayer.HasFolded && !isWinByFoldHand;

var shouldShowCard =
    (isSevenCardStud && card.IsVisible)
    || (isScrewYourNeighbor && card.IsVisible)
    || (isInBetween && card.IsVisible)
    || shouldShowCardsForContestedShowdown
    || (isWinByFoldHand && !gamePlayer.HasFolded && card.IsVisible); // only already-public cards
```

3. Update all call sites of `BuildSeatPublicDto` in the same file to pass `isWinByFold`.

**Concrete hand (Hold’em):**  
3-handed, A raises, B and C fold. Phase `Showdown`.  
- **Before:** Seat DTO for A shows A♠K♠ face-up to everyone.  
- **After:** A’s seat shows card backs (or empty rank/suit); B/C still show folded state; A’s private payload still has A♠K♠.

---

## 5. Fix layer B — `BuildShowdownPublicDtoAsync`

**File:** same, ~922–1857.

**Current behavior:** For `gamePlayers.Count(gp => !gp.HasFolded) == 1`, code adds a winner (~1689–1694) and still builds `PlayerResults` with:

```csharp
.Select(c => new CardPublicDto { IsFaceUp = true, Rank = ..., Suit = ... })
```

**Required change:**

1. Add `bool isWinByFoldHand` parameter (from §3).
2. When building each `ShowdownPlayerResultDto.Cards` list (~1785–1797), use:

```csharp
IsFaceUp = isWinByFoldHand ? c.IsVisible : true,
Rank = (isWinByFoldHand && !c.IsVisible) ? null : MapSymbolToRank(c.Symbol),
Suit = (isWinByFoldHand && !c.IsVisible) ? null : GetCardSuitString(c.Suit),
```

   Or, for fold-win only: set `Cards = []` on the winner’s result if **all** winner cards are private (simpler for Hold’em; stud may want visible upcards preserved).

3. **Optional but recommended:** Add `bool WonByFold { get; init; }` to `ShowdownPublicDto` in `src/CardGames.Contracts/SignalR/TableStatePublicDto.cs` and set it from `isWinByFoldHand` so the Blazor client does not infer from empty cards alone. This is a contract change — update any OpenAPI/Refit regen if applicable.

4. Do **not** populate `HandRanking` / `HandDescription` for fold-win (already null when `playerHandEvaluations` is empty).

---

## 6. Fix layer C — `PerformShowdown` win-by-fold responses

Every handler that returns winner `Cards` on fold must return **empty** `Cards`.

### 6.1 Shared helper (recommended)

**New file:** e.g. `src/CardGames.Poker.Api/Features/Games/Common/Showdown/WinByFoldResponseBuilder.cs`

```csharp
internal static ShowdownPlayerHand BuildFoldWinPlayerHand(
    GamePlayer winner,
    UserInfo? user,
    int totalPot)
    => new()
    {
        PlayerName = winner.Player.Name,
        PlayerFirstName = user?.FirstName,
        Cards = [], // critical: no hole cards
        HandType = null,
        HandStrength = null,
        IsWinner = true,
        AmountWon = totalPot
    };
```

Use in all fold-win return blocks instead of `winnerCards.Select(...)`.

### 6.2 Files to update (each has `WonByFold = true` and populates `Cards` from `winnerCards`)

| Handler |
|---------|
| `Features/Games/HoldEm/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` (~150–175) |
| `Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` — `HandleWinByFoldAsync` (~457–482) |
| `Features/Games/HoldTheBaseball/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| `Features/Games/FiveCardDraw/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| `Features/Games/TwosJacksManWithTheAxe/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| `Features/Games/SevenCardStud/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| `Features/Games/GoodBadUgly/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| `Features/Games/Baseball/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| `Features/Games/FollowTheQueen/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |

**Generic inline showdown** (`SupportsInlineShowdown`, ~196–246): when `showdownResult.WonByFold` is true, **do not** attach `inlineCards` to `PlayerHands`; use empty `Cards` for winners (Screw Your Neighbor / similar).

**Already aligned:** Kings and Lows fold-win branch with `PlayerHands = []` — verify still true after refactor.

**Downstream:** Games that call **Generic** showdown (Omaha, Nebraska, Klondike, Irish, etc.) are fixed when `HandleWinByFoldAsync` is fixed. Pair Pressure / Tollbooth wrappers delegate to Generic — no separate handler body.

---

## 7. Client defense in depth (optional but low cost)

Server fixes are **required**. These reduce damage if an old API or cached response still carries cards:

| File | Change |
|------|--------|
| `src/CardGames.Poker.Web/Components/Shared/ShowdownOverlay.razor` | When `ShowdownResult?.WonByFold == true`, do not render `winnerCards` / per-hand card rows from `PlayerHands` (show name + amount + “wins by fold” only). Some copy already says “Everyone folded” (~1007). |
| `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` | `HasUsableShowdownResult` already allows fold-win **without** cards (~2425–2427) — keep that behavior. Ensure seat merge (~2141–2144) does not promote face-up from stale API if server sends empty public seats. |

Do **not** rely on client-only fixes for security.

---

## 8. What not to change

- **`BuildPrivateStateAsync`** — winner must still see own cards.
- **Contested showdown** paths (`playerHandEvaluations` populated, `WonByFold == false`).
- **Hand history recording** for fold wins (`reachedShowdown == false`) — already omits showdown cards.
- **Community cards** on fold-win (already public).
- **Folded players’ cards** — must remain hidden (already `HasFolded`; do not add showdown reveal for folded seats).

---

## 9. Tests

### 9.1 Integration — public state after fold (primary)

**File:** extend `src/Tests/CardGames.IntegrationTests/Services/TableStateBuilderTests.cs`  
or `Games/HoldEm/HoldEmHandLifecycleTests.cs`.

**Flow:**

1. Create 3-player dealt Hold’em (`CreateDealtHoldEmGameAsync` or existing setup).
2. Bet + two folds (same as `ProcessBettingAction_AllButOneFold_HandCompletesWithFoldWin` in `HoldEmHandLifecycleTests.cs` ~261).
3. `BuildPublicStateAsync(gameId)`.
4. Find winner seat (only `!IsFolded`).
5. **Assert:** every card on winner seat has `IsFaceUp == false` OR null rank/suit (match product choice).
6. **Assert:** `BuildPrivateStateAsync(gameId, winnerEmail)` still has face-up hole cards.

Repeat once after `HoldEmPerformShowdownAsync` (phase `Complete`) — public seats must still hide hole cards.

### 9.2 Unit — `IsWinByFoldHand` helper

Table-driven tests:

| Players in hand | Someone folded | Phase | Pot WinReason | Expected |
|-----------------|----------------|-------|---------------|----------|
| 1 | yes | Showdown | null | true |
| 2 | yes | Showdown | null | false |
| 1 | yes | Complete | All others folded | true |
| 1 | no | WaitingToStart | null | false |

### 9.3 API — PerformShowdown response

**File:** `CardGames.Poker.Tests/Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/` (add test file if missing).

- Seed game with one player in hand, phase Showdown, hole cards in DB.
- Call `PerformShowdownCommand`.
- Assert `WonByFold == true`, `PlayerHands[0].Cards` is empty.

### 9.4 Regression — contested showdown

Existing showdown tests must still pass (cards still revealed when 2+ players reach showdown). Run:

```bash
cd src
dotnet test Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~FoldWin|FullyQualifiedName~Showdown"
dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~Showdown"
```

---

## 10. Implementation checklist

1. Implement `IsWinByFoldHand` + unit tests.
2. Thread `isWinByFold` through `BuildPublicStateAsync` → `BuildSeatPublicDto` (§4).
3. Update `BuildShowdownPublicDtoAsync` card face-up mapping (§5).
4. Add `WinByFoldResponseBuilder` (or equivalent) and replace winner card payloads in all §6.2 handlers.
5. Fix Generic inline showdown when `WonByFold` (§6).
6. Optional: `ShowdownPublicDto.WonByFold` + ShowdownOverlay guard (§7).
7. Run tests in §9; manual: two browsers, fold to winner, confirm observer does not see hole cards on table or overlay.

---

## 11. Acceptance criteria

- [ ] After fold-win, **observer** public seat state does not show winner’s hidden hole card ranks/suits in Showdown **or** Complete.
- [ ] Winner’s **private** state still shows their hole cards.
- [ ] `PerformShowdown` with `WonByFold == true` returns **no** hole cards in `PlayerHands[].Cards`.
- [ ] `TableStatePublicDto.Showdown` does not expose hidden winner cards on fold-win.
- [ ] Multi-player contested showdown still reveals hands as before.
- [ ] Hand history entries for fold-win still have `ReachedShowdown == false` and no `ShowdownCards` for the winner.
- [ ] No server logs write opponent hole card ranks to clients (stud debug logging in `TableStateBuilder` ~110–148 is server-only — out of scope unless it ships to clients; verify it does not).

---

## 12. Reference — leak timeline (Hold’em)

```text
ProcessBettingAction (last fold)
  → CurrentPhase = "Showdown"
  → GameStateBroadcaster → BuildPublicStateAsync
  → BuildSeatPublicDto: shouldShowCardsForShowdown → LEAK (today)

Client TryLoadShowdownAsync → PerformShowdown
  → PlayerHands with Cards → LEAK (today)
  → CurrentPhase = "Complete"
  → BuildPublicStateAsync again → still LEAK via seat + Showdown DTO (today)
```

---

## 13. Audit cross-reference

- Related but **separate** findings: header auth / `GameHub` private state spoofing, timer acting for wrong seat — not solved by this plan.
- **Red River / Follow the Queen** client showdown routing gaps are unrelated to card visibility logic.
