# Decision Inbox: Issue #216 Governance roles

## Context
Requested by Rob Gibbens for [Leagues P0] governance safety + member administration.

## Decisions made
- Added three minimal MediatR/endpoint flows under `Leagues/v1`:
  - Transfer ownership: manager-only transfer to another active member (`transfer-ownership`).
  - Demote admin: governance-capable actor can demote active admin to member (`demote-admin`).
  - Remove member: governance-capable actor can remove active member (`remove`).
- Enforced governance invariants in these operations:
  - League must retain at least one manager.
  - League must retain at least one governance-capable member (manager/admin).
- Reused existing membership event model and extended minimally for history/auditing:
  - Added event history values for admin demotion and ownership transfer.
  - Reused `MemberLeft` for member removal (actor differs from target user).
- Kept changes scoped to API Leagues v1 feature, Contracts, and integration tests (no unrelated refactors).

## Notes
- New event values are written from the feature layer and surfaced through membership history contract enum to preserve minimal scope for this pass.
