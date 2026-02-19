# Danny History

## Project Learnings (from import)
- Project stack: C#/.NET (`net10.0`) with API services and domain modules.
- Requested by: Rob Gibbens.
- Key backend areas: src/CardGames.Poker.Api, src/CardGames.Poker, src/CardGames.Core.

## Learnings
- 2026-02-17 (Leagues design review): Keep Leagues as account-scoped social orchestration (membership/admin/invites/schedule) and keep poker rules/hand flow outside this boundary.
- 2026-02-17 (Leagues design review): Use append-only membership/admin events with current-state projections to support temporal membership, auditability, and safer concurrency handling.
- 2026-02-17 (Leagues design review): MVP invite links should be revocable + expiring token URLs validated server-side, with join idempotency and hashed token storage.
- 2026-02-17 (Leagues design review): MVP supports both season containers and one-off events as scheduling primitives; standings/leaderboards are post-MVP.
- Scribe merged Leagues design-review and backend-design decisions into canonical `.ai-team/decisions.md`; treat that file as the team-approved MVP contract baseline.
- 2026-02-18 (#216/#224 sync): backend Leagues v1 governance endpoints (transfer ownership, demote admin, remove member) and new membership-history values are now canonicalized in `.ai-team/decisions.md`; API changes should preserve manager/governance-capable invariants and existing moderation/idempotency response semantics expected by integration gates.
- 2026-02-19 (league invite permissions): keep persisted creator membership as `Owner` for governance/audit semantics, but project current-user `Owner` as `Manager` in create/detail/my-leagues responses so management-capable clients remain aligned with invite authorization checks and newly created leagues can create invites immediately from detail flows.
- 2026-02-19 (league invite permissions follow-up): client gating for invite/management actions should treat `Owner` as management-capable alongside `Manager`/`Admin` to stay resilient if any response path returns unprojected role values.
- 2026-02-19 (team update): #216 authority model is now finalized—active admins and managers can execute admin promote/demote operations; ownership transfer remains manager-only with governance safety invariants unchanged.
