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
