# Leagues Backend Design

Requested by: Rob Gibbens  
Author: Danny (Backend Dev)  
Date: 2026-02-17

## 1. Scope and boundary

Leagues are **account-scoped social coordination containers**. They own:
- membership over time,
- league administration (RBAC),
- invite-link access,
- season and one-off event metadata.

Leagues do **not** own poker game rules, hand orchestration, betting/dealing phases, or showdown mechanics. Those remain in existing game domains per `docs/ARCHITECTURE.md`.

## 2. MVP vs post-MVP

### MVP (this design targets)
- Create league (creator auto-admin).
- Temporal membership with join/leave history.
- Multi-admin support and admin promotion of active members.
- Invite-by-link join flow with token lifecycle: active, revoked, expired.
- Event model for:
  - season containers (e.g., 10 tournaments),
  - one-off nights/events.
- Basic list/detail/read models for leagues, members, invites, seasons/events.

### Post-MVP
- Leaderboards/standings/scoring aggregation across season events.
- Invite policies: max uses, single-use, domain allowlist, richer anti-abuse controls.
- Public/discoverable leagues and moderation workflows.
- Admin demotion/transfer constraints UX hardening (last-admin protection UI flows).

## 3. Domain model

## 3.1 Aggregate boundaries

### League aggregate (write model root)
**Identity:** `LeagueId` (guid/ulid), account-scoped.  
**Core state:**
- `Name`, `Description?`, `CreatedByUserId`, `CreatedAtUtc`,
- `Status` (Active, Archived),
- `Version` (optimistic concurrency token).

**Owned behaviors (invariant enforcement):**
- Create league and bootstrap creator as admin.
- Join/leave membership (temporal history events).
- Promote member to admin (RBAC + membership checks).
- Create/revoke invite links.
- Create/update season containers and one-off events.

### Read projections (separate persistence/read model)
- `LeagueSummaryProjection` (my leagues listing).
- `LeagueMemberCurrentProjection` (current active members + role).
- `LeagueMembershipHistoryProjection` (timeline view/audit).
- `LeagueInviteProjection` (active/revoked/expired + expiration).
- `LeagueSeasonProjection` / `LeagueEventProjection`.

Rationale: append-only event-backed writes plus current-state projections match review decisions and reduce race complexity.

## 3.2 Temporal membership model

Represent membership as append-only domain events:
- `LeagueMemberJoined`
- `LeagueMemberLeft`
- `LeagueMemberPromotedToAdmin`
- (post-MVP) `LeagueMemberDemotedFromAdmin`

Event fields:
- `LeagueId`, `UserId`, `OccurredAtUtc`, `ActorUserId`, `CorrelationId`, `IdempotencyKey?`.

Derived current membership rules:
- A member is active when the latest interval is `joinedAt != null && leftAt == null`.
- Rejoin is allowed after a leave (new interval/event sequence).
- Promotion requires the target user to be currently active in league.

Concurrency/integrity:
- enforce aggregate version check on write,
- unique active-membership constraint in projection (one active row per `LeagueId+UserId`),
- command idempotency key for retry-safe join/promote endpoints.

## 3.3 Invite token lifecycle model

### InviteLink entity (owned by League aggregate)
- `InviteId`
- `LeagueId`
- `TokenHash` (never persist raw token)
- `Status` (`Active`, `Revoked`)
- `CreatedByUserId`, `CreatedAtUtc`
- `ExpiresAtUtc` (required for MVP)
- `RevokedAtUtc?`, `RevokedByUserId?`

### Lifecycle and validation
Raw token is generated once, returned only on create as URL-safe opaque string.

Join validation pipeline:
1. token lookup by hash,
2. invite exists and belongs to target league,
3. status is `Active`,
4. `ExpiresAtUtc > now`,
5. caller authenticated,
6. caller not already active member (idempotent success or explicit no-op).

State transitions:
- create -> `Active`
- admin revoke -> `Revoked`
- passive expiration -> treated invalid at read/validation time (no write required)

