# Tollbooth prompt

## Mission
Team, add a new poker game variant to this repository in a way that is consistent with existing architecture, conventions, and quality gates.

Inputs you must use:
- Primary playbook: [docs/AddingNewGames20.md](docs/AddingNewGames20.md)

Game Name: Tollbooth
Game Type Code: TOLLBOOTH
Image Name: tollbooth.png
Variant Family: Seven Card Stud
Mode: Production-Ready

Rules:
Tollbooth is a Seven Card Stud game. It has mostly the same game play mechanics as Seven Card Stud (antes, betting rounds, showdown). The difference is in how cards are dealt. We start the same as Seven Card Stud, by dealing each player 2 cards face down and 1 card face up. Then we have a betting round. Then, instead of immediately dealing a card face up to each player, we are going to turn two cards face up in the middle of the table next to the deck. The first player to act will be presented with an overlay and given the option to purchase the card furthest from the deck for free, or purchase the card nearest the deck for 1x the ante, or to take the top face down card from the deck for 2x the ante. If the player takes the furthest card, then the nearest card moves into the furthest position and the top card from the deck is flipped over face up into the nearest position. If the player takes the nearest card then the furthest card stays where it is and the top card from the deck is flipped face up and takes the nearest position. The player can only take one card. If the choice timer of 30 seconds times out, then the player will automatically take the furthest card. Once the player chooses a card (either manually or automatically) then the action moves to the next card. Once every player makes a choice in the round, then the betting action takes place as normal. 

Community card cleanup: After all players choose in a TollboothOffer round, the remaining 2 display cards should be remain visible on the table and used for the next betting round. However, they should NOT be included as community cards.

All-in runout: When all remaining players are all-in during a betting round, the subsequent tollbooth rounds should auto-deal using the "free" (furthest) option for each player, dealing out remaining streets rapidly. This mirrors how 7CS deals remaining streets automatically when all-in. Recommendation: handle in ProcessBettingAction — detect all-in state and bypass tollbooth offers.

Card display on table: The 2 tollbooth face-up cards should be visible to all players via TableStatePublicDto.CommunityCards. After the round, they should stay visible on the table and used for the next round. They should NOT be used as / counted as community cards during Hand evaluation or odds calculation though. 


## Operating Mode
Support two implementation modes. Confirm selected mode at the start.

1. Minimal Diff
- Goal: smallest safe set of changes to get the game playable and test-covered.
- Reuse existing endpoint families and handlers whenever possible.
- Avoid adding new contracts/endpoints unless required.

2. Production-Ready
- Goal: full launch-quality implementation with stronger validation, broader tests, and explicit docs updates.
- Include negative-path and regression coverage.
- Include rollout readiness checks.

## Pre-Flight and Repository State Rules
Before coding:
1. Inspect uncommitted changes first.
- Run git status
- Run git diff --name-only
- Run git diff and evaluate whether there is in-flight work for this game
2. If relevant uncommitted work exists:
- Build on top of it
- Preserve existing intent
- Do not overwrite unrelated edits
3. If no relevant uncommitted work exists:
- Derive all changes from repository conventions in [docs/AddingNewGames20.md](docs/AddingNewGames20.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), and current neighboring implementations

## Guardrails (Mandatory)
- If anything is unclear, prompt/ask me
- Do not stage, commit, amend, reset, checkout, or discard changes unless explicitly asked.
- Preserve unrelated working tree changes.
- Never revert user changes you did not create.
- Do not directly edit generated files.
- Treat [src/CardGames.Contracts/RefitInterface.v1.cs](src/CardGames.Contracts/RefitInterface.v1.cs) as generated output.
- If contracts change, regenerate via project build for [src/CardGames.Poker.Refitter](src/CardGames.Poker.Refitter) instead of hand-editing generated artifacts.
- Keep diffs minimal and localized; avoid opportunistic refactors.

## Phased Execution Plan (Mandatory)
Execute in these phases and report completion of each phase before moving on.

### Phase 1: Discovery
- Identify nearest existing game family implementation to mirror.
- Map all required touchpoints:
  - Domain: [src/CardGames.Poker](src/CardGames.Poker)
  - API: [src/CardGames.Poker.Api](src/CardGames.Poker.Api)
  - Web: [src/CardGames.Poker.Web](src/CardGames.Poker.Web)
  - Contracts: [src/CardGames.Contracts](src/CardGames.Contracts)
  - Tests: [src/Tests](src/Tests)
  - Docs: [docs](docs)
- Produce a concrete file plan before editing.

### Phase 2: Implementation
Implement required changes across domain/API/web/tests/docs per selected mode.

### Phase 3: Verification
Run required build/test/run commands and targeted tests. Fix failures caused by your changes.

### Phase 4: Documentation and Final Report
Update docs as needed and deliver the final change report format defined below.

