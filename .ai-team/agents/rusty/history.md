# Rusty History

## Project Learnings (from import)
- Project: CardGames (.NET 10 card games platform with API, Blazor Web, and tests).
- Requested by: Rob Gibbens.
- Primary architecture references: docs/ARCHITECTURE.md and README.md.

## Learnings

- Recorded team-level decision in `.ai-team/decisions.md` to prioritize OAuth providers first on auth pages, with local auth preserved as secondary fallback and no behavior changes.
- Designed `docs/CashierFeatureDesign.md` for an account-level Cashier feature: add `Cashier` nav item, new `/cashier` page (balance hero + add chips + ledger), and Profile-scoped MediatR APIs/contracts for summary, add-chips, and paged ledger while keeping existing table/game chip flows unchanged.
- Session outcome captured by Scribe: cashier design decisions were merged from inbox into `.ai-team/decisions.md` and orchestration/session logs were recorded under `.ai-team/orchestration-log` and `.ai-team/log`.
- Consolidated Leagues design direction into `docs/LeaguesFeatureDesign.md` as approval baseline: account-scoped league boundary, event-based mutable membership with projections, invite-link security controls (hash/expiry/revoke/rate-limit), contract regeneration-first API direction, explicit MVP/non-goals, and implementation phases mapped to `CardGames.Poker`, `CardGames.Poker.Api`, `CardGames.Contracts`, `CardGames.Poker.Web`, and `src/Tests`.
- Design-review decision set for Leagues (boundary, temporal membership, RBAC bootstrap/promotion, invite-link lifecycle, season + one-off event support) was merged into canonical `.ai-team/decisions.md` for implementation reference.
- Implemented issue #216 governance P0 flows in `Leagues/v1` with minimal MediatR endpoints for ownership transfer, admin demotion, and member removal; enforced manager/governance-capable invariants in each operation and extended membership history auditing with demotion/ownership-transfer event types plus integration coverage for success + safety-conflict paths.
- Session record updated for #224 quality-gate direction: Leagues release progression now depends on API integration P0 journey coverage and CI enforcement in `squad-ci`, so implementation changes should preserve deterministic moderation and conflict semantics.

## Team Updates

📌 Team update (2026-02-19): Issue #216 Governance roles for Leagues added three MediatR/endpoint flows (ownership transfer, admin demotion, member removal) with governance invariants enforced (leagues retain at least one manager and one governance-capable member) — decided by Rusty
📌 Team update (2026-02-19): Aspire plugin installed to squad skills — enables squad agents to efficiently reference Aspire patterns, troubleshoot orchestration issues, and leverage the full ecosystem including MCP-driven documentation lookups — decided by Livingston
