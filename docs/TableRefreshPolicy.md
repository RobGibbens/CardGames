# Table Page Refresh Policy

This document describes how the table page (`src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`)
decides when to re-read server state, so that the page does not perform duplicate or
over-broad fetches. The backend table-state read path (`TableStateBuilder`) is non-trivial,
so unnecessary full reloads matter.

The policy is encoded in code as the single source of truth:

- `src/CardGames.Poker.Web/Services/TableActions/TableRefreshPolicy.cs`
  - `TableInteraction` — the logical interaction families on the page.
  - `TableRefreshKind` — the four refresh strategies the page may use.
  - `TableRefreshPolicy.ResolveRefreshKind(interaction)` — maps interaction → strategy.

The page turns a strategy into a concrete action in one place:
`TablePlay.ApplyRefreshPolicyAsync(TableInteraction)`.

## The four refresh strategies

| Kind | Meaning | Concrete page action |
|------|---------|----------------------|
| **Full** | Re-read the whole table snapshot (game + players + rules). Expensive; bootstrap/fallback only. | `LoadDataAsync()` |
| **Slice** | Re-read one focused slice only. | `ApplySliceRefreshAsync(...)` (e.g. `RefreshOddsVisibilityFromServerAsync()`) |
| **HubDriven** | Do nothing now; the server broadcasts the authoritative update over a hub. | no-op (debug log) |
| **LocalOnly** | Apply a purely presentational/local transition, or the page is navigating away. | no-op (debug log) |

## Interaction → strategy

| Interaction | Strategy | Why |
|-------------|----------|-----|
| `InitialLoad` | Full | First render; the server snapshot is the only source. |
| `JoinRequestApproved` | Full | The player is newly seated; multiple slices changed and no prior push covers them. |
| `ManualStartFallback` | Full | Hub re-sync after manual start failed (no live connection); HTTP reload is the fallback. |
| `BettingAction` | HubDriven | The betting endpoint mutates state and the hub broadcasts the new public/private state. |
| `DrawDiscardAction` | HubDriven | Same as betting; the draw result arrives over the hub. |
| `SpecialVariantDecision` | HubDriven | Drop-or-stay / keep-or-trade / buy-card / pot-match results arrive over the hub. |
| `AddChips` | HubDriven | The cashier update is broadcast over the hub. |
| `SitOutToggle` | HubDriven | The server broadcasts the seat change; the toast is the only local effect. |
| `TableSettingsPush` | Slice | Only the settings/odds slice changed; re-read just that part. |
| `OddsVisibilityToggle` | LocalOnly | The authoritative value is already on the toggle action's response. |
| `LeaveTable` | LocalOnly | The page navigates away (immediate) or waits for the hand to end. |

## Concrete questions this policy answers

1. **What happens after a successful betting action?** Nothing is refetched directly; the
   page waits for the hub broadcast (`HubDriven`).
2. **When does the page perform a full refetch versus a focused refresh?** Full only on
   `InitialLoad`, `JoinRequestApproved`, and the `ManualStartFallback` path. Focused (`Slice`)
   for `TableSettingsPush`.
3. **When does the page wait for hub-driven updates?** For all gameplay actions and chips
   (`BettingAction`, `DrawDiscardAction`, `SpecialVariantDecision`, `AddChips`, `SitOutToggle`).
4. **Which flows update only local UI state?** `OddsVisibilityToggle` (value already on the
   response) and `LeaveTable` (navigation).
5. **Where is the policy implemented?** `TableRefreshPolicy.cs`, applied via
   `TablePlay.ApplyRefreshPolicyAsync`. Change the mapping in one place.

## Adding a new interaction

1. Add a value to `TableInteraction`.
2. Map it to a `TableRefreshKind` in `TableRefreshPolicy.ResolveRefreshKind`.
3. If it is `Slice`, add the focused fetch to `TablePlay.ApplySliceRefreshAsync`.
4. Call `ApplyRefreshPolicyAsync(...)` from the handler instead of inlining a fetch.
5. Add/extend the cases in `TableRefreshPolicyTests`.

## Out of scope (intentionally)

This is a fetch-policy rationalization, not a `TablePlay` rewrite or a backend redesign.
The page still uses the active dictionary-based router (`IGameApiRouter`); the legacy
`Services/GameApi` wrapper stack is not used. Per-variant animation and sound flows keep
their existing local sequencing.
