# Edit Table — Requirements

## Summary
Add an **Edit Table** feature that allows authorized users to modify a poker table’s settings **only while the table has not started** (i.e., `Phase == WaitingToStart` or `Phase == WaitingForPlayers`). Once play begins (any other phase), settings become read-only and editing must be rejected by the API.

The feature spans:
- Database (persist table config + audit/concurrency)
- API (read/update endpoints + validation)
- SignalR (broadcast table-updated events)
- Blazor front end (UI for editing + live updates)

## Goals
- Allow table settings to be edited pre-start.
- Ensure changes are validated and persisted.
- Ensure all connected clients see updates in real time.
- Prevent updates once the table is beyond the allowed phases.

## Non-Goals
- Editing settings mid-hand or mid-game.
- New table creation flow changes (unless needed to support initial persistence).
- Table “start” logic changes beyond guarding against post-start edits.

## Definitions
- **Editable phases**: `WaitingToStart`, `WaitingForPlayers`
- **Table settings**: persisted fields that affect how a hand/game is configured (see `Editable Settings` section).

## User Stories
1. **As a host/table owner**, I can edit the table settings before the game starts so I can correct mistakes.
2. **As a player**, I see the updated table settings immediately without refreshing.
3. **As the system**, I reject edit attempts once the table is not in an editable phase.

## Permissions / Authorization
- Only a user with the appropriate role for the table may edit.
  - Recommended rule: **table owner/creator** or **user with “ManageTable” permission**.
- All users may read current table settings.

## Editable Settings (initial scope)
The exact fields should map to whatever the project already stores for a table/game configuration. The initial editable set should include:
- `Name` (display name)
- `GameType` (if multiple game types exist) — optional if currently locked
- `MaxPlayers`
- `SmallBlind`, `BigBlind`
- `Ante`
- `StartingChips` / `BuyIn` (whichever exists)
- Optional toggles that exist in current ruleset (e.g., `AllowStraddles`, etc.)

### Validation rules
- Settings must satisfy existing domain constraints.
  - `MaxPlayers` within allowed range
  - `BigBlind >= SmallBlind`
  - `Ante >= 0`
  - `StartingChips/BuyIn > 0`
- If rules depend on `GameType`, validate against that ruleset.

## State / Phase Rules
- Editing is allowed only if `Phase` is one of:
  - `WaitingToStart`
  - `WaitingForPlayers`
- Editing is disallowed for all other phases.
- If a change would invalidate current seating (e.g., lowering `MaxPlayers` below seated players), reject.

## Concurrency Rules
- Edits must require an optimistic concurrency token (e.g., `RowVersion`/ETag) to prevent overwriting someone else’s changes.
- If a concurrency conflict occurs, API returns a conflict response and the UI prompts the editor to refresh.

## Auditing
- Record:
  - who changed the table
  - when it changed
  - old/new values (or a change summary)

This can be lightweight at first (e.g., store `LastUpdatedByUserId`, `LastUpdatedUtc`) and extended later.

---

## Database Requirements

### Schema
Update the table persistence model to support editable configuration and concurrency.

1. **Table configuration fields**
   - Ensure the table entity/table-config entity persists all settings listed above.
   - If configuration is currently embedded in another entity, normalize only if needed.

2. **Concurrency token**
   - Add `RowVersion` (binary rowversion/timestamp or equivalent) to the table record.

3. **Audit fields**
   - Add `UpdatedUtc`
   - Add `UpdatedByUserId`
   - (Optional) `CreatedUtc`, `CreatedByUserId` if not present.

### Migration
- Create an EF Core migration (or equivalent) to add/alter schema.
- Backfill defaults for existing tables (if required).

---

## API Requirements (`CardGames.Poker.Api`)

### Contracts
Add/extend DTOs in `CardGames.Contracts` (or existing shared contracts location).

1. `TableSettingsDto`
   - Contains current settings + `Phase` + `RowVersion`.

