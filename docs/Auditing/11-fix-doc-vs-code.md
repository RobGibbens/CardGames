# Fix plan: documentation vs. implementation drift (“Doc vs code”)

**Audit finding:** “12. Doc vs code.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** **Documentation and onboarding accuracy** — align `README.md`, `docs/ARCHITECTURE.md`, and `docs/ADDING_NEW_GAMES.md` with how the CardGames repo **actually** registers games, starts hands, routes API/web behavior, and uses (or does not use) metadata-driven UI. **Default strategy: fix docs to match code**, not a repo-wide refactor to match obsolete “fully metadata-driven” promises unless explicitly chosen in §8.

---

## 1. Problem summary

New contributors and LLM agents are directed to **primary docs** that describe an **older or aspirational** architecture. Following those docs leads to **wrong steps** (manual registry edits that do not exist), **missing steps** (flow handlers, MediatR features, web routers, timer auto-action overrides), and **false expectations** (UI fully driven by `GameRules` with no variant switches).

A **more accurate** playbook already exists as `docs/AddingNewGames20.md`, but **entry points** still point at stale material:

| Entry point | What it claims | What code does |
|-------------|----------------|----------------|
| `README.md` § Adding New Games | “Register the game in `PokerGameRulesRegistry`” | **Assembly scan** of `[PokerGameMetadata]` + `GetGameRules()` — **no manual dictionary** (`PokerGameRulesRegistry.cs` ~8–16) |
| `docs/ARCHITECTURE.md` | Manual `FrozenDictionary<string, Func<GameRules>>` registration; UI consumption is **“Future Implementation”** | Registry is **auto-discovery**; `TablePlay.razor` **already** calls `GameRulesService.GetGameRulesAsync` (~1367, ~2554) **and** large `gameTypeCode` switch blocks (~3296+) |
| `docs/ADDING_NEW_GAMES.md` | Step 4: edit `PokerGameRulesRegistry.cs`; simplified `[PokerGameMetadata(...)]`; “UI will automatically” adapt | Attribute requires **full** constructor (`PokerGameMetadataAttribute.cs` ~8–22); **no** `IGameFlowHandler` section; troubleshooting tells devs to “add to registry” |

### Symptom (user-visible / team-visible)

- PRs that add dead registry entries or skip `*FlowHandler.cs`.
- Repeated bugs where audit fix plans assume **one** orchestration path (e.g. only `GenericStartHand` or only `DealHands`) because docs omit the **split** between generic and per-variant MediatR handlers.
- Agents implement `GetGameRules()` on domain games but never wire **timer** `SendBettingActionAsync`, **showdown** handlers, or **TablePlay** sequences — documented as automatic.
- Onboarding time wasted reconciling `ARCHITECTURE.md` with `AddingNewGames20.md`.

### Why this matters for other audit items

Several `docs/Auditing/fix-*.md` plans assume accurate knowledge of:

- **Which endpoint** runs `StartHand` / `CollectAntes` / `DealHands` per variant (`TablePlay.razor` ~3296–3564).
- **Flow handlers** vs **MediatR command handlers** (e.g. stud bring-in in `DealHands` vs `SevenCardStudFlowHandler.DealCardsAsync`).
- **Registry discovery** vs manual registration.

Fixing doc drift **reduces wrong fixes** when implementing those plans.

---

## 2. Source of truth (code — treat as canonical)

Use these when rewriting docs:

| Concern | Canonical implementation |
|---------|---------------------------|
| Game type metadata (players, betting structure, image) | `[PokerGameMetadata(...)]` on `IPokerGame` in `CardGames.Poker` (`PokerGameMetadataAttribute.cs`) |
| Metadata discovery | `PokerGameMetadataRegistry` — reflection over `IPokerGame` (`PokerGameMetadataRegistry.cs` ~45–67) |
| Phase/rules JSON for API/UI | `PokerGameRulesRegistry` — reflection, calls `instance.GetGameRules()` (`PokerGameRulesRegistry.cs` ~17–55) |
| Hand lifecycle / dealing / auto-action routing | `IGameFlowHandler` + `GameFlowHandlerFactory` (reflection in `GameFlowHandlerFactory.cs`) |
| Shared start-hand orchestration | `Features/Games/Generic/v1/Commands/StartHand/StartHandCommandHandler.cs` |
| Per-variant **duplicate** start-hand handlers | Still present under `Features/Games/{Variant}/v1/Commands/StartHand/` (e.g. `SevenCardStud`, `Baseball`, `HoldEm`) — **web often calls variant routes**, not Generic |
| Live table orchestration | `CardGames.Poker.Web/Components/Pages/TablePlay.razor` — per-game `StartHand` / `CollectAntes` / `DealHands` API calls |
| Web API routing | `CardGames.Poker.Web/Services/IGameApiRouter.cs` + `Services/GameApi/*ApiClientWrapper.cs` |
| Rules in UI (partial) | `GameRulesService.GetGameRulesAsync` + phase metadata on `TableStatePublicDto` (`CurrentPhaseRequiresAction`, etc.) |

