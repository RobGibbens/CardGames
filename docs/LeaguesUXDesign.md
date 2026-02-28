# Leagues UX Design (Blazor Web)

## Purpose and Scope
This document defines a minimal UX for Leagues in `CardGames.Poker.Web`.
Leagues are social/account containers for membership, admin roles, invite access, and event scheduling metadata.
Gameplay rules and hand flow remain in existing table/game flows (`/table/{tableId}`) and are not duplicated in league UI.

## UX Principles
- Keep flows minimal: create, invite, join, manage members, manage schedule.
- Reuse existing auth/session behavior and service-discovery patterns.
- Gate UI by permissions from API; never infer authority from client-only state.
- Prefer list/detail screens over complex dashboards for MVP.

## Navigation Entry Points
1. Primary nav item: `Leagues` (authenticated users only).
2. Route: `/leagues` for list + create CTA.
3. Deep-link join route: `/leagues/join/{token}` (invite link target).
4. League detail route: `/leagues/{leagueId}`.

## Information Architecture
### 1) Leagues List (`/leagues`)
Minimal content:
- Page title + `Create league` button.
- `My Leagues` list (name, role badge, member count, active season summary if present).
- Empty state with primary CTA: `Create your first league`.

Primary actions:
- Open league detail.
- Create league.

### 2) Create League Flow
Entry points:
- `Create league` button from `/leagues`.

MVP interaction model:
- Single-page form or simple modal (choose whichever matches existing patterns).
- Fields:
  - `League name` (required)
  - `Description` (optional, short)
  - `Visibility` fixed to private in MVP (shown as non-editable info text)
- Submit action: `Create league`.

On success:
- Navigate to `/leagues/{leagueId}`.
- Show toast/banner: `League created. You are now an admin.`

On failure:
- Inline validation and non-blocking error banner.

### 3) League Detail (`/leagues/{leagueId}`)
Minimal sections/tabs:
1. **Overview**
   - League name, description.
   - Current user role badge (`Admin` / `Member`).
   - Membership status (`Active` expected for visible details).
2. **Members**
   - Member rows: display name, role badge, joined date, active status.
   - Admin-only action on eligible rows: `Promote to admin`.
3. **Invites** (admin-only section visibility)
   - Active invite links list (token alias, expiry, status).
   - Actions: `Create invite link`, `Revoke`.
4. **Schedule**
   - Seasons list.
   - One-off events list.

Keep as one page with segmented sections; avoid nested sub-navigation for MVP.

## Invite Flow (Admin)
### Create Invite
From `Invites` section:
- Click `Create invite link`.
- Minimal form:
  - Expiration date/time (required for MVP)
- Submit creates tokenized URL.

Post-create:
- Show generated link with `Copy link` action.
- Add row to invites list with `Active` status and expiration.

### Revoke Invite
From invites row action:
- `Revoke` with confirm dialog.
- After success, status updates to `Revoked`; join no longer allowed.

## Join Flow (Invite-by-Link)
### Route
`/leagues/join/{token}`

### States
1. **Authenticated + valid active token**
   - Show league name and concise confirmation.
   - CTA: `Join league`.
2. **Authenticated + already active member**
   - Info state: `You are already a member.`
   - CTA: `Go to league`.
3. **Unauthenticated**
   - Redirect to login with return URL to join route.
4. **Invalid/expired/revoked token**
   - Error state + CTA: `Back to leagues`.

After successful join:
- Navigate to league detail with success message.

## Admin Management Flow
### Promote Another Member
Location: `Members` section, row action.

Visibility and enablement:
- Visible only to current admins.
- Enabled only for active non-admin members.

Behavior:
- Optional lightweight confirm (`Promote {name} to admin?`).
- Success: role badge updates immediately after server response.
- Failure: inline row error.

MVP exclusions:
- No demote/remove-admin flow unless explicitly added by backend policy.

## Membership Over Time UX
- Default members view shows current active members first.
- Each row shows `Joined` timestamp.
- If API provides historical intervals, surface compact `Previous membership periods` in a collapsible history panel per user.
- Re-join is displayed as a new active interval, not destructive overwrite.

## Season Setup and Overview UX
### Season List / Overview
In `Schedule` section:
- List seasons with:
  - Name
  - Planned tournament count (e.g., `10 tournaments`)
  - Date range (optional)
  - Status (`Planned`, `In progress`, `Completed`)

### Create Season (Admin)
Minimal form:
- `Season name` (required)
- `Target tournaments` (required numeric, e.g., 10)
- Optional start/end dates

After create:
- Season appears in list with `Planned` status.

MVP note:
- No standings/leaderboard visualization in this phase.

## One-Off Events UX
In `Schedule` section, separate list from seasons:
- Event rows: title, date/time, type (`Cash game` or `Tournament`), status.
- Admin CTA: `Create one-off event`.

Create one-off event form (minimal):
- `Title` (required)
- `Date/time` (required)
- `Event type` (`Cash game` or `Tournament`)
- Optional notes

## Components (Conceptual)
- `LeaguesPage` (list + create CTA)
- `LeagueCreateForm`
- `LeagueDetailPage`
- `LeagueMembersSection`
- `LeagueInvitesSection`
- `LeagueScheduleSection`
- `LeagueSeasonForm`
- `LeagueOneOffEventForm`
- `LeagueJoinPage`

These should be implemented as thin UI components over API contracts; keep domain rules server-side.

## State and Permissions Behavior
## Client State (UI-only)
- Page loading/error states.
- Local form state/validation.
- Copy-link feedback state.
- Section expand/collapse state.

## Server-driven State
- Current role, membership status, invite status, season/event data.
- Action authorization outcomes (promote, invite create/revoke, create season/event).

## Permission Matrix (MVP)
- Authenticated user:
  - Can view `/leagues` and leagues they belong to.
  - Can create league.
- Active member:
  - Can view league overview, members, schedule.
- Admin:
  - Member permissions plus create/revoke invite links, promote members, create seasons, create one-off events.
- Non-member:
  - Cannot access league detail except via valid join token flow.

## Empty/Error States
- No leagues: clear CTA to create.
- No members beyond self: prompt admin to share invite link.
- No seasons/events: admin sees setup CTA; non-admin sees informational empty state.
- Invite token invalid/expired/revoked: dedicated join error state.

## Minimality Guardrails
- No leaderboard or scoring UI in MVP.
- No public league discovery UI in MVP.
- No game-rule controls embedded in league screens.
- No additional visual complexity beyond existing app conventions.

## API Integration Expectations (UI-facing)
UI expects endpoints for:
- Create/list/get leagues
- List members and role metadata
- Promote member to admin
- Create/list/revoke invite links
- Join league by token
- Create/list seasons
- Create/list one-off events

All actions should support optimistic button disable + authoritative server response handling.