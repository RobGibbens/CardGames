# Fix plan: bring-ins are never posted on stud third street

**Audit finding:** “Bring-ins are never posted.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** Persisted stud stakes on `Game` (`BringIn`, `SmallBet`, `BigBet`) at table creation / Dealer’s Choice selection, and consistent **posting** of the bring-in when Third Street betting begins. Primary live path: `CollectAntes` → `DealHands` (not flow-handler-only dealing).

---

## 1. Problem summary

On **ante + bring-in stud-family** tables, Third Street should:

1. Identify the **bring-in player** (forced first actor on lowest upcard for high stud; rules vary for Razz — see §11).
2. **Post** a mandatory bring-in amount from that player’s stack into the main pot.
3. Open the betting round with `BettingRound.CurrentBet == bring-in amount` and that seat as `CurrentActorIndex`.

**Observed:** Lowest-card seat may still be chosen as first actor (`BringInPlayerIndex` set), but **no chips move**, **pot does not increase** beyond antes, and **`CurrentBet` on the street is 0**. Players can check around as if no bring-in existed.

### Symptom (user-visible)

- Third Street: bring-in seat shows **no** posted chips / same stack as after antes only.
- Main pot total equals **antes only** (e.g. 20 with 10 ante × 2) instead of antes + bring-in (e.g. 25 with bring-in 5).
- Action panel treats the round as **unopened** (`CurrentBet == 0`) instead of “complete to small bet.”
- `GET /games/{id}` returns `bringIn: null` (or `0`) even for `SEVENCARDSTUD` / `RAZZ` / etc.

### Domain reference (correct behavior)

`SevenCardStudGame` with `useBringIn: true` (`src/CardGames.Poker/Games/SevenCardStud/SevenCardStudGame.cs`):

- Stakes: `ante: 5`, `bringIn: 5`, `smallBet: 10`, `bigBet: 20` (`SevenCardStudGameTests.CreateTwoPlayerGame` ~419–426).
- `PostBringIn()` moves chips and adds to pot (`PostBringIn_AddsChipsToPot` ~117–129): pot **15** after antes (10) + bring-in (5).

The API **mirrors** posting logic in code, but it is **gated** on `game.BringIn > 0`, which is **never set** in production create flows.

---

## 2. Root cause (exact)

### 2.1 `Game.BringIn` is never populated when tables are created

**File:** `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/CreateGame/CreateGameCommandHandler.cs` (~117–141)

The `Game` entity is created with:

- `Ante`, `MinBet`, `SmallBlind`, `BigBlind` from `CreateGameCommand`
- **No** assignment to `BringIn`, `SmallBet`, or `BigBet`

**File:** `CreateGameCommand.cs` — command has `Ante`, `MinBet`, blinds only; **no** bring-in / small-bet / big-bet fields.

**Client:** `CreateTable.razor` (~1657–1670) sends `CreateGameCommand(ante, gameCode, …, minimumBet, …)` with blinds for Hold’em only. Stud tables send e.g. ante **5**, min bet **10** — never `BringIn`.

**League launch:** `LaunchLeagueEventSessionCommandHandler` (~120–130, ~205–215) forwards the same `CreateGameCommand` shape (ante + minBet + blinds). No stud stake columns on league events.

**Dealer’s Choice:** `ChooseDealerGameCommandHandler` (~182) sets `game.MinBet` only — not `BringIn` / `SmallBet` / `BigBet`.

So for virtually all live games, `game.BringIn` stays **`null`** → treated as **0**.

### 2.2 Posting logic exists but is a no-op when `BringIn == 0`

**Primary path (TablePlay):** After `StartHand`, host/client calls `CollectAntes` then `DealHands` (`TablePlay.razor` ~3358–3364 for Seven Card Stud, similar for Razz, FTQ, Baseball, etc.).

**File:** `SevenCardStud/v1/Commands/DealHands/DealHandsCommandHandler.cs` (~270–287)

```csharp
var bringIn = game.BringIn ?? 0;
if (bringIn > 0 && firstActorSeatPosition >= 0)
{
    var actualBringIn = Math.Min(bringIn, bringInPlayer.ChipStack);
    bringInPlayer.CurrentBet = actualBringIn;
    bringInPlayer.ChipStack -= actualBringIn;
    currentBet = actualBringIn;
}
```

