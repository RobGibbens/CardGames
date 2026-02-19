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
