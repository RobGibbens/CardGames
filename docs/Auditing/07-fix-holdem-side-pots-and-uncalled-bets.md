# Fix: Contested Hold'em showdown pays one pot

## Defect

A live Hold'em hand stores every blind and every bet in the single `PotOrder == 0` pot. `PerformShowdownCommandHandler.Handle` then does this for a contested showdown:

```csharp
var totalPot = currentHandPots.Sum(p => p.Amount);
var payoutPerWinner = totalPot / winners.Count;
var remainder = totalPot % winners.Count;
```

`winners` is every player still in the hand whose `HoldemHand.Strength` equals the best strength. The best hand is paid `totalPot`. A short all-in is paid chips they did not match. Chips nobody called stay in that pot and are paid to the winner.

Red River uses this handler. `src/CardGames.Poker.Web/Services/GameApi/RedRiverApiClientWrapper.cs` `PerformShowdownAsync` calls `IHoldEmApi.HoldEmPerformShowdownAsync`. There is no Red River showdown handler.

The hand, blinds 5/10. Contributions are `TotalContributedThisHand` after betting. Stacks in the last column are the stacks at showdown, after those chips have left the player.

| Seat | Player | Position | Stack at hand start | Contributed | Stack at showdown |
|---|---|---|---|---|---|
| 0 | Alice | button | 1000 | 1000 | 0 |
| 1 | Bob | small blind | 100 | 100 | 0 |
| 2 | Carol | big blind | 300 | 300 | 0 |

Bob posts 5, Carol posts 10, Alice shoves 1000, Bob calls the rest of his stack (95 more), Carol calls the rest of her stack (290 more). One pot row, `Amount` 1400. Board `2c 7d 9c Js 3h`. Bob has aces, Carol has kings, Alice has queens.

The handler adds 1400 to Bob. Alice and Carol stay at 0.

Required stacks: Alice 700, Bob 300, Carol 400. Alice is refunded the uncalled 700. Bob wins the 300 layer (100 from each of the three). Carol wins the 400 layer (200 from Alice and 200 from Carol). Bob cannot win Carol's extra 200 or Alice's unmatched 700.

## Do not change

- `ProcessBettingActionCommandHandler` and `BaseGameFlowHandler.PostBlindAsync`. Both add the same chip count to `GamePlayer.TotalContributedThisHand` and to the `PotOrder == 0` pot. Leave that. That betting handler also runs Omaha, Nebraska, South Dakota, Bob Barker, Irish Hold'em, Phil's Mom, Crazy Pineapple, Klondike, and Hold the Baseball. Do not build side pots during betting.
- The fold-win branch, the block `if (playersInHand.Count == 1)`. It pays `totalPot` to that player, which already includes any uncalled chips. Do not also refund on that branch. Do not run the new pot-sum check on that branch. The existing test `Handle_WhenOnlyOnePlayerRemains_ReturnsFoldWinWithoutHoleCards` seeds a pot of 120 and leaves `TotalContributedThisHand` at 0. That test must keep returning success and paying the 120.
- The phase check, the hole-card evaluation, `HoldemHand`, `FindBestFiveCardHand`, and community-card loading. Red River can have six community cards. Pass every non-discarded `CardLocation.Community` card for this hand into `HoldemHand`, as the handler does now. Do not require exactly five board cards.
- `HandSettlementService` and `PlayerChipWalletService.RecordHandSettlementAsync`. Settlement is `payout - TotalContributedThisHand` for each `GamePlayer` with `Status == Active` or `IsAllIn`. Make the payout dictionary and `TotalContributedThisHand` correct before the call. Do not edit the service.
- The odd-chip rule. For each pot, `share = amount / winners.Count`, `remainder = amount % winners.Count`, and the entire remainder goes to the first winner. Do not implement the seat-after-the-button rule in `docs/SidePots.md`. That is a different change.
- `CardGames.Poker.Betting.PotManager`. The API does not call it. Its `CalculateSidePots` drops a short contribution and adds the shortfall to the last pot. Do not call it.
- `FiveCardDraw/.../ProcessBettingActionCommandHandler.CalculateSidePots`. Do not copy it. A folded player's smaller amount matches no layer, and the missing chips are then added to the highest pot.
- Generic, stud, draw, and Kings and Lows showdown handlers. Do not create `ContestedPotLayers` in this change. `docs/Auditing/08-fix-generic-showdown-side-pots.md` says to share the type later, if this type already exists. This change does not do that sharing.
- `LeaveGameCommandHandler`. A folded player who left stays on `Game.GamePlayers`. Their `TotalContributedThisHand` is dead money in the pot. Do not drop those chips and do not settle them here.
- `TableStateBuilder`. It sums `WinnerPayouts` by `playerName` and `amount`. Per-pot JSON makes that sum correct.