With `bringIn == 0`, this block **never runs**. `BettingRound.CurrentBet` stays **0** (~323).

The same `if (bringIn > 0)` pattern exists in:

- `FollowTheQueen/.../DealHandsCommandHandler.cs` (~265–274)
- `Baseball/.../DealHandsCommandHandler.cs` (~277–286)
- `GoodBadUgly/.../DealHandsCommandHandler.cs` (~319–328)
- `PairPressureFlowHandler`, `FollowTheQueenFlowHandler`, `BaseballFlowHandler`, `SevenCardStudFlowHandler`, `TollboothFlowHandler` (flow-handler deal paths)

**Flow-handler path** (`SevenCardStudFlowHandler.DealCardsAsync` ~241–261) would post if `BringIn` were set, but stud hands in production use **MediatR `DealHands`** after antes, not this method for the initial Third Street deal.

### 2.3 Tests mask the production gap

Unit tests **manually** set stakes on the `Game` entity, e.g. `FollowTheQueen/.../DealHandsCommandHandlerTests.cs` `SeedGameAsync` (~161–163):

```csharp
BringIn = 5,
SmallBet = 10,
BigBet = 20,
```

`DatabaseSeeder.CreateGameAsync` (`IntegrationTests/Infrastructure/DatabaseSeeder.cs` ~28–44) sets `Ante` and `MinBet` only — **no** `BringIn`. Integration tests like `BaseballDealHandsCommandHandlerTests` assert **first actor seat** (`BringInPlayerIndex`) but **not** chip posting.

### Architecture (current vs intended)

```
CreateTable / League / DC
  → CreateGameCommand (Ante, MinBet only)
       → Game.BringIn = null  ❌

CollectAntes → ThirdStreet phase
  → DealHandsCommand
       → bringIn = game.BringIn ?? 0  → 0
       → skip posting
       → BettingRound.CurrentBet = 0  ❌
```

**Intended:**

```
CreateGame / ChooseDealerGame
  → normalize stud stakes from rules + ante/minBet
       → Game.BringIn, SmallBet, BigBet set  ✓

DealHands (ThirdStreet)
  → post bring-in, update pot + contributions  ✓
       → BettingRound.CurrentBet = actualBringIn  ✓
```

---

## 3. Intended behavior (spec)

### 3.1 Which games get a bring-in

Use `GameRules.SpecialRules["HasBringIn"]` from `PokerGameRulesRegistry` (same source as UI rules metadata):

| Game code | `HasBringIn` (rules) | Post bring-in on Third Street? |
|-----------|----------------------|--------------------------------|
| `SEVENCARDSTUD` | `true` | Yes |
| `RAZZ` | `true` | Yes (amount; **actor** rules may differ — §11) |
| `FOLLOWTHEQUEEN` | `true` | Yes |
| `PAIRPRESSURE` | `true` | Yes |
| `BASEBALL` | `true` (stud structure) | Yes |
| `TOLLBOOTH` | `true` | Yes |
| `GOODBADUGLY` | **`false`** | **No** forced post (lowest upcard may still act first per handler) |

Do **not** set `BringIn` for variants with `HasBringIn == false`.

### 3.2 Stake derivation (when host supplies ante + min bet only)

Align with domain tests and fixed-limit stud:

| Field | Rule |
|-------|------|
| `SmallBet` | `MinBet` (host “minimum bet” is the small bet on 3rd/4th street) |
| `BigBet` | `2 × SmallBet` (fixed-limit stud; matches `SevenCardStudGameTests` small 10 / big 20) |
| `BringIn` | If `HasBringIn`: **`BringIn = Ante`** when `Ante > 0`; else `max(1, SmallBet / 2)` |

Examples (matches `CreateTwoPlayerGame` / DealHands tests):

- Ante **5**, MinBet **10** → BringIn **5**, SmallBet **10**, BigBet **20**
- Ante **10**, MinBet **20** → BringIn **10**, SmallBet **20**, BigBet **40**

