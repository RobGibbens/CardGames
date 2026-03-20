# Master Prompt: Add a New Game to CardGames

## Mission
Team, add a new poker game variant to this repository in a way that is consistent with existing architecture, conventions, and quality gates.

Inputs you must use:
- Primary playbook: [docs/AddingNewGames20.md](docs/AddingNewGames20.md)

The game name is "Screw Your Neighbor". The Game type code is "SCREWYOURNEIGHBOR". The image name is "screwyourneighbor.png". The variant family is "custom". I want it production ready.

## Game play and Rules

"Screw Your Neighbor" is NOT a poker based game, so there will need to be a lot of changes to support this game. The rules of "Screw Your Neighbor" are: 

- Each player puts three stacks of chips (the bring-in, or buy-in) in front of them. The amount in each stack is configurable when the game is created. These do NOT go into the center pot yet.
- Each player is dealt a single card face down.
- The player to the left of the dealer is first to act
- The player to act is presented with an overlay to choose if they want to keep their current card or trade it with the player to their left.
- If the player chooses to keep their current card then the player to their left is the next to act and they get the same choice of keep their card or trade it with the player to their left.
- If a player is dealt a King, they can not be traded with or trade their card. A King is a blocker.
- If a player chooses to trade their card, then the two players swap cards and the next player is to act.
- When play rotates all the way to the dealer, the dealer has the choice of keeping their card or swapping it with the top card in the deck.
- Once all player have made their choice, the cards are turned face up and revealed. After a short delay so that all players can see everyone's cards, the player or players that have the lowest card have to put one stack of their chips into the center pot.
- Aces are always low
- The deal then rotates one player to the left and a new round is dealt.
- Play continues until only one player has any stacks of chips left. That player then wins all of the chips in the center pot.
- When a player runs out of stacks of chips, they no longer get dealt cards in subsequent hands.
- If there is a tie that would knock out all remaining players, none of those players adds their chips to the pot and a new hand is dealt.

## Example game

- There are three players in the game; Rob (dealer), Eric, and Goose
- The bring-in is 75 chips, in three stacks of 25
- Round 1
  - Each player is dealt a single card. Eric gets an Ace, Goose gets a Jack, and Rob gets a 6.
  - Eric is first to act. He is presented with the keep or trade overlay, and he chooses to trade (because an Ace is the lowest card). He trades with Goose. Now Eric has the Jack and Goose has the Ace.  Action moves to Goose.
  - Goose is presented with the keep or trade overlay, and he choose to trade with Rob. Goose now has the 6 and Rob has the Ace. Action moves to Rob.
  - Rob (the dealer) is presented with the keep or trade overlay. He chooses to trade with the deck. The ace is discarded and a new card is drawn from the top of the deck for Rob.  This is an 8.
  - Rob has an 8, Goose has a 6, and Eric has a Jack. The 6 is the lowest, so Goose loses and Goose adds one stack (25 chips) to the pot.  Rob still has three stacks, Eric still has three stacks, and Goose has two stacks left.
- Round 2
  - The deal moves to Eric.
  - Each player is dealt a single card. Goose gets a 3, Rob gets a King, and Eric gets a 5.
  - Goose is the first to act. Because Rob has a King which is a blocker, Goose is NOT presented with the keep or trade overlay. There's nothing that Goose can do. So, action immediately moves to Rob.
  - Rob has a King and can not trade, so Rob is NOT presented with the keep or trade overlay. So, action immediately moves to Eric.
  - Eric is presented with the keep or trade overlay. He chooses to trade with the deck. The 5 is discarded and a new card is drawn from the top of the deck for Eric. This is a 3.
  - Goose has a 3, Rob has a King, and Eric has a 3. 3 is the lowest, so Eric and Goose both lose and both of them add a stack (25 chips) to the center pot.
  - Rob still has three stacks of chips, Eric has two stacks, and Goose has one stack.
- Once a player has no chips left, they no longer get dealt cards in subsequent hands.
- Game play continues until one player with stacks is left.

Ask me for these values before implementation if not provided:
- Game name
- Game type code (all caps convention)
- image name
- Variant family (HoldEm-style, Draw-style, Stud-style, or custom)
- Minimal Diff or Production-Ready mode
- Any game-specific rules and UX constraints

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
