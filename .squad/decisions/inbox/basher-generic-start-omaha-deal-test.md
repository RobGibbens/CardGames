# Decision: Generic StartHand for Omaha ends in PreFlop after auto-deal

**Date:** 2026-03-06
**Owner:** Basher (Tester)
**Requested by:** Rob Gibbens

## Context
The generic StartHand handler now auto-calls `flowHandler.DealCardsAsync(...)` for `SkipsAnteCollection` variants (e.g., Hold’em/Omaha). This means the StartHand API response and persisted game state reflect the *post-deal* phase (not the initial setup phase).

## Decision
For `OMAHA` games with `SmallBlind`/`BigBlind` configured, an integration regression test asserts that after calling the generic StartHand command:
- Blinds are collected (main pot amount becomes > 0)
- 4 hole cards are dealt per eligible player
- The game phase is `PreFlop`

## Rationale
- `OmahaFlowHandler.DealCardsAsync` posts blinds and then delegates to the base dealing routine, which advances phase from `Dealing` to the next community-card phase (`PreFlop`).
- Asserting `PreFlop` provides a clear “setup completed” sentinel while still staying close to the game rules.