If product later needs configurable bring-in, add optional `CreateGameCommand.BringIn` override; **default** to derivation above.

### 3.3 Posting on Third Street (all code paths)

When `BringIn > 0` and bring-in player is identified:

1. `actualBringIn = min(BringIn, ChipStack)`
2. Deduct stack; set `GamePlayer.CurrentBet = actualBringIn`
3. **`TotalContributedThisHand += actualBringIn`** (DealHands handlers today **omit** this — fix)
4. If `ChipStack == 0` after post → **`IsAllIn = true`** (mirror `CollectAntes` pattern)
5. Add `actualBringIn` to **main pot** `Pot.Amount`
6. Add **`PotContribution`** row (eligible to win)
7. `BettingRound.CurrentBet = actualBringIn`
8. `game.BringInPlayerIndex = bring-in seat`

**Reference implementation:** `SevenCardStudFlowHandler.DealCardsAsync` (~246–261) updates pot; extend to contributions + all-in. Prefer **one shared helper** used by DealHands + flow handlers (§5.3).

### 3.4 Persistence timing

Set `BringIn`, `SmallBet`, `BigBet` on the `Game` row at:

- `CreateGameCommandHandler` (non–Dealer’s Choice tables)
- `ChooseDealerGameCommandHandler` when a stud variant is selected
- Optionally: one-time **read-time fallback** in `GetGame` / `DealHands` for legacy rows (§5.4) — prefer explicit migration/normalize on first hand if needed

---

## 4. Implementation plan

### 4.1 Add `StudGameStakesNormalizer` (single source of truth)

**Suggested path:** `src/CardGames.Poker.Api/Games/StudGameStakesNormalizer.cs`

```csharp
internal static class StudGameStakesNormalizer
{
    public static bool TryGetHasBringIn(GameRules rules) { ... }

    public static void ApplyToGame(Game game, GameRules rules, int ante, int minBet)
    {
        // If not stud / no antes: return without changes
        // SmallBet = minBet; BigBet = 2 * SmallBet
        // If HasBringIn: BringIn = ante > 0 ? ante : max(1, SmallBet / 2)
        // Else: BringIn = null or 0
    }
}
```

Resolve rules via `PokerGameRulesRegistry.TryGet(gameTypeCode, out rules)` in callers.

**Do not** hard-code only `SEVENCARDSTUD`; use `HasBringIn` + `Betting.HasAntes` (or street phases in rules) so Razz, FTQ, Pair Pressure, Baseball, Tollbooth are included; exclude Good Bad Ugly.

### 4.2 `CreateGameCommandHandler`

After `gameType` is resolved (~105–114), before `context.Games.Add(game)`:

```csharp
if (gameType is not null && PokerGameRulesRegistry.TryGet(gameType.Code, out var rules) && rules is not null)
{
    StudGameStakesNormalizer.ApplyToGame(game, rules, command.Ante, command.MinBet);
}
```

Dealer’s Choice: skip until variant chosen (game type null).

### 4.3 `ChooseDealerGameCommandHandler`

After setting `game.GameTypeId`, `game.MinBet`, `game.Ante` (~182 area):

- Load rules for selected `GameTypeCode`
- Call `StudGameStakesNormalizer.ApplyToGame(game, rules, command.Ante, command.MinBet)`

### 4.4 Optional: `CreateGameCommand` explicit overrides

Add optional `int? BringIn`, `int? SmallBet`, `int? BigBet` only if you need host-configurable stakes without changing MinBet semantics. If omitted, normalizer wins. **Not required** for minimal fix.

### 4.5 Shared `PostBringInForThirdStreetAsync`

**Suggested path:** `src/CardGames.Poker.Api/Features/Games/Common/StudBringInPosting.cs`

Parameters: `CardsDbContext`, `Game`, `GamePlayer bringInPlayer`, `int bringInAmount`, `DateTimeOffset now`, `CancellationToken`.

Returns: `actualBringIn` posted.

Call from:

- `SevenCardStud/.../DealHandsCommandHandler.cs` (and parallel FTQ, Baseball, GBU only when `bringIn > 0`)
- `SevenCardStudFlowHandler` and siblings (`PairPressure`, `FollowTheQueen`, `Baseball`, `Tollbooth`)

