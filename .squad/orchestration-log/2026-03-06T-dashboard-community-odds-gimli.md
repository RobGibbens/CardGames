# Orchestration Log: Gimli — Dashboard Community-Card Odds Fix

| Field | Value |
|-------|-------|
| **Agent routed** | Gimli (Backend Dev) |
| **Why chosen** | Dashboard odds bug required backend/game-logic alignment and odds-path correction in table-play flow. |
| **Mode** | sync |
| **Why this mode** | Tight coupling between odds selection logic and active UI state updates required direct verification before handoff. |
| **Files authorized to read** | `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`; `src/CardGames.Poker.Web/Services/DashboardHandOddsCalculator.cs`; `src/CardGames.Poker/Evaluation/OddsCalculator.cs`; related tests under `src/Tests/CardGames.Poker.Tests/Evaluation/` |
| **File(s) agent must produce** | `src/CardGames.Poker.Web/Services/DashboardHandOddsCalculator.cs` (new); `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` (community-aware odds wiring and public-state recalculation) |
| **Outcome** | Completed |

## Notes
- Fixed dashboard odds path to include visible community cards for community-card variants.
- Recalculation now runs when public table state updates so newly revealed board cards immediately affect displayed odds.