Files this change may modify:

- `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/HoldEmPotLayers.cs` (new)
- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/HoldEmPotLayersTests.cs` (new)
- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`

No other file.

## Where the fix goes

Put the pot math in a pure static type. No `CardsDbContext`, no `Game`, no `GamePlayer`, no `HoldemHand`.

`src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/HoldEmPotLayers.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;

public static class HoldEmPotLayers
{
    public readonly record struct Stake(
        Guid GamePlayerId,
        string Name,
        int Contributed,
        bool Contesting,
        bool AllIn);

    public readonly record struct Slice(
        Guid GamePlayerId,
        string Name,
        int Amount,
        bool Eligible);

    public readonly record struct Layer(
        int PotOrder,
        int Amount,
        int MaxContributionPerPlayer,
        IReadOnlyList<Slice> Slices);

    public readonly record struct BuildResult(
        int Uncalled,
        Guid? UncalledPlayerId,
        IReadOnlyList<Layer> Layers);

    public static BuildResult Build(IReadOnlyList<Stake> stakes) { ... }
}
```

`Contesting` is the handler's existing `playersInHand` membership: `!HasFolded && (Status == Active || IsAllIn)`. Pass the boolean in. `Build` does not interpret status.

`AllIn` is captured by the handler before any refund: `gamePlayer.IsAllIn || gamePlayer.ChipStack == 0`. A shove that is partly refunded stays all-in at the reduced contribution. `Build` does not clear the flag.

## What the handler does, in order

Keep the method's load, phase check, `playersInHand` filter, user lookup, hole-card load, and community-card load.

1. Fold win (`playersInHand.Count == 1`): the existing block, unchanged, including its `SettleHandAsync`, `SaveChanges`, and `RecordHandHistoryAsync`. Return from that block as it does now. Do not call `HoldEmPotLayers.Build`.
2. Evaluate hands with the existing `HoldemHand` loop. If `playerHandEvaluations.Count == 0`, return the existing `InvalidGameState` whose message contains `No eligible hands could be evaluated`. Do this before the pot-sum check. `Handle_WhenPlayersDoNotHaveEnoughHoleCards_ReturnsInvalidGameState` seeds one hole card per player, a pot of 120, and contributions of 0. It must still hit this message.
3. If `isAlreadyAwarded` is true, do not call `Build`, do not move chips, do not settle, do not write history. Build the response from stored JSON, described below, and return.
4. Otherwise run steps A through I, then `SaveChanges` once, then hand history, then the response.

`isAlreadyAwarded` stays `currentHandPots.Any(p => p.IsAwarded)`, computed from pots with `HandNumber == game.CurrentHandNumber` before any of the new writes.

### Step A — pot total and contributions already match

```text
potSum = pots where HandNumber == game.CurrentHandNumber, Sum(Amount)
contributionSum = game.GamePlayers.Sum(TotalContributedThisHand)
```

Use every `GamePlayer` on the game, including folded and left players. Use only pots for this hand number.

If `potSum != contributionSum`, return `PerformShowdownErrorCode.InvalidGameState`. The message must contain both integers, `potSum` and `contributionSum`. Do not add the difference to a pot. Do not change stacks. Do not call `SettleHandAsync`. Do not call `SaveChanges`.

Blinds and betting increment `TotalContributedThisHand` and the main pot by the same amount, so a hand that reached showdown through those paths matches. A mismatch means the stored hand is already wrong.

### Step B — `Build` returns the uncalled bet from contesting stakes

Contesting stakes only:

