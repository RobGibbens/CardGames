# Dashboard Control Panel (Left Side) — Requirements

## 1. Goal
Create a left-side **Dashboard control panel** in the Blazor UI that mirrors the existing right-side **Odds overlay** interaction model (slide-in/slide-out, expandable/collapsible). The Dashboard provides a compact, always-available control surface with **vertically collapsible sections**:

1. **Leaderboard**
2. **Odds** (reusing the existing odds panel content)
3. **Hand History**

The Dashboard must:
- Be anchored to the **left** edge of the screen.
- Be **expandable/collapsible** (open/close) via a handle/button.
- Contain **multiple sections** that can each be expanded/collapsed independently.
- Be styled and animated consistently with the existing right-side odds overlay.

Non-goal: inventing new poker logic/statistics. This feature is primarily UI composition + wiring to existing data sources; any missing data should be surfaced as TODOs and backed by well-defined interfaces/events.

---

## 2. Terminology
- **Dashboard**: The entire left slide-out panel.
- **Panel**: A slide-in container (Dashboard panel on left; Odds panel on right already exists).
- **Section**: A vertically stacked, collapsible sub-area inside the Dashboard.
- **Leaderboard entry**: A row representing a player and stats.
- **Hand history entry**: A row/event describing an outcome of a completed hand.

---

## 3. UX / UI Behavior

### 3.1 Global layout
- Dashboard is fixed-position (or otherwise overlayed) so it can be opened regardless of page scroll.
- When closed, it should collapse to a narrow **edge handle** on the left.
- When open, it should not push the main play surface unless the existing layout already does. Prefer overlay (like the odds overlay).

### 3.2 Open/close interactions
- Provide a **toggle affordance**:
  - A handle/tab on the left edge.
  - Must be clickable/tappable.
  - Must be keyboard-accessible.
- Smooth animation for open/close:
  - Slide from left `transform: translateX(...)`.
  - Duration and easing should match the existing odds overlay.
- Close behavior:
  - Toggle button closes.
  - Optional: click-outside closes (only if the odds overlay behaves similarly; maintain parity).
  - Optional: `Esc` closes (accessibility).

### 3.3 Size and responsiveness
- Default width when open:
  - Desktop: ~320–420px (align with odds overlay width).
  - Small screens: max 70–85vw.
- Height:
  - Full available height of viewport.
- Content scrolling:
  - Dashboard body should scroll vertically if sections exceed available height.
  - Section headers remain visible as you scroll only if it matches existing design; otherwise standard scrolling.

### 3.4 Visual consistency
- Use existing component styles/tokens:
  - Colors, borders, shadows, rounded corners.
  - Typography and spacing.
- Match the odds overlay in:
  - Shadow intensity.
  - Backdrop usage (if any).
  - Header style (if any).

---

## 4. Dashboard Structure

### 4.1 Component breakdown
Create reusable building blocks. Recommended:

- `DashboardPanel` (new):
  - Responsible for slide-in/out behavior.
  - Contains a vertical stack of sections.
  - Holds open/closed state.

- `DashboardSection` (new):
  - Collapsible section with header + body.
  - Supports expand/collapse.
  - Supports optional "right-side header actions" area.

- `LeaderboardSection` (new content) (may be a specialized section or a component used inside `DashboardSection`).
- `OddsSection` (new wrapper) that reuses the existing Odds overlay content.
- `HandHistorySection` (new content).

The implementation may choose to make each section a self-contained component for data binding and UI.

### 4.2 Section expand/collapse behavior
- Each section has:
  - Header row with title and a chevron icon.
  - Body content collapses/expands.
- Default expanded state:
  - `Leaderboard`: expanded
  - `Odds`: collapsed or expanded (match user expectation; recommend expanded if it’s frequently referenced)
  - `Hand History`: expanded
- Persistence:
  - Expanded states should persist during navigation within the same session (recommend: scoped state service or local storage).

---

## 5. Data Requirements

### 5.1 Leaderboard section

