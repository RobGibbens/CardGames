# Decision: Generic StartHand auto-deals for skip-ante variants

**Date:** 2026-03-06
**Owner:** Danny (Backend)
**Requested by:** Rob Gibbens

## Context
The web UI can start hands via `IGamesApi.GenericStartHandAsync`, which routes to the generic StartHand handler. That handler previously only advanced the hand number/phase and returned, meaning blind-based games like Hold’em and Omaha never posted blinds or dealt hole cards when started via the generic endpoint.

## Decision
In the generic StartHand handler, after persisting the initial hand state, invoke `flowHandler.DealCardsAsync(...)` only when `flowHandler.SkipsAnteCollection` is true.

## Rationale
- Matches existing Hold’em start behavior: save initial state, then deal.
- Keeps ante-based variants unchanged: they intentionally remain in `CollectingAntes` awaiting ante collection.
- Uses the flow-handler abstraction as the source of truth for whether a variant should auto-deal on start.

## Impact
- Hold’em/Omaha started via the generic endpoint now post blinds and deal hole cards.
- The `StartHandSuccessful.CurrentPhase` returned from the generic handler reflects the post-deal phase (since `DealCardsAsync` advances phase and persists).