- Sort a copy by `Contributed` descending. Do not reorder the caller's list.
- If fewer than two contesting stakes, `Uncalled` is 0 and `UncalledPlayerId` is null. The fold-win branch never calls `Build`. This is a guard.
- `uncalled = ranked[0].Contributed - ranked[1].Contributed`.
- If `uncalled == 0`, nobody is refunded.
- If `uncalled > 0`, one contesting player holds that maximum. `UncalledPlayerId` is that player's `GamePlayerId`.

Folded stakes do not reduce the refund. The second-highest contesting contribution is the cap.

`Build` copies the contributions, subtracts `Uncalled` from that one player's copy, and builds layers from the copies. The caller's `Stake` values are unchanged. The handler does not call `Build` a second time with reduced numbers.

The handler applies the refund once, after `Build` returns and after the conservation check:

- `ChipStack += Uncalled` on the tracked `GamePlayer` with that `GamePlayerId`.
- `TotalContributedThisHand -= Uncalled` on that same player.
- Do not change `IsAllIn`, `Status`, `HasFolded`, or `CurrentBet`.
- Do not put `Uncalled` in the payout dictionary, `WinnerPayouts`, `AmountWon`, or the hand-history winner list.

Worked numbers for the opening hand: contributions 1000, 100, 300, all contesting. `Uncalled` is 700. Alice's copied contribution is 300. Her stack becomes 700. Her stored `TotalContributedThisHand` becomes 300.

### Step C — cap folded chips at the contesting maximum

Still inside `Build`, on the post-refund copies.

```text
cap = maximum Contributed among contesting stakes
```

If there is no contesting stake, return `Uncalled` 0 and no layers.

For every stake, `matched = min(Contributed, cap)`. `excess = Contributed - matched`. Sum `excess` into `deadMoney`. Folded players are the only ones who can have `excess > 0` after the refund, because every contesting contribution was already capped by the second contesting contribution. A folded player is never `Eligible`.

`deadMoney` is part of main-pot amount. It is not its own pot.

### Step D — layers from `matched`

All-in levels are the distinct post-refund `matched` values of contesting stakes whose `AllIn` is true and whose `matched` is greater than 0, sorted ascending.

Do not emit a layer with `Amount == 0`.

If there is no all-in level:

- Emit one layer when `sum(matched) + deadMoney > 0`.
- `PotOrder` 0.
- `Amount` = `sum(matched) + deadMoney`.
- `MaxContributionPerPlayer` = `cap`.
- One slice per stake with `matched > 0`, amount `matched`, `Eligible` true only when `Contesting` is true.
- For each folded stake with `excess > 0`, add `excess` to that stake's slice. If that stake had no slice, add a slice of `excess` with `Eligible` false.
- If `cap == 0` and `deadMoney > 0`, every contesting stake gets a slice of amount 0 with `Eligible` true, and the folded chips sit on the ineligible slices. Those zero-amount eligible slices must be persisted. They are how a player who matched nothing still wins a pot that contains only folded chips.

If there is at least one all-in level:

```text
previous = 0
for each level in all-in levels, ascending:
    for each stake:
        sliceAmount = max(0, min(matched, level) - previous)
    layerAmount = sum(sliceAmount)
    eligible = contesting stakes whose matched >= level
    if layerAmount > 0:
        emit the layer
        PotOrder = number of layers emitted so far, starting at 0
        MaxContributionPerPlayer = level - previous
        a slice is included when sliceAmount > 0
        Eligible is true only for the eligible set
    previous = level

if any stake has matched > previous:
    sliceAmount = matched - previous
    layerAmount = sum of those
    if layerAmount > 0:
        emit the layer
        MaxContributionPerPlayer = max(matched) - previous
        Eligible = contesting stakes whose matched > previous
```

`sliceAmount` uses `min(matched, level) - previous`. A folded stake of 10 against a first level of 60 contributes 10 to that layer and 0 after it. It does not need `matched >= level` to put chips in. It is not eligible.

`PotOrder` 0 is the main pot. Every later emitted layer is a side pot. The handler maps `PotOrder == 0` to `PotType.Main` and any other order to `PotType.Side`.

### Step E — dead money is on the main pot

On the all-in path, the loop above uses `matched`, so `deadMoney` is not in any slice yet.

Add `deadMoney` to the `PotOrder` 0 layer's `Amount`. Add it to the folded stake's slice on that layer (`Eligible` stays false). If that folded stake has no slice on layer 0, append one.

