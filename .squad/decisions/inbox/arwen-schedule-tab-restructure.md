# Decision: Inline Season Events in Schedule Tab

**Author:** Arwen (Frontend Dev)  
**Date:** 2026-02-27  
**Status:** Implemented

## Context
The League Detail Schedule tab had seasons and season events in two separate card sections. Users had to mentally connect which events belonged to which season, creating a disjointed experience.

## Decision
Restructured `LeagueDetailScheduleTab.razor` so season events render **inline beneath their parent season row** within the same card. Added toggle behavior (View/Hide Events) and moved the "Create Event" button to appear next to the toggle when a season is expanded.

## Key Changes
- **LeagueDetailScheduleTab.razor**: Removed separate "Season Events" `<section>`. Events now render inside `league-season-events-inline` div within the season `@foreach` loop. Added `DeselectSeason` EventCallback and local `ToggleSeasonAsync` method for expand/collapse. "Create Event" button appears inline per-season for managers.
- **LeagueDetail.razor**: Added `DeselectSeasonAsync()` handler (clears selection + events list). Wired `DeselectSeason` parameter.

## Trade-offs
- Nested markup is deeper but the visual hierarchy is now correct.
- Only one season's events visible at a time (unchanged behavior, just relocated).
