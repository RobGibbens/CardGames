# 2026-03-06: Table join uses modal-driven buy-in selection with balance cap

**By:** Linus (Frontend Dev)
**Requested by:** Rob Gibbens

## What
Changed table join UX in `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` so clicking an empty seat no longer joins immediately. The flow now:
- fetches cashier balance first,
- blocks join with existing no-chips modal when balance is `<= 0`,
- opens a buy-in modal showing current account balance,
- provides both slider and numeric input bound to the same buy-in amount,
- enforces buy-in bounds via existing join buy-in helpers with max capped by fetched balance,
- submits join only from the modal confirm action using existing join endpoint.

Also removed the old always-visible top-strip buy-in controls and added minimal modal styling hooks in `src/CardGames.Poker.Web/wwwroot/app.css`.

## Why
Players need per-game control over how many cashier-managed chips they bring to a specific table. Modal-first seat join keeps this choice contextual to seat selection, prevents accidental joins, and preserves existing cashier/no-chips safeguards with minimal UI complexity.

## Notes
- Kept game rules out of UI logic; this is UI orchestration around existing join/cashier APIs.
- Build sanity validated for `src/CardGames.Poker.Web`.
