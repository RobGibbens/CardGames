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
- 2026-02-19: Applied final My Clubs UX pass in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor` by converting stats badges to capsule pills with inner count circles (Clubs, Manager/Admin aggregate, Pending with muted/highlight states), updating each club card to H2 title + role oval (`Manager/Admin` or `Member`), preserving description placement, adding clearer pre-CTA spacing, and keeping Open-only card actions and all existing behavior/data flow unchanged.
- 2026-02-19 (team update): Final-pass My Clubs implementation/review artifacts (`linus-my-clubs-pill-card-final-pass.md`, `tess-my-clubs-pill-card-final-review.md`) were merged from inbox into canonical `.ai-team/decisions.md`; use canonical entries as source of truth for any subsequent UI polish.
