# Decision: Create Dropdown on League Detail Header

**Date:** 2026-02-24  
**Author:** Arwen (Frontend Dev)  
**Requested by:** Rob Gibbens

## Context
The League Detail page had a single "Create" button that opened a season modal. The goal was to replace it with a dropdown that lets managers choose between creating a Cash Game or Tournament.

## Decision
- Extracted one-off event modal into `CreateOneOffEventModal.razor` under `LeagueDetailTabs/`.
- The new component owns form state, validation, and API call — errors display inline within the modal rather than the parent banner. This keeps the modal self-contained.
- The parent page tracks only `_showOneOffModal` and `_oneOffType`, delegates everything else to the child.
- Click-outside dismissal uses a transparent fixed backdrop element (pure Blazor, no JS interop).
- The Schedule tab's "Create Event" button still works — it opens the modal defaulting to `GameNight`.

## Files Changed
- `LeagueDetail.razor` — replaced Create button with dropdown, swapped inline modal for component reference, simplified one-off state/methods
- `LeagueDetailTabs/CreateOneOffEventModal.razor` — new extracted component
- `wwwroot/app.css` — dropdown positioning, backdrop, item hover styles
