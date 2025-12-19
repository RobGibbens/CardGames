# Poker Website — Product Requirements Document (PRD)

## 1. Overview
Build a web-first Poker product where users can register, authenticate, create/join tables, invite other players, start games, and play hands in real time. The platform must support a wide variety of poker styles by modeling rules and game flow as a configurable/extendable “game definition,” not a single hard-coded variant.

## 2. Goals
- Enable a user to register and join a game quickly.
- Provide reliable real-time gameplay with an authoritative server.
- Support multiple poker variants (e.g., Hold’em, Omaha, Stud, Draw) through an extensible rules model.
- Support private play with invitations and shareable links.

## 3. Non-Goals (MVP)
- Real-money gambling, payments, withdrawals, KYC, geo-fencing, and regulatory compliance.
- Advanced collusion/cheating detection beyond basic integrity controls.
- Full tournament operations at large scale (can be phased).

## 4. Personas & Roles
- **Player**: joins games, plays hands, manages profile, reviews hand history.
- **Host / Table Owner**: creates and configures games, invites/removes players, starts/pauses/ends games.
- **Admin/Moderator** (optional/phase): moderates users/tables, handles reports and bans, views audits.

## 5. Key User Journeys
### 5.1 Registration → Verification → Sign-in
1. User registers (email+password and/or OAuth).
2. User verifies email (for email-based registration).
3. User signs in and lands in a lobby.

### 5.2 Create Game → Invite Players → Start Game
1. Host chooses a variant/template and configures table settings.
2. Host invites players (share link and/or in-app invites).
3. Players join and take seats.
4. Host starts the game once minimum players are met.

### 5.3 Play → Reconnect → Finish
1. Players take turn-based actions in real time.
2. If a player disconnects, they can reconnect and resume.
3. Each hand completes with settlement and recorded history.

## 6. Functional Requirements

### 6.1 Registration & Authentication
**Registration**
- Support email + password registration.
- Require email verification (token-based) for email signups.
- Support optional OAuth providers (e.g., Google/Microsoft) to reduce friction.
- Enforce username/display name rules and uniqueness strategy.

**Account Recovery**
- Password reset via email token.
- Invalidate active sessions on password reset.

**Session Management**
- Use secure session handling (secure cookies or token-based with refresh).
- Protect against replay and CSRF as appropriate for the chosen auth approach.

**MFA (Phase 2)**
- Optional TOTP-based MFA.

### 6.2 Authorization (Permissions)
**Role-based permissions**
- Player: join/leave, sit/stand (if supported), act when it is their turn, view history.
- Host: create/edit table settings (within guardrails), invite/remove players, start/pause/end, optionally assign seats.
- Admin: user and table moderation, audit visibility.

**Resource-scoped authorization**
- Host actions must validate table ownership/host privileges.
- A player can only act if:
  - they are seated and eligible,
  - it is their turn,
  - the action is valid for the current state.

### 6.3 Lobby & Discovery
- Lobby pages:
  - “My tables”, “Invited”, “Recent”.
  - Optional “Public tables” listing.
- Filtering (as applicable): variant, seats, stakes/buy-in, private/public, in-progress/waiting.
- Table detail page:
  - Rules summary, configuration, seats list, join/invite controls.

### 6.4 Creating Games (Table / Match Setup)
**Variant selection**
- Choose a variant family (community-card, stud, draw) and a specific template.

**Configurable parameters** (guardrailed per variant)
- Seats (min/max).
- Blinds/antes and/or buy-in/stack rules (cash-style).
- Turn timers and timeout behaviors.
- Rebuy/add-on policy (phase depending on scope).
- Private/public visibility.
- Spectator allowed toggle.
- Auto-start conditions (optional).

**Validation & guardrails**
- Validate configurations against variant capabilities.
- Safe defaults for timers and limits.

### 6.5 Inviting Players
**Invite methods**
- Shareable invite link with token.
- Optional in-app invite to existing users.

