# Linus History

## Project Learnings (from import)
- Project includes Blazor web app in src/CardGames.Poker.Web.
- Requested by: Rob Gibbens.
- UI should remain metadata-driven and consistent with architecture docs.

## Learnings

- 2026-03-10: SYN showdown delay must be enforced from shared SignalR table-state transitions, not only in local `HandleKeepOrTradeDecision`. In `TablePlay.razor`, when phase moves from KeepOrTrade/Reveal into showdown flow, schedule the existing 7-second delay via `ScheduleShowdownOverlayAfterDelay` so every client defers `TryLoadShowdownAsync` and overlay reveal consistently.

- 2026-03-10 (team update): SYN dealer-trade showdown-delay decision (`linus-syn-showdown-delay`) was merged from inbox into canonical `.squad/decisions.md`; use canonical entry as source of truth for follow-up TablePlay showdown/overlay timing changes.

- 2026-03-10: In `TablePlay.razor`, Screw Your Neighbor now applies a dealer-only, Trade-only 7-second showdown delay by reusing `_showdownDelayUntil` and a shared `ScheduleShowdownOverlayAfterDelay` helper. This prevents immediate showdown reveal after dealer deck-trade while keeping SignalR-driven showdown loading/overlay behavior unchanged for other games and decisions.

- 2026-03-07: Lobby Join now passes an explicit one-time URL intent (`?autojoin=1`) and `TablePlay` consumes that intent immediately after initial table/seat load to trigger `BeginAutoSeatJoinAsync` only once when the player is not already seated. This preserves Lobby no-chip gating, keeps existing full-table/race handling in join submit flow, and ensures buy-in modal appears on direct Lobby→Table joins.
- 2026-03-08 (team update): Lobby-triggered auto-seat join fix decision (`linus-lobby-autojoin-fix`) was merged from inbox into canonical `.squad/decisions.md`; use canonical entry as source of truth for follow-up Lobby→Table join behavior changes.
- 2026-03-08 (team update): Create-table no auto-enter decision (`linus-create-table-no-autojoin`) was merged from inbox into canonical `.squad/decisions.md`; keep create success navigation Lobby-first so join/seat remains explicit user intent.

- 2026-03-07: `TablePlay` join flow now auto-selects the lowest available seat index client-side before opening buy-in confirmation, because `JoinGameRequest` still requires explicit `SeatIndex`. This removes manual seat choice while preserving existing wallet-gated join checks and post-join phase/overlay behavior.
- 2026-03-08 (team update): Auto-seat join decision (`linus-auto-seat-join`) was merged from inbox into canonical `.squad/decisions.md`; use canonical entry as source of truth for follow-up TablePlay join UX work.

- 2026-03-07: Hold the Baseball frontend blind/pot issues were caused by blind-based game detection gaps in UI components, not by table rendering itself. `HOLDTHEBASEBALL` must be included anywhere blind-based controls/indicators are gated (`CreateTable`, `DealerChoiceModal`, `TableCanvas`, and edit-settings blind visibility checks) so SB/BB values are actually configured and displayed.
- 2026-03-07: Web client start-hand routing for Hold the Baseball should use its dedicated API endpoint (`/api/v1/games/hold-the-baseball/{gameId}/start`) via Contracts partial `IGamesApi` extension, keeping client mapping aligned with existing backend game-specific flow behavior.

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

