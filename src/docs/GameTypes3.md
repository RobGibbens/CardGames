# Game Type / Phase Decision Points (Current Code)

This document lists the current locations where behavior branches on **game type codes** or **phase names**. These are the places that will require code changes when introducing a new game type with new rules (beyond adding a new `IPokerGame` implementation and rules).

## Executive Summary

The codebase has improved extensibility via reflection-based registries and flow handlers, but several **hardcoded game type checks** and **phase name lists** remain. The largest concentration of branching is in the API table state builder and the Blazor UI page (`TablePlay.razor`).

## 1) API Services

### 1.1 `CardGames.Poker.Api/Services/TableStateBuilder.cs`
**Game type checks**
- `BuildPublicStateAsync`: Seven Card Stud-specific logging and ordering (`PokerGameMetadataRegistry.SevenCardStudCode`, lines ~65-105).
- `BuildPrivateStateAsync`: game-type specific evaluation for Seven Card Stud, Twos/Jacks/Axe, and Kings and Lows (lines ~295-358).
- `BuildShowdownPublicDtoAsync`: per-game hand creation and results for Twos/Jacks/Axe, Seven Card Stud, Kings and Lows, plus player-vs-deck logic (lines ~703-1038).
- `CalculateTotalPotAsync`: Kings and Lows pot calculation/phase behavior (lines ~1053-1086).
- `BuildWildCardRulesDto`: hardcoded wild card parsing for Twos/Jacks/Axe and Kings and Lows (lines ~1366-1376).
- `BuildPlayerVsDeckStateAsync`: Kings and Lows deck-hand evaluation (lines ~1485-1514).
- Card ordering depends on `isSevenCardStud` (lines ~1876-1888).

**Phase checks / lists**
- `BuildSeatPublicDto`: `Showdown`, `Complete`, `PotMatching` define card visibility (lines ~476-507).
- `BuildAvailableActionsAsync`: hardcoded betting phases list (`FirstBettingRound`, `SecondBettingRound`, `ThirdStreet`…`SeventhStreet`) (lines ~613-625).
- `BuildDrawPrivateDto`: hardcoded `DrawPhase` (lines ~672-688).
- `BuildShowdownPublicDtoAsync`: only runs during `Showdown`, `Complete`, `PotMatching` (lines ~703-706).
- `BuildPlayerVsDeckStateAsync`: only runs for `PlayerVsDeck` (lines ~1396-1400).
- `BuildDropOrStayPrivateDto`: only runs for `DropOrStay` (lines ~1550-1552).
- `BuildAllInRunoutStateAsync`: only runs for `Showdown` (lines ~1651-1654).
- `GetStreetPhaseOrder`: hardcoded mapping for `ThirdStreet`…`SeventhStreet` (lines ~1823-1830).
- Street descriptions are hardcoded for `FourthStreet`…`SeventhStreet` (lines ~1747-1753).

**Impact:** New game types or phases require updates here for UI visibility, hand evaluation, showdown rendering, pot calculation, and special-rule DTOs.

### 1.2 `CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs`
**Phase checks / lists**
- `inProgressPhases` list defines which phases are treated as “in-progress” (lines ~118-128).
- Query for next-hand start only checks `Complete` or `WaitingForPlayers` (lines ~85-90).
- `CollectAntesAsync` directly sets `Dealing` phase (lines ~632, ~669).

**Impact:** New phases or phase flow for new games require updates to the phase lists and transition logic.

### 1.3 `CardGames.Poker.Api/Games/PokerGamePhaseRegistry.cs`
- Only parses the unified `Phases` enum for all games (lines ~25-26).

**Impact:** Any new phase requires updates to `Phases.cs` (and potentially downstream display logic).

### 1.4 `CardGames.Poker.Api/Features/Games/ActiveGames/v1/Queries/GetActiveGames/PhaseDescriptionResolver.cs`
- Resolves descriptions only from the `Phases` enum (line ~28).

**Impact:** New phases require enum updates to provide descriptions.

