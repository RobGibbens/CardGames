# Fix plan: Razz showdown must award the best **low** hand, not high stud

**Audit finding:** “Razz: the live showdown handler picks the best high hand; the Razz domain rules pick the best low.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** Live **PerformShowdown** path used by Razz tables (HTTP + settlement + hand history + API response). Table **display** evaluation in `TableStateBuilder` is already lowball-correct; this plan aligns payout/showdown API with that model.

---

## 1. Problem summary

Razz (`RAZZ`) is a **seven-card stud structure** with **Ace-to-five lowball** winners: lowest five-card hand wins; straights and flushes do not count against the hand.

The product already implements lowball in the **domain** and **table UI**:

| Layer | Razz-aware? | Evidence |
|-------|-------------|----------|
| `RazzHand` + `RazzHandEvaluator` | Yes | `src/CardGames.Poker/Hands/StudHands/RazzHand.cs`, `[HandEvaluator("RAZZ")]` |
| `TableStateBuilder` showdown preview / descriptions | Yes | `isRazz` → `new RazzHand(...)`, `GetBestLowHand()`, `EvaluateRazzHandDescription` (~1489–1492, ~3263–3272) |
| `OddsCalculator.CalculateRazzOdds` / stud turn query | Yes | `GetCurrentPlayerTurnQueryHandler` passes `useRazzLowball` when `GameType.Code == RAZZ` |
| `ShowdownOverlay` (web) | Yes | `RazzShowdownOverlayParityTests` |
| **Live showdown API** (pot award) | **No** | `SevenCardStud...PerformShowdownCommandHandler` always builds `StudHand` and picks **highest** `Strength` |

### Live call path (why Stud handler runs)

1. `TablePlay.TryLoadShowdownAsync` → `SevenCardStudApi.SevenCardStudPerformShowdownAsync` when `IsRazz` (~3724–3726).
2. `RazzApiClientWrapper.PerformShowdownAsync` → same Stud endpoint (~40–42).
3. Betting/start/deal correctly reuse Stud endpoints; **only showdown** applies the wrong evaluator.

### Wrong logic (exact)

**File:** `src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`

```csharp
var studHand = new StudHand(holeCards, openCards, downCards);
// ...
var maxStrength = playerHandEvaluations.Values.Max(h => h.hand.Strength);  // high-hand wins
```

Response building (~316–346) calls `kvp.Value.hand.GetBestHand()` (high-hand best five on `StudHand`) and sets `HandType = kvp.Value.hand.Type.ToString()` (pair, flush, etc.).

Hand history (~463–467) also uses `GetBestHand()` for persisted showdown cards.

### Reference implementation (already in repo)

**File:** `src/CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` (~177–216, ~334–336, ~443–445)

- Detects `isRazz` from `gameTypeCode`.
- Builds `RazzHand` with stud positional split (or fallback by deal order).
- Winner selection: `Max(h.hand.Strength)` on **`RazzHand`** — **correct for this codebase** because `RazzHand.CalculateStrength()` encodes lowball (higher numeric strength = better low; see `RazzShowdownOverlayParityTests`).

**File:** `src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`

- Uses `handEvaluatorFactory.GetEvaluator("RAZZ")` → `RazzHandEvaluator`.
- Stud-style `CreateHand(hole×2, board, down×1)` (~629–637).
- Pot winners via `Max(Hand.Strength)` with Razz-specific descriptions (~940–965).

The Five Card Draw and Generic handlers are **not** on the Razz web path today; they are the templates to copy or delegate to.

---

## 2. Intended behavior (spec)

