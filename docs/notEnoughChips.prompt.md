# Plan: Chip Coverage Check Before Kings and Lows Hands

Before each Kings and Lows hand, verify all players can cover the pot; pause the game for up to 2 minutes if any player is short, allowing them to add chips or be auto-dropped.

## Steps

1. **Extend Game.cs entity** with pause tracking fields: `IsPausedForChipCheck`, `ChipCheckPauseStartedAt`, and `ChipCheckPauseEndsAt` to track when the game is waiting for players to add chips.

2. **Modify StartHandCommandHandler.cs** to add chip coverage validation before dealing. After the existing ante check (lines 70-97), calculate current pot from Pots table and compare each player's `ChipStack` against `CurrentPot`. If any player is short, set the pause flag and return early without dealing.

3. **Create new API endpoint `ResumeAfterChipCheck`** in a new handler that checks if the pause timer expired or all players now have sufficient chips, then either resumes dealing or marks short players for auto-drop.

4. **Extend ActionTimerService.cs** to support a 2-minute "chip check pause" timer. When expired, invoke AutoActionService.cs to set `AutoDropOnDropOrStay = true` for each short player.

5. **Create `ChipCoveragePauseOverlay.razor` component** in CardGames.Poker.Web/Components/TablePlay/Overlays/ showing: short players list, countdown timer (2 minutes), and prominent link to the Dashboard's ChipManagementSection.razor for adding chips.

6. **Modify TablePlay.razor** to render `ChipCoveragePauseOverlay` when game state indicates `IsPausedForChipCheck = true`, and broadcast updates via SignalR when players add chips during the pause.

7. **Extend GameStateDto and SignalR hub** to broadcast the chip check pause state including short players, pause end time, and real-time updates when chip stacks change during the pause.

8. **Modify DropOrStayCommandHandler** to automatically record "Drop" for any player flagged with `AutoDropOnDropOrStay` when transitioning to the DropOrStay phase.

9. Add Entity Framework Migration