**Accurate mental model (one paragraph for docs):**

> Discovery is automatic for **metadata** and **flow handlers**; **behavior** is still wired explicitly through MediatR feature folders, duplicated or generic command handlers, `TableStateBuilder`, and `TablePlay` switches. `GameRules` drive **some** UI (phases, actions list) but **not** all table behavior.

`docs/AddingNewGames20.md` §1–2 already states this; **promote it** to the primary guide.

---

## 3. Documented claims vs code (exact drift inventory)

### 3.1 Manual `PokerGameRulesRegistry` registration

**Docs:** `ADDING_NEW_GAMES.md` Step 4 (~206–217), `ARCHITECTURE.md` (~196–205), `README.md` (~46), troubleshooting (~387–391).

**Code:** `PokerGameRulesRegistry` static ctor scans types; comment: *“No manual registration required.”* There is **no** `RulesByGameTypeCode` dictionary to edit.

**Fix:** Remove Step 4 as “edit registry”; replace with “ensure `GetGameRules()` works on a constructible `IPokerGame` instance” and link to `PokerGameRulesRegistryTests`.

### 3.2 `[PokerGameMetadata]` example signature

**Docs:** `ADDING_NEW_GAMES.md` (~26–31) — five string/int args only.

**Code:** `PokerGameMetadataAttribute` requires `code`, `name`, `description`, min/max players, `initialHoleCards`, `initialBoardCards`, `maxCommunityCards`, `maxPlayerCards`, `hasDrawPhase`, `maxDiscards`, `wildCardRule`, `bettingStructure`, optional `imageName`.

**Fix:** Replace examples with a **copy-paste from a real game**, e.g. `SevenCardStudGame.cs` ~14–28 or `KingsAndLowsGame` metadata block.

### 3.3 “UI will automatically…” / “Future Implementation”

**Docs:**

- `ADDING_NEW_GAMES.md` (~415–419): automatic action buttons, card display, special rules.
- `ARCHITECTURE.md` (~214–241): “UI Consumption (Future Implementation)”.

**Code:**

- `TablePlay.razor` uses `_gameRules` for **some** behavior but **hard-codes** start/deal/collect sequences per `GameTypeCode`.
- Action panel still uses table state + variant logic; not a single rules-only renderer.

**Fix:** Rewrite as **“Partially metadata-driven”** with a table: what rules **do** drive vs what still needs `TablePlay` / router / handler changes. Remove “Future” wording; describe **current** `GameRulesService` usage.

### 3.4 Missing `IGameFlowHandler` from main guide

**Docs:** `ADDING_NEW_GAMES.md` — **zero** mentions of flow handlers.

**Code:** Required for `Generic StartHand`, auto-actions, dealing configuration (`GameFlowHandlerFactory`).

**Fix:** Add mandatory step (mirror `AddingNewGames20.md` §2 step 3): create `{Game}FlowHandler.cs`, override `SendBettingActionAsync` when timer betting must not use FCD default (cross-link `fix-stud-timer-auto-fold-stall.md`).

### 3.5 Generic vs per-variant MediatR handlers

**Docs:** Imply one path after “create game” / rules only.

**Code:**

- `Generic/v1` maps `MapStartHand()` (`Generic/v1/V1.cs` ~31).
- Stud/Baseball/etc. **also** map their own `MapStartHand()` to **different** `StartHandCommandHandler` types in the same assembly.
- `TablePlay` calls `SevenCardStudStartHandAsync`, `GenericStartHandAsync`, etc. by branch (~3296–3540).

**Fix:** Document **“orchestration matrix”** (§5.2) — which games use Generic start vs dedicated handler; that **both** may exist during migration.

### 3.6 `IPokerGame.GetGameRules()` vs static `*Rules.CreateGameRules()`

**Docs:** `ARCHITECTURE.md` shows rules only via static factory registration.