2. `UpdateTableSettingsRequest`
   - Fields that can be updated.
   - Must include concurrency token (`RowVersion` or `If-Match` style ETag).

3. `UpdateTableSettingsResponse`
   - Updated `TableSettingsDto`.

### Endpoints
All endpoints must enforce phase + authorization.

1. **GET** `GET /tables/{tableId}/settings`
   - Returns `TableSettingsDto`
   - Used to populate edit UI.

2. **PUT** `PUT /tables/{tableId}/settings`
   - Body: `UpdateTableSettingsRequest`
   - Validations:
     - Table exists
     - Caller is authorized
     - Table phase is editable
     - Request passes domain validation
     - Concurrency token matches
   - Returns: updated `TableSettingsDto`

### Response codes
- `200 OK` on success
- `400 BadRequest` for validation errors
- `401/403` for auth failures
- `404 NotFound` if table missing
- `409 Conflict` if concurrency token mismatch
- `409 Conflict` (or `400`) if phase not editable (recommend `409` with clear error)

### Domain / Service layering
- Introduce an application service method like:
  - `UpdateTableSettingsAsync(tableId, userId, request, rowVersion)`
- Ensure the check “editable phase only” is performed in the domain/service layer (not only controller).

---

## SignalR Requirements (`CardGames.Poker.Events` / Web)

### Event
Add a new event that is emitted after a successful update.

- Name: `TableSettingsUpdated`
- Payload:
  - `TableId`
  - `UpdatedByUserId`
  - `UpdatedUtc`
  - `TableSettingsDto` (or a minimal diff if preferred)

### Broadcast
- Broadcast to all clients connected to the table’s group (e.g., `table:{tableId}`).
- The editor should also receive the event (so UI updates match server canonical state).

### Client handling
- Clients should update:
  - the table header/summary display
  - any lobby/table details panels
  - edit screen fields if open (and not currently dirty, or show conflict UI)

---

## Front End Requirements (`CardGames.Poker.Web` — Blazor)

### Navigation / Entry points
- Provide an “Edit Table” action/button where appropriate:
  - Lobby table details page
  - Table screen (only pre-start)

### Visibility rules
- Show “Edit Table” button only when:
  - user is authorized
  - `Phase` is editable

### UI: Edit Table page / modal
- Implement either:
  - dedicated route: `/tables/{tableId}/edit`
  - or a modal from table/lobby page

UI should include:
- Form fields for editable settings
- Current phase indicator
- Save / Cancel
- Validation messages

### Client-side validation
- Mirror server rules (ranges, required fields, relationships).
- Still rely on server validation as source of truth.

### Save behavior
- On Save:
  - call `PUT /tables/{tableId}/settings` with concurrency token
  - disable Save while request in-flight
  - on success, navigate back or show confirmation

### Real-time updates
- Subscribe to `TableSettingsUpdated` event.
- If user is currently editing and an update arrives:
  - If no local changes: refresh form
  - If local changes exist: show non-destructive warning “Settings changed elsewhere. Refresh to latest?”

### Error handling
- Phase no longer editable:
  - show message and disable form
  - offer navigation back to table view
- Concurrency conflict:
  - prompt refresh; show server values
- Validation errors:
  - show field-level messages

---

## Testing Requirements

### Unit tests
- Domain/service: update allowed phases only
- Validation: invalid values rejected
- Concurrency: mismatch produces conflict

### Integration tests
- API `PUT /tables/{id}/settings`:
  - success in editable phases
  - reject in non-editable phases
  - reject unauthorized

### UI tests (optional)
- “Edit Table” button visibility rules
- Successful save updates UI
- SignalR update refreshes table summary

---

## Telemetry / Logging
- Log each successful update with table id + user id.
- Log rejected edits due to phase or authorization.

---

## Open Questions
- Which exact settings are currently stored and should be editable now?
- Who is authorized to edit (creator only, seated players, admins)?
- Should changes be permitted if players are already seated in `WaitingForPlayers`?
- Should some settings become locked once any player is seated?