## Required Deliverables by Layer

### Domain Deliverables
- Add new game metadata and rules under [src/CardGames.Poker](src/CardGames.Poker), typically in Games/<NewGame>/.
- Ensure metadata and rules codes match exactly (all caps game type code convention).
- Define ordered phases and rule configs (dealing, betting, drawing if applicable, showdown).
- Keep implementation aligned with metadata-driven architecture.

Acceptance criteria:
- Game is discoverable via existing metadata/rules registration flow.
- Rules are coherent and phase order matches intended gameplay.

### API Deliverables
- Add or update game code constants and registry wiring used by orchestration paths.
- Add flow handler where required and rely on existing discovery patterns.
- Update command handlers only where variant behavior differs.
- Update showdown and state projection logic when transitional states require it.
- Follow MediatR + validation pipeline conventions in [src/CardGames.Poker.Api/Program.cs](src/CardGames.Poker.Api/Program.cs).

Acceptance criteria:
- API correctly handles hand lifecycle and variant-specific actions.
- No regressions in existing variants.
- Variant behavior is explicit where needed and reused where possible.

### Web Deliverables
- Update API routing maps for game code in [src/CardGames.Poker.Web/Services/IGameApiRouter.cs](src/CardGames.Poker.Web/Services/IGameApiRouter.cs).
- Update relevant game-family UI predicates and conditional rendering in table/play/setup flows.
- Ensure create/edit/dealer-choice/table canvas behavior is consistent for the new game.
- Add game image asset if needed under [src/CardGames.Poker.Web/wwwroot/images/games](src/CardGames.Poker.Web/wwwroot/images/games).

Acceptance criteria:
- Game appears in relevant web flows and uses correct endpoints.
- UI behavior matches game rules and constraints.

### Test Deliverables
- Add targeted integration lifecycle tests near the closest family area in [src/Tests](src/Tests).
- Include at least one negative-path test for invalid action/state.
- Add regression tests for any state-builder or phase-transition logic you changed.

Acceptance criteria:
- New targeted tests pass.
- Existing impacted test suites pass.

### Docs Deliverables
- Update relevant docs in [docs](docs) for any new assumptions, behavior, or operational steps.
- If a new reusable pattern was introduced, document it.

Acceptance criteria:
- Documentation is sufficient for future game additions and maintenance.

## Targeted Test Strategy (Mandatory)
At minimum:
1. Lifecycle happy path:
- Hand start
- Dealing progression
- Action windows
- Showdown completion
2. Negative path:
- Invalid action timing, invalid discard/count/index/state, or equivalent variant rules
3. Regression path:
- Any modified generic handler/state builder/showdown logic must be covered

Prefer targeted tests first, then broaden only as needed.

## Mandatory Verification Commands
Run from repository root in this order:

1. dotnet build src/CardGames.sln
2. dotnet test src/CardGames.sln
3. dotnet run --project src/CardGames.Poker.Api
4. dotnet run --project src/CardGames.Poker.Web

Targeted test command pattern (required):
- dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter FullyQualifiedName~<NewGameOrFeatureName>

If contracts changed:
- dotnet build src/CardGames.Poker.Refitter

If a command cannot be run, state exactly why and provide the closest equivalent signal.

## Implementation Heuristics Specific to This Repo
- Prefer extending existing endpoint/handler families before creating new ones.
- Reflection/metadata discovery helps registration but does not eliminate explicit API/UI wiring.
- Validate all discovery points: domain metadata, flow handlers, API router mappings, UI family predicates, showdown inclusion, and state projection.
- Keep game-specific orchestration out of generic layers unless absolutely required by behavior.

## Final Change Report (Mandatory Output Format)
At the end, return a final report with these sections.

### 1) Summary
- Mode used: Minimal Diff or Production-Ready
- What was implemented and why

### 2) Per-File Checklist
For every changed file, list:
- File path
- Change type (add/update)
- Purpose
- Acceptance check passed (yes/no)

### 3) Verification Results
- Each required command
- Pass/fail
- Key failures and fixes applied

### 4) Targeted Test Coverage
- New tests added
- Existing tests touched
- Gaps or deferred cases

### 5) Known Pitfalls and Watchouts
Include repo-specific pitfalls, such as:
- Missing API router mapping
- Missing setup/edit/dealer-choice/table canvas inclusion
- Incomplete showdown/state-builder updates for transitional card counts
- Variant-specific assumptions leaking into generic handlers
- Editing generated files directly instead of regeneration workflows

### 6) Follow-Up Recommendations
- Optional hardening tasks
- Any debt intentionally deferred

## Stop Conditions
Do not stop after partial implementation. Continue until:
- All planned layers for selected mode are complete,
- Mandatory verification has been attempted and reported,
- Final change report is delivered in the required format.
