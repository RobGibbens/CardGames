### 2026-02-26: Lobby tabs visual redesign
**By:** Linus (Frontend Dev), requested by Rob Gibbens
**What:** Replaced pill-shaped button tabs (`.lobby-tabs`) with proper tab navigation — bottom-border rail, 3px `var(--primary)` active indicator, 8px top-corner radius for folder-tab shape, no gradient/box-shadow on active. Badges stay functional with primary color on active tabs and muted on inactive.
**Why:** User feedback that tabs looked like buttons, not navigation tabs. CSS-only change, zero markup impact.
