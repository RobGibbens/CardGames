# Decision: Pot-Size Betting — Use Actual TotalPot

**Date:** 2026-03-05  
**Author:** Arwen (Frontend Dev)  
**Item:** 4.17 — Pot-Size Betting Support

## Context
The "½ Pot" and "Pot" quick-bet buttons in `ActionPanel.razor` were using a rough approximation (`CurrentBetToCall * 2`) instead of the real pot total.

## Decision
- Added a `[Parameter] public int TotalPot` to `ActionPanel.razor`.
- `GetPotBet` now uses `TotalPot` when it's positive, falling back to the old approximation (`CurrentBetToCall * 2`) when zero (e.g., pre-flop or before state arrives).
- `TablePlay.razor` passes the existing `_pot` field (sourced from `state.TotalPot` via SignalR) into the new parameter.

## Rationale
Graceful fallback keeps the buttons functional even if `TotalPot` hasn't been populated yet, while the primary path now shows accurate pot-based bet suggestions.
