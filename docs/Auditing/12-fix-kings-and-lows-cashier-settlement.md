# Fix: Kings and Lows never records multi-player showdowns or deck losses in the cashier ledger

## Defect

Kings and Lows moves chips on the table, then does not write the matching cashier rows.

`HandSettlementService.SettleHandAsync` is the only writer. For each `GamePlayer` with `Status == Active` or `IsAllIn` it records `payout - TotalContributedThisHand` and skips a net of 0. `PlayerChipWalletService.RecordHandSettlementAsync` will not write a second `HandSettlement` row for the same player, game, and hand.

Three live branches never call it:

| What happened | Where the chips move | Settlement |
|---|---|---|
| Two or more players stayed, the draw finished, and the background service runs the showdown | `KingsAndLowsFlowHandler.PerformShowdownAsync` | None. The method has no cashier dependency. |
| One player stayed and lost to the deck | `KingsAndLows/.../PerformShowdownCommandHandler`, the `deckWins` branch | Skipped. Settlement runs only inside `if (isWinner)`. |
| Every player dropped | Same command handler, `playersInHand.Count == 0` | None. The pot is marked awarded and carried forward. |

`ContinuousPlayBackgroundService.ProcessDrawCompleteGamesAsync` calls `PerformShowdownAsync` when the `DrawComplete` delay has expired and the current-hand pots are not awarded. That is the live multi-player path. `TablePlay` then calls the Kings and Lows showdown endpoint. The pots are already `IsAwarded`, so the endpoint's own `SettleHandAsync` does not run.

The endpoint's multi-player branch, when it does run first, already settles after the match is added to `TotalContributedThisHand`. Do not change that branch.

The hand. Three players, 500 each. Ante 10, so each `TotalContributedThisHand` is 10 and the pot is 30. Stacks are 490.

Hand 1, all three stay. Alice wins. Bob and Carol each match 30.

| Player | Stack | Contributed after the match | Payout |
|---|---|---|---|
| Alice | 520 | 10 | 30 |
| Bob | 460 | 40 | 0 |
| Carol | 460 | 40 | 0 |

The next pot is 60. No ledger rows are written.

Hand 2, Bob stays alone and beats the deck. No new ante, because the pot is not empty. Bob's `TotalContributedThisHand` is 0. The existing win branch records `60 - 0 = +60`.

Cumulative ledger: Alice 0, Bob +60, Carol 0. That creates 60 chips. Bob's match and Carol's match were never debited.

The ledger after both hands has to be Alice +20, Bob +20, Carol −40. Hand 1 has to record Alice +20, Bob −40, Carol −40. Hand 2's existing +60 is then correct.

## Do not change

- `KingsAndLowsFlowHandler`'s constructor. `GameFlowHandlerFactory` creates handlers with `Activator.CreateInstance`. A new constructor parameter makes the factory skip this handler.
- Pot matching. Losers still pay `Math.Min(potAmount, ChipStack)` into `HandNumber + 1`. The winner is still paid the current pot. A tie still gives the player the pot over the deck.
- `TotalContributedThisHand += matchAmount`. Settlement has to see that increase. Do not also put the match in the payout dictionary. The match is not money won.
- The endpoint multi-player block that already calls `SettleHandAsync(game, payouts)` after the match. `payouts` there is each winner's share of the pot only.
- The player-beats-deck settlement. It already passes `{ player.Name: totalPot }`. Other active players are debited their contribution because they are absent from that dictionary.
- `HandSettlementService` and `RecordHandSettlementAsync`.
- Hand history. It already computes `payout - TotalContributedThisHand` after the match. The ledger has to match those numbers, not the other way around.
- Remainder chips. The flow handler deals them one at a time in staying-player order. The endpoint gives the whole remainder to `winners[0]`. Leave both.
- Generic showdown's inline `SettleHandAsync(game, settlementPayouts)`. For Kings and Lows that dictionary is empty, so it would record `0 - TotalContributedThisHand` for the winner and ignore the pot they were just paid. Do not send Kings and Lows through that call, and do not fill that dictionary in. Screw Your Neighbor is the caller that dictionary is for.