If the all-in loop emitted no `PotOrder` 0 layer and `deadMoney > 0`, do not create a side pot for it. Return `Uncalled` and an empty layer list. The handler treats an empty layer list, when `potSum != Uncalled`, as `InvalidGameState` and saves nothing. With the inputs this handler produces, a positive `deadMoney` always has a layer 0, because `cap > 0` implies a contesting `matched > 0`, which fills the first level. The empty-list failure is the guard for a result that violates that.

A layer with `Amount > 0` and no `Eligible` slice is not returned. That would drop chips or pay a folded player. The `cap == 0` case avoids it by marking the contesting players eligible.

### Step F — conservation, before any entity changes

`Build` is given the pre-refund contributions. This identity is on that input:

```text
Uncalled + sum(Layer.Amount) == sum(Stake.Contributed)
```

And for each layer:

```text
sum(Slice.Amount) == Layer.Amount
Layer.Amount > 0
at least one Slice.Eligible
```

If the handler's result breaks any of these, return `InvalidGameState` and do not change entities. Do not call `SaveChanges`. The message must contain the two sides of the failed identity.

An empty layer list is valid when the identity holds. That is `Uncalled == sum(Stake.Contributed)`. The opening hand is not this case. It happens when the refund consumes every contesting chip and there is no folded excess, for example contesting contributions 100 and 0: `Uncalled` is 100 and there is no layer. The handler still applies the refund, deletes this hand's pot rows, adds no pots, and completes the hand. It returns `InvalidGameState` for an empty layer list only when `Uncalled + sum(Layer.Amount)` does not equal `sum(Stake.Contributed)`.

Check this before the refund write in step B's handler half. `Build` itself does not touch entities, so calling it before the check is required.

### Step G — replace this hand's pot rows

Only pots with `HandNumber == game.CurrentHandNumber`. Do not load or edit pots from another hand.

`Pot.Contributions` uses `DeleteBehavior.Cascade`, and `CardsDbContext.PotContributions` is the `DbSet`. The handler tests use `UseInMemoryDatabase`, which does not apply that cascade. Delete the dependents explicitly:

- `potIds` = the current-hand pot ids.
- `context.PotContributions.Where(c => potIds.Contains(c.PotId))`, `RemoveRange`.
- `context.Pots.RemoveRange` on those pot entities, and remove them from `game.Pots` if they are in that collection.
- Add one `Pot` per layer:
  - `Id` left at `Guid.CreateVersion7()` (the entity default). `PotConfiguration` uses `ValueGeneratedNever()`.
  - `GameId`, `HandNumber = game.CurrentHandNumber`
  - `PotOrder`, `PotType`, `Amount`, `MaxContributionPerPlayer`
  - `CreatedAt = now`, `IsAwarded = false`
- Add one `PotContribution` per slice that has `Amount > 0` or `Eligible` true:
  - `PotId` = the new pot's id
  - `GamePlayerId`, `Amount`, `IsEligibleToWin = slice.Eligible`, `ContributedAt = now`
  - `IsPotMatch` left false

### Step H — award each new pot

Walk the new pots by `PotOrder` ascending.

Eligible players for a pot are its contributions with `IsEligibleToWin` whose `GamePlayerId` is a key in `playerHandEvaluations`. Match `GamePlayer.Id`, not `Player.Name`. The evaluation dictionary is keyed by `Player.Name`; look up the `GamePlayer` from the tuple.

If a pot has at least one `IsEligibleToWin` contribution and none of those `GamePlayerId`s were evaluated, return `InvalidGameState` and do not save. Do not skip the pot. Skipping it deletes chips. Because this return can happen after in-memory edits, do not call `SaveChanges` on this path. The request context is disposed without a save.

Winners of that pot:

- Strength is `HoldemHand.Strength` among those eligible evaluated players only.
- Walk `playersInHand` in its existing order. That list is `game.GamePlayers.Where(...)` with no `OrderBy`. Collect the `Player.Name` values whose evaluation is eligible for this pot and whose strength equals the maximum.
- Do not sort by seat. Do not sort the dictionary. This is the same order the handler uses today for the single pot, because it fills `playerHandEvaluations` by walking `playersInHand`.

