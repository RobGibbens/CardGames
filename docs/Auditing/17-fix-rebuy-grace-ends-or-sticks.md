# Fix: rebuy grace can end the game after a successful rebuy, or never end

Implement this document only. Cash rebuy grace is the pause in `ContinuousPlayBackgroundService.RebuyGrace.cs`. It is not the Kings and Lows chip-coverage pause.

## Defect

A cash game (`TournamentBuyIn` is null) with one funded player and at least one busted player enters rebuy grace inside `TryHandleCashGameRebuyGraceAsync`. That method sets `IsPausedForRebuyGrace`, `RebuyGraceEndsAt` to `now` plus `CashRebuyGraceDurationSeconds` (20), `CurrentPhase` to `WaitingForPlayers`, `Status` to `BetweenHands`, and `NextHandStartsAt` to null. It then starts an in-memory `ActionTimerService` chip-check timer whose callback is `HandleCashGameRebuyGraceTimerExpiredAsync`.

`ProcessGamesReadyForNextHandAsync` loads the next-hand query only when `NextHandStartsAt != null` and `NextHandStartsAt <= now`. After grace starts, that query does not return the game. Nothing in the database makes the window expire. If the process restarts, the timer is gone and the table stays in grace.

The timer and `AddChipsCommandHandler` also commit different rows. Add chips during grace updates `GamePlayer.ChipStack`, `IsSittingOut`, and `Status`, then `SaveChanges`. It does not modify `Game`. The timer loads `Game` and `GamePlayers`, counts funded players from that load, and later saves `Game` as `Ended` / `Completed`. The game row version is still the one the timer loaded, so that save commits. The rebuy stays on the player row. The table is over.

`Game` and `GamePlayer` both have an `IsRowVersion` `RowVersion`. SQL Server throws `DbUpdateConcurrencyException` when a save uses a stale version. The in-memory test provider does not. Do not write a test that expects `UseInMemoryDatabase` to throw that exception.

## Worked hand

Cash Hold'em. `TournamentBuyIn` is null. `Ante` is 10. Grace has started. `NextHandStartsAt` is null. `IsPausedForRebuyGrace` is true. `RebuyGraceEndsAt` is 20 seconds after the start. `CurrentPhase` is `WaitingForPlayers`. `Status` is `BetweenHands`.

| Seat | Player | ChipStack | Status | IsSittingOut | LeftAtHandNumber |
| --- | --- | --- | --- | --- | --- |
| 0 | Alice | 1000 | Active | false | -1 |
| 1 | Bob | 0 | SittingOut | true | -1 |

Bob is busted. Alice is the only funded player. Funded means `Status == Active`, `IsSittingOut` is false, `LeftAtHandNumber == -1`, and (`Ante` is 0 or `ChipStack >= Ante`).

### The table sticks

The process stops while the window is open and starts again after `RebuyGraceEndsAt`. No timer is running. Bob is still at 0.

Today `ProcessGamesReadyForNextHandAsync` does not load this game. It stays `WaitingForPlayers`, `BetweenHands`, `IsPausedForRebuyGrace` true, `NextHandStartsAt` null.

After this fix the next `ProcessGamesReadyForNextHandAsync` ends it. `CurrentPhase` is the string `Ended`. `Status` is `Completed`. `EndedAt` and `HandCompletedAt` are set. `NextHandStartsAt` is `HandCompletedAt` plus `CashRebuyGameOverDisplayDurationSeconds` (15). `IsPausedForRebuyGrace` and `IsPausedForChipCheck` are false. Bob's stack stays 0.

### The rebuy commits, then the timer ends the table

The timer loads Alice at 1000 and Bob at 0. Bob then adds 100 through `AddChipsCommand`. That save commits: Bob's stack is 100, `IsSittingOut` is false, `Status` is `Active`. The timer then saves the game it already loaded.

Today the game becomes `Ended` / `Completed` while Bob still has 100.

