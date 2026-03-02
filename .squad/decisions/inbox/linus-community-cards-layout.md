### 2026-03-01: Community cards moved to horizontal row beside deck
**By:** Arwen (Linus) — requested by Rob Gibbens
**What:** Restructured `.table-center` in TableCanvas.razor so the deck and community cards sit in a horizontal `.table-center-row` flex container, with pot and phase indicator stacked above. This prevents community cards from overlapping the bottom player's hand and uses the available horizontal space in the table center.
**Why:** Community cards for "The Good, the Bad, and the Ugly" were hidden behind the bottom player's hand in the previous vertical-only layout.
