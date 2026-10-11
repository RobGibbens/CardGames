# Fix plan: Red River table stalls in Showdown (showdown never runs)

**Audit finding:** “Table stalls: Red River never runs a showdown.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** End-to-end path from `CurrentPhase == "Showdown"` through **PerformShowdown** API call, pot award, overlay, and transition to **Complete**. The primary defect is **missing client routing**. All-in runouts can also deal a second bonus card; that defect is `docs/Auditing/12-fix-red-river-double-bonus.md` and is out of scope here.

---

## 1. Problem summary

Red River (`REDRIVER`) reuses the Hold’em **betting** stack (`ProcessBettingActionCommand` / Hold’em endpoints). When the final River betting round completes, the API correctly sets `game.CurrentPhase = "Showdown"` and may deal the conditional **RedRiverBonus** community card (`ProcessBettingActionCommandHandler`).

**Nothing automatically awards pots** for Hold’em-family games on the server background path:

- `RedRiverFlowHandler` inherits `SupportsInlineShowdown => false` from `BaseGameFlowHandler` (same as Hold’em).
- `ContinuousPlayBackgroundService` only runs inline showdown when transitioning from **DrawComplete** (or similar) with `SupportsInlineShowdown == true` — not for community-card games sitting in **Showdown**.

Therefore **some connected client must call** `PerformShowdown` (HTTP). Hold’em works because `TablePlay.razor` routes `IsHoldEm` to `HoldEmApi.HoldEmPerformShowdownAsync`. **Red River is not included** in that branch and falls through to:

```csharp
throw new NotSupportedException(
    $"No showdown routing configured for game type '{_gameTypeCode}'. " +
    "Add an explicit branch for this game type in TryLoadShowdownAsync.");
```

(`TablePlay.razor` ~3774–3778)

### Symptom chain

| Step | What happens | Result |
|------|----------------|--------|
| 1 | River betting completes | `CurrentPhase = "Showdown"` (DB correct) |
| 2 | SignalR pushes public table state | UI shows showdown phase; pots **not** awarded (`Pot.IsAwarded == false`) |
| 3 | `OnTableStateUpdated` / timer calls `TryLoadShowdownAsync` | `IsShowdownPhase == true`, but no API branch for `REDRIVER` |
| 4 | `NotSupportedException` | Showdown load aborts; `_showdownLoadInFlight` cleared in `finally` only |
| 5 | No `PerformShowdown` | Phase stays **Showdown** indefinitely; chips stuck in pot; no results overlay / next-hand countdown |

### What is **not** broken (do not re-implement)

- Betting routed via `IGameApiRouter` → `HoldEmProcessBettingActionAsync` for `REDRIVER`.
- Start hand via `HoldEmStartHandAsync` when `IsHoldEm \|\| IsRedRiver \|\| IsKlondike` (`TablePlay.razor` ~3470).
- **Generic** `PerformShowdownCommandHandler` already treats `REDRIVER` as shared-community Hold’em-style (`UsesSharedCommunityCards` includes `PokerGameMetadataRegistry.RedRiverCode` ~1300–1301).
- **Hold’em** `PerformShowdownCommandHandler` does **not** validate `GameType.Code` — it only requires `CurrentPhase == "Showdown"`; it loads **all** community cards (including `RedRiverBonus`) and builds `HoldemHand` (best five from 2 hole + N board cards via `CommunityCardsHand` combinations).
- `TableStateBuilder.IsHoldEmGame` already includes `RedRiverCode` for evaluation/odds paths.

---

## 2. Intended behavior (spec)

1. When a Red River hand enters **Showdown** (with 5 or 6 community cards), **at least one** seated client at the table must successfully invoke **PerformShowdown** (same contract as Texas Hold’em).
2. API awards pots, sets `CurrentPhase = "Complete"`, sets `HandCompletedAt` / `NextHandStartsAt`, broadcasts state.
3. Web clients show the showdown/results overlay and next-hand timer (same as Hold’em / Klondike / Omaha family).
4. Hand evaluation with **6** board cards must use **best 5 of 8** (2 hole + 6 community), identical to passing six cards into `HoldemHand` / generic Hold’em evaluator.

