---
name: poker-wallet-join-guard
description: "Wallet-gated join pattern for table buy-ins with integration assertions for balance, ledger, and seating invariants."
user-invocable: false
---

# poker-wallet-join-guard

Use this pattern when implementing or auditing table join flows that move chips from an account wallet into an in-game stack.

## Pattern
1. Validate request-level buy-in bounds first (`StartingChips > 0`).
2. Read current account balance and short-circuit zero-balance joins with a distinct error.
3. Execute an atomic wallet debit guard (`TryDebitForBuyInAsync`) to enforce `requested <= balance` at write time.
4. Seat player only after successful debit.
5. Persist a buy-in ledger entry tied to game reference for auditability.

## Recommended error semantics
- `InvalidStartingChips`: requested buy-in is non-positive.
- `ZeroAccountBalance`: account has no chips available.
- `InsufficientAccountChips`: account exists but requested buy-in exceeds available balance.

## Integration test assertions
For each path, assert all three surfaces:
- **Wallet account balance** (debited or unchanged as expected)
- **Ledger entries** (buy-in record created or absent)
- **Game seating state** (player seated only on success)

## CardGames reference
Validated in `JoinGameCommandHandler` and `JoinLeaveWalletCommandTests` (2026-03-06) as canonical implementation for wallet-backed joins.

## Frontend companion pattern (Blazor)
When applying wallet guard in UI join flows:
1. On empty-seat click, fetch cashier summary before attempting join.
2. If balance `<= 0`, show existing no-chips modal and do not call join.
3. Open a modal that shows account balance and binds slider + numeric input to one buy-in value.
4. Reuse existing buy-in min/step helpers and cap UI max by fetched balance.
5. Submit join only from modal confirmation with seat index + selected buy-in.

This keeps seat join intent explicit, avoids accidental full-wallet table exposure, and stays consistent with backend `InsufficientAccountChips` guard semantics.
