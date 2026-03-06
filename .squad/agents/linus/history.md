# Linus History

## Project Learnings (from import)
- Project includes Blazor web app in src/CardGames.Poker.Web.
- Requested by: Rob Gibbens.
- UI should remain metadata-driven and consistent with architecture docs.

## Learnings

- Added per-card deal sound in `CardGames.Poker.Web` using private-state deltas so only the current client hears sounds for their own newly received cards.
- For deal-animation variants, hooked playback at per-card animation time for the local seat to preserve one-sound-per-card behavior during staged dealing.
- Implemented audio format preference in `wwwroot/audio.js` by selecting `.ogg` when playable and falling back to `.mp3`.
- Reprioritized auth UX in `Login.razor` and `Register.razor` to present `ExternalLoginPicker` first as the primary sign-in/sign-up path, while keeping local account forms and all existing behaviors intact as secondary fallback flows.
- Decision consolidated to `.ai-team/decisions.md` as canonical record for OAuth-first auth page prioritization.
- Applied a minimal copy-only pass on auth pages to explicitly frame external providers as the preferred path and local credentials as fallback, with no layout or logic changes.

- Produced `docs/LeaguesUXDesign.md` defining a minimal Blazor Leagues UX with explicit entry routes (`/leagues`, `/leagues/{leagueId}`, `/leagues/join/{token}`), create/join/invite/admin flows, and schedule coverage for both seasons and one-off events.
- Kept league UX server-authoritative for permissions and state (role badges, admin-only actions, invite validity, membership activity) to avoid client-side policy drift.
- Preserved architecture boundaries by treating Leagues as account-scoped social coordination UI and explicitly excluding game-rule controls and leaderboard/scoring complexity from MVP screens.
- Leagues UX-aligned decisions are now consolidated in canonical `.ai-team/decisions.md`, including server-driven role gating and invite lifecycle expectations used by UI flows.
- 2026-02-18 (#216/#224 sync): Leagues governance operations and quality-gate semantics are now canonicalized; UI/admin affordances should align with manager/admin invariants and keep moderation action expectations consistent with API integration test journeys.
- 2026-02-19 (team update): league role projection now normalizes current-user `Owner` to `Manager` in create/detail/my-leagues API responses; keep invite/admin gating coherent for `Owner`/`Manager`/`Admin` across list and detail flows.
- 2026-02-19: Applied a class-only visual refresh to the `My Clubs` section in `Leagues.razor` (header rhythm, KPI tiles, quick switch cohesion, list-row polish, neutralized non-critical badges) while preserving all bindings, events, text, responsive behavior, and domain logic unchanged.
- 2026-02-19 (team update): My Clubs refresh constraints are now canonical in `.ai-team/decisions.md`; continue using utility-class-only polish with no feature, behavior, API, or permission-surface changes.
- 2026-02-19: Completed second-pass `My Clubs` polish in `Leagues.razor` by grouping KPIs + quick switch in one bordered container, tightening spacing rhythm (`g-2`, `p-2`, `py-2 px-3 gap-2`), strengthening KPI emphasis (`fw-bold`), switching quick-switch CTA to outline style, and adding mobile-friendly action widths (`w-100 w-sm-auto`) while preserving all existing behavior and bindings.
- 2026-02-19 (team update): My Clubs second-pass implementation and design-review artifacts were merged from inbox into canonical `.ai-team/decisions.md`; treat that record as source of truth for scope-safe follow-up polish.
- 2026-02-19: Implemented a full My Clubs IA redesign in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor` with command-header summary, top priority strip (pending workload + manage-capable count + quick open), and role-bucketed club lists (Manager/Owner, Admin, Member) while preserving all existing handlers, data bindings, and loading/empty/pending logic.
- 2026-02-19 (team update): Full-redesign directive/spec/implementation/review artifacts for My Clubs were merged from inbox into canonical `.ai-team/decisions.md`; treat canonical record as source of truth for any follow-up refinements.
- 2026-02-19: Reworked only the `My Clubs` section in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor` into a card-grid command-center baseline with icon-only refresh header, Total/Admin/Pending pills, Quick Open removal, per-card Open-only CTA, warm manager/admin role badges, and truncated descriptions, while preserving loading/empty states and existing data/handler wiring.
- 2026-02-19 (team update): Command Center implementation and review artifacts were merged from inbox into canonical `.ai-team/decisions.md`; treat canonical record as source of truth for this redesign pass.
- 2026-02-26: Redesigned `.lobby-tabs` from pill-shaped buttons (rounded container, gradient active fill, box-shadow) into proper tab navigation with a bottom-border rail, 3px red bottom-border active indicator, folder-tab top border-radius (8px), and no background fill on active. Kept responsive icon-only mobile behavior, badge logic, and all design-system variables. CSS-only change in `src/CardGames.Poker.Web/wwwroot/app.css`; no markup edits.
- 2026-02-19: Applied final My Clubs UX pass in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor` by converting stats badges to capsule pills with inner count circles (Clubs, Manager/Admin aggregate, Pending with muted/highlight states), updating each club card to H2 title + role oval (`Manager/Admin` or `Member`), preserving description placement, adding clearer pre-CTA spacing, and keeping Open-only card actions and all existing behavior/data flow unchanged.
- 2026-02-19 (team update): Final-pass My Clubs implementation/review artifacts (`linus-my-clubs-pill-card-final-pass.md`, `tess-my-clubs-pill-card-final-review.md`) were merged from inbox into canonical `.ai-team/decisions.md`; use canonical entries as source of truth for any subsequent UI polish.
- 2026-02-26: Unified card UI across league pages in `app.css`. Applied consistent border + left accent (`border-left: 3px solid var(--primary)`) + hover glow (`box-shadow: 0 0 0 1px var(--primary)`) to six card element types: `.league-detail-event-row`, `.league-detail-active-game-card`, `.league-detail-season-row`, `.league-club-card`. Removed `box-shadow: var(--card-shadow)` from `.league-club-card` in favor of border-based hover. CSS-only change — no Razor markup modifications. Key file: `src/CardGames.Poker.Web/wwwroot/app.css`.
- 2026-03-01: Moved community cards (The Good, The Bad, The Ugly) from a vertical stack below the deck to a horizontal row beside the deck in the table center. Added `.table-center-row` flex container wrapping deck + community cards. Pot and phase indicator remain above as a vertical column. Removed `margin-bottom` from `.deck-stack` and `margin-top` from `.community-cards` since they're now siblings in a row. Key files: `src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor`, `src/CardGames.Poker.Web/wwwroot/app.css`.
- 2026-03-02: Produced comprehensive Dealer's Choice UI design doc at `docs/DealersChoiceUIDesign.md`. Key design decisions:
  - DC is a client-side hardcoded variant card in CreateTable (code `DEALERS_CHOICE`), not an API-returned game type — keeps game rules in domain, table mode in API/UI.
  - Between-hands flow inserts a `DealerChoiceRequired` phase after showdown where the dealer picks game/ante/minBet via overlay modal.
  - Two new Blazor components: `DealerChoiceModal.razor` (dealer sees) and `DealerChoiceWaiting.razor` (others see), following existing `table-overlay` pattern.
  - Two new SignalR events: `DealerChoiceRequired` and `DealerChoiceMade`, with DTOs in `Contracts/SignalR/`.
  - `_gameTypeCode` on `TablePlay.razor` updates per hand from `DealerChoiceMadeDto`, so all existing game-specific UI switches (draw panel, wild cards, overlays) work unchanged.
  - Ante/min bet in info strip update per hand from SignalR state for DC tables.
  - 60-second dealer timeout with server auto-fallback to previous hand's game type.
  - Key files analyzed: `CreateTable.razor`, `TablePlay.razor`, `GameHubClient.cs`, `TableStatePublicDto.cs`, `ShowdownOverlay.razor`.

- 2026-03-05: Implemented PRD Phase 2 items 4.8, 4.9, 4.10, 4.12 for Texas Hold 'Em visual enhancements:
  - **4.8 Community Card Labels**: Extended `GetCommunityCardLabel()` in `TableCanvas.razor` to return Flop/Turn/River labels for HOLDEM games; added `.holdem` CSS class to community-cards div; added `margin-left: 0.75rem` gap for turn/river cards via `nth-child(n+4)`.
  - **4.9 SB/BB Position Indicators**: Added `IsSmallBlind`/`IsBigBlind` parameters to `TableSeat.razor`; computed blind seat indexes in `TableCanvas.razor` from dealer position + occupied seats (with heads-up rule); styled as 28px radial-gradient badges (blue for SB, red for BB) in `TableSeat.razor.css`.
  - **4.10 Street Progress Indicator**: Added horizontal breadcrumb bar above `table-center-row` showing Pre-Flop/Flop/Turn/River/Showdown phases with current/past highlighting; only visible for HOLDEM games.
  - **4.12 Community Card Deal Animation**: Added `community-card-fly-in` keyframe animation with staggered delays per card slot; scoped to `.community-cards.holdem` parent to avoid breaking GBU display.
  - All changes are CSS + Blazor parameter-driven; no game rules moved into UI logic.
  - Key files: `TableCanvas.razor`, `TableSeat.razor`, `TableSeat.razor.css`, `app.css`.
  - `IsHoldEmGame` computed property gates all Hold'Em-specific rendering.
