# Decision: Join buy-in backend is already enforced; keep behavior stable

**By:** Gimli (Backend Dev)  
**Requested by:** Rob Gibbens  
**Date:** 2026-03-06

## What
Validated the join flow and retained existing domain/API behavior for buy-in enforcement:
- Join already rejects `StartingChips <= 0` (`InvalidStartingChips`).
- Join already blocks zero-wallet players (`ZeroAccountBalance`).
- Join already blocks requested buy-ins above available account balance via wallet debit guard (`InsufficientAccountChips`).
- Existing integration tests (`JoinLeaveWalletCommandTests`) already cover sufficient/insufficient/zero-balance wallet outcomes and ledger integrity.

## Why
The requested UX (modal + slider/input for selected buy-in) needs authoritative backend enforcement; that enforcement is already present and test-backed. Changing runtime semantics now would create unnecessary risk without adding protection.

## Scope of change
Kept runtime behavior unchanged. Applied minimal API contract clarity updates only:
- removed stale TODO comments in join request/command contracts,
- corrected `JoinGameRequest` XML default documentation to match actual default (`100`),
- expanded endpoint validation description to include wallet constraints,
- added explicit 403 response metadata for league-gated tables.

## Follow-up
Frontend can rely on existing join error codes for UX mapping:
- `ZeroAccountBalance` → disable/join-block with cashier prompt.
- `InsufficientAccountChips` → prompt user to lower buy-in or add chips.