## Multi-player showdown

`ProcessGamesReadyForNextHandAsync` already resolves `IPlayerChipWalletService` from the same scope as `context`. Resolve `IHandSettlementService` from that same scope and pass it into `ProcessDrawCompleteGamesAsync`.

Settle only inside the success branch that just called `PerformShowdownAsync`, and only when `flowHandler.GameTypeCode` is `KINGSANDLOWS`. The `isAlreadyHandled` branch must not settle. The endpoint already did, or the pot was awarded without this showdown.

`PerformShowdownAsync` has already added the winner's chips, added each loser's match to `TotalContributedThisHand`, set `mainPot.WinnerPayouts`, and saved. `ProcessPostShowdownAsync` does not clear `TotalContributedThisHand`.

Build the payout dictionary from that pot's `WinnerPayouts` JSON. Each element has `playerName` and `amount`. The dictionary is `playerName → amount`. Ignore an element with an empty name or a missing amount. Do not recompute the split. The JSON is the amount that was added to `ChipStack`.

Call `SettleHandAsync(game, payouts)` on the game instance `PerformShowdownAsync` just updated. Then fall through to the `SaveChanges` already at the end of that game's processing. `RecordHandSettlementAsync` does not save the ledger row. That existing `SaveChanges` has to persist it. Do not add a second save unless that call is removed.

`Player.Name` is loaded: the query `Include`s `GamePlayers` then `Player`. The flow handler writes that same name into `WinnerPayouts`.

Active players who are not in the dictionary, including a player who dropped and still has `Status == Active`, are debited `TotalContributedThisHand`. A dropped player's contribution is the ante. A staying loser's contribution is the ante plus the match.

## Deck loss

In `PerformShowdownCommandHandler`, inside `if (!isAlreadyAwarded)` and after both the win arm and the deck-loss arm have finished changing stacks.

The win arm already settles. Call `SettleHandAsync` once for the whole player-versus-deck showdown, after the `if (isWinner)` / `else` block, still inside `if (!isAlreadyAwarded)`, and delete the nested `if (isWinner)` settlement so it cannot run twice.

```csharp
var settlementPayouts = isWinner
    ? new Dictionary<string, int> { { player.Player.Name, totalPot } }
    : new Dictionary<string, int>();
await handSettlementService.SettleHandAsync(game, settlementPayouts, cancellationToken);
```

On a deck loss this runs after `TotalContributedThisHand += actualMatch`. The losing player's payout is 0. Their net is `-(ante + actualMatch)`. Every other active player is debited their own `TotalContributedThisHand`. The carried pot is not a payout.

`SaveChanges` stays where it is, after this call.

## Dead hand

In the `playersInHand.Count == 0` block, inside `if (!isAlreadyAwarded)`, after the current pots are marked awarded and the next-hand pot is created, and before `SaveChanges`:

```csharp
await handSettlementService.SettleHandAsync(game, new Dictionary<string, int>(), cancellationToken);
```

Nobody is paid. Each active player's net is `-TotalContributedThisHand`. That is the ante that already left their stack. The carried pot is the next hand's pot, not a win.

## Worked hands

Antes are already on `TotalContributedThisHand` and already subtracted from `ChipStack` before showdown. Settlement does not move table chips.

### 1. The reported multi-player hand

Stakes after the ante: Alice, Bob, and Carol at 490, contributed 10, pot 30. All stay. Alice wins. Bob and Carol match 30.

`WinnerPayouts` contains Alice 30 only.

| Player | Payout passed in | Contributed at settlement | Ledger row |
|---|---|---|---|
| Alice | 30 | 10 | +20 |
| Bob | 0 | 40 | −40 |
| Carol | 0 | 40 | −40 |

The rows sum to −60. Stacks are 520, 460, and 460. The next pot is 60.

### 2. Bob then beats the deck

No new ante. Pot 60. Bob's contribution this hand is 0. The existing win branch records +60. Alice and Carol contribute 0, so they get no new row.

