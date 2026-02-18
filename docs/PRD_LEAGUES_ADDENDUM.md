# Poker Website — Leagues PRD Addendum

> This document augments the main PRD ([docs/PRD.md](PRD.md)) with requirements for **Leagues** as a core user experience.
>
> This addendum intentionally builds on (and may supersede parts of) the existing MVP-focused design docs:
> - [docs/LeaguesFeatureDesign.md](LeaguesFeatureDesign.md)
> - [docs/LeaguesUXDesign.md](LeaguesUXDesign.md)
> - [docs/LeaguesBackendDesign.md](LeaguesBackendDesign.md)
> - [docs/LeaguesTestStrategy.md](LeaguesTestStrategy.md)

## 1. Overview
Leagues are **private clubs** for recurring groups to organize play over time.
They provide membership and governance, scheduling, and a competitive loop (results → standings) while keeping poker rules and hand orchestration in the existing table/game engine.

This addendum focuses on making Leagues a durable, high-retention experience similar to “Home Games”-style clubs:
- discover/join with trust
- manage membership and roles
- schedule events
- launch playable sessions
- record results
- compute standings and leaderboards

## 2. Goals
- Make it easy to **join a League by code** from inside the app.
- Introduce **request-to-join** with admin approval to create a safer, more controlled club experience.
- Make scheduled League events **actionable**: launch a playable table/tournament from the League.
- Provide a **competitive loop**: capture results and compute season standings/leaderboards.
- Keep Leagues decoupled from poker rules and state machines (Leagues orchestrates; table/game engine plays).

## 3. Non-Goals (90-day window)
- Public browsing/discovery of leagues.
- Deep theming/branding beyond basic name/description.
- Advanced anti-collusion investigation workflows (baseline abuse controls and audit trail are in scope).
- Large-scale tournament operations beyond “League event launches a playable session.”
- Economy features (buy-ins ledgering, payouts, cashier) unless already supported elsewhere.

## 4. Personas & Roles
### 4.1 Personas
- **Member**: participates in League events, views schedule and standings, requests to join.
- **Admin**: approves join requests, manages invites/codes, manages members, creates events, manages results.
- **Manager/Owner**: top-level authority for governance edge-cases (ownership transfer, last-admin safety).

### 4.2 Role model
Leagues require an explicit top authority once approval workflows exist.

**Roles**
- `Member`
- `Admin`
- `Manager` (or `Owner`)

**Bootstrap**
- The League creator becomes `Manager` and is an active `Member`.

**Safety rules**
- A League must always have at least one `Manager`.
- A League must never end up with zero administrators capable of managing join requests.

## 5. Key User Journeys
### 5.1 Join League by Code (Primary)
**Start**: Leagues landing page.
1. User selects `Join league`.
2. User enters a short League code.
3. App shows a **trust preview** (league name, manager identity, member count, basic rules).
4. User submits **request to join**.
5. On approval, user becomes an active member and lands in the League lobby.

**Success outcome**: user becomes a member, understands what they joined, and has an obvious “next event / play” CTA.

### 5.2 Invite Link Join (Secondary)
**Start**: invite URL.
1. If unauthenticated, redirect to login with return path.
2. Show trust preview.
3. User requests to join (same approval workflow).

**Success outcome**: invite links remain useful, but do not bypass governance.

### 5.3 Manager/Admin Moderation
**Start**: League detail page.
- Admin views join request queue.
- Admin approves/denies requests.
- Admin can remove a member.
- Manager can promote/demote admins and transfer ownership.

**Success outcome**: governance actions are safe, auditable, and predictable.

### 5.4 Schedule → Play → Results → Standings
**Start**: League schedule.
1. Admin creates season and events.
2. When it’s time to play, admin selects `Create table from event`.
3. A League-scoped playable session starts (table or tournament depending on event type).
4. At completion, the session produces a League result payload.
5. Standings update for the season.

**Success outcome**: League members can repeatedly “show up and play” and see progress.

### 5.5 Member Retention Loop
**Start**: League lobby.
- Member views current season standings and personal progress.
- Member sees the next event and can jump into play.

**Success outcome**: standings and upcoming schedule drive repeat participation.

## 6. Functional Requirements
### 6.1 League Basics
- Authenticated user can create a league with name and optional description.
- League is private and not publicly discoverable.
- League detail is visible only to active members (except trust preview screen).

**Acceptance criteria**
- Creator becomes active member and `Manager`.
- Non-member cannot access League detail and cannot enumerate members.

### 6.2 Join by Code + Trust Preview
- App provides an in-product join entry point on Leagues landing page.
- Join requires displaying a trust preview before request submission.

**Acceptance criteria**
- Code validation errors are user-actionable.
- Trust preview always displays league identity and join policy.

### 6.3 Request-to-Join Workflow
- Joining a League is a request that must be approved by a League admin (or manager).
- Admins can approve or deny requests.
- Request state is visible to requesting user (pending/approved/denied/expired).

**Acceptance criteria**
- Request submission is idempotent (repeat submits do not create duplicates).
- Admin actions are authorized and resource-scoped to the league.
- Denied requests do not leak sensitive details.

### 6.4 Membership and Roles
- Members can leave a League.
- Admins can remove a member.
- Managers can:
  - promote/demote admins,
  - transfer manager ownership,
  - resolve last-admin safety constraints.

**Acceptance criteria**
- Membership history is auditable and non-destructive.
- System prevents orphaned governance states (no “no-admin” League).

