# Quality Risks — Hold 'Em Integration Tests (Phase 3)

**Author:** Legolas (Tester)  
**Date:** 2026-03-05  
**Scope:** Item 4.16 — Hold 'Em API Integration Tests

## Tests Created

`src/Tests/CardGames.IntegrationTests/Games/HoldEm/HoldEmHandLifecycleTests.cs` — 10 tests across 4 categories:

1. **Flow Handler Properties** (4 tests): SkipsAnteCollection, GetInitialPhase, GetNextPhase full chain, DealingConfiguration
2. **Blind Collection** (3 tests): 2 hole cards per player, correct SB/BB deduction (multi-way and heads-up), all cards face-down
3. **Phase Progression & Community Cards** (1 test): PreFlop → Flop transition with 3 community cards dealt
4. **Fold-to-Win** (1 test): Bet + all-fold triggers showdown with correct fold state
5. **Heads-up Blinds** (1 test): Dealer posts SB in 2-player game

## Quality Risks Identified

### Risk 1: CurrentBet reset after blind posting
`BaseGameFlowHandler.DealDrawStyleCardsAsync` resets `CurrentBet = 0` for all players **after** `HoldEmFlowHandler.CollectBlindsAsync` sets them. This means the PreFlop betting round starts with `CurrentBet = 0` and `bettingRound.CurrentBet = 0`, even though the big blind has been posted. Players can _check_ through PreFlop instead of having to call the BB. This is a functional bug — PreFlop should start with the betting round's `CurrentBet` set to the big blind amount.

### Risk 2: PostBlindAsync silently no-ops when pot doesn't exist
`HoldEmFlowHandler.PostBlindAsync` updates an existing pot but does not create one. If no pot is pre-created, blind chip deductions occur but the pot amount stays at zero. The test helper works around this by calling `CreatePotAsync` before dealing. Real game start flow should be audited to ensure pot creation happens before blind collection.

### Risk 3: No Turn/River community card integration tests
The tests verify Flop community card dealing but don't exercise the full PreFlop → Flop → Turn → River community card sequence. This is because each transition requires a complete betting round, making multi-phase tests complex. Dedicated Turn/River dealing tests would catch regressions in `DealCommunityCardsForPhaseAsync` card count and `DealtAtPhase` tagging.

### Risk 4: Fold validation prevents folding when can check
The `ProcessBettingActionCommandHandler` rejects folds when `bettingRound.CurrentBet == player.CurrentBet`. While technically correct (checking is preferable), this means fold-to-win scenarios in tests require a preceding bet, making tests more coupled to betting validation logic. Consider if this constraint should be relaxed for integration test clarity.
