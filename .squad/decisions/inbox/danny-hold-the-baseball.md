# Hold the Baseball — Implementation Decision

**Date:** 2026-03-07
**Author:** Danny (Backend Dev)
**Status:** Implemented — pending review

## Summary

Implemented "Hold the Baseball" — a Texas Hold 'Em variant where all 3s and 9s (in both hole and community cards) are wild. Wild cards substitute as any rank/suit during hand evaluation, enabling Five of a Kind.

## Key Decisions

1. **HandTypeStrengthRanking.Classic reused** — no new ranking enum needed. `HandTypeStrength` already handles FiveOfAKind (value 9) for both Classic and ShortDeck rankings. The `WildCardHandEvaluator` naturally produces FiveOfAKind when wilds allow it.

2. **Wild card rules via BaseballWildCardRules** — reuses the existing Baseball wild card detection (3s and 9s) from `src/CardGames.Poker/Hands/WildCards/BaseballWildCardRules.cs`. No new wild card rule class needed.

3. **HoldTheBaseballHand extends CommunityCardsHand** — the hand class overrides `CalculateStrength` and `DetermineType` to route through `WildCardHandEvaluator.EvaluateBestHand()` when wild cards are present, falling back to standard community card evaluation otherwise.

4. **Game type code: HOLDTHEBASEBALL** — registered in `PokerGameMetadataRegistry` and used consistently across metadata attribute, flow handler, evaluator, and all API endpoints.

5. **Community card showdown support** — added HOLDTHEBASEBALL to `UsesSharedCommunityCards()` in the Generic showdown handler so the authoritative showdown path loads community cards.

## Files Created (22)

### Domain (6)
- `src/CardGames.Poker/Games/HoldTheBaseball/HoldTheBaseballGame.cs`
- `src/CardGames.Poker/Games/HoldTheBaseball/HoldTheBaseballRules.cs`
- `src/CardGames.Poker/Games/HoldTheBaseball/HoldTheBaseballGamePlayer.cs`
- `src/CardGames.Poker/Games/HoldTheBaseball/HoldTheBaseballShowdownResult.cs`
- `src/CardGames.Poker/Hands/CommunityCardHands/HoldTheBaseballHand.cs`
- `src/CardGames.Poker/Evaluation/Evaluators/HoldTheBaseballHandEvaluator.cs`

### API (16)
- `src/CardGames.Poker.Api/GameFlow/HoldTheBaseballFlowHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/HoldTheBaseball/HoldTheBaseballApiMapGroup.cs`
- `src/CardGames.Poker.Api/Features/Games/HoldTheBaseball/v1/Feature.cs`
- `src/CardGames.Poker.Api/Features/Games/HoldTheBaseball/v1/V1.cs`
- `src/CardGames.Poker.Api/Features/Games/HoldTheBaseball/v1/Commands/StartHand/*` (4 files)
- `src/CardGames.Poker.Api/Features/Games/HoldTheBaseball/v1/Commands/ProcessBettingAction/*` (4 files)
- `src/CardGames.Poker.Api/Features/Games/HoldTheBaseball/v1/Commands/PerformShowdown/*` (4 files)

## Files Modified (2)
- `src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs` — added `HoldTheBaseballCode` constant
- `src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` — added HOLDTHEBASEBALL to `UsesSharedCommunityCards()`

## Build Status
Clean build — 0 new errors, 1 harmless warning (unread `logger` parameter).