## 3.4 RBAC rules

Roles (MVP):
- `Member`
- `Admin`

Bootstrap:
- creator is auto-joined and auto-admin on `CreateLeague`.

Authorization matrix:
- Any authenticated user: create league, list own leagues, view league where active member.
- Active member: leave league, view member/event/season listings.
- Admin only: create/revoke invite, promote active member to admin, create/update season/event metadata.

Safety rules:
- promotion target must be active member,
- actor must be active admin,
- last-admin removal/demotion is out of MVP write surface (no demote command in MVP).

## 4. API and application layer design

Follow existing API conventions: MediatR requests + validators + authorization policies in API pipeline.

## 4.1 Commands (writes)

- `CreateLeagueCommand(name, description?) -> LeagueCreatedResult`
- `CreateLeagueInviteCommand(leagueId, expiresAtUtc) -> InviteCreatedResult(rawToken, inviteUrl)`
- `RevokeLeagueInviteCommand(leagueId, inviteId) -> Unit`
- `JoinLeagueByInviteCommand(rawToken) -> JoinLeagueResult`
- `LeaveLeagueCommand(leagueId) -> Unit`
- `PromoteLeagueMemberToAdminCommand(leagueId, memberUserId) -> Unit`
- `CreateLeagueSeasonCommand(leagueId, name, plannedEventCount?, startsAtUtc?, endsAtUtc?) -> SeasonResult`
- `CreateLeagueOneOffEventCommand(leagueId, name, scheduledAtUtc?, metadata?) -> EventResult`
- `CreateLeagueSeasonEventCommand(leagueId, seasonId, name, sequenceNumber?, scheduledAtUtc?, metadata?) -> EventResult`

Validation examples:
- non-empty names, length constraints,
- `expiresAtUtc` must be future UTC,
- season event must reference existing season in same league,
- sequence uniqueness (league+season+sequenceNumber) for MVP optional if provided.

## 4.2 Queries (reads)

- `GetMyLeaguesQuery() -> LeagueSummary[]`
- `GetLeagueDetailQuery(leagueId) -> LeagueDetailDto`
- `GetLeagueMembersQuery(leagueId) -> LeagueMemberDto[]`
- `GetLeagueMembershipHistoryQuery(leagueId, take, skip) -> MembershipEventDto[]`
- `GetLeagueInvitesQuery(leagueId, includeExpired=false) -> LeagueInviteDto[]`
- `GetLeagueSeasonsQuery(leagueId) -> LeagueSeasonDto[]`
- `GetLeagueEventsQuery(leagueId, seasonId?) -> LeagueEventDto[]`

## 4.3 Endpoint surface (`/api/v1`)

Suggested routes:
- `POST /api/v1/leagues`
- `GET /api/v1/leagues/mine`
- `GET /api/v1/leagues/{leagueId}`
- `GET /api/v1/leagues/{leagueId}/members`
- `GET /api/v1/leagues/{leagueId}/members/history?take=&skip=`
- `POST /api/v1/leagues/{leagueId}/invites`
- `GET /api/v1/leagues/{leagueId}/invites`
- `POST /api/v1/leagues/join-by-invite`
- `POST /api/v1/leagues/{leagueId}/leave`
- `POST /api/v1/leagues/{leagueId}/members/{memberUserId}/promote-admin`
- `POST /api/v1/leagues/{leagueId}/seasons`
- `GET /api/v1/leagues/{leagueId}/seasons`
- `POST /api/v1/leagues/{leagueId}/events/one-off`
- `POST /api/v1/leagues/{leagueId}/seasons/{seasonId}/events`
- `GET /api/v1/leagues/{leagueId}/events?seasonId=`

AuthN/AuthZ:
- use existing authenticated principal identity (`UserId` claim),
- apply explicit policies (`LeagueMember`, `LeagueAdmin`) with resource checks in handlers.

## 5. Persistence and migration notes

## 5.1 New tables (MVP)

