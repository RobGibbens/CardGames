# Cashier Feature Design

## Objective
Introduce a dedicated **Cashier** experience where authenticated players can manage account chips outside active table play.

This feature adds:
- A new top-level menu item in authenticated navigation: **Cashier**.
- A new Cashier page with:
  - Account chip balance as the primary hero element.
  - Add chips flow reusing existing add-chips behavior patterns.
  - A paged ledger of chip transactions over time.

## Scope
- Web (Blazor): new route/page and nav entry.
- API (MediatR + endpoint map groups): account chip summary + transaction ledger + account chip add command.
- Contracts: DTOs and Refit interface additions for Cashier APIs.
- Persistence: account-level chip balance and immutable ledger records.

## Non-Goals
- No change to in-hand betting rules or game-phase logic.
- No redesign of existing in-table `ChipManagementSection` in `TablePlay`.
- No cross-currency/payment gateway integration (chips are in-app units only).
- No broad dashboard or lobby redesign beyond adding `Cashier` nav access.

---

## UX Flow

### 1) Navigation
1. User signs in and sees `AuthenticatedNavBar`.
2. User clicks new **Cashier** menu item.
3. Route navigates to `/cashier`.

### 2) Cashier Page Load
1. Page requests account cashier summary (balance + pending + recent metadata).
2. Page requests first ledger page (default newest-first).
3. User sees:
   - Hero balance card (highlight).
   - Add chips panel.
   - Ledger table.

### 3) Add Chips
1. User enters amount and submits.
2. API validates/authenticates and appends ledger record.
3. UI refreshes summary and prepends new ledger row (or refetches page 1).
4. Success/error message displayed inline.

### 4) Ledger Browsing
1. User pages through history (cursor or page/size).
2. Optional filter (phase 2): transaction type.

---

## Page Information Architecture

## Cashier Page (`/cashier`)

### A. Chip Balance Hero (top, primary focus)
- Large formatted balance (`N0` formatting, consistent with existing chips UI).
- Secondary metadata:
  - Last updated time.
  - Optional pending indicator if account has pending operations.

### B. Add Chips Panel (reuse existing flow semantics)
- Reuse amount validation rules from current add-chips behavior (`Amount > 0`).
- Reuse request/response UX pattern used by `ChipManagementSection` and `ChipCoveragePauseOverlay`:
  - Submit button disabled while request in progress.
  - Inline success/error messaging.
- Difference: target **account wallet endpoint**, not game-scoped endpoint.

### C. Ledger Table
- Columns:
  - Timestamp (UTC rendered in local format).
  - Type (e.g., Add, BuyIn, CashOut, Adjustment).
  - Delta (`+/-` chips).
  - Balance After.
  - Context (table name/game id/reference).
  - Note/Reason (optional).
- Sort: newest-first.
- Paging controls at bottom.
- Empty state: “No chip transactions yet.”

---

## Backend/API Design (MediatR + Contracts)

## Design Principles
- Follow existing API feature organization and MediatR command/query handlers.
- Keep endpoint contracts in `CardGames.Contracts` and expose through Refit.
- Keep game-scoped chip behavior intact; introduce account-scoped APIs in Profile domain.

## Proposed API Surface (v1)

### 1) `GET /api/v1/profile/cashier/summary`
- Returns account chip summary.

**Response contract (new):** `CashierSummaryDto`
- `CurrentBalance`
- `PendingBalanceChange` (optional, default `0`)
- `LastTransactionAtUtc`

### 2) `POST /api/v1/profile/cashier/add-chips`
- Adds chips directly to account balance and creates immutable ledger entry.

**Request contract (new):** `AddAccountChipsRequest`
- `Amount`
- `Reason` (optional, normalized server-side)

**Response contract (new):** `AddAccountChipsResponse`
- `NewBalance`
- `AppliedAmount`
- `TransactionId`
- `Message`

### 3) `GET /api/v1/profile/cashier/ledger`
- Returns paged account ledger entries.

**Query params**
- `take` (default 25, max 100)
- `skip` (or cursor)
- `type` (optional, phase 2)

**Response contract (new):** `CashierLedgerPageDto`
- `Entries: IReadOnlyList<CashierLedgerEntryDto>`
- `HasMore`
- `TotalCount` (optional if cursor-based)

## Handler Pattern
- `GetCashierSummaryQuery` + handler.
- `AddAccountChipsCommand` + handler.
- `GetCashierLedgerQuery` + handler.

Each endpoint remains thin and delegates to MediatR handlers, consistent with current API architecture.

---

## Data Model

## New entities (account-level)

### `PlayerChipAccount`
- `PlayerId` (PK/FK)
- `Balance`
- `CreatedAtUtc`
- `UpdatedAtUtc`
- RowVersion/concurrency token

