# Decisions

### 2026-02-17: Initialize squad roster and routing
**By:** Squad (Coordinator)
**What:** Created initial team roster, routing map, and casting state for the CardGames repository.
**Why:** Team workflows require authoritative squad metadata before multi-agent work can run.

### 2026-02-17: OAuth-first auth page prioritization
**By:** Linus (Frontend)
**What:** Prioritized OAuth providers on `Login.razor` and `Register.razor` by placing `ExternalLoginPicker` first and keeping local auth forms as a secondary fallback.
**Why:** Most users are expected to use third-party sign-in; UI should guide that default without changing auth behavior or available fields.

### 2026-02-17: OAuth-first wording convention for auth pages
**By:** Linus (Frontend)
**What:** Standardized auth-page copy to present Google/Microsoft OAuth as the preferred path while keeping local email/password as a clearly labeled fallback, with no layout or logic changes.
**Why:** Reinforces OAuth-first UX intent through wording only, preserving existing auth behavior.

**Applied in:**
- `src/CardGames.Poker.Web/Components/Account/Pages/Login.razor`
- `src/CardGames.Poker.Web/Components/Account/Pages/Register.razor`

### 2026-02-17: Cashier feature architecture and API direction
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Defined Cashier as account-scoped functionality with Profile-based endpoints (`/api/v1/profile/cashier/*`), immediate account wallet add-chips behavior (without game-phase queueing), an MVP ledger using `take/skip` newest-first pagination, and an append-only audited ledger entry model.
**Why:** Preserves clear boundary between account wallet operations and table/game-phase operations while minimizing implementation risk for MVP and ensuring auditability.

**Reference:** `docs/CashierFeatureDesign.md`

### 2026-02-17: Leagues release gating is P0 invariants-first with telemetry-backed rollout
**By:** Basher (Tester)
**Requested by:** Rob Gibbens

**What:** Established a risk-based testing strategy for Leagues that gates release on P0 coverage for (1) creator bootstrap/admin assignment, (2) invite-link lifecycle validation, (3) league-scoped RBAC for admin actions, and (4) temporal membership integrity/idempotency. Defined P1/P2 follow-up coverage for concurrency depth, UI affordances, accessibility, and performance hardening.

**Why:** Leagues introduces high-risk authorization and temporal-state transitions; preventing privilege escalation and data integrity drift is more critical than broad feature breadth for MVP.

**Guardrails:**
- CI must pass all P0 tests before release.
- Progressive rollout behind feature flag with cohort expansion after stable metrics windows.
- Production alerts for unauthorized mutation success, invite-join failure anomalies, and any member with overlapping active league intervals.

**Reference:** `docs/LeaguesTestStrategy.md`

### 2026-02-17: Backend design direction for Leagues MVP
**By:** Danny (Backend Dev)
**Requested by:** Rob Gibbens
**What:** Proposed a Leagues backend design with a dedicated league aggregate boundary (social/membership/admin/invite/schedule only), append-only temporal membership/admin events with current-state projections, revocable+expiring invite-link tokens, and MediatR-based command/query/API surface for create/join/manage/seasons/events.
**Why:** Delivers required Leagues capabilities while preserving existing poker game-rule/domain boundaries and aligning with repository architecture conventions.

**MVP included:**
- league create with creator auto-admin bootstrap
- temporal membership join/leave and admin promotion
- invite-link lifecycle (active/revoked/expired-by-time)
- season containers + one-off events (metadata primitives)

**Deferred post-MVP:**
- standings/leaderboards/scoring aggregation
- richer invite abuse controls (max uses/single-use/allowlist)
- public league discovery and expanded moderation flows

**Reference:** `docs/LeaguesBackendDesign.md`