1. **Winner:** Among eligible players at showdown, award pot(s) to the player(s) with the **best Razz low** (same ordering as `RazzHand.Strength` / `GetBestLowHand()`).
2. **Ties:** Split pot(s) when two or more players share the same best `RazzHand.Strength` (same as current split logic).
3. **Descriptions:** Win reason, `ShowdownPlayerHand.HandType`, and hand history should use `RazzHand.GetLowHandDescription(GetBestLowHand())` (e.g. `"12-11-6-5-2 low"`), **not** `HandType.Pair` / `FullHouse` from high-hand taxonomy.
4. **Best five cards in API:** `BestCardIndexes` / displayed winner cards must match `GetBestLowHand()`, not `StudHand.GetBestHand()`.
5. **Table UI parity:** After fix, the player who wins chips must match who `TableStateBuilder` / overlay lowball evaluation would rank first for the same cards.
6. **Seven Card Stud tables** (`SEVENCARDSTUD`): unchanged — still `StudHand` and high-hand `Max(Strength)`.

### Strength semantics (do not invert)

For `RazzHand`, **larger `Strength` means a better (lower) poker hand**. Tests document this explicitly (`RazzShowdownOverlayParityTests` ~37–38).  

**Do not** change winner selection to `Min(Strength)` unless you replace the entire strength model. Keep `Max(Strength)` when hands are `RazzHand`.

---

## 3. Concrete regression example (use in tests)

From `RazzShowdownOverlayParityTests`:

| Player | Seven cards | Best Razz low (domain) | High stud would favor |
|--------|-------------|------------------------|------------------------|
| P1 | `Ks Js 6s 5h Qs 2h 6d` | `12-11-6-5-2 low` (**wins**) | Pair of sixes / high cards |
| P2 | `Qc Kh 9d 7h 9h 7d 5s` | `13-12-9-7-5 low` (loses) | Two pair 9s and 7s |

`EvaluateRazzStrength(P1) > EvaluateRazzStrength(P2)` — P1 must receive payouts.

Under current Stud handler, P2 often wins because pairs beat “high card” in `StudHand`.

---

## 4. Card categorization (must match existing stud dealing)

When building `RazzHand`, use the **same** entity splits as the Stud showdown handler and `TableStateBuilder`:

| Segment | Entity filter |
|---------|----------------|
| Initial hole (2) | `Location == Hole` && `DealtAtPhase != SeventhStreet` |
| Up cards (≤4) | `Location == Board` |
| Seventh street down (1) | `Location == Hole` && `DealtAtPhase == SeventhStreet` |

Constructor: `new RazzHand(initialHoleCards, openCards, [downCard])`.

**Fallback** (legacy / inconsistent locations): if positional split yields &lt; 5 cards, use ordered deal-order list like FCD handler (`new RazzHand([], allCardsByDealOrder, [])`).

Resolve game type as:

```csharp
var gameTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code;
var isRazz = string.Equals(gameTypeCode, PokerGameMetadataRegistry.RazzCode, StringComparison.OrdinalIgnoreCase);
```

Use `CurrentHandGameTypeCode` so **Dealer’s Choice** hands of type Razz are not misclassified when the table’s `GameType.Code` is not `RAZZ`.

---

## 5. Recommended fix strategies

Pick **one** primary approach; avoid leaving two divergent Razz showdown implementations long term.

### Option A (recommended): Delegate Razz from Stud handler to Generic showdown

At the start of `SevenCardStud...PerformShowdownCommandHandler.Handle`, after loading `game`:

```csharp
var gameTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code;
if (string.Equals(gameTypeCode, PokerGameMetadataRegistry.RazzCode, StringComparison.OrdinalIgnoreCase))
{
    return await mediator.Send(
        new Generic.v1.Commands.PerformShowdown.PerformShowdownCommand(command.GameId),
        cancellationToken);
}
```

- Inject `IMediator` into the Stud handler (or an `IRequestHandler<GenericPerformShowdownCommand, ...>`).
- **Pros:** Single maintained implementation (evaluator factory, side-pot eligibility, Razz descriptions, settlement).
- **Cons:** Requires constructor/DI change; ensure no MediatR recursion (Generic must not call back into Stud endpoint).

**Web:** Optional follow-up — route `IsRazz` to `GamesApiClient.GenericPerformShowdownAsync` in `TablePlay` and `RazzApiClientWrapper` for clarity; not strictly required if server delegates.

### Option B: Inline Razz branch in Stud handler (minimal DI churn)

Mirror FCD handler in the same file:

1. Change evaluations dictionary to `(HandBase hand, ...)` or dual code paths.
2. If `isRazz`, build `RazzHand` instead of `StudHand`.
3. Keep `Max(Strength)` for winners.
4. Response + history:
   - `bestCards = (hand as RazzHand)?.GetBestLowHand() ?? (hand as StudHand)?.GetBestHand()`
   - `HandType` = Razz low description when `RazzHand`
   - `winReason` = low description, not `hand.Type`
5. Update `RecordHandHistoryAsync` signature or branch inside to use `GetBestLowHand()` for Razz when serializing `showdownCards`.

**Pros:** No new endpoint routing; localized change.  
**Cons:** Duplicates FCD/Generic logic; stud handler still lacks per-pot side-pot eligibility (pre-existing for all stud showdowns).

### Option C: Web-only route change

`TablePlay` + `RazzApiClientWrapper` call `GenericPerformShowdownAsync` instead of Stud.

**Pros:** Zero Stud handler code if Generic is correct.  
**Cons:** Any direct client still calling Stud showdown for a Razz `gameId` remains broken; prefer **server-side guard** (Option A or `if (isRazz) return BadRequest` on Stud endpoint).

**Recommendation:** **Option A** (delegate) plus optional web alignment. If delegation is too heavy for a hotfix, **Option B** with a follow-up task to consolidate into Generic.

---

## 6. Implementation checklist

### 6.1 Server — showdown handler

- [ ] Resolve `gameTypeCode` with `CurrentHandGameTypeCode ?? GameType.Code`.
- [ ] Razz path uses `RazzHand` + `Max(Strength)` (or Generic delegate).
- [ ] Never call `StudHand.GetBestHand()` for Razz.
- [ ] `ShowdownPlayerHand.HandType` → `RazzHand.GetLowHandDescription(...)`.
- [ ] `BestCardIndexes` derived from `GetBestLowHand()` card identity match (copy index logic from existing Stud block).
- [ ] `pot.WinReason` / `winningHandDescription` use low string (FCD ~334–339).
- [ ] `RecordHandHistoryAsync`: showdown card list from `GetBestLowHand()` for Razz.

### 6.2 Server — guardrails

- [ ] Add early exit on Stud endpoint for `GOODBADUGLY` (already exists ~66–72); mirror pattern for misrouted types if needed.
- [ ] Do **not** alter `ProcessBettingAction` for Razz (Stud handler is correct for streets).

### 6.3 Web (optional but clear)

- [ ] `TablePlay.razor` ~3724–3726: `GenericPerformShowdownAsync` **or** keep Stud call if server delegates.
- [ ] `RazzApiClientWrapper.PerformShowdownAsync` → Generic or documented Stud delegate.

### 6.4 Docs / onboarding

- [ ] `docs/AddingNewGames20.md` or stud variant section: “Razz showdown must **not** use `StudHand` in `SevenCardStud` PerformShowdown; use Generic evaluator or `RazzHand`.”

---

## 7. Tests

### 7.1 Integration — Razz showdown awards lowball winner

**New file or extend:** `src/Tests/CardGames.IntegrationTests/EndToEnd/RazzGameFlowTests.cs`

**Arrange:**

- Create `RAZZ` game with two active players at showdown (use seeder + mediator to advance streets, or insert controlled `GameCards` for two `GamePlayer`s with known hands from §3).
- Set `game.CurrentPhase = "Showdown"`, pots unawarded.

**Act:**

```csharp
var result = await Mediator.Send(
    new SevenCardStud.v1.Commands.PerformShowdown.PerformShowdownCommand(game.Id));
// After Option A, same test should still pass via delegate.
```

**Assert:**

- Success.
- `Payouts` keys contain P1’s player name (better low).
- P1 **not** P2 when P1 has `12-11-6-5-2` and P2 has `13-12-9-7-5`.
- `PlayerHands` winner row `HandType` contains `" low"` and does not read `"Pair"` / `"TwoPair"`.

Prefer seeding cards via existing integration test helpers (`DatabaseSeeder`, deal commands) rather than raw SQL.

