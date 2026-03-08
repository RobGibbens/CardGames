# 2026-03-08: Create table flow does not auto-enter table

**By:** Linus (Frontend Dev)
**Requested by:** Rob Gibbens

**What:** Updated `CreateTable.razor` so successful table creation redirects to `/lobby` instead of `/table/{gameId}`.

**Why:** The direct redirect to table caused immediate table-entry behavior and could trigger auto-join/auto-seat intent handling on table load. Product behavior now requires explicit user intent to join after creation.

**Applied in:**
- `src/CardGames.Poker.Web/Components/Pages/CreateTable.razor`