#### 5.1.1 Required fields per player
For each current player seated in the game:
- `PlayerId` (stable identifier)
- `DisplayName`
- `ChipCount` (current stack)
- `HandsWonPct` (percentage of hands won)
- `ShowdownsSeenPct` (percentage of times player saw showdown)

Leaderboard sorting:
- Primary: `ChipCount` descending.
- Secondary: `DisplayName` ascending (stable ordering).

Display formatting:
- Chip count: integer with separators.
- Percentages: `0–100%` with 0–1 decimal places.

#### 5.1.2 Definitions (important)
- **Hands won %**:
  - `handsWon / handsPlayed * 100`
  - `handsPlayed` should include hands where player was dealt in (or participated) based on existing game definition.
- **Showdowns seen %**:
  - `showdownsSeen / handsPlayed * 100`
  - A showdown is counted when the hand reaches showdown and the player is still in at showdown (or revealed cards) based on existing game events.

If the engine already computes these metrics, bind to existing values. If not:
- Add a small stats accumulator service fed by game events.

#### 5.1.3 Edge cases
- No players: show empty state "No players".
- Player with `handsPlayed = 0`: show `—` or `0%` (choose one consistently; recommend `—`).
- Ties in chips: stable sort secondary criteria.

### 5.2 Odds section
- Must show the same information currently displayed in the right-side odds overlay.
- Prefer to **reuse the same component** that renders odds (do not duplicate markup).
- If the existing odds overlay is a container + content, extract the content into a reusable component:
  - `OddsPanelContent` (existing or new), used by both overlays.

### 5.3 Hand history section

#### 5.3.1 Required entry fields
Each entry represents a completed hand (or meaningful event) and must include:
- `HandNumber` or timestamp (ordering)
- Winner(s):
  - Winner name(s)
  - Amount won (if available)
- Winning hand description:
  - e.g., "Two Pair (Aces and Tens)"
  - Or "Flush (Ace high)"
- Current player’s hand at the time:
  - hole cards
  - or a textual label if cards are hidden (depends on game rules)

#### 5.3.2 Ordering, retention, and truncation
- Newest entries at top.
- Retain last `N` entries (recommend `N=20–50`).
- Provide "Clear" button only if that fits existing product patterns.

#### 5.3.3 Empty state
- If no hands have completed: display "No hands yet".

---

## 6. State Management Requirements

### 6.1 Open/close state
- Dashboard open/close state should be stored centrally so other components can toggle it if needed.
- Suggested:
  - A scoped UI state service: `DashboardState` with `IsOpen` property and `Toggle()`.

### 6.2 Section expand/collapse state
- Each section should maintain `IsExpanded`.
- Suggested:
  - `DashboardState` holds a dictionary keyed by section id: `Leaderboard`, `Odds`, `HandHistory`.

### 6.3 Persistence
- Persist in browser per user:
  - `localStorage` keys, e.g. `dashboard.isOpen`, `dashboard.section.leaderboard`.
- Optional: persist per table/game instance.

---

## 7. Accessibility Requirements
- Toggle handle/button:
  - Must be a `button` element with `aria-expanded` and `aria-controls`.
  - Must be reachable via Tab.
- Sections:
  - Each section header is a button.
  - Use `aria-expanded` and `aria-controls`.
- Keyboard support:
  - `Enter`/`Space` toggles.
  - Optional: `Esc` closes panel.
- Reduced motion:
  - Respect prefers-reduced-motion (reduce/disable animation).

---

## 8. Styling and CSS Requirements
- Add CSS in the existing styling approach used by the odds overlay (project conventions):
  - If odds uses `*.razor.css` (scoped CSS), use the same per-component.
  - If odds uses shared stylesheet, follow that.

### 8.1 CSS behaviors
- Panel open/closed:
  - Use transform translateX for smoothness.
  - Closed state should keep a small interactive strip visible.
- Z-index:
  - Ensure it overlays the table content but does not break modals.

---

## 9. Integration Points (LLM investigation required)
The implementation must identify existing components/services in the workspace:

- Existing right-side odds overlay component(s)
  - Determine the container and inner content.
  - Refactor into reusable content component if needed.

- Current game state source
  - Player list, chip counts.
  - Events for hand completion.
  - Odds data.

- Any existing UI state services
  - Prefer reusing existing patterns rather than introducing new ones.

Deliverable includes updating relevant layout/root component to render `DashboardPanel` globally (likely in `CardGames.Poker.Web`).

---

## 10. Acceptance Criteria

### 10.1 Panel behavior
- Dashboard appears on left edge on relevant pages (where the odds overlay exists).
- It opens and closes with animation.
- It is keyboard accessible.

### 10.2 Section behavior
- Each of the three sections expands/collapses independently.
- Section state persists (at least during the session).

### 10.3 Leaderboard
- Shows all current players.
- Sorted by chip count descending.
- Shows hands won % and showdowns seen %.
- Handles 0-hand edge cases.

### 10.4 Odds section
- Displays the same odds content as the existing right overlay.
- No duplication of odds rendering logic.

### 10.5 Hand history
- Shows recent hand outcomes.
- Newest-first.
- Includes winner(s), winning hand text, and current player hole cards (or fallback if not available).

---

## 11. Implementation Checklist (Do This)

### 11.1 Discovery
- [ ] Locate the right-side odds overlay component in `CardGames.Poker.Web`.
- [ ] Identify CSS/animation approach used by odds overlay.
- [ ] Identify the data model/services providing:
  - [ ] current players + chip stacks
  - [ ] odds data
  - [ ] hand completion results / history

### 11.2 UI components
- [ ] Create `DashboardPanel` component with left slide-in/out behavior.
- [ ] Create `DashboardSection` component with header + collapsible body.
- [ ] Create `LeaderboardSection` UI (table/list).
- [ ] Create `HandHistorySection` UI (list of entries with truncation).
- [ ] Create `OddsSection` wrapper reusing existing odds content.

### 11.3 State
- [ ] Create or extend a UI state service `DashboardState`.
- [ ] Wire open/close toggle to the state.
- [ ] Wire per-section expanded state.
- [ ] Add persistence via `localStorage` if the project already uses JS interop/state persistence patterns.

### 11.4 Data wiring
- [ ] Add a `LeaderboardViewModel` mapping layer if needed.
- [ ] Implement/plug-in stats calculation if not available:
  - [ ] hands played
  - [ ] hands won
  - [ ] showdowns seen
- [ ] Implement/plug-in hand history source:
  - [ ] store last N events
  - [ ] update on hand finished event

### 11.5 Layout integration
- [ ] Add `DashboardPanel` to the main poker table page/layout component.
- [ ] Ensure it does not conflict with the existing odds overlay.

### 11.6 Quality
- [ ] Confirm no visual overlap or z-index issues with modals.
- [ ] Verify keyboard navigation and ARIA attributes.
- [ ] Validate behavior with reduced motion.

### 11.7 Testing
- [ ] Add/extend unit tests for stats computation (if added).
- [ ] Add snapshot/component tests if the repo has a pattern (optional).
- [ ] Manual test flows:
  - [ ] open/close panel
  - [ ] expand/collapse each section
  - [ ] verify leaderboard sorting
  - [ ] complete hands and verify history updates

---

## 12. Open Questions / TODOs (Resolve During Implementation)
- Where does the odds overlay get its data and UI content component?
- What constitutes "hands played" in this application?
- Are the required stats already computed anywhere? If so, where?
- What game event indicates hand completion and provides winner + hand ranking?
- Does the current player’s hole cards remain accessible after the hand ends?
- Should the Dashboard be visible during non-game routes/pages?

---

## 13. Out-of-Scope (Explicit)
- Changing the right-side odds overlay UX.
- Advanced filtering/search in hand history.
- Persisting history/stats server-side across sessions.
- Adding new poker evaluation logic (unless trivial glue code for existing evaluators).
- Implementing the leaderboard or hand history. These will be implemented separately once the UI framework is in place.