After this fix the expiry reloads both players before it decides. Bob is funded, so the game resumes. `Status` stays `BetweenHands`. `CurrentPhase` stays `WaitingForPlayers`. `IsPausedForRebuyGrace` is false. `NextHandStartsAt` is set. `EndedAt` is null. Bob's stack stays 100. Alice stays funded. The toast text stays `Rebuy timer expired. Resuming play without busted players.`

If the timer's end save commits before Bob's add-chips save, Bob's save does not commit. The handler returns `AddChipsErrorCode.GameEnded` with message `This game has ended and chips cannot be added.` Bob's stack stays 0. That call is not a successful rebuy.

A rebuy that leaves `ChipStack` below `Ante` does not make the player funded. Expiry still ends the game. Do not change that rule.

## One expiry method

Add a private method on `ContinuousPlayBackgroundService`:

```csharp
private async Task<CashRebuyGraceExpiryResult> ApplyCashRebuyGraceExpiryAsync(
	CardsDbContext context,
	IGameStateBroadcaster broadcaster,
	LeagueGameCompletionSyncService? leagueCompletionSync,
	Game game,
	DateTimeOffset now,
	CancellationToken cancellationToken)
```

Add this private enum in the same file: `Ignored`, `Resumed`, `Ended`.

The method has two attempts. Attempt 0 catches `DbUpdateConcurrencyException`. Attempt 1 does not.

On each attempt:

1. `await context.Entry(game).ReloadAsync(cancellationToken)`. Then reload every `GamePlayer` in `game.GamePlayers`. This runs before the funded count. A player graph tracked before the rebuy committed must not be the graph that decides.
2. Return `Ignored` when `game.TournamentBuyIn` has a value, `IsPausedForRebuyGrace` is false, `RebuyGraceEndsAt` is null, or `now < game.RebuyGraceEndsAt`. Write nothing in that case.
3. Count funded players with the timer predicate already in `HandleCashGameRebuyGraceTimerExpiredAsync`: `Status == Active`, `IsSittingOut` is false, `LeftAtHandNumber == -1`, and `(ante == 0 || ChipStack >= ante)`, where `ante` is `game.Ante ?? 0`.
4. When that count is at least 2, call `SetBustedPlayersToObserve(game.GamePlayers)` and `ClearCashGameRebuyGracePause(game, now, scheduleNextHand: true)`. `SaveChanges`. Broadcast the existing resume toast and `BroadcastGameStateAsync`. Return `Resumed`.
5. Otherwise set the same end fields the timer sets today: `CurrentPhase = "Ended"`, `Status = Completed`, `EndedAt = now`, `HandCompletedAt = now`, `NextHandStartsAt = now.AddSeconds(CashRebuyGameOverDisplayDurationSeconds)`, clear both pause flag sets, `UpdatedAt = now`. `SaveChanges`. Call `RecordGameEnded` with reason `rebuy_grace_expired` and `SyncLeagueCompletionIfNeededAsync`, then the existing end toast and `BroadcastGameStateAsync`. Return `Ended`.

On `DbUpdateConcurrencyException` during attempt 0: `context.ChangeTracker.Clear()`, load the game again with `Include(g => g.GamePlayers)`, replace the local `game`, and run attempt 1. Do not end the game from the snapshot that failed. If attempt 1 throws `DbUpdateConcurrencyException`, let it propagate.

`Clear`, reload, resume, and end stay in this method. Delete those duplicated field assignments from `HandleCashGameRebuyGraceTimerExpiredAsync` and from the two expired branches in `TryHandleCashGameRebuyGraceAsync`.

`HandleCashGameRebuyGraceTimerExpiredAsync` still loads the game by id through its scope. If the load is null, or the guard at the top of `ApplyCashRebuyGraceExpiryAsync` would ignore it, return. Otherwise call `ApplyCashRebuyGraceExpiryAsync` and return. Do not start another timer.