---

## 3. Root cause (exact code locations)

### 3.1 Client — missing branch (primary)

**File:** `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`  
**Method:** `TryLoadShowdownAsync` (~3637–3846)

- `IsRedRiver` is defined (~878) and used for start hand, rabbit hunt, animations, etc.
- Showdown routing uses `else if (IsHoldEm)` only (~3740–3742) — **`REDRIVER` ≠ `HOLDEM`**.
- Red River is **not** grouped with `IsOmaha || ... || IsKlondike` generic showdown branch (~3748–3750).

### 3.2 Architectural inconsistency (contributing)

**Betting** uses centralized routing:

- `IGameApiRouter` maps `[RedRiver] = RouteRedRiverBettingActionAsync` → Hold’em API (`IGameApiRouter.cs` ~352–354).

**Showdown** uses a long `if/else` in `TablePlay` with **no** `GameApiRouter.PerformShowdownAsync` call (unlike betting at ~4825). New Hold’em-family variants are easy to omit.

### 3.3 No server-side fallback

**File:** `src/CardGames.Poker.Api/GameFlow/RedRiverFlowHandler.cs` — no override of `SupportsInlineShowdown`.

Enabling inline showdown on the flow handler **could** unblock tables without a client, but it **duplicates** settlement logic already in PerformShowdown handlers and diverges from Hold’em’s client-triggered model. **Prefer fixing the client** unless product requires headless/server-only tables.

---

## 4. Recommended fix

### 4.1 Minimal fix (required)

In `TryLoadShowdownAsync`, route Red River to the same PerformShowdown endpoint used for other Hold’em-family games.

**Option A — Hold’em endpoint (matches betting reuse):**

```csharp
else if (IsHoldEm || IsRedRiver)
{
    showdownResponse = await HoldEmApi.HoldEmPerformShowdownAsync(GameId);
}
```

**Option B — Generic endpoint (matches Klondike / Omaha / Irish):**

```csharp
else if (IsRedRiver)
{
    showdownResponse = await GamesApiClient.GenericPerformShowdownAsync(GameId);
}
```

**Recommendation:** **Option A** for consistency with `RouteRedRiverBettingActionAsync` (both hit Hold’em feature handlers). Option B is equally valid if you are standardizing on `GenericPerformShowdownAsync` for all non-dedicated variants.

Do **not** leave Red River only on Option B and Hold’em on Option A without reason — pick one family rule and document it in `AddingNewGames20.md`.

### 4.2 Structural fix (strongly recommended — prevents recurrence)

1. **Register** `RedRiverApiClientWrapper` implementing `IGameApiClient` with `GameTypeCode => "REDRIVER"`:
   - `StartGameAsync` → `HoldEmStartHandAsync`
   - `ProcessBettingActionAsync` → `HoldEmProcessBettingActionAsync`
   - `PerformShowdownAsync` → `HoldEmPerformShowdownAsync` (or `GenericPerformShowdownAsync` via `IGamesApi`)
   - Mirror pattern: betting already delegated in `IGameApiRouter`; wrapper centralizes for `GameApiRouter`.

   **Files:**
   - New: `src/CardGames.Poker.Web/Services/GameApi/RedRiverApiClientWrapper.cs`
   - `src/CardGames.Poker.Web/Program.cs` — `AddScoped<IGameApiClient, RedRiverApiClientWrapper>()`

2. **Refactor** `TryLoadShowdownAsync` tail to call:

   ```csharp
   showdownResponse = await GameApiRouter.PerformShowdownAsync(_gameTypeCode ?? "", GameId);
   ```

   for all game types that have an `IGameApiClient` registration, **or** maintain explicit branches but add a **single** helper:

   ```csharp
   private bool UsesHoldEmFamilyShowdown =>
       IsHoldEm || IsRedRiver || IsKlondike || IsOmaha || IsNebraska || IsSouthDakota
       || IsBobBarker || IsIrishStyleHoldEm || IsHoldTheBaseball;
   ```

   and route that group to one API call.