### 2026-02-17: Leagues MVP UX is list/detail with server-driven role gating
**By:** Linus (Frontend)
**Requested by:** Rob Gibbens
**What:** Defined a minimal Blazor UX for Leagues centered on four routes (`/leagues`, `/leagues/{leagueId}`, `/leagues/join/{token}`, create flow from list) with sectioned league detail (Overview, Members, Invites, Schedule). Admin actions (invite create/revoke, promote member, create season, create one-off event) are visible and enabled strictly from server-provided role and membership state.
**Why:** Keeps Leagues as a social coordination layer, aligns with architecture boundaries, and delivers required create/join/admin/schedule scenarios without adding leaderboard or game-rule complexity in MVP.

**Reference:** `docs/LeaguesUXDesign.md`

### 2026-02-17: League admin RBAC bootstrap and promotion rules
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Set admin policy that league creator is auto-admin, leagues can have multiple admins, and any current admin may promote another active member to admin.
**Why:** Matches product requirements while enabling shared administration without introducing a separate owner-only bottleneck.

**Review context:** Design Review ceremony (Danny, Linus, Basher)

### 2026-02-17: League join uses revocable shareable invite-link tokens
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Standardized on league-scoped invite URLs backed by tokens that support status (active/revoked) and expiration for MVP, with join validation before membership insertion.
**Why:** Delivers required shareable join UX while preserving basic abuse controls and admin governance over access.

**Review context:** Design Review ceremony (Danny, Linus, Basher)

### 2026-02-17: Support both season schedules and one-off league events in MVP model
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Decided MVP will model both season containers (e.g., ten tournaments) and one-off events/nights, while deferring advanced standings/leaderboard scoring logic.
**Why:** Satisfies required league usage modes immediately and controls scope by sequencing event primitives before analytics-heavy ranking features.

**Review context:** Design Review ceremony (Danny, Linus, Basher)

### 2026-02-17: Leagues are social coordination containers, not game-rule containers
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Defined Leagues as account-scoped entities that own membership, admin roles, invite links, and event/season metadata while leaving game rules and hand orchestration in existing poker domain flows.
**Why:** Preserves architecture boundaries from `docs/ARCHITECTURE.md` and avoids coupling league concerns to game-engine mechanics.

**Review context:** Design Review ceremony (Danny, Linus, Basher)

### 2026-02-17: League membership is temporal and event-backed
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Chose an append-only membership history model with join/leave timestamps and role-change events, with current membership derived from active intervals.
**Why:** Supports membership changes over time, auditability, and safer conflict handling for concurrent membership/admin updates.

**Review context:** Design Review ceremony (Danny, Linus, Basher)

### 2026-02-17: Consolidated Leagues design artifact is MVP approval baseline
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Adopted `docs/LeaguesFeatureDesign.md` as the consolidated approval baseline for Leagues MVP, including account-scoped social boundary, event-based membership projection, invite security controls, contract-regeneration workflow, and scoped MVP delivery phases.
**Why:** Aligns backend, UX, and testing directions in a single source of truth while minimizing coupling risk and controlling MVP scope.

**Execution mapping:**
- Phase 1: domain + persistence
- Phase 2: API + contracts + authorization
- Phase 3: Blazor UX
- Phase 4: tests + rollout hardening

**Reference:** `docs/LeaguesFeatureDesign.md`

### 2026-02-18: Leagues governance safety operations landed for P0 (#216)
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Added Leagues v1 governance operations for ownership transfer, admin demotion, and member removal using minimal MediatR + endpoint flows; enforced invariants that leagues always retain at least one manager and at least one governance-capable member (manager/admin); extended membership history values for ownership transfer/admin demotion and reused member-leave events for removals.
**Why:** Provides required governance controls for league administration while preventing lockout or governance loss states in MVP.

### 2026-02-18: Leagues P0 quality gates standardized in integration coverage and CI (#224)
**By:** Basher (Tester)
**Requested by:** Rob Gibbens
**What:** Added API integration coverage for invite-code preview/join-request and moderation approve/deny queue behavior, added event-launch conflict coverage to protect linkage stability, introduced a skipped standings-ingestion scaffold test for future scope visibility, and enforced Leagues P0 checks in `squad-ci` with .NET integration test execution.
**Why:** Centers release confidence on the highest-risk external behaviors (authz, moderation semantics, idempotency/conflicts) and makes required gates explicit in CI.
