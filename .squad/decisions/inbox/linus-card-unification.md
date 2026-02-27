### 2026-02-26: Unified card styling across league UI
**By:** Linus (Frontend Dev), requested by Rob Gibbens
**What:** All "card" collection items (event rows, active game cards, season rows, league club cards) now share a consistent visual treatment: thin grey border, left primary-color accent (`border-left: 3px solid var(--primary)`), and border-based hover glow (`box-shadow: 0 0 0 1px var(--primary)`). Shadow-based hover on `.league-club-card` was replaced with border-based hover. CSS-only changes in `app.css` — no Razor markup modified.
**Why:** Rob requested visual consistency across all card-like list items in the league pages.