Pay that pot:

```text
share = pot.Amount / winners.Count
remainder = pot.Amount % winners.Count
```

Each winner gets `share`. The first name in that winner list also gets `remainder`. Do not carry a remainder into the next pot.

Accumulate into `Dictionary<string, int>` keyed by `Player.Name`. A player who wins two pots has one entry, the sum of those pot payouts. Do not insert a name whose amount is 0.

On that pot only:

- `IsAwarded = true`, `AwardedAt = now`
- `WinReason` = `hand.Type.ToString()` when the pot has one winner, or `"Split pot - {hand.Type}"` when that pot has more than one winner. `hand` is the winning `HoldemHand`.
- `WinnerPayouts` = `JsonSerializer.Serialize` of an array of `{ playerId, playerName, amount }` for this pot's winners only. `amount` is this pot's payout, not the cross-pot sum. `playerId` is `GamePlayer.PlayerId.ToString()`. Use the same anonymous-type property names the handler already writes: `playerId`, `playerName`, `amount`. Do not pass `JsonSerializerOptions`. Do not write one pot's JSON onto another pot.

Remember `anyPotSplit` if any pot's winner list has more than one name. Two players winning different pots is not a split.

### Step I — stacks, settlement, history, response

After every pot is awarded:

- `ChipStack +=` the dictionary value for that `Player.Name`. The refund is already on the stack. Do not add it again.
- `SettleHandAsync(game, payouts, cancellationToken)` where `payouts` is that dictionary and does not contain the refund. Call it after `TotalContributedThisHand` has been reduced. `HandSettlementService` uses `GetValueOrDefault(name, 0)`, so a refunded player who won nothing contributes `0 - reduced TotalContributedThisHand`.
- Then the existing assignments: `CurrentPhase = "Complete"`, `UpdatedAt`, `HandCompletedAt`, `NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds)`, `MoveDealer(game)`.
- `SaveChanges` once.
- `RecordCompletedHandMetrics`.
- Hand history, then the response.

`RecordHandHistoryAsync` currently sets `var isSplitPot = winners.Count > 1` from the winner list. Two players who won different pots would become a split. Change the method in this file: add a `bool isSplitPot` parameter and delete that local. The fold-win call passes `false`. The contested call passes `anyPotSplit`.

Hand-history arguments for the contested path:

- `totalPot` is the sum of the new pots' `Amount`. It does not include the refund.
- `winners` are the dictionary entries with amount greater than 0, as `(PlayerId, Player.Name, amount)`.
- `winnerNames` are those names.
- `WinningHandDescription` is the `WinReason` of the `PotOrder` 0 pot.
- `NetChipDelta` inside `RecordHandHistoryAsync` reads `TotalContributedThisHand`. Call it after the refund so a refunded player's delta uses the reduced contribution.

Response `PlayerHands`, using the same card, type, strength, and `BestCardIndexes` code as now:

- `IsWinner` is true when the dictionary value is greater than 0.
- `AmountWon` is that value, or 0. It does not include the refund.
- `Payouts` is that dictionary.
- `WonByFold` is false.
- `CurrentPhase` is `game.CurrentPhase` after it is set to `Complete`.

### Already-awarded response

When `isAlreadyAwarded` is true, after evaluation has succeeded:

Copy the parse used by `FiveCardDraw/.../PerformShowdownCommandHandler` in the `else` branch that starts at the comment `Pots already awarded`. For each current-hand pot whose `WinnerPayouts` is not null, deserialize `List<Dictionary<string, object>>`. For each object, read `playerName` and `amount`. `amount` is a `JsonElement`; use `Convert.ToInt32(((JsonElement)amountObj).GetInt32())`. Sum amounts by name. Swallow parse failures the same way that handler's empty `catch` does.

`IsWinner` is `AmountWon > 0`. Do not mark a player a winner only because their name appeared. `AmountWon` is the summed stored amount, or 0.

Do not call `SettleHandAsync`. Do not change `ChipStack`, `TotalContributedThisHand`, pot rows, phase, or dealer.

## Worked hands

Contributions are the pre-refund `TotalContributedThisHand`. "Stack before" means the stack after those chips have already been taken. `Build` sees the contributions. The handler then changes stacks.