**Code:** Registry instantiates `IPokerGame` and calls **`GetGameRules()`** on the instance. Static `XxxRules.CreateGameRules()` is what most games return from that method.

**Fix:** Document instance method as the **wire**; static factory as the **implementation**.

### 3.7 API rules endpoint path

**Docs:** `ADDING_NEW_GAMES.md` (~224): `curl .../api/v1/games/rules/KINGSANDLOWS`.

**Code:** Handlers live under `Features/Games/Common/v1/Queries/GetGameRules/` (`GetGameRulesQuery` by `gameTypeCode`). **Verify** actual route prefix in `Program.cs` / minimal API maps when editing (do not guess — run `grep MapGetGameRules` or OpenAPI).

**Fix:** Doc must cite **generated** route from `GetGameRulesEndpoint` after verification.

### 3.8 Two competing guides

| File | Status |
|------|--------|
| `docs/ADDING_NEW_GAMES.md` | Linked from README; **stale** |
| `docs/AddingNewGames20.md` | **Accurate** playbook; not linked from README |

**Fix:** Merge v2 into v1 **or** redirect README to v2 and mark v1 deprecated.

### 3.9 Stud phase names in examples

**Docs:** `ARCHITECTURE.md` / `ADDING_NEW_GAMES.md` use fictional phases like `DealingTwoDown`, `DealFirstStreet`.

**Code:** Live phases are `ThirdStreet` … `SeventhStreet` (`SevenCardStudRules.cs`, `Phases` in `CardGames.Poker.Betting`).

**Fix:** Align all examples with **actual** `PhaseId` strings used in DB and handlers.

---

## 4. Intended end state (spec)

1. **Single canonical guide:** `docs/ADDING_NEW_GAMES.md` (merged content from `AddingNewGames20.md`) is the only guide README links to for “add a variant.”
2. **ARCHITECTURE.md** describes **current** hybrid model (metadata + explicit wiring), not a future-only UI.
3. **No doc** instructs editing a non-existent manual registry dictionary.
4. **Checklists** include: metadata, rules, flow handler, MediatR features (if needed), web router, TablePlay branch, timer override, tests.
5. **“Truth table”** documents which subsystems are metadata-driven vs hard-coded (§5.1).
6. Optional: lightweight **doc lint** in CI (§6.4) to prevent regression.

---

## 5. Implementation plan (documentation changes)

### 5.1 Add “Architecture truth table” to `ARCHITECTURE.md`

Insert after “Solution: Rule-Driven Architecture”:

| Subsystem | Metadata-driven? | Where wired when not |
|-----------|------------------|----------------------|
| Game list / lobby images | Yes | `PokerGameMetadataRegistry` |
| `GET` game rules JSON | Yes | `PokerGameRulesRegistry` |
| Phase labels / `RequiresPlayerAction` on table | Mostly | `TableStateBuilder` + rules registry |
| Start hand / antes / deal sequence | **No** (variant branches) | `TablePlay.razor`, per-variant APIs |
| Betting commands | **No** | `Features/Games/{Variant}/.../ProcessBettingAction` |
| Showdown | **No** | Variant or `Generic/PerformShowdown` |
| Timer auto-action | **No** | `IGameFlowHandler.SendBettingActionAsync` overrides |
| Create-game stakes (ante, blinds, stud bring-in) | **No** | `CreateGameCommandHandler` (+ planned normalizers — see `fix-bring-in-never-posted.md`) |

### 5.2 Add “Orchestration matrix” appendix (new section in ADDING_NEW_GAMES)

Generate from code (LLM must **verify** before publishing table):

**Method:** For each `PokerGameMetadataRegistry` code, grep `TablePlay.razor` for `StartHand`, `CollectAntes`, `DealHands` API used; note `Generic` vs dedicated.

**Starter rows (verify in repo):**

| Game code | StartHand API (TablePlay) | Post-start deal path |
|-----------|---------------------------|----------------------|
| `HOLDEM` | `HoldEmStartHandAsync` or `Generic` (branch ~3475) | Blinds via flow handler |
| `SEVENCARDSTUD` / `RAZZ` | `SevenCardStudStartHandAsync` | `CollectAntes` + `DealHands` (stud API) |
| `KINGSANDLOWS` | `KingsAndLowsStartHandAsync` | Variant-specific |
| Many others | `GenericStartHandAsync` | Flow handler flags in Generic handler |

Keep this table **in the doc** and add a note: “Update this table when changing `TablePlay.razor`.”

### 5.3 Rewrite `docs/ADDING_NEW_GAMES.md` (merge from v2)