### `PlayerChipLedgerEntry`
- `Id` (Guid)
- `PlayerId` (indexed)
- `Type` (enum/string)
- `AmountDelta` (signed int)
- `BalanceAfter`
- `OccurredAtUtc` (indexed desc)
- `ReferenceType` (nullable: Game/Manual/System)
- `ReferenceId` (nullable Guid)
- `Reason` (nullable)
- `ActorUserId` (who triggered it)

## Paging/Filtering Decisions
- Default to **offset paging** (`skip`/`take`) for v1 to match existing patterns (`GetHandHistory`).
- Keep response shape compatible with future cursor migration.
- Default order: `OccurredAtUtc DESC`, tie-breaker `Id DESC`.
- Optional filter in v1 if low-cost: `Type`; otherwise defer to phase 2.

---

## Security, Auth, Audit

- Require authenticated user on all cashier endpoints.
- Enforce ownership: user may only view/mutate their own account.
- Validate amount bounds server-side (positive, optional max-per-request).
- Log command execution with user id, amount, transaction id.
- Ledger rows are append-only (no updates/deletes in public flows).
- Include anti-abuse controls (rate limiting and idempotency strategy for retries).
- Preserve existing JWT/header-based auth setup; no new auth scheme.

---

## Phased Implementation Plan

### Phase 0: Contract & schema skeleton
- Add cashier DTOs in `src/CardGames.Contracts`.
- Add DB entities + migration for account + ledger tables.
- Add Refit methods (generated/manual extension path as used in repo).

### Phase 1: Read-only Cashier page
- Add `/cashier` page in `src/CardGames.Poker.Web/Components/Pages`.
- Add `Cashier` link in `src/CardGames.Poker.Web/Components/Shared/AuthenticatedNavBar.razor`.
- Implement summary + ledger read endpoints and wire page load.

### Phase 2: Add chips (account-level)
- Add `POST /profile/cashier/add-chips` command + handler.
- Reuse existing client-side amount validation/submit UX pattern.
- Refresh summary and ledger after success.

### Phase 3: Integration touchpoints
- Define/implement game buy-in source of truth (account wallet vs current game join inputs).
- Add ledger reference links to table/game context where available.

### Phase 4: Hardening
- Add rate-limit tuning, idempotency key support, and richer audit fields.
- Optional ledger filters and CSV export (future, not MVP).

---

## Testing Strategy

## Unit Tests
- Command/query handlers:
  - Amount validation and boundary conditions.
  - Ownership checks.
  - Correct balance/ledger math and ordering.
- Mapping tests for DTO contracts.

## Integration Tests (API)
- Endpoint auth behavior (401/403/200).
- `POST add-chips` writes both account balance and ledger atomically.
- `GET ledger` paging returns deterministic order.

## UI Tests (Blazor)
- Nav contains and routes to `Cashier`.
- Cashier page loads hero + ledger.
- Add chips form success/error states and reload behavior.

## Regression
- Ensure existing game/table chip flows (`AddChips` game endpoint, dashboard chip section) continue unchanged.

---

## Concise Wireframe (Markdown)

```text
┌────────────────────────────────────────────────────────────┐
│ Cashier                                                   │
├────────────────────────────────────────────────────────────┤
│ [Current Chip Balance]                                    │
│  12,500                                                   │
│  Last updated: 2026-02-17 14:02 UTC                       │
├────────────────────────────────────────────────────────────┤
│ Add Chips                                                 │
│  Amount: [   500 ]  [Add Chips]                           │
│  (success/error inline message)                           │
├────────────────────────────────────────────────────────────┤
│ Ledger                                                    │
│  Date/Time   Type     Delta   Balance After   Context     │
│  ...                                                       │
│  [Prev] [1] [2] [Next]                                   │
└────────────────────────────────────────────────────────────┘
```

---

## Open Questions / Risks

1. **Source of truth**: Should table buy-ins always debit account wallet, or remain game-local initially?
2. **Backfill**: Do we migrate historical game results into account ledger for existing users?
3. **Consistency model**: Is eventual consistency acceptable between game events and account ledger updates?
4. **Limits**: Product/business limits for add-chips amount and frequency are not yet defined.
5. **Admin adjustments**: Need separate privileged flow for manual corrections (likely future endpoint + audit reason required).

## Recommendation Summary
- Ship Cashier as an account-scoped vertical slice under Profile APIs.
- Reuse proven add-chips UX behavior, but separate game-phase-specific rules from account wallet behavior.
- Keep MVP focused: balance hero + add chips + paged ledger, then iterate on filtering and cross-game reconciliation.