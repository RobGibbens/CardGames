---
name: community-dashboard-odds
summary: Fix and validate dashboard odds when games use community cards.
owner: danny
---

## Use when
- Dashboard/side-panel odds are computed on the client from private hand data.
- A game has shared board/community cards (e.g., Hold'em, Good Bad Ugly).
- Symptoms include impossible outcomes (like High Card) still showing after board+hole already form a pair.

## Pattern
1. Locate the odds call site in the UI state update path, not just domain calculators.
2. Ensure odds input includes both:
   - current player's known cards
   - visible community cards from public table state
3. Recalculate odds on both private-state and public-state updates so board reveals immediately update displayed probabilities.
4. Route by game code to variant-appropriate calculator instead of generic draw fallback.

## Minimal validation checks
- Hold'em example: hole `8c Kh`, flop `7d Kc Jc` must not include `HighCard`.
- Community-card variant test should prove pair-or-better eliminates high-card once already present.
- Run targeted tests + project build for changed web/test projects.

## Notes
- Keep generated contracts untouched.
- Prefer focused helper extraction for testability when Razor components own the calculation flow.