### 7.2 Unit — handler evaluation branch

**New:** `SevenCardStudPerformShowdownCommandHandlerTests` (or `RazzShowdownTests`)

- Mock/in-memory `CardsDbContext` with fixed `GameCard` rows for P1/P2 hands from §3.
- Invoke handler directly.
- Assert winner dictionary and `HandType` strings.

### 7.3 Regression — Seven Card Stud unchanged

```bash
cd src
dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~RazzShowdownOverlayParity"
dotnet test Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~SevenCardStud|FullyQualifiedName~Razz"
```

Add a stud high-hand showdown test if one exists; ensure winner still highest **high** hand.

### 7.4 Parity — API vs table builder

Optional: given the same card entities, assert `RazzHand.Strength` from handler matches `TableStateBuilder` evaluation order for the two players (same helper as overlay tests).

---

## 8. Acceptance criteria

- [ ] Razz showdown awards pot to the **lowest** qualifying five-card hand per `RazzHand` rules.
- [ ] Wrong-way high-hand winner (e.g. two pair beating `6-5-2` low) no longer occurs on the production API path.
- [ ] `PerformShowdown` response descriptions match lowball (`*-* low` or overlay-equivalent best five).
- [ ] Hand history showdown lines list the **low** best five, not high-hand five.
- [ ] Seven Card Stud (`SEVENCARDSTUD`) showdown behavior unchanged.
- [ ] `RazzShowdownOverlayParityTests` still pass; new integration test locks payout winner.

---

## 9. File reference

| Area | Path |
|------|------|
| **Bug — live showdown** | `Features/Games/SevenCardStud/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| Stud showdown endpoint | `Features/Games/SevenCardStud/v1/Commands/PerformShowdown/PerformShowdownEndpoint.cs` |
| **Correct — table state** | `Services/TableStateBuilder.cs` (`isRazz`, `EvaluateRazzHandDescription`) |
| **Correct — domain** | `CardGames.Poker/Hands/StudHands/RazzHand.cs` |
| **Correct — evaluator** | `CardGames.Poker/Evaluation/Evaluators/RazzHandEvaluator.cs` |
| **Template — Razz in FCD handler** | `Features/Games/FiveCardDraw/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| **Template — Generic** | `Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| Web showdown call | `CardGames.Poker.Web/Components/Pages/TablePlay.razor` (`IsRazz` branch) |
| Web API wrapper | `CardGames.Poker.Web/Services/GameApi/RazzApiClientWrapper.cs` |
| Flow handler | `GameFlow/RazzFlowHandler.cs` (stud streets; no inline showdown) |
| Overlay / parity tests | `Tests/CardGames.Poker.Tests/Web/RazzShowdownOverlayParityTests.cs` |
| Odds / turn | `Features/Games/SevenCardStud/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs` |

---

## 10. Out of scope

- Changing `RazzHand` strength algorithm (unless tests prove domain bug).
- Side-pot eligibility in Stud showdown handler for **all** stud games (pre-existing; Generic delegate fixes Razz side pots if Option A).
- Red River / auth / fold-win leaks (separate audit docs).
- Dealer’s Choice UI routing beyond `CurrentHandGameTypeCode` resolution (include in §4 if implementing Option B in Stud handler only).

---

## 11. Anti-patterns (explicit)

| Do not | Why |
|--------|-----|
| Use `StudHand` for `RAZZ` showdown | High-hand ranking |
| Use `Min(Strength)` on `RazzHand` without changing `RazzHand` | Breaks established encoding; tests use `Max` |
| Only fix `TableStateBuilder` | UI already lowball; pots still wrong |
| Copy-paste FCD handler forever | Prefer delegate to Generic (Option A) |

---

## 12. Manual QA

1. Create a **Razz** table, two players, play to showdown (or test harness).
2. Compare table hand descriptions (low strings) with showdown overlay.
3. Confirm chip winner is the **lower** hand, not the player with pairs/trips visible.
4. Repeat on **Seven Card Stud** table — higher hand still wins.