`TryHandleCashGameRebuyGraceAsync` keeps the in-window branch. While `now < RebuyGraceEndsAt` and `bustedPlayers.Count > 0`, it still broadcasts and returns true. While `now < RebuyGraceEndsAt` and nobody is busted, it still clears the pause, saves, broadcasts, and returns false so `StartNextHandAsync` continues. The block that starts grace, including `NextHandStartsAt = null` and `StartChipCheckPauseTimer`, stays as it is.

When grace is already active and `now >= RebuyGraceEndsAt`, `TryHandleCashGameRebuyGraceAsync` calls `ApplyCashRebuyGraceExpiryAsync` instead of writing the end or resume itself. `Ended` returns true. `Resumed` returns false so the same `StartNextHandAsync` call can deal. `Ignored` returns false.

## The background service loads an expired window

In `ProcessGamesReadyForNextHandAsync`, after the next-hand `foreach`, query games that the next-hand query cannot see:

```csharp
g.IsPausedForRebuyGrace &&
g.RebuyGraceEndsAt != null &&
g.RebuyGraceEndsAt <= now &&
g.TournamentBuyIn == null &&
(g.Status == GameStatus.InProgress || g.Status == GameStatus.BetweenHands)
```

`Include` `GamePlayers` and `GameType`. For each game, call `ApplyCashRebuyGraceExpiryAsync`. Catch `Exception` around that call, log it with the game id, and continue to the next game. Do not call `StartChipCheckPauseTimer` from this loop. Do not deal from this loop. A `Resumed` result sets `NextHandStartsAt` to `now`; the next iteration's existing next-hand query starts the hand.

Leave the next-hand query's `NextHandStartsAt != null` condition as it is.

A rebuy that brings every busted stack above zero does not resume the table before `RebuyGraceEndsAt`. Production grace has `NextHandStartsAt` null, so only expiry resumes it. Keep that.

## Add chips shares the game row

In `AddChipsCommandHandler`, when `applyImmediately` is true and `game.IsPausedForRebuyGrace` is true, set `game.UpdatedAt = DateTimeOffset.UtcNow` on the same `Game` instance that `SaveChanges` will write with the chip update. Do not clear grace flags. Do not change `NextHandStartsAt`. Do not change who `applyImmediately` is true for.

Catch `DbUpdateConcurrencyException` around that save only.

- `context.ChangeTracker.Clear()`.
- Load the game again with `GameType`, `GamePlayers`, and `Player`.
- Game missing: return `GameNotFound`.
- `Status` is `Completed` or `Cancelled`: return `GameEnded` with message `This game has ended and chips cannot be added.` The failed save did not add chips.
- Player missing or `Status` is `Left`: return `PlayerNotInGame`.
- Otherwise apply the same immediate or pending update the handler already uses, and if `IsPausedForRebuyGrace` is still true set `UpdatedAt` again. Call `SaveChanges` once more with no catch. If that save throws `DbUpdateConcurrencyException`, let it propagate.

The first successful save is the successful rebuy. The expiry attempt that loaded the game before that save then fails its game-row save, reloads, and sees the new stack.

## Tests

Add these facts to `ContinuousPlayBackgroundServiceTests`. The existing reflection tests `HandleCashGameRebuyGraceTimerExpiredAsync_EndsGameAndStartsLobbyCountdown`, `HandleCashGameRebuyGraceTimerExpiredAsync_WithTwoFundedPlayers_ResumesPlay`, and `HandleCashGameRebuyGraceTimerExpiredAsync_MarksLinkedOneOffEventCompleted` stay, and they still call `HandleCashGameRebuyGraceTimerExpiredAsync`.