- 2026-03-06: Reviewed `TablePlay.razor` controls strip architecture for redesign planning. Current strip mixes common actions, join buy-in controls, table metadata, host controls, and connection status in a single horizontal container (`.table-controls-strip`), which creates density and wrapping issues on small screens.
- 2026-03-06: Confirmed existing draggable overlay pattern consistency via `DrawPanel.razor` and `DropOrStayOverlay.razor` (`draggablePanel.init` + `drag-handle`) and identified this as the lowest-friction pattern to reuse for table/game metadata.
- 2026-03-06: Noted table info styling debt in `wwwroot/app.css` (`.table-blinds-pill` and `.table-blinds-line`) with hard-coded yellow/red colors and global placement near file top, contributing to visual inconsistency in the strip.
- 2026-03-06: Recommended MVP direction: keep strip focused on common controls and move full table/game metadata (title, game type, ante/SB/BB/min bet, game-specific rule fields) into a draggable metadata overlay component aligned with DrawPanel/DropOrStay interaction patterns.
- 2026-03-06 (team update): Linus/Tess table-controls-strip alternatives were merged into canonical `.squad/decisions.md` as one deduped decision; decision inbox entries were cleared.
- 2026-03-06: Implemented Option 2 in `TablePlay.razor` by removing inline metadata from `table-controls-strip`, adding an accessible `Game Info` toggle (`aria-expanded` + title), and introducing draggable `GameInfoOverlay` in `Components/Shared` using existing `draggablePanel.init` pattern. Overlay now surfaces table name, game type/Dealer's Choice context, Hold'Em SB/BB or non-Hold'Em ante/min bet, plus available state-driven context (hand/phase, Dealer's Choice dealer, draw rules, wild cards) without new backend calls.
- 2026-03-06 (team update): Option 2 implementation completion decision was merged from `.squad/decisions/inbox/linus-option2-implementation.md` into canonical `.squad/decisions.md`; inbox item cleared.
- 2026-03-06: Completed focused narrow-screen polish pass for Option 2 top controls + Game Info overlay.
  - Added a scoped class hook (`join-buy-in-controls`) in `TablePlay.razor` only for responsive targeting; no behavior or feature changes.
  - In `wwwroot/app.css`, reduced strip crowding on tablet/phone by neutralizing bootstrap `ms-2` offsets in the strip at narrow widths, allowing buy-in controls to wrap as one grouped row, and tightening spacing rhythm.
  - Preserved host-control readability on narrow screens with stronger wrap behavior and minimum button footprint; retained existing actions and host gating exactly as-is.
  - Kept connection indicator visible/stable by making right-side layout grid-based on mobile with a dedicated indicator slot.
  - In `GameInfoOverlay.razor.css`, improved small-screen usability via viewport-bounded panel/body heights, internal scrolling, safer text wrapping, and stacked key/value rows on very small phones.
  - Validation: `dotnet build src/CardGames.Poker.Web/CardGames.Poker.Web.csproj` succeeded (warnings only, pre-existing).

- 2026-03-06: Started Omaha PRD Phase 0 frontend hardening with focused web-UI/router changes only (no domain-rule migration).
  - `CreateTable.razor`: switched availability gating to game-code checks and enabled `OMAHA`; generalized blind gating helper so both `HOLDEM` and `OMAHA` use blind inputs.
  - `DealerChoiceModal.razor`: expanded blind-based selection logic to include `OMAHA` so Dealer's Choice prompts blinds for Omaha hands.
  - `TablePlay.razor`: added `IsOmaha` branching, Omaha start path via generic start endpoint, Omaha showdown path via generic showdown endpoint, and blind-context display parity in Game Info overlay.
  - `TableCanvas.razor`: generalized SB/BB indicator gating from Hold'Em-only to blind-based (Hold'Em + Omaha).
  - `Services/IGameApiRouter.cs`: added explicit `OMAHA` betting/draw routes to avoid unsafe fallback to Five Card Draw; Omaha betting currently follows the same endpoint path as Hold'Em while Omaha draw is explicitly unsupported.
  - Added focused web router coverage in `src/Tests/CardGames.Poker.Tests/Web/GameApiRouterTests.cs` for Omaha betting and draw routing behavior.
- 2026-03-06 (cross-agent sync): Scribe merged Omaha Phase 0 decisions from Basher/Danny/Linus into canonical `.squad/decisions.md` with one consolidated Omaha Phase 0 session log entry.
- 2026-03-06: Updated `TablePlay.razor` join UX to modal-first buy-in selection triggered by empty-seat click. Flow now fetches cashier balance before join, opens a focused buy-in modal with slider + numeric input bound to the same value, caps modal max buy-in by fetched account balance, and keeps existing `NoChipsModal` behavior for zero-or-less balance. Removed conflicting always-visible top-strip buy-in controls and reused existing confirm-dialog styling patterns for minimal visual change.

