# Leagues Test Strategy

Requested by: Rob Gibbens  
Author: Basher (Tester)  
Date: 2026-02-17

## 1) Scope and Quality Goals

Leagues is an account-scoped social container with mutable membership over time, season scheduling, one-off events, invite-by-link onboarding, and role-based administration.

Quality goals for MVP:
- Preserve existing poker engine boundaries (no league coupling into game rules/orchestration paths).
- Prevent privilege escalation and unauthorized membership/admin mutations.
- Ensure membership timeline integrity under rejoin/leave/promote concurrency.
- Guarantee invite token lifecycle correctness (active, revoked, expired) and safe join behavior.
- Ensure season + one-off event primitives are consistent and auditable without introducing leaderboard/scoring coupling.

Out of scope (MVP):
- Standings/leaderboard/scoring correctness beyond storing event primitives.
- Public discoverability/search ranking of leagues.
- Advanced anti-abuse systems beyond baseline rate limit + revoke/expiry behavior.

## 2) Test Approach by Layer

### 2.1 Domain tests (unit + property-style edge checks)
Focus: invariants, temporal projection, idempotency, and conflict-safe transitions.

Primary targets:
- League creation bootstrap (creator becomes active member + admin in same effective transaction).
- Membership timeline events (Join, Leave, Promote/Demote if present, Rejoin).
- Invite token lifecycle transitions (Active -> Revoked, Active -> Expired by time).
- Season and one-off modeling constraints.

Expected test style: xUnit + FluentAssertions under `src/Tests`.

### 2.2 API contract tests (integration)
Focus: DTO shape stability, validation, status codes, and state changes from commands/queries.

Primary targets:
- `POST /leagues` create behavior.
- `POST /leagues/{leagueId}/invite-links` mint/revoke lifecycle.
- `POST /leagues/join` (or equivalent token join endpoint) auth + token validation.
- `POST /leagues/{leagueId}/admins` promote admin authorization.
- `GET /leagues/{leagueId}` projection of current members/admin badges and event/season summaries.

### 2.3 Authorization and security tests
Focus: resource-scoped RBAC and anti-escalation.

Primary targets:
- Only current admins can mint/revoke invite links.
- Only current admins can promote members.
- Non-members/non-admins cannot mutate league state.
- Removed members lose admin capabilities immediately after leave/removal effective timestamp.

### 2.4 UI flow tests (component + E2E smoke)
Focus: critical user journeys and role-gated affordances.

Primary targets:
- Create league flow, success navigation, and creator admin indicator.
- Join via invite-link flow (happy path + expired/revoked handling).
- League detail member list with role labels.
- Admin-only controls visibility and action outcomes (promote, invite creation/revoke).
- Season/one-off list rendering and basic create/view interactions.

## 3) Acceptance Criteria (MVP)

### 3.1 Domain invariants
1. Creating a league produces exactly one league aggregate and records creator as active admin member.
2. A member cannot have more than one simultaneously active membership interval in the same league.
3. Leave action closes the active interval (`leftAt` set) and keeps history immutable.
4. Rejoin after leave creates a new interval and does not mutate prior intervals.
5. Admin role assignment requires target to be an active member at assignment time.
6. Invite link join is idempotent per user/league active interval: duplicate join requests do not create duplicate active intervals.

### 3.2 API contracts
1. Invalid create inputs return validation failures with stable problem details shape.
2. Join with valid active token and authenticated user returns success and reflects active membership in league detail.
3. Join with revoked/expired/unknown token returns non-success (`403`/`404`/`410` per contract) and no membership mutation.
4. Promote admin by non-admin principal returns forbidden and no role mutation.
5. List/detail endpoints only expose leagues visible to authenticated user by membership/authorization policy.

### 3.3 Authorization
1. Creator is auto-admin immediately after create.
2. Multiple admins are supported; any active admin can promote another active member.
3. Non-admin active members cannot mint/revoke invites or promote admins.
4. Authorization checks are league-scoped (cross-league admin rights do not leak).

### 3.4 Invite lifecycle
1. Admin can mint league-scoped link token with expiration.
2. Revoked token cannot be used after revoke effective time.
3. Expired token cannot be used after expiry timestamp.
4. Valid token join creates (or preserves existing) active membership and emits auditable event(s).

### 3.5 Membership timeline changes
1. Join -> Leave -> Rejoin yields ordered immutable timeline and exactly one active interval at end.
2. Concurrent duplicate joins resolve to one active interval (idempotent/optimistic concurrency).
3. Admin promotion after member leaves is rejected.

### 3.6 Season progression and one-off events
1. League supports creation/listing of season container with scheduled events (e.g., 10 tournaments).
2. League supports one-off event creation independent of season.
3. Event references remain league-scoped; no cross-league association allowed.
4. Deferred scoring is not required for MVP and must not block season/event CRUD readiness.

### 3.7 UI flows
1. Authenticated user can create a league and sees own admin badge on league detail.
2. Invite-link route prompts auth when needed, then completes join and redirects to league detail.
3. Expired/revoked invite shows actionable failure message and no phantom membership appears.
4. Admin controls are visible only to admins; non-admins do not see promote/invite management actions.

## 4) Prioritized Test Matrix

| Priority | Area | Test Cases | Type | Release Gate |
|---|---|---|---|---|
| P0 | League create bootstrap | Creator auto-admin + active member interval created atomically | Domain + API integration | Must pass |
| P0 | Invite join happy path | Valid token + authenticated user joins once; repeat is idempotent | API integration | Must pass |
| P0 | Invite rejection paths | Revoked/expired/invalid token blocked with no state mutation | API integration + security | Must pass |
| P0 | Admin RBAC | Only active admins can promote/mint/revoke; non-admin forbidden | API integration + auth policy | Must pass |
| P0 | Membership temporal integrity | No overlapping active intervals; leave closes interval; rejoin opens new interval | Domain | Must pass |
| P0 | Cross-league isolation | Rights in League A do not grant mutation rights in League B | API integration | Must pass |
| P1 | Concurrency races | Simultaneous join/join and join/leave maintain single active interval consistency | Domain + integration | Pass before broad rollout |
| P1 | UI role-gated controls | Admin controls hidden for non-admin and visible for admin | UI component/E2E | Pass before broad rollout |
| P1 | Season + one-off CRUD shell | Create/list season and one-off events with league scoping | API + UI smoke | Pass before broad rollout |
| P1 | Audit/event ordering | Timeline and role changes are chronologically consistent | Integration | Pass before broad rollout |
| P2 | Invite abuse guardrails | Basic per-user/IP throttling and error UX quality | Security/non-functional | Monitor + iterate |
| P2 | Performance/resilience | Join/promote latency and transient failure handling under load | Non-functional | Monitor + iterate |
| P2 | Accessibility polish | Keyboard and screen-reader affordances on league flows | UI accessibility | Monitor + iterate |

## 5) Regression Focus (Critical Paths)

1. Existing table/game actions remain unaffected by league state changes.
2. Existing auth flows continue to work (OAuth-first UI fallback unaffected).
3. Existing invitation patterns for tables are not regressed by league invite routing/token handling.
4. Existing profile/account endpoints remain unchanged in behavior when league features are absent.

## 6) Rollout Guardrails

### 6.1 Pre-release gates
- All P0 tests green in CI.
- Zero open Sev-1/Sev-2 defects in league create/join/admin paths.
- Contract tests approved for status codes + error payloads on invite and promotion endpoints.
- Basic backward-compatibility smoke passes for non-league game creation and table play.

### 6.2 Progressive rollout
- Feature flag leagues by environment and optionally by user cohort.
- Start with internal/staff cohort, then limited beta cohort, then general availability.
- Require 24h stable metrics window between rollout phases.

### 6.3 Production monitors and thresholds
- Join failure rate by reason (`expired`, `revoked`, `forbidden`, `unknown token`) with alert on abnormal spikes.
- Unauthorized mutation attempts (invite/promote) anomaly detection.
- Membership timeline integrity monitor (detect >1 active interval per member/league; threshold = 0 tolerated).
- Endpoint latency/error SLOs for create/join/promote/revoke.

### 6.4 Rollback triggers
- Any data-integrity breach (overlapping active intervals) detected in production.
- Unauthorized mutation success incident.
- Sustained P0-path error rate above agreed SLO for two consecutive windows.

## 7) Execution Plan for Tests

1. Implement domain invariant tests first (fast feedback, highest logic risk).
2. Add API integration tests for RBAC and invite lifecycle.
3. Add UI smoke tests for create/join/role-gating.
4. Add concurrency/race tests and non-functional checks for staged rollout confidence.

Definition of Done for testing:
- P0 passing and automated in CI.
- P1 passing before broad rollout.
- P2 tracked with owner + telemetry-backed follow-up backlog.