### 6.5 Invites and Codes
- Leagues support:
  - join-by-code (primary),
  - invite link (secondary).
- Invites/codes have lifecycle controls (rotate/revoke/expire) and abuse protections.

**Acceptance criteria**
- Raw invite tokens are never stored.
- Failed join attempts return deterministic, documented error semantics.

### 6.6 Scheduling
- League supports seasons and events.
- Events have:
  - type (cash table / tournament),
  - scheduled time,
  - status (planned/in-progress/completed/canceled).

**Acceptance criteria**
- Members can view schedule.
- Admins can create/update events.

### 6.7 League-to-Play Bridge
- From a League event, an admin can launch a playable session.
- Only League members may join the session.
- The playable session must reference the League event so results can be attached.

**Acceptance criteria**
- There is a single authoritative linkage between event and session.
- Users can return from play to League results/standings.

### 6.8 Results Capture
- For each completed League event, store:
  - participants,
  - placements,
  - points awarded,
  - completion timestamp,
  - session reference.

**Acceptance criteria**
- Result writes are idempotent.
- Corrections are possible via explicit admin action and are fully audited.

### 6.9 Standings / Leaderboards
- Define a season scoring system with tie-breakers.
- Provide:
  - season standings,
  - per-member stats for the season,
  - recent results.

**Acceptance criteria**
- Standings are deterministic and recomputable.
- Users can view standings scoped to a season.

## 7. Non-Functional Requirements
### 7.1 Security
- Rate limit join-by-code, join-by-invite, and request-to-join endpoints.
- Audit log for:
  - join requests and admin decisions,
  - role changes,
  - member removals,
  - invite/code rotations.

### 7.2 Reliability & Integrity
- Membership and join request invariants are safe under concurrency.
- Cross-league authorization isolation is enforced.
- All write endpoints support idempotency (client retries safe).

### 7.3 Observability
- Track funnel conversion for join-by-code and join-by-invite.
- Track SLOs for join/request/approve endpoints.
- Add integrity monitors (duplicate active membership, orphaned role state).

## 8. Epics (Backlog) with Owners
> Owners map to squad roles: Rusty (architecture), Danny (API/domain), Linus (Blazor UX), Basher (tests/quality).

### P0 — League Onboarding & Trust (Join-by-Code)
- **Owner(s)**: Linus (primary), Danny, Basher, Rusty
- **Scope**: in-app join entry point, code input, trust preview, request submission.
- **Acceptance criteria**:
  - join-by-code exists in UI with clear empty states.
  - trust preview shown before request.
  - request is idempotent.

### P0 — Governance & Request Approval
- **Owner(s)**: Danny (primary), Linus, Basher, Rusty
- **Scope**: join request lifecycle, admin approve/deny, role safety rules.
- **Acceptance criteria**:
  - only authorized admins can approve/deny.
  - no orphaned governance state possible.
  - full audit trail for decisions.

### P0 — League Event → Playable Session Bridge
- **Owner(s)**: Rusty (primary), Danny, Linus, Basher
- **Scope**: event links to table/tournament creation and membership-gated joins.
- **Acceptance criteria**:
  - event can launch a session.
  - only league members can join.
  - completed session emits a results payload.

### P1 — Results + Standings
- **Owner(s)**: Danny (primary), Rusty, Linus, Basher
- **Scope**: results model, scoring rules, standings queries, UI leaderboard.
- **Acceptance criteria**:
  - standings deterministic and recomputable.
  - per-season leaderboard renders and updates after results.

### P1 — Admin Operations Console
- **Owner(s)**: Linus (primary), Danny, Basher
- **Scope**: join request queue UX, member removal, role management panels, audits visibility.
- **Acceptance criteria**:
  - admin workflows are test-covered and role gated.
  - error states are actionable.

### P2 — Templates & Club Ergonomics
- **Owner(s)**: Linus (primary), Danny, Basher
- **Scope**: reusable setup templates and a “My Clubs” portfolio UX.
- **Acceptance criteria**:
  - users can quickly switch clubs and see pending actions.
  - admins can reuse templates for events/sessions.

## 9. 90-Day Roadmap
### Now (0–30 days)
- Join-by-code + trust preview + request-to-join submission.
- Governance: request approval queue + basic member removal.
- Release gates: feature flags, baseline telemetry, authorization/integrity acceptance criteria.

### Next (31–60 days)
- League event → playable session bridge.
- Results capture (baseline) + first standings implementation.
- UI for standings + “next event” retention loop.

### Later (61–90 days)
- Admin console deepening + audits.
- Templates and multi-club ergonomics.
- Scale hardening (rate limits, integrity jobs, SLO dashboards).

## 10. What to Measure
- Activation: % of users who join a league after visiting Leagues.
- Join funnel: code entered → trust preview → request submitted → approved → first league event played.
- Approval health: median approval time, backlog size.
- Invite/code health: join failure reasons (invalid/expired/revoked), abuse blocks.
- Play conversion: event views → session launched → session completed.
- Competitive engagement: standings views per active member/week.
- Retention: D7/D30 return rate for league members.
- Reliability: error rate and p95 latency for join/request/approve endpoints.
- Integrity: duplicate active membership incidents, orphaned role state incidents.

## 11. Open Questions
- Final scoring model (points per placement, attendance points, tie-breakers) and correction policy.
- Event taxonomy: minimum set of event types for “cash table” vs “tournament”.
- Notifications: in-app only vs email/push (phase decision).
- Manager/Owner transfer UX and policy boundaries.