**Invite lifecycle**
- Pending/accepted/declined/expired.
- Host can revoke invites.
- Link restrictions (optional): expiration, max uses, allowlist.

### 6.6 Starting Games
- Pre-start room state:
  - Players join, sit, and optionally mark ready.
- Start conditions:
  - Minimum player count.
  - Buy-in/stack requirements satisfied.
- Runtime controls:
  - Host can pause/resume/end.
  - Optional host handover on host disconnect.

### 6.7 Playing Games (Real-Time)
**Real-time transport**
- Bidirectional real-time updates (e.g., WebSockets/SignalR).

**Authoritative server**
- Server validates actions and advances game state.
- Client is a view + input layer.

**Turn-based actions**
- Fold/check/call/bet/raise/all-in.
- Enforce betting rules (min raise, side pots, all-in handling).

**Timers & auto-actions**
- Per-turn timer.
- Timeout policy based on state (check if possible, else fold).
- Sit-out policy for repeated timeouts.

**Reconnect**
- Rejoin restores current table state.
- Players see only their private information (hole cards) and public state.

**Hand history**
- Record per-hand summary.
- Optional full action log (phase depending on storage/UX).

## 7. Variant Support & Extensibility
Because poker styles vary widely, the system must represent rules in a generalizable way.

### 7.1 Game Definition Model
A “game definition” should describe:
- Deck(s) configuration.
- Seat count constraints.
- Card dealing scheme (hole cards, community cards, up cards).
- Round structure (betting rounds and transitions).
- Allowed actions per phase.
- Hand evaluation rules (ranking, tie-breaks).
- Settlement rules (pots, side pots, splits).

### 7.2 Engine / State Machine
- Implement a deterministic state machine with explicit phases:
  - waiting → seating → dealing → betting rounds → showdown → settlement → next hand.
- Ensure idempotent action submission and ordered event processing.

### 7.3 UI Strategy
- Shared “table shell” UI: seats, stacks, pot, action controls, timers.
- Variant-specific UI overlays:
  - Draw selection,
  - Stud up-card presentation,
  - Community-card boards.

## 8. Non-Functional Requirements
**Performance**
- Low-latency action propagation; target sub-250ms typical action-to-broadcast within region.

**Reliability**
- No lost actions; server-side event/audit trail.
- Graceful handling of disconnects and restarts (phase-dependent).

**Security**
- Secure auth storage and transport.
- Rate limiting and abuse controls for registration and invites.
- Prevent information leaks (players only see authorized private state).

**Privacy**
- Minimize PII; define data retention for accounts, invites, hand histories, and chat.

**Accessibility**
- Keyboard-operable actions; clear turn indicators; color-safe visualization.

**Observability**
- Structured logs, metrics (latency, disconnect rate), tracing for action pipeline.

## 9. Success Metrics
- Activation: % of users who join a table within 10 minutes of registration.
- Engagement: hands played per user/day; D1/D7 retention.
- Reliability: reconnect success rate; action validation error rate; average turn latency.
- Social: invite acceptance rate; tables created per active host.

## 10. Phased Delivery (Suggested)
**Phase 1 (MVP)**
- Registration/auth + email verification.
- Private invite-only tables.
- One primary variant (e.g., Hold’em).
- Create/invite/start flow.
- Real-time play with authoritative server.
- Reconnect support.
- Basic hand history.

**Phase 2**
- Add variants (Omaha/Stud/Draw).
- Public lobby/discovery.
- Better profiles/stats.
- Moderation controls.
- MFA.

**Phase 3**
- Integrity enhancements.
- Scheduled events/tournaments.
- Spectator mode.
- Mobile-first polish.

## 11. Open Questions
- Auth approach: email-only vs OAuth-first; allow guest play?
- What is the first post-MVP variant priority (Omaha vs Stud vs Draw)?
- Should a table continue without the host? How is host transferred?
- Data retention expectations for hand histories and chat?
- Initial scope: cash tables only, or include simple tournaments early?