### 1. Canonical all-in

Contributions: Alice 1000, Bob 100, Carol 300. All contesting. All three `AllIn`. Pot row 1400. Stacks before: 0, 0, 0.

Refund 700 to Alice. Copied contributions: 300, 100, 300.

All-in levels: 100, then 300.

| PotOrder | Amount | MaxContributionPerPlayer | Eligible | Slices | Winner |
|---|---|---|---|---|---|
| 0 | 300 | 100 | Alice, Bob, Carol | 100 each | Bob |
| 1 | 400 | 200 | Alice, Carol | Alice 200, Carol 200 | Carol |

Stacks after: Alice 700, Bob 300, Carol 400.

`TotalContributedThisHand` after the refund: Alice 300, Bob 100, Carol 300.

Payout dictionary: Bob 300, Carol 400. Alice's key is absent.

`SettleHandAsync` receives that dictionary. With the reduced contributions the service computes Alice `0 - 300 = -300`, Bob `300 - 100 = +200`, Carol `400 - 300 = +100`. Those nets sum to 0. The test does not assert the nets by reimplementing the service. It asserts the dictionary argument.

`WinnerPayouts` on pot 0 names Bob for 300 only. Pot 1 names Carol for 400 only.

### 2. Heads-up uncalled shove

Alice 1000, Bob 100, both all-in, both contesting. Pot 1100. Stacks before: 0, 0.

Refund 900 to Alice. One pot of 200, `MaxContributionPerPlayer` 100, both eligible. Bob wins. Stacks: Alice 900, Bob 200. Dictionary: Bob 200. Settlement inputs imply Alice -100, Bob +100.

### 3. Folded chips stay in the main pot

Alice 110, Bob 60 all-in, Carol 110, Doug folded 10. Alice and Carol are not all-in. Pot 290. No refund: the two highest contesting contributions are both 110.

All-in levels: 60 only. Doug's `matched` is 10. `deadMoney` is 0.

| PotOrder | Amount | Eligible | Slices |
|---|---|---|---|
| 0 | 190 | Alice, Bob, Carol | Alice 60, Bob 60, Carol 60, Doug 10 ineligible |
| 1 | 100 | Alice, Carol | Alice 50, Carol 50 |

Bob, winning pot 0, receives 190, not 180. Doug wins nothing.

### 4. Three all-in amounts

A 20, B 50, C 100, D 100. All contesting. A and B all-in. C and D are not all-in. Pot 270. No refund.

| PotOrder | Amount | Eligible |
|---|---|---|
| 0 | 80 | A, B, C, D |
| 1 | 90 | B, C, D |
| 2 | 100 | C, D |

`MaxContributionPerPlayer` is 20, then 30, then 50.

### 5. No all-in

Alice 40, Bob 40, Carol 40. Nobody all-in. Pot 120. `Uncalled` 0. One main pot of 120, all three eligible. `MaxContributionPerPlayer` 40. Winner selection and the remainder match today's single-pot code: integer division, the entire remainder to the first of the tied names in `playersInHand` order.

### 6. Folded player put in more than the players still contesting

Doug folded 500. Bob and Carol contest 100 each. Nobody all-in. Pot 700. No refund. `cap` is 100. Doug's `matched` is 100 and `excess` is 400. `deadMoney` is 400.

One main pot of 700. Eligible: Bob and Carol. Doug's slice is 500 and `Eligible` is false. The 400 is not a side pot.

### 7. Second showdown call

After hand 1 has been saved, call `Handle` again on the same game. Stacks stay 700, 300, 400. `TotalContributedThisHand` stays 300, 100, 300. `SettleHandAsync` is not called. The response `Payouts` are Bob 300 and Carol 400, read from the two `WinnerPayouts` JSON values. Alice is absent. Phase stays `Complete`.

### 8. Fold win

Do not change this path. The existing test seeds pot `Amount` 120, Alice and Bob `ChipStack` 200, `TotalContributedThisHand` 0, Bob `HasFolded`. Alice's response is the fold-win shape the test already asserts: one player hand, empty cards, null hand type. Her stack becomes 320 because the existing branch adds `totalPot`. No extra pot rows. No refund. The new pot-sum check does not run, even though 120 is not equal to 0.