**Order of sections (recommended):**

1. Quick orientation (from `AddingNewGames20.md` §1).
2. Minimal diff path (§2) — metadata + flow handler + reuse handlers.
3. Full production path (§3) — API, web, tests, assets.
4. **Registration (corrected):** only `[PokerGameMetadata]` + working `GetGameRules()` / parameterless or discoverable ctor; **no** registry file edit.
5. **Real** `PokerGameMetadata` example (full attribute).
6. Game rules (`*Rules.cs`) — phase IDs must match `Phases` enum / DB strings.
7. Flow handler requirements + `SendBettingActionAsync` note.
8. When metadata is not enough (Bob Barker, Tollbooth) — keep existing Bob Barker section; point to Tollbooth section in v2.
9. Testing — registry test pattern from current `ADDING_NEW_GAMES.md` (~372–382) is **valid**; troubleshooting **remove** “add to registry”.
10. Link to `ARCHITECTURE.md` truth table.

**After merge:** Add at top of `AddingNewGames20.md`:

```markdown
> **Deprecated:** Content merged into [ADDING_NEW_GAMES.md](../ADDING_NEW_GAMES.md). Do not edit this file except to move variant-specific addenda.
```

Or delete v2 if fully merged (prefer merge + deprecation banner to preserve git history links).

### 5.4 Update `README.md` § Adding New Games

Replace steps 1–3 with:

1. Domain game + `[PokerGameMetadata]` + `*Rules.cs` (`GetGameRules()`).
2. API `*FlowHandler.cs` (discovered by `GameFlowHandlerFactory`).
3. Wire behavior: MediatR features, `TableStateBuilder`, `IGameApiRouter`, `TablePlay` as needed per `docs/ADDING_NEW_GAMES.md`.

Remove “Register in PokerGameRulesRegistry.”

### 5.5 Update `docs/ARCHITECTURE.md`

- Replace manual registry snippet (~196–205) with pointer to auto-discovery and `PokerGameRulesRegistry` remarks.
- Rename § “UI Consumption (Future Implementation)” → **“UI consumption (today)”** with short description + link to truth table; optional trimmed code sample from `GameRulesService`.
- Update “Migration Path” (~252–258): state that UI **partially** adopted rules; variant switches remain.
- Fix stud phase examples to `ThirdStreet`…`SeventhStreet`.

### 5.6 Cross-links from audit fix plans

In each existing `docs/Auditing/fix-*.md`, add under **Related** (one line each, optional batch edit):

- “Orchestration path: see `fix-doc-vs-code.md` §5.2 before assuming Generic-only flow.”

At minimum update plans that mention wrong abstractions:

- `fix-stud-timer-auto-fold-stall.md` (flow handler vs FCD default)
- `fix-bring-in-never-posted.md` (`DealHands` vs `CreateGame` stakes)
- `fix-red-river-showdown-stall.md` (`TablePlay` / `TryLoadShowdownAsync` routing)

### 5.7 `docs/ADDING_NEW_GAMES.md` — stud stakes note (pointer only)

Do **not** duplicate full bring-in fix; add one paragraph:

> Stud `BringIn` / `SmallBet` / `BigBet` on `Game` are **not** set by `CreateGameCommand` today; see `docs/Auditing/fix-bring-in-never-posted.md`.

Prevents docs from claiming “create game + rules only” yields correct stud betting.

---

## 6. Verification

### 6.1 Human review checklist

- [ ] README does not mention manual `PokerGameRulesRegistry` dictionary edits.
- [ ] `ARCHITECTURE.md` has no “Future Implementation” for UI rules consumption.
- [ ] `ADDING_NEW_GAMES.md` `PokerGameMetadata` example compiles against `PokerGameMetadataAttribute`.
- [ ] All phase names in doc examples exist in at least one `*Rules.cs`.
- [ ] Flow handler step is present and mentions `GameFlowHandlerFactory`.
- [ ] `AddingNewGames20.md` deprecated or merged.

### 6.2 Smoke test for new-game doc accuracy

Follow the **minimal path** in the updated guide for a **toy** game code (or read-only walkthrough):

1. Confirm only files listed in guide are touched for “metadata-only” variant.
2. `PokerGameRulesRegistry.TryGet("NEWCODE")` in a unit test (as documented).

### 6.3 Existing tests (registry discovery)

```bash
cd src
dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~PokerGameRulesRegistry"
dotnet test Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~PokerGameRegistry"
```

All registered games in metadata should appear in rules registry tests.