3. **Checklist** in `docs/AddingNewGames20.md` § Red River — add explicit bullet:
   - “Include `REDRIVER` in `TablePlay.TryLoadShowdownAsync` (or `IGameApiClient.PerformShowdownAsync`).”

### 4.3 Related gap (same bug class — fix in same PR if cheap)

**Follow the Queen (`FOLLOWTHEQUEEN`)** is also missing from `TryLoadShowdownAsync` but has `FollowTheQueenApiClientWrapper.PerformShowdownAsync` registered. Add:

```csharp
else if (IsFollowTheQueen)
{
    showdownResponse = await FollowTheQueenApi.FollowTheQueenPerformShowdownAsync(GameId);
}
```

(or route via `GameApiRouter` once FTQ is only used through the router).

---

## 5. API endpoints (for tests and Refit)

| Route | Handler | Valid for REDRIVER? |
|-------|---------|---------------------|
| `POST /api/v1/games/hold-em/{gameId}/showdown` | `HoldEm...PerformShowdownCommandHandler` | **Yes** (no game-type guard) |
| `POST /api/v1/games/generic/{gameId}/showdown` | `Generic...PerformShowdownCommandHandler` | **Yes** (`UsesSharedCommunityCards`) |

Web Refit: `HoldEmPerformShowdownAsync`, `GenericPerformShowdownAsync` (`CardGames.Poker.Refitter/Output/RefitInterface.v1.cs`).

---

## 6. Six-card board evaluation (verify, do not assume broken)

After fix, run showdown with **6** community cards (`DealtAtPhase == "RedRiverBonus"`).

- **Hold’em handler:** loads all `CardLocation.Community` cards (~94–101), `new HoldemHand(hole, communityCoreCards)`.
- **Domain:** `CommunityCardsHand.PossibleHandsWithFixedNumberOfHoleCards` uses `CommunityCards.SubsetsOfSize(5 - numberOfHoleCards)` — valid for N=6.

**Add integration test** (see §8) so a regression cannot ship with “showdown runs but wrong winner” on bonus boards.

---

## 7. Implementation order (checklist)

1. Add Red River branch to `TryLoadShowdownAsync` (§4.1).
2. (Optional) `RedRiverApiClientWrapper` + DI registration (§4.2).
3. (Optional) Refactor showdown dispatch to `GameApiRouter` or shared predicate.
4. Add integration test: Showdown awards pot and completes hand (§8.1).
5. Add integration test: 6-card Red River bonus showdown (§8.2).
6. Manual: play Red River to river → check → confirm overlay and next hand.
7. Update `docs/AddingNewGames20.md` Red River section with showdown routing bullet.

---

## 8. Tests

### 8.1 Red River — PerformShowdown completes stalled hand

**File:** extend `src/Tests/CardGames.IntegrationTests/Games/HoldEm/RedRiverHandLifecycleTests.cs`

**Arrange:**

- Use existing `CreateDealtRedRiverGameAsync` / `AdvanceToPhaseByCheckingAsync` helpers.
- Drive betting until `game.CurrentPhase == "Showdown"` (check-call through streets or force phase via controlled actions — prefer real `ProcessBettingActionCommand` sends like existing tests).

**Act:**

```csharp
var showdown = await Mediator.Send(new HoldEmPerformShowdownCommand(setup.Game.Id));
// OR
var showdown = await Mediator.Send(new GenericPerformShowdownCommand(setup.Game.Id));
```

Use the same command type the web client will call after your fix.

**Assert:**

- `showdown.IsT0` (success).
- Reload game: `CurrentPhase == "Complete"`.
- Current-hand pots: all `IsAwarded == true`.
- Sum of winner chip deltas matches pot (or use existing settlement assertions from other Hold’em lifecycle tests).

### 8.2 Red River — bonus board showdown evaluation

**Arrange:**

- Reuse `RiverAllInRunout_WithControlledDeck_DealsDeterministicBonusCardWhenRiverIsRed` setup (~160–217) — already asserts `Showdown` phase and 6 community cards.