### 9. Contested pot does not match contributions

Pot `Amount` 100. Alice and Bob contest with `TotalContributedThisHand` 40 and 50. Sum 90. Both have two hole cards and the board below so evaluation would succeed. The handler returns `InvalidGameState`. The message contains `100` and `90`. Stacks are unchanged. `SettleHandAsync` is not called. `SaveChanges` is not called.

### 10. Tied side pot, remainder stays on that pot

Alice, Bob, and Carol all contest and are all-in for 10, 10, and 10. Pot 30. No refund. One pot of 30. Give the three the same hole cards so they tie, or otherwise force equal `HoldemHand.Strength`. `30 / 3` is 10, remainder 0. This hand only checks a clean split.

The remainder case: pot 5, two contesting all-in players with equal strength, contributions 2 and 3, so refund 1 to the player who put in 3, one pot of 4. `4 / 2` is 2, remainder 0. Use this instead for a non-zero remainder: contributions 5 and 5, pot 10, equal strength. `10` does not produce a remainder.

Use contributions 5 and 5 and a third contesting all-in of 5, pot 14 by setting the stored pot to 14 and contributions to 5, 5, and 4? That fails step A.

Remainder hand that conserves: three contesting all-in contributions 5, 5, 5. Pot 15. Equal strength. `share` 5, remainder 0.

Two-way remainder: contributions Alice 5, Bob 5, both contesting, not all-in. Pot 11. Step A fails because 11 != 10.

A legal remainder is an all-in layer whose amount is not divisible by the winner count. Contributions Alice 5, Bob 5, Carol 4, all contesting, Carol all-in, Alice and Bob not all-in. Pot 14. No refund. Levels: 4. Main pot `4 * 3 = 12`. Side pot `1 + 1 = 2`. If all three tie, main pot `12 / 3 = 4` remainder 0. Side pot eligible Alice and Bob only. If they tie, `2 / 2 = 1` remainder 0.

Force a remainder with contributions 4, 4, 4, pot 12, and only two of them eligible? They are all eligible.

Contributions Alice 7, Bob 7, both contesting and all-in, equal strength. Pot 14. No refund. One pot of 14. `share = 7`, remainder 0.

`share` remainder needs `amount % count != 0`. Three equal all-ins of 5 produce 15, which divides. Change the pot math by using a folded chip: Alice 5, Bob 5, Doug folded 1. Pot 11. No all-in. One pot of 11. Alice and Bob tie. `share = 5`, `remainder = 1`. The first of Alice and Bob in `playersInHand` receives 6, the other 5. Doug receives 0. Stacks before are whatever the test set after the chips left; the payouts are 6 and 5.

Seed `playersInHand` by `GamePlayers` insertion order Alice then Bob. The first winner is Alice. Alice's `AmountWon` is 6. Bob's is 5. Do not sort the names.

## Tests

### `HoldEmPotLayersTests`

`src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/HoldEmPotLayersTests.cs`

Call `HoldEmPotLayers.Build` only. No database. Cover hands 1 through 6 and these two:

- Hand 3: Doug's slice on pot 0 is 10, `Eligible` false, and pot 0's amount is 190.
- Hand 6: one layer, amount 700, Doug's slice 500 ineligible, Bob and Carol eligible.

For every case assert:

```text
Uncalled + sum(Layer.Amount) == sum(Stake.Contributed)
each layer's slices sum to its Amount
each layer Amount > 0 and has an Eligible slice
```

Use fixed `Guid`s so `UncalledPlayerId` can be asserted. Hand 1's id is Alice's.

### `PerformShowdownCommandHandlerTests`

Extend the existing file. It already has `CreateContext` (`UseInMemoryDatabase`), `CreateSut` (`Substitute.For<IHandHistoryRecorder>()` and `Substitute.For<IHandSettlementService>()`), and `SeedGameAsync`.

Keep these tests and their assertions:

- `Handle_WhenGameIsMissing_ReturnsGameNotFound`
- `Handle_WhenGameIsNotInShowdown_ReturnsInvalidGameState`
- `Handle_WhenPlayersDoNotHaveEnoughHoleCards_ReturnsInvalidGameState`
- `Handle_WhenOnlyOnePlayerRemains_ReturnsFoldWinWithoutHoleCards`