### 6.4 Optional CI guard (prevent regression)

Add script `scripts/verify-doc-consistency.sh` (or MSBuild target) that **fails** if:

- `docs/ADDING_NEW_GAMES.md` or `docs/ARCHITECTURE.md` contains `RulesByGameTypeCode` manual dictionary pattern **or** “Register in the API” + `PokerGameRulesRegistry.cs` edit instruction.
- `README.md` contains `Register the game in \`PokerGameRulesRegistry\``.

Wire into CI only if maintainers want enforcement; otherwise run manually before release.

---

## 7. Acceptance criteria

- [ ] Canonical new-game guide matches `PokerGameRulesRegistry` / `PokerGameMetadataRegistry` / `GameFlowHandlerFactory` behavior.
- [ ] No misleading “UI automatically adapts entirely from rules” without caveats.
- [ ] README + ARCHITECTURE + ADDING_NEW_GAMES are mutually consistent.
- [ ] `AddingNewGames20.md` deprecated or fully merged; no conflicting step lists.
- [ ] Orchestration matrix present and reflects `TablePlay.razor` (verified).
- [ ] Audit fix plans can reference one doc for “which handler path” questions.

---

## 8. Out of scope (unless product explicitly expands scope)

- Refactoring `TablePlay.razor` to be fully metadata-driven (code change, not doc-only).
- Deleting duplicate per-variant `StartHandCommandHandler` copies (separate migration project).
- Merging all variants onto `GenericStartHand` only.
- Rewriting `docs/PRD.md`, `SecurityReview.md`, or league design docs (unless they duplicate wrong registry instructions — grep and fix only if found).

### Optional follow-up (code to match *better* docs)

If the team wants docs **and** code aligned to a single orchestration path:

1. Route all `MapStartHand` endpoints to **one** `Generic.StartHandCommandHandler` via thin endpoint wrappers.
2. Delete duplicate handlers after parity tests.

Track as a **separate** engineering initiative; do not block doc fix on it.

---

## 9. File reference

| Area | Path |
|------|------|
| Stale primary guide | `docs/ADDING_NEW_GAMES.md` |
| Accurate playbook (merge source) | `docs/AddingNewGames20.md` |
| Stale architecture narrative | `docs/ARCHITECTURE.md` |
| README drift | `README.md` (~40–50) |
| Rules auto-discovery | `src/CardGames.Poker.Api/Games/PokerGameRulesRegistry.cs` |
| Metadata auto-discovery | `src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs` |
| Flow handler discovery | `src/CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs` |
| Generic start hand | `src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/StartHand/StartHandCommandHandler.cs` |
| Live table orchestration | `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` |
| Partial rules in UI | `src/CardGames.Poker.Web/Services/GameRulesService.cs` |
| Metadata attribute (real signature) | `src/CardGames.Poker/Games/PokerGameMetadataAttribute.cs` |
| Example metadata usage | `src/CardGames.Poker/Games/SevenCardStud/SevenCardStudGame.cs` |
| Registry tests | `src/Tests/CardGames.Poker.Tests/Api/PokerGameRulesRegistryTests.cs` |

---

## 10. Related audit items

| Doc | Relationship |
|-----|----------------|
| `fix-bring-in-never-posted.md` | Docs imply create-game + rules suffice; code omits stud stake fields |
| `fix-stud-timer-auto-fold-stall.md` | Docs omit `SendBettingActionAsync` override requirement |
| `fix-trusted-identity-authz.md` | Security doc separate; ensure ADDING_NEW_GAMES mentions auth on new endpoints |
| `fix-red-river-showdown-stall.md` | TablePlay / router gaps not visible in old architecture doc |

---

## 11. Implementation order (suggested for LLM)

1. Grep repo docs for `PokerGameRulesRegistry`, `RulesByGameTypeCode`, `Future Implementation`, `UI will automatically` — build edit list.
2. Verify `GetGameRules` HTTP route; fix curl examples in ADDING_NEW_GAMES.
3. Merge `AddingNewGames20.md` into `ADDING_NEW_GAMES.md` (§5.3).
4. Patch `ARCHITECTURE.md` (§5.1, §5.5).
5. Patch `README.md` (§5.4).
6. Build orchestration matrix from `TablePlay.razor` (§5.2).
7. Deprecate `AddingNewGames20.md`; add cross-links in auditing folder (§5.6).
8. Optional: `scripts/verify-doc-consistency.sh` + CI (§6.4).
9. Run registry tests (§6.3); human checklist (§6.1).