Cumulative ledger: Alice +20, Bob +20, Carol −40. Cumulative stack change from 500 each is the same: +20, +20, −40.

### 3. Deck loss

Pot 30. Alice is the only player still in. Her contribution is 10. The deck wins. She matches 30, so her contribution becomes 40 and her stack falls by 30. Bob and Carol dropped after the ante and are still `Active`, contributed 10.

Settlement payouts are empty.

| Player | Ledger row |
|---|---|
| Alice | −40 |
| Bob | −10 |
| Carol | −10 |

The next pot is 60. The rows sum to −60, which is the stack change.

### 4. Dead hand

Pot 30. All three dropped. Contributions are 10. Payouts are empty. Each ledger row is −10. The next pot is 30. No player matches.

### 5. Second call

Hand 1 has saved its three ledger rows. `ProcessDrawCompleteGamesAsync` runs again. The pot is `IsAwarded`, so the method takes `isAlreadyHandled` and does not settle. A second `SettleHandAsync` for that hand would also no-op inside `RecordHandSettlementAsync`, but this path must not call it.

The endpoint's player-versus-deck and dead-hand blocks are already inside `if (!isAlreadyAwarded)`. A second HTTP call does not settle again.

## Tests

`src/Tests/CardGames.IntegrationTests/GameFlow/KingsAndLowsFlowHandlerTests.cs` can construct `KingsAndLowsFlowHandler` with `new`. It cannot see a cashier call inside the handler, because the call is not there.

Add a test that drives `ContinuousPlayBackgroundService.ProcessGamesReadyForNextHandAsync`. It is `internal`, and `CardGames.IntegrationTests` is an `InternalsVisibleTo` friend. IntegrationTestBase already builds the API services. Seed one Kings and Lows game in `DrawComplete` with `DrawCompletedAt` at least six seconds in the past, `Status == InProgress`, three active players, contributions 10, stacks 490, one current-hand pot of 30, and five cards each so a winner exists. Let Alice's cards beat Bob's and Carol's. Leave `DropOrStayDecision` as `Stay` and `HasFolded` false.

Run `ProcessGamesReadyForNextHandAsync`. Assert the `PlayerChipLedgerEntry` rows for that game and hand: Alice +20, Bob −40, Carol −40. Assert there is no second row per player. Run it again and assert the same three rows.

Deck loss and dead hand do not go through that service. Add them to `src/Tests/CardGames.IntegrationTests/Features/Commands/KingsAndLowsCommandTests.cs`, or a new test file next to it, by sending `PerformShowdownCommand` at the existing handler. The handler already takes `IHandSettlementService`. Use the real one from the integration host so the assertion is the ledger table, not a substitute. If that host is too heavy, use the in-memory `CardsDbContext` pattern from `HoldEm/.../PerformShowdownCommandHandlerTests` and a substitute, and assert the dictionary passed to `SettleHandAsync`:

- Deck loss: an empty dictionary, and Alice's `TotalContributedThisHand` is 40 before the call. The substitute receives the game after the match is added. Assert `game.GamePlayers` contributions at the moment of the call, not a second copy of the math.
- Dead hand: an empty dictionary, contributions still 10, and `SettleHandAsync` called once.

Do not assert chip amounts by reimplementing pot matching in the test. Read `ChipStack`, `TotalContributedThisHand`, and the ledger delta.

Run:

```text
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~KingsAndLows"
```

## Done when

- The background multi-player path writes Alice +20, Bob −40, and Carol −40 for hand 1.
- Bob beating the deck on hand 2 still writes only Bob +60. The cumulative ledger is Alice +20, Bob +20, Carol −40.
- A deck loss writes one row per active player for `-(ante + match)` or `-ante`, and does not write a win for the deck.
- A dead hand writes `-ante` for each active player and does not pay the carried pot to anyone.
- A second pass over an awarded hand does not add ledger rows.
- `KingsAndLowsFlowHandler` still has no constructor parameters.
- Generic showdown's empty inline settlement is not used for this game.