Replace duplicated inline blocks to avoid pot/contribution drift.

### 4.6 Legacy games (optional hardening)

For existing `Game` rows with `BringIn == null` and stud type:

- **Option A (recommended):** In `DealHandsCommandHandler` (stud shared base or first line of handler), if `game.BringIn is null or 0` and rules `HasBringIn`, call `StudGameStakesNormalizer.ApplyToGame` from current `Ante`/`MinBet`, then post. Persists stakes on save.
- **Option B:** SQL/data fix script for production — out of scope unless ops requests it.

### 4.7 Client / API contract

- `GetGameResponse` already exposes `bringIn`, `smallBet`, `bigBet` — no DTO change required once DB is populated.
- **No** TablePlay change required if server derives stakes; UI may later display bring-in amount from `GetGame`.

### 4.8 `LeagueEventGameSettings` (optional clarity)

Add `NormalizeStudStakes` used by launch + create, or document that `CreateGameCommandHandler` normalization is sufficient. League events do **not** need new DB columns if derivation from ante/minBet is acceptable.

---

## 5. Verification — automated tests

### 5.1 Unit — `CreateGameCommandHandler`

1. Create game with `GameCode = "SEVENCARDSTUD"`, `Ante = 5`, `MinBet = 10`.
2. Load `Game` from DB.
3. Assert `BringIn == 5`, `SmallBet == 10`, `BigBet == 20`.

Repeat `GOODBADUGLY`: assert `BringIn` is null or 0; `SmallBet`/`BigBet` still set if you normalize street bets for all stud structures.

### 5.2 Integration — end-to-end Third Street posting

**New test** (e.g. `SevenCardStudBringInTests.cs` in IntegrationTests):

1. `DatabaseSeeder.CreateGameAsync(..., "SEVENCARDSTUD", ante: 5, minBet: 10)` then **apply normalizer in seeder** OR use real `CreateGameCommand` through MediatR.
2. Add 2 players, `StartHand`, `CollectAntes`, `DealHands`.
3. Assert:
   - One player has `CurrentBet == 5` (or min(5, stack))
   - Main `Pot.Amount == 10 + 5` (antes + bring-in) for full stacks
   - Active `BettingRound.CurrentBet == 5`
   - `BringInPlayerIndex ==` that seat
   - Optional: `PotContributions` includes bring-in amount

### 5.3 Unit — `DealHandsCommandHandler`

Extend `FollowTheQueen/.../DealHandsCommandHandlerTests` or Seven Card Stud equivalent:

- After deal on Third Street, assert bring-in player **chip stack** decreased and `TotalContributedThisHand` includes bring-in (once helper adds it).

### 5.4 Update `DatabaseSeeder.CreateGameAsync`

After creating `Game`, call `StudGameStakesNormalizer` when rules apply so integration tests reflect production.

### Commands

```bash
cd src
dotnet test Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~BringIn|FullyQualifiedName~DealHands"
dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~CreateGame|FullyQualifiedName~DealHands"
```

---

## 6. Manual QA checklist

1. Create **Seven Card Stud** table (default ante/min bet from UI).
2. Two players join; start hand; let flow collect antes and deal Third Street.
3. Confirm bring-in seat has chips in front / lower stack than non–bring-in (minus ante).
4. Pot = sum(antes) + bring-in.
5. Other player’s legal actions require **call/raise** to at least complete to small bet, not free check (unless bring-in player is already at current bet and action rules allow check — verify against `ProcessBettingAction`).
6. Repeat **Razz**, **Follow the Queen**, **Baseball**.
7. **Good Bad Ugly:** no bring-in post; Third Street still deals.
8. `GET game`: `bringIn`, `smallBet`, `bigBet` populated for stud tables.

---

## 7. Acceptance criteria

- [ ] New stud tables have `Game.BringIn > 0` when rules `HasBringIn == true` and ante/minBet are valid.
- [ ] `SmallBet` / `BigBet` persisted (not only inferred at deal time from `MinBet`).
- [ ] Third Street `DealHands` posts bring-in chips, updates main pot, and sets `BettingRound.CurrentBet`.
- [ ] `TotalContributedThisHand` and `PotContribution` updated on bring-in post (parity with antes).
- [ ] Short-stack bring-in posts `min(BringIn, stack)` and sets `IsAllIn` when stack hits 0.
- [ ] Good Bad Ugly does **not** post a bring-in.
- [ ] Dealer’s Choice stud selection applies same normalization.