## 2) Domain Layer

### 2.1 `CardGames.Poker/Betting/Phases.cs`
- Single enum containing **all** phases, including Kings and Lows, Seven Card Stud, etc.

**Impact:** New game phases require enum updates, and all phase-based logic assumes a single shared enum.

### 2.2 `CardGames.Poker/History/FoldStreet.cs`
- Enum hardcodes draw and stud streets (lines ~29-66).

**Impact:** New betting/street structures require updates to fold tracking.

## 3) API Game Flow

### 3.1 `CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs`
- Default fallback handler is `FiveCardDrawFlowHandler` (lines ~51-53), used when no handler exists for a game type (lines ~93-112).

**Impact:** New game types must provide a handler or they will silently fall back to Five Card Draw behavior.

### 3.2 Game-specific feature folders (API endpoints)
- `CardGames.Poker.Api/Features/Games/FiveCardDraw/*`
- `CardGames.Poker.Api/Features/Games/SevenCardStud/*`
- `CardGames.Poker.Api/Features/Games/KingsAndLows/*`
- `CardGames.Poker.Api/Features/Games/TwosJacksManWithTheAxe/*`

**Impact:** New game types still need new feature folders/endpoints unless fully generic handlers are introduced.

## 4) Blazor UI

### 4.1 `CardGames.Poker.Web/Components/Pages/TablePlay.razor`
**Game type checks**
- `IsTwosJacksManWithTheAxe`, `IsKingsAndLows`, `IsSevenCardStud` (lines ~526-528).
- Game-type specific overlays (Drop/Stay, Player vs Deck, Pot Matching) for Kings and Lows (lines ~227-279).

**Phase checks / lists**
- Drop/Stay, DrawComplete, PlayerVsDeck, PotMatching string checks (lines ~556-570).
- Showdown/Complete/Ended phase grouping (lines ~608-618).
- Waiting/Ended phase comparisons via `GamePhase` enum (lines ~89-101, ~337-365).

**Impact:** New phases or special UI flows require UI updates here.

### 4.2 `CardGames.Poker.Web/Services/GameApi/GameApiRouter.cs`
- Default fallback client is Five Card Draw (lines ~24-39).

**Impact:** New game types require new `IGameApiClient` implementations and DI registration, or they fall back to Five Card Draw behavior.

### 4.3 `CardGames.Poker.Web/Services/GameApi/*ApiClientWrapper.cs`
- Hardcoded game type codes and endpoints for:
  - `FiveCardDrawApiClientWrapper` (`FIVECARDDRAW`)
  - `KingsAndLowsApiClientWrapper` (`KINGSANDLOWS`)
  - `SevenCardStudApiClientWrapper` (`SEVENCARDSTUD`)
  - `TwosJacksManWithTheAxeApiClientWrapper` (`TWOSJACKSMANWITHTHEAXE`)

**Impact:** New game types require a new wrapper and routing.

## 5) Other Notes / Indirect Dependencies

- `PokerGameMetadataRegistry` and `PokerGameRulesRegistry` are now reflection-based, but **constants** (e.g., `SevenCardStudCode`, `KingsAndLowsCode`) are still referenced elsewhere. New game types may need new constants if they are used in comparisons.
- `TableStateBuilder.BuildSpecialRulesDto` expects specific `SpecialRules` keys (`DropOrStay`, `LosersMatchPot`, `WildCards`, `SevensSplit`). New special rules require updates to this mapping.

## Summary of Key Change Hotspots

1. `CardGames.Poker.Api/Services/TableStateBuilder.cs` (largest surface area)
2. `CardGames.Poker.Web/Components/Pages/TablePlay.razor`
3. `CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs`
4. `CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs`
5. `CardGames.Poker.Web/Services/GameApi/*` routing/wrappers
6. `CardGames.Poker/Betting/Phases.cs` and `CardGames.Poker/History/FoldStreet.cs`

These are the primary places where adding a new game type or phase will require code changes beyond introducing a new game class and rules.