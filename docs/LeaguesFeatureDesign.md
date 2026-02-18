# Leagues Feature Design

Requested by: Rob Gibbens  
Owner: Rusty (Lead)  
Date: 2026-02-17

## 1. Problem framing and goals

CardGames currently supports table-centric gameplay but lacks a durable social container for recurring groups. Users need a way to organize members over time, run recurring season schedules, and host one-off nights without coupling those concerns to poker game-rule orchestration.

### Goals (MVP)
- Create private leagues as account-scoped social containers.
- Support mutable membership over time with an auditable history.
- Support role-based admin management (multi-admin, creator bootstrap).
- Support invite-by-link join with revoke + expiry security controls.
- Provide minimal season + one-off event metadata management.
- Keep game rules and table orchestration fully decoupled from leagues.

### Non-goals (MVP)
- No standings, leaderboard, or scoring aggregation logic.
- No public/discoverable league browsing.
- No advanced moderation workflows.
- No admin demotion/ownership transfer UX in MVP.
- No league-specific game rules or dealing/betting/showdown customization.

## 2. Scope and architecture boundaries

Leagues are account-scoped and manage social coordination only:
- Membership lifecycle and roles.
- Invite link lifecycle.
- Season and event planning metadata.

Leagues do **not** own game engines, hand state, or table round mechanics. Gameplay continues under existing Poker/Core boundaries (`CardGames.Poker`, `CardGames.Core`, `CardGames.Core.French`) and existing table flows.

## 3. Domain model and lifecycle model

## 3.1 Core aggregates and entities

### League (aggregate root)
- `LeagueId`
- `Name`, `Description?`, `Status` (`Active`, `Archived`)
- `CreatedByUserId`, `CreatedAtUtc`
- `Version` (optimistic concurrency)

Responsibilities:
- Create league and bootstrap creator membership/admin role.
- Enforce role-protected operations.
- Own invite lifecycle operations.
- Own season and event metadata creation.

### Membership timeline (event-based)
Membership is modeled as append-only events:
- `LeagueMemberJoined`
- `LeagueMemberLeft`
- `LeagueMemberPromotedToAdmin`
- (post-MVP) `LeagueMemberDemotedFromAdmin`

Event fields:
- `LeagueId`, `UserId`, `OccurredAtUtc`, `ActorUserId`
- `CorrelationId`, optional `IdempotencyKey`

Current membership derivation:
- Active member = latest interval has join and no leave.
- Rejoin after leave creates a new interval.
- Exactly one active interval per `LeagueId + UserId`.

### Invite link entity
- `InviteId`, `LeagueId`
- `TokenHash` (never store raw token)
- `Status` (`Active`, `Revoked`)
- `CreatedByUserId`, `CreatedAtUtc`
- `ExpiresAtUtc`
- `RevokedAtUtc?`, `RevokedByUserId?`

### Season and event entities
- `LeagueSeason`: named container for recurring schedule.
- `LeagueEvent`: either `SeasonEvent` (linked to season) or `OneOff`.

## 3.2 Lifecycle model

### League lifecycle
- `Active` on create.
- `Archived` reserved for post-MVP lifecycle hardening.

### Membership lifecycle
- Join -> Active interval opens.
- Leave -> Active interval closes (`LeftAtUtc` set).
- Rejoin -> New interval opens.
- Promote to admin requires active membership.

### Invite lifecycle
- Create -> `Active`.
- Revoke -> `Revoked` (immediate invalidation).
- Expire -> invalid at validation time when `ExpiresAtUtc <= now`.

### Season/event lifecycle (MVP)
- `Planned` metadata-first lifecycle.
- Minimal create/list, status progression optional and lightweight.

## 4. Admin model and authorization rules

Roles:
- `Member`
- `Admin`

Bootstrap:
- League creator is auto-joined and auto-admin on create.

Authorization matrix (MVP):
- Authenticated user:
  - Create league
  - List own leagues
- Active member:
  - View league detail, members, schedule
  - Leave league
- Admin:
  - Create/revoke invite links
  - Promote active member to admin
  - Create seasons and one-off events

Safety rules:
- Promotion target must be active member in same league.
- Authorization is resource-scoped by `LeagueId` (no cross-league privilege bleed).
- No demotion command in MVP (avoids last-admin lockout complexity).

## 5. Invite-by-link flow and security controls

## 5.1 Flow
1. Admin creates invite with expiry.
2. API returns one-time raw token and shareable URL.
3. Recipient opens `/leagues/join/{token}`.
4. If unauthenticated: redirect to login with return URL.
5. Authenticated user confirms join.
6. API validates token + status + expiry and writes membership event.

## 5.2 Validation pipeline
- Lookup by token hash.
- Confirm invite exists and matches league.
- Require `Status == Active`.
- Require `ExpiresAtUtc > now`.
- Require authenticated caller.
- If already active member, return idempotent success/no-op.

## 5.3 Security controls (MVP)
- Persist hashed token only (`TokenHash`).
- Raw token returned once at create-time only.
- Revoke support with immediate effect.
- Expiry required for all invites.
- Rate-limit join endpoint to reduce abuse.
- Structured audit logging for invite create/revoke/join attempts.

## 6. API surface and contracts direction

API conventions:
- `CardGames.Poker.Api`: MediatR requests + validators + authorization policies.
- `CardGames.Contracts`: contract-first DTO interfaces and generated Refit client artifacts.

**Contracts direction:**
- Do not manually edit generated files such as `CardGames.Contracts/RefitInterface.v1.cs`.
- Add/adjust contract source definitions following existing patterns, then regenerate via `CardGames.Poker.Refitter`.

Proposed `/api/v1` surface (MVP):
- `POST /leagues`
- `GET /leagues/mine`
- `GET /leagues/{leagueId}`
- `GET /leagues/{leagueId}/members`
- `GET /leagues/{leagueId}/members/history?take=&skip=`
- `POST /leagues/{leagueId}/invites`
- `GET /leagues/{leagueId}/invites`
- `POST /leagues/{leagueId}/invites/{inviteId}/revoke`
- `POST /leagues/join-by-invite`
- `POST /leagues/{leagueId}/leave`
- `POST /leagues/{leagueId}/members/{memberUserId}/promote-admin`
- `POST /leagues/{leagueId}/seasons`
- `GET /leagues/{leagueId}/seasons`
- `POST /leagues/{leagueId}/events/one-off`
- `POST /leagues/{leagueId}/seasons/{seasonId}/events`
- `GET /leagues/{leagueId}/events?seasonId=`

Error semantics direction:
- Validation errors via standard problem-details shape.
- Invalid invite states return explicit non-success with no mutation.
- Idempotent duplicate joins return success/no-op convention.

## 7. Blazor UX flows (MVP)

## 7.1 Pages/routes
- `/leagues`: list + create CTA
- `/leagues/{leagueId}`: detail sections (Overview, Members, Invites, Schedule)
- `/leagues/join/{token}`: invite join flow

## 7.2 Core user flows
- Create league: name (+ optional description), navigate to detail, show admin bootstrap success.
- Join by link: validate/authenticate/confirm/join/redirect.
- Manage members: admin-only promote action on active non-admin rows.
- Manage invites: admin-only create/revoke + copy-link.
- Manage schedule: admin create season + one-off event; all members can view.

## 7.3 UX guardrails
- Server is source of truth for permission and role state.
- Keep MVP as list/detail + simple forms; avoid dashboard complexity.
- No leaderboard/scoring UX in MVP.

## 8. Data model and migration strategy

## 8.1 Relational model (MVP)
- `Leagues`
- `LeagueMembershipEvents` (append-only)
- `LeagueMembersCurrent` (projection)
- `LeagueInvites`
- `LeagueSeasons`
- `LeagueEvents`

Suggested constraints/indexes:
- Membership events index on `(LeagueId, OccurredAtUtc DESC)`.
- Current projection index on `(LeagueId, Role, IsActive)`.
- Unique invite hash index on `TokenHash`.
- Invite list index on `(LeagueId, Status, ExpiresAtUtc)`.
- Events index on `(LeagueId, SeasonId, ScheduledAtUtc)`.

## 8.2 Migration sequencing
1. Add schema + constraints + indexes.
2. Introduce write handlers that append events and update current projection transactionally.
3. Add read endpoints over projections.
4. Add feature flag and deploy dark (non-visible) initially.
5. Enable staged rollout by cohort/environment.

Backfill:
- None required (greenfield feature).

## 9. Testing and rollout strategy

## 9.1 Testing priorities
P0:
- League creation bootstrap invariants.
- Invite join happy path + idempotency.
- Invite rejection paths (revoked/expired/invalid).
- RBAC enforcement for admin-only actions.
- Membership temporal integrity (no overlapping active intervals).
- Cross-league authorization isolation.

P1:
- Concurrency races (`join/join`, `join/leave`).
- UI role-gated visibility and action flows.
- Season/one-off create/list smoke coverage.

P2:
- Rate-limit and abuse behavior.
- Performance/latency baselines.
- Accessibility polish on league pages.

## 9.2 Rollout approach
- Feature-flag leagues by environment and optional cohort.
- Stage rollout: internal -> beta cohort -> GA.
- Require stable metrics window between stages.

Monitors:
- Join failure reason distribution.
- Unauthorized mutation attempts.
- Data integrity check for duplicate active intervals.
- Latency/error rates for create/join/promote/revoke paths.

Rollback triggers:
- Any membership integrity breach.
- Any unauthorized mutation success incident.
- Sustained P0 path error-rate SLO breach.

## 10. MVP scope and explicit non-goals

### MVP in
- Private league creation and listing.
- Membership timeline with join/leave and admin promotion.
- Invite-by-link create/revoke/join with expiry.
- Season and one-off event metadata create/list.
- Blazor routes and role-gated management actions.

### MVP out
- Standings/points/leaderboards.
- Public league discovery.
- Rich moderation and anti-abuse controls beyond rate limiting.
- Admin demotion/ownership transfer workflows.
- Gameplay engine changes tied to leagues.

## 11. Implementation phases mapped to repo areas

### Phase 1: Domain + persistence primitives
- `src/CardGames.Poker` (league domain aggregate, commands, invariants)
- `src/CardGames.Core` / `src/CardGames.Core.French` (only if shared primitives are required; otherwise unchanged)
- `src/CardGames.Poker.Api` (EF/migration wiring and repository/handler persistence)

Deliverables:
- League aggregate + membership/invite/season/event models
- Schema migrations + indexes + concurrency constraints

### Phase 2: API write/read surface + authorization
- `src/CardGames.Poker.Api` (MediatR handlers, validators, auth policies, endpoints)
- `src/CardGames.Contracts` (contract sources + regeneration pipeline output)
- `src/CardGames.Poker.Refitter` (client regeneration process)

Deliverables:
- MVP command/query endpoints
- League resource-scoped authorization policies
- Regenerated contracts (without direct edits to generated artifacts)

### Phase 3: Blazor UX integration
- `src/CardGames.Poker.Web` (pages/components/services for leagues)

Deliverables:
- `/leagues`, `/leagues/{leagueId}`, `/leagues/join/{token}`
- Create/join/member/invite/schedule flows with role-gated actions

### Phase 4: Tests + rollout hardening
- `src/Tests` (domain + integration + UI test coverage)
- `src/CardGames.AppHost` (feature flag/config and environment orchestration)
- `src/CardGames.ServiceDefaults` (telemetry/resilience conventions if updates needed)

Deliverables:
- P0/P1 coverage in CI
- Progressive rollout toggles + monitoring hooks

## 12. Approval checklist

This design is ready for approval if all of the following are accepted:
- Account-scoped league boundary and non-coupling with gameplay domain.
- Event-based mutable membership with current-state projection.
- Invite-by-link security baseline (hash, expiry, revoke, rate-limit).
- Contract regeneration-first approach (no generated-file edits).
- MVP includes schedule metadata only, defers standings/leaderboards.
- Four-phase implementation plan mapped to current repo boundaries.