---

## 8. File reference

| Area | Path |
|------|------|
| Create game (missing stakes) | `Features/Games/Common/v1/Commands/CreateGame/CreateGameCommandHandler.cs` |
| Create command shape | `Features/Games/Common/v1/Commands/CreateGame/CreateGameCommand.cs` |
| Web create table | `CardGames.Poker.Web/Components/Pages/CreateTable.razor` |
| Dealer’s Choice stakes | `Features/Games/Common/v1/Commands/ChooseDealerGame/ChooseDealerGameCommandHandler.cs` |
| League launch → create | `Features/Leagues/v1/Commands/LaunchLeagueEventSession/LaunchLeagueEventSessionCommandHandler.cs` |
| Third Street deal + post gate | `Features/Games/SevenCardStud/v1/Commands/DealHands/DealHandsCommandHandler.cs` |
| Parallel deal handlers | `FollowTheQueen`, `Baseball`, `GoodBadUgly` `DealHandsCommandHandler.cs` |
| Flow-handler post (reference) | `GameFlow/SevenCardStudFlowHandler.cs` (`DealCardsAsync` ~236–261) |
| Collect antes (resets CurrentBet) | `SevenCardStud/.../CollectAntes/CollectAntesCommandHandler.cs` (~133–137) |
| Rules metadata | `Games/SevenCardStud/SevenCardStudRules.cs` (`HasBringIn`), `GoodBadUglyRules.cs` (`HasBringIn: false`) |
| Domain reference | `Games/SevenCardStud/SevenCardStudGame.cs` (`PostBringIn`) |
| Entity fields | `Data/Entities/Game.cs` (`BringIn`, `SmallBet`, `BigBet`, `BringInPlayerIndex`) |
| Client deal sequence | `CardGames.Poker.Web/Components/Pages/TablePlay.razor` |
| Test seeder gap | `Tests/CardGames.IntegrationTests/Infrastructure/DatabaseSeeder.cs` |

---

## 9. Related audit items

| Doc | Relationship |
|-----|----------------|
| `fix-short-blind-all-in.md` | Short bring-in post should set `IsAllIn` when stack empties |
| `fix-big-blind-option.md` | Hold’em only; listed for “who can act” edge cases |
| `fix-stud-timer-auto-fold-stall.md` | Third Street action timer; unrelated to bring-in amount |
| `fix-razz-showdown-lowball.md` | Razz showdown; separate from bring-in posting |
| `11-fix-doc-vs-code.md` | Orchestration path: see §5.2 and the matrix in `docs/ADDING_NEW_GAMES.md` before assuming a Generic-only flow. Create-game stakes and `DealHands` are different steps |

---

## 10. Out of scope

- **Razz bring-in player selection** (highest upcard vs lowest) — `DealHands` uses lowest-card comparer for all types sharing Seven Card Stud deal handler; fix only if audit requires Razz-specific actor rules.
- Changing fixed-limit **betting** increments in `ProcessBettingAction` (assumes `MinBet` on round is already correct).
- Wallet / `PlayerChipLedgerEntryType.BringIn` on **join** (buy-in audit) — different meaning from stud **forced bet**.
- UI stake editor for explicit bring-in (optional future).

---

## 11. Implementation order (suggested for LLM)

1. Implement `StudGameStakesNormalizer` + unit tests for derivation table (§3.2).
2. Wire into `CreateGameCommandHandler` and `ChooseDealerGameCommandHandler`.
3. Implement `StudBringInPosting` helper (pot + contributions + all-in).
4. Replace inline bring-in blocks in `SevenCardStud/DealHands` (then FTQ, Baseball, GBU, flow handlers).
5. Add DealHands fallback normalize for legacy games (§4.6 Option A) if desired.
6. Integration test §5.2 + update `DatabaseSeeder`.
7. Manual QA §6.