Recommended relational shape (names illustrative, align with existing naming conventions):
- `Leagues`
  - `LeagueId` PK
  - `Name`, `Description`, `Status`
  - `CreatedByUserId`, `CreatedAtUtc`
  - `Version` rowversion/concurrency token

- `LeagueMembershipEvents` (append-only)
  - `EventId` PK
  - `LeagueId` FK, `UserId`
  - `EventType` (Joined, Left, PromotedToAdmin)
  - `OccurredAtUtc`, `ActorUserId`
  - `CorrelationId`, `IdempotencyKey?`

- `LeagueMembersCurrent` (projection)
  - `LeagueId`, `UserId` composite PK
  - `IsActive`, `Role` (Member/Admin)
  - `JoinedAtUtc`, `LeftAtUtc?`, `UpdatedAtUtc`
  - unique filtered index on active membership (`LeagueId`,`UserId`,`IsActive=true`)

- `LeagueInvites`
  - `InviteId` PK
  - `LeagueId` FK
  - `TokenHash` unique
  - `Status`, `ExpiresAtUtc`
  - `CreatedByUserId`, `CreatedAtUtc`, `RevokedAtUtc?`, `RevokedByUserId?`

- `LeagueSeasons`
  - `SeasonId` PK
  - `LeagueId` FK
  - `Name`, `StartsAtUtc?`, `EndsAtUtc?`, `PlannedEventCount?`, `Status`

- `LeagueEvents`
  - `EventId` PK
  - `LeagueId` FK
  - `SeasonId` nullable FK
  - `EventKind` (`SeasonEvent`|`OneOff`)
  - `Name`, `SequenceNumber?`, `ScheduledAtUtc?`, `Status`
  - `MetadataJson?`

## 5.2 Migration sequencing

1. Add schema for leagues, membership events, current projection, invites, seasons/events.
2. Add indexes:
   - `LeagueMembershipEvents(LeagueId, OccurredAtUtc DESC)`
   - `LeagueMembersCurrent(LeagueId, Role, IsActive)`
   - `LeagueInvites(TokenHash)` unique and `LeagueInvites(LeagueId, Status, ExpiresAtUtc)`
   - `LeagueEvents(LeagueId, SeasonId, ScheduledAtUtc)`
3. Deploy write handlers that maintain projection transactionally with event insertion.
4. Deploy read endpoints backed by projections.
5. Backfill: none required for greenfield Leagues.

## 5.3 Security/operational notes

- Store only hashed invite tokens (e.g., SHA-256 + app secret pepper/HMAC).
- Add rate limiting for `join-by-invite` endpoint.
- Log membership/admin/invite lifecycle changes as auditable structured events.
- Ensure PII minimization in event metadata.

## 6. Edge-case handling (MVP defaults)

- Duplicate join while already active: return success with no-op semantics.
- Join with revoked/expired/unknown token: 400/404 style domain error (consistent with API standards).
- Promote non-member or inactive member: validation/domain conflict error.
- Leave when not active member: idempotent no-op or conflict; choose one convention globally (recommended: idempotent no-op).
- Concurrent promote/leave race: optimistic concurrency failure -> client retry with refreshed state.

## 7. Implementation alignment checklist

- Keep contracts generation process intact; if contracts are generated in this repo, regenerate rather than manual edits.
- Keep game-rule engine untouched.
- Keep MediatR + FluentValidation + policy-based authorization patterns from `CardGames.Poker.Api`.
- Keep account-scoped boundaries explicit in naming and policies (`League*` not table/game host policies).

## 8. Recommended MVP slice order

1. League create + my leagues + detail (creator auto-admin/member).
2. Membership event model + current projection + leave.
3. Invite create/revoke + join-by-invite.
4. Admin promotion endpoint.
5. Seasons and one-off event CRUD-lite (create/list only for MVP).
6. Integration tests for RBAC, invite lifecycle, membership timeline/idempotency.

This sequence delivers the requested user outcomes early while preserving architecture boundaries and minimizing coupling risk.
