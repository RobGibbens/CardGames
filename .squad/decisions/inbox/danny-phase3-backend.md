# Phase 3 Backend Decisions — Danny (Gimli)

## 4.13 — Blind Collection Consolidation
Extracted `CollectBlindsAsync` and `PostBlindAsync` into `BaseGameFlowHandler` as protected methods. Both `HoldEmFlowHandler` and `OmahaFlowHandler` now inherit the shared implementation. Removed unused `Microsoft.EntityFrameworkCore` using from both subclasses since they no longer call `FirstOrDefaultAsync` directly.

## 4.14 — ContinuousPlay Abandoned Game Phases
Added all missing phase strings to `ProcessAbandonedGamesAsync.inProgressPhases`: Hold'Em/Omaha (`CollectingBlinds`, `PreFlop`, `Flop`, `Turn`, `River`), Seven Card Stud streets (`ThirdStreet` through `SeventhStreet`), and miscellaneous (`DrawComplete`, `DropOrStay`, `PotMatching`). Used string literals since these phases don't exist in the `Phases` enum.

## 4.15 — HoldEm AutoAction Override
Added `SendBettingActionAsync` override in `HoldEmFlowHandler` that dispatches `Features.Games.HoldEm.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand` instead of the default FiveCardDraw command. Used fully-qualified type name to avoid adding an extra using alias.

## 4.16 — TableStateBuilder Betting Phases
Added `PreFlop`, `Flop`, `Turn`, `River` to the `bettingPhases` array in `BuildAvailableActionsAsync` so Hold'Em games show available actions during betting rounds.