Add handler tests. Do not replace the seed helper's behavior for the old tests. Add a new seed path, or set fields after `SeedGameAsync`, so the old seeds stay a pot of 120 and contributions of 0.

Ranking cards, entity enums in `CardGames.Poker.Api.Data.Entities`:

- Board, `Location = CardLocation.Community`, `GamePlayerId = null`, `HandNumber` = the game's current hand, `IsDiscarded` false: `2c` (`Deuce`, `Clubs`), `7d` (`Seven`, `Diamonds`), `9c` (`Nine`, `Clubs`), `Js` (`Jack`, `Spades`), `3h` (`Three`, `Hearts`).
- Bob `As Ah` (`Ace`, `Spades` and `Ace`, `Hearts`). Carol `Ks Kh`. Alice `Qs Qh`.
- Each of those three has `holeCardEntities.Count >= 2`. The handler skips a player with fewer than two hole cards.
- `DealtAtPhase` can be `nameof(Phases.Dealing)` for hole cards and `"River"` is unnecessary. `DealOrder` must be unique enough that the rows insert. The existing `CreateHoleCard` helper is the pattern.

Those hole cards are one pair each and do not play the board. Bob's pair of aces beats Carol's kings, which beat Alice's queens.

For hand 1, set `ChipStack` 0, `IsAllIn` true, `Status` Active, `HasFolded` false, and `TotalContributedThisHand` to 1000, 100, and 300. Set the single current-hand pot `Amount` to 1400. Add Carol the same way `SeedGameAsync` adds Bob, seat 2.

Assert:

- `result.IsT0`, `WonByFold` false.
- `Payouts` contains Bob 300 and Carol 400 and does not contain the key `Alice`.
- Bob and Carol `IsWinner` true, `AmountWon` 300 and 400. Alice `IsWinner` false, `AmountWon` 0.
- After `Handle`, `GamePlayers` stacks are Alice 700, Bob 300, Carol 400.
- `TotalContributedThisHand` is 300, 100, 300.
- Two current-hand pots, `Amount` 300 and 400, both `IsAwarded`, `PotOrder` 0 and 1, types `Main` and `Side`.
- Pot 0 `WinnerPayouts` contains Bob and `300` and does not contain `400`. Pot 1 contains Carol and `400`.
- The settlement substitute received exactly one call. The dictionary argument has Bob 300 and Carol 400 and no Alice key. `Received(1)` with `Arg.Any` plus a callback, or `Arg.Is<Dictionary<string, int>>(...)`, is enough. Do not new up `HandSettlementService` to recompute nets.
- A second `Handle` on that same context does not change the three stacks and does not call `SettleHandAsync` again (`Received(1)` still).

Also add:

- Hand 3 as a handler test. Bob's best hand. His `AmountWon` is 190. Doug's stack does not increase. The 190 pot's contributions include Doug for 10 with `IsEligibleToWin` false.
- Hand 6 as a handler test. One pot of 700. The winner between Bob and Carol receives 700. Doug's `AmountWon` is 0. There is no `PotOrder` 1 row.
- Hand 9. Message contains `100` and `90`. The settlement substitute received no call. The pot `Amount` is still 100 and `IsAwarded` is false.
- The remainder hand at the end of the worked-hand list: pot 11, Alice 5, Bob 5, Doug folded 1, Alice and Bob the same hole cards so they tie, Doug folded so he is not evaluated as a winner. Alice `AmountWon` 6, Bob 5, when Alice was inserted into `GamePlayers` before Bob.

`Handle_WhenOnlyOnePlayerRemains` stays a fold win. Do not add a contribution-sum assertion to it.

Run:

```text
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~HoldEm.v1.Commands.PerformShowdown"
```

## Done when

- Hands 1 through 6 and 9 produce the amounts above from `Build` or from `Handle`, as assigned in the test section.
- Hand 7 does not move chips and does not settle again.
- Hand 8 still takes the old fold-win branch. The four existing tests pass without changed assertions.
- A contested showdown writes one `WinnerPayouts` document per pot, and a second call reconstructs `Payouts` by summing those documents.
- No file outside the four listed under "Files this change may modify" is edited.