1. `ProcessGamesReadyForNextHandAsync_ExpiredRebuyGraceWithoutNextHandStartsAt_EndsGame`. Seed the worked hand with Bob at 0, `NextHandStartsAt` null, `RebuyGraceEndsAt` one second in the past, `IsPausedForRebuyGrace` true, phase `WaitingForPlayers`, status `BetweenHands`, `TournamentBuyIn` null, `Ante` 10. Call `ProcessGamesReadyForNextHandAsync`. The game is `Ended` / `Completed`, `NextHandStartsAt` is after `HandCompletedAt`, and both pause flags are false. The flow handler's `DealCardsCalled` is false.

2. `ProcessGamesReadyForNextHandAsync_ExpiredRebuyGraceAfterRebuy_Resumes`. Same seed, except Bob's stack is 100, `Status` is `Active`, and `IsSittingOut` is false before the call. `NextHandStartsAt` is null. After the call, `Status` is `BetweenHands`, `CurrentPhase` is `WaitingForPlayers`, `EndedAt` is null, `IsPausedForRebuyGrace` is false, `NextHandStartsAt` is not null, and Bob's stack is 100. `DealCardsCalled` is false on this call.

3. `ApplyCashRebuyGraceExpiryAsync_ReloadsStaleBustedPlayer_DoesNotEnd`. Build two `CardsDbContext` instances with one shared `InMemoryDatabaseRoot` and the same database name. Context A seeds the worked hand, saves, and loads the game with players so Bob is tracked at 0. Context B loads Bob, sets stack 100, `IsSittingOut` false, `Status` Active, sets `Game.UpdatedAt`, and saves. Invoke `ApplyCashRebuyGraceExpiryAsync` on context A with the stale tracked game, `RebuyGraceEndsAt` already in the past, and `now` after that instant. The game is not `Completed`. Bob's stack is 100. `IsPausedForRebuyGrace` is false. `NextHandStartsAt` is not null. `CardGames.IntegrationTests` is already an `InternalsVisibleTo` friend, so the method can stay private and the test can invoke it with reflection the same way the existing timer tests do. Pass the stale context, a `FakeGameStateBroadcaster`, a null league sync, the stale game, and `CancellationToken.None`.

`Handle_RebuyGraceWithOtherBustedPlayers_KeepsSharedPauseActive` stays. Seed `UpdatedAt` to one hour before the call and assert it is later than that seed. The assertions that grace stays active, `NextHandStartsAt` stays null, and the rebuying player's stack is 50 stay.

## Do not change

- The cashier. `AddChipsCommandHandler` does not debit `PlayerChipAccount`. Pending-chip cancellation in `StartNextHandAsync` stays as it is.
- `applyImmediately` for Kings and Lows, `IsPausedForChipCheck`, `BetweenHands`, and `WaitingForPlayers`.
- The chip-coverage pause in `StartNextHandAsync` (`RequiresChipCoverageCheck`, `StartChipCheckPauseTimer` for a pot shortage).
- The next-hand query's `NextHandStartsAt` condition.
- `CashRebuyGraceDurationSeconds` and `CashRebuyGameOverDisplayDurationSeconds`.
- Tournament games. `TryHandleCashGameRebuyGraceAsync` still returns false when `TournamentBuyIn` has a value, and the new query excludes them.
- Showdown, settlement, and betting actions.

## Done when

1. The stuck hand, with `NextHandStartsAt` null and `RebuyGraceEndsAt` in the past, is ended by `ProcessGamesReadyForNextHandAsync` without an in-memory timer.
2. The same hand with Bob already at 100 is resumed by that call. `Status` is not `Completed`. Bob still has 100.
3. A context that still has Bob tracked at 0, after another context committed Bob at 100, resumes inside `ApplyCashRebuyGraceExpiryAsync` and does not set `Completed`.
4. An add-chips call that loses the game row because expiry already committed `Completed` returns `GameEnded` and leaves the stack unchanged.
5. `dotnet test src/Tests/CardGames.IntegrationTests --filter "FullyQualifiedName~ContinuousPlayBackgroundServiceTests|FullyQualifiedName~AddChipsCommandHandlerTests"` passes.