**Act:**

- `PerformShowdown` (Hold’em or Generic command).

**Assert:**

- Success; winners non-empty; no `InvalidGameState` / `NoValidHands`.
- Optional: construct hands with known deck control and assert specific winner (only if test harness already supports it).

### 8.3 Regression — Hold’em unchanged

Run existing Hold’em showdown / lifecycle tests:

```bash
cd src
dotnet test Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~RedRiver|FullyQualifiedName~HoldEmHandLifecycle"
```

### 8.4 Web (optional)

If the solution has bUnit coverage for `TablePlay`, add a test that `_gameTypeCode = "REDRIVER"` and `IsShowdownPhase` triggers the expected API client method. If no such tests exist, skip — integration tests are sufficient.

---

## 9. Acceptance criteria

- [ ] Red River hand in **Showdown** with an authenticated/seated client at the table transitions to **Complete** within one UI update cycle (no infinite stall).
- [ ] Pots for the current hand are **awarded** (`IsAwarded == true`).
- [ ] Showdown/results overlay appears (same as Hold’em).
- [ ] `NextHandStartsAt` / continuous play advances (if enabled).
- [ ] Works with **5** community cards (black river) and **6** (red river + bonus).
- [ ] No `NotSupportedException` in logs for `TryLoadShowdownAsync` when `gameTypeCode == REDRIVER`.
- [ ] `AddingNewGames20.md` documents showdown routing for new Hold’em-family variants.

---

## 10. File reference

| Area | Path |
|------|------|
| **Bug — showdown routing** | `CardGames.Poker.Web/Components/Pages/TablePlay.razor` (`TryLoadShowdownAsync`, `IsRedRiver`) |
| Betting routing (correct) | `CardGames.Poker.Web/Services/IGameApiRouter.cs` (`RouteRedRiverBettingActionAsync`) |
| Phase → Showdown (correct) | `CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Bonus card (correct) | Same file, `DealRedRiverBonusCommunityCardIfNeededAsync` |
| Flow handler | `CardGames.Poker.Api/GameFlow/RedRiverFlowHandler.cs` |
| Generic showdown support | `CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| Hold’em showdown | `CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| Table state / odds family | `CardGames.Poker.Api/Services/TableStateBuilder.cs` (`IsHoldEmGame` includes Red River) |
| Integration tests | `Tests/CardGames.IntegrationTests/Games/HoldEm/RedRiverHandLifecycleTests.cs` |
| Onboarding doc | `docs/ADDING_NEW_GAMES.md` (Red River addendum; `AddingNewGames20.md` is deprecated) |

---

## Related

Orchestration path: see [11-fix-doc-vs-code.md](11-fix-doc-vs-code.md) §5.2 and the matrix in `docs/ADDING_NEW_GAMES.md` before assuming a Generic-only flow. Red River showdown is a `TablePlay` / `TryLoadShowdownAsync` branch, not Generic `PerformShowdown` by default.

---

## 11. Out of scope

- Hole-card leak on fold wins (`docs/Auditing/fix-fold-win-hole-card-leak.md`).
- Player identity / auth on PerformShowdown (`docs/Auditing/fix-trusted-identity-authz.md`).
- Server-side auto-showdown for all Hold’em-family games (product change; would touch `ContinuousPlayBackgroundService` and all flow handlers).
- BB option / raise cap bugs.

---

## 12. Manual test script (QA)

1. Create a **Red River** cash table, 2 players, sufficient stacks.
2. Play a hand to **River**; both players **check** (or call to end betting).
3. If river is red (hearts/diamonds), confirm **sixth** board card appears (bonus).
4. Confirm phase label **Showdown**.
5. Within a few seconds: results overlay, pot to winner, phase **Complete**, countdown to next hand.
6. Repeat with a **black** river — five board cards, same completion behavior.

If step 5 fails before the fix, check browser console / server logs for `NotSupportedException` and `No showdown routing configured for game type 'REDRIVER'`.