- 2026-03-07: Added Irish Hold'Em (IRISHHOLDEM) UI branch points across all game-type-aware Blazor files:
  - `CreateTable.razor`: added IRISHHOLDEM to `IsBlindBasedGame` pattern match.
  - `DealerChoiceModal.razor`: added IRISHHOLDEM to `IsBlindBasedGame` OrdinalIgnoreCase check.
  - `TableCanvas.razor`: added IRISHHOLDEM to `IsBlindBasedGame` property.
  - `TablePlay.razor`: added `IsIrishHoldEm` property, included in `UsesCardDealAnimation`, `GameInfoOverlay IsHoldEm` binding, `StartHandAsync` (generic start, same as Omaha), and `TryLoadShowdownAsync` (generic showdown).
  - `IGameApiRouter.cs`: added `IrishHoldEm` constant, betting route (reusing Hold'Em endpoint, same as Omaha), and real draw route via `IGamesApi.IrishHoldEmDiscardAsync` for the post-flop discard phase.
  - Created `src/CardGames.Contracts/IrishHoldEmDiscardExtensions.cs`: partial `IGamesApi` extension for `/api/v1/games/irish-hold-em/{gameId}/discard` endpoint (same partial-interface pattern as `GenericStartHandExtensions.cs`).
  - `DashboardHandOddsCalculator.cs`: added IRISHHOLDEM branch using Omaha-style odds (4 hole cards pre-discard) or Hold'Em-style odds (2 hole cards post-discard).
  - Irish Hold'Em uses blinds (not ante), deals 4 hole cards, has a post-flop discard-2 phase, then continues as Hold'Em. DrawPanel auto-activates when phase category is "Drawing" — no DrawPanel changes needed as MaxDiscards is server-driven.
  - Build verified: 0 errors.

- 2026-03-07: Added `MinDiscards` parameter to `DrawPanel.razor` for enforcing mandatory discard counts (Irish Hold'Em requires exactly 2).
  - `DrawPanel.razor`: new `[Parameter] public int MinDiscards { get; set; } = 0;` — controls subtitle, selection hint, stand pat disable, discard button disable, button label ("Discard N" vs "Discard N & Draw"), warning message, and both `HandleStandPat`/`HandleDiscard` guards.
  - When `MinDiscards == MaxDiscards`, subtitle reads "Select exactly N cards to discard" and discard button omits "& Draw" (Irish discards only, no replacements).
  - When `MinDiscards > 0`, stand pat button is disabled (players must discard).
  - Warning shown if `0 < SelectedCount < MinDiscards`.
  - `TablePlay.razor`: added `GetMinDiscards()` returning 2 for Irish Hold'Em, 0 otherwise; added Irish safety override in `GetMaxDiscards()` returning 2.
  - `DrawingConfigDto` in Contracts does not have `MinDiscards` yet — used game-type check (`IsIrishHoldEm`) as fallback.
  - Key files: `src/CardGames.Poker.Web/Components/Shared/DrawPanel.razor`, `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`.
  - Build verified: 0 errors.- 2026-02-18 (Irish Hold 'Em Phase 2): Added MinDiscards to DrawPanel.razor for enforced 2-card discard. Wired from TablePlay.razor via IsIrishHoldEm check. Part of Phase 2 staged deploy session.
- 2026-03-08: Create-table success flow now returns to `/lobby` instead of navigating to `/table/{gameId}`. This prevents immediate table-entry side effects (including auto-join/auto-seat intent handling on the table page) and keeps join as an explicit action from Lobby.
- 2026-03-10: Dealer's Choice variant picker now keeps over-cap variants visible but disabled when `MaximumNumberOfPlayers` is less than current active seats (`IsOccupied && !IsSittingOut`), with inline max-vs-active messaging; active count is passed from `TablePlay.razor` into `DealerChoiceModal` as `CurrentActivePlayerCount`.