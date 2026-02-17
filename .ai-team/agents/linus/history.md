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
