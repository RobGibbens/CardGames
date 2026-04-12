---
name: poker-odds-regression
description: Regression-test pattern for board-aware poker odds invariants (community cards + known outcomes).
user-invocable: false
---

# poker-odds-regression

## When to use
- A reported odds issue claims community/board cards are ignored.
- You need stable, high-signal tests that avoid fragile exact-percentage assertions.

## Pattern
1. Use a scenario where known cards force an invariant (e.g., hero already paired by flop).
2. Assert **impossible outcomes are absent** (e.g., `HighCard` not present).
3. Assert **aggregate class probability** (e.g., Pair+ totals ~100%) rather than exact breakdown across Pair/TwoPair/Trips.
4. Keep simulations moderate (`~1000`) and use `BeApproximately` tolerance.

## CardGames example
- Hold'em hole `8c Kh`, flop `7d Kc Jc`.
- Expected:
  - `HighCard` probability key absent.
  - Sum of non-HighCard probabilities ≈ `1.0`.
  - `OnePair` present and > `0`.

## Why it works
- Protects against regressions where board cards are omitted.
- Remains resilient to Monte Carlo variance.
- Fits existing xUnit + FluentAssertions style in `src/Tests`.
