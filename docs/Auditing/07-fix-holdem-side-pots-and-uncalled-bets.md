# Fix: Hold'em showdown has no side pots and no uncalled-bet return

## Defect

A live Hold'em or Red River hand puts every blind and every bet into one pot. At showdown the best hand takes that whole pot. A short all-in is paid off for more than they put in, and chips nobody called are not returned.

Red River uses this same handler. `RedRiverApiClientWrapper.PerformShowdownAsync` calls `IHoldEmApi.HoldEmPerformShowdownAsync`.

The hand that demonstrates it, blinds 5/10:

| Seat | Player | Position | Stack before the hand | Put in | Stack when betting closes |
|---|---|---|---|---|---|
| 0 | Alice | button | 1000 | 1000 | 0 |
| 1 | Bob | small blind | 100 | 100 | 0 |
| 2 | Carol | big blind | 300 | 300 | 0 |

Bob posts 5, Carol posts 10, Alice shoves 1000, Bob calls all-in for 95 more, Carol calls all-in for 290 more. One pot of 1400. Board `2c 7d 9c Js 3h`. Bob has aces, Carol has kings, Alice has queens.

Bob is paid 1400. Alice and Carol finish at 0. Bob should win 300, Carol should win 400, and 700 should come back to Alice. Final stacks: Alice 700, Bob 300, Carol 400.

## Do not change

- `ProcessBettingActionCommandHandler` and `BaseGameFlowHandler.PostBlindAsync`. They already add each chip to `GamePlayer.TotalContributedThisHand` and to the single `PotOrder == 0` pot. Leave that alone. That betting handler also runs Omaha, Nebraska, South Dakota, Bob Barker, Irish Hold'em, Phil's Mom, Crazy Pineapple, Klondike, and Hold the Baseball. Creating side pots there would change those games.
- The fold-win branch in `PerformShowdownCommandHandler` (`playersInHand.Count == 1`). The last player already receives the whole pot, which includes anything uncalled. Do not also return uncalled chips on that branch.
- Hand evaluation. Do not change `HoldemHand`, community-card selection, or Red River's sixth board card. Pot eligibility does not depend on the board.
- `HandSettlementService`. It already settles `payout - TotalContributedThisHand` for `Active` and `IsAllIn` players. This fix has to make those two inputs correct before it is called.
- The odd-chip rule. For each pot, keep integer division and give the entire remainder to the first winner in the current winner-list order. Do not implement the seat-after-the-button rule in `docs/SidePots.md` §10.5.
- `CardGames.Poker.Betting.PotManager`. The API does not use it. Its `CalculateSidePots` adds any shortfall to the last pot. Do not call it.
- `FiveCardDraw/.../ProcessBettingActionCommandHandler.CalculateSidePots`. Do not copy it. A player contributes to a layer only when `contribution >= level`, so a folded player's smaller amount matches no layer. The method then adds the missing chips to the highest `PotOrder` pot. That puts dead money in the wrong pot.
- Generic, stud, draw, and Kings and Lows showdowns.
- `LeaveGameCommandHandler`. A folded player who has already left is still listed in `Game.GamePlayers`. Their `TotalContributedThisHand` stays in the pot as dead money. Do not drop those chips and do not try to settle them here.
- `TableStateBuilder`. It already sums each pot's `WinnerPayouts` by `playerName` and `amount`. Per-pot JSON makes that sum correct. Do not change the builder.

## Where the fix goes

All of the new behavior runs inside `PerformShowdownCommandHandler.Handle`, in the contested branch (`playersInHand.Count > 1`), and only when `isAlreadyAwarded` is false.

Put the pot math in a new pure type with no database and no hand ranking:

`src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/HoldEmPotLayers.cs`

```csharp
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
        IReadOnlyList<Slice> Slices);

    public readonly record struct BuildResult(
        int Uncalled,
        Guid? UncalledPlayerId,
        IReadOnlyList<Layer> Layers);

    public static BuildResult Build(IReadOnlyList<Stake> stakes) { ... }
}
```

`Contesting` means the player is in the existing `playersInHand` set: not folded, and `Status == Active` or `IsAllIn`. Pass that boolean in. The type does not look at `GamePlayer`.

## Order of work in the handler

Do the steps in this order. Evaluate cards before moving any chips. Return an error before `SaveChanges` if a check fails, and do not award, settle, or record history on that path.

1. Keep the existing load, phase check, `playersInHand` filter, card load, and `HoldemHand` evaluation.
2. Fold win: existing branch, unchanged.
3. If no hand can be evaluated: existing error, unchanged.
4. If `isAlreadyAwarded`: do not call `Build`, do not move chips, do not settle. Build the response payouts by summing stored `WinnerPayouts` across current-hand pots (`playerName`, `amount`), the same way the Five Card Draw showdown does in its already-awarded branch. Then build `PlayerHands` from the evaluation already done. `AmountWon` is that sum. `IsWinner` is `AmountWon > 0`.
5. If not already awarded, run the steps below, then `SaveChanges` once.

### Step A — the pot and the contributions must already match

```text
potSum = current-hand pots (HandNumber == game.CurrentHandNumber).Sum(Amount)
contributionSum = game.GamePlayers.Sum(TotalContributedThisHand)
```

If `potSum != contributionSum`, return `PerformShowdownErrorCode.InvalidGameState`. The message must contain both numbers. Do not add the difference to a pot. Do not award.

Blinds and betting both increment `TotalContributedThisHand` and the main pot by the same amount, so a normal hand matches. A mismatch means the hand is already wrong.

### Step B — return the uncalled bet

`HoldEmPotLayers.Build` does this first, from contesting stakes only.

- Let `ranked` be the contesting stakes sorted by `Contributed` descending.
- If there are fewer than two contesting stakes, `Uncalled` is 0. The handler does not call `Build` in the fold-win branch, so this is only a guard.
- `uncalled = ranked[0].Contributed - ranked[1].Contributed`.
- If `uncalled == 0`, nobody is refunded. Two players at the same maximum have called each other.
- If `uncalled > 0`, exactly one contesting player holds that maximum. `UncalledPlayerId` is that player's `GamePlayerId`.

Folded stakes do not cap the refund. The second highest contesting contribution is the cap.

`Build` takes the pre-refund stakes and subtracts `Uncalled` from that player's contribution internally before it builds layers. The layers it returns already exclude the refund. The handler does not pass reduced contributions back into `Build`.

The handler then applies the refund once, to the tracked entities:

- That player's `ChipStack += Uncalled`.
- That player's `TotalContributedThisHand -= Uncalled`.
- Do not change `IsAllIn`, `Status`, or `CurrentBet`.
- Do not put `Uncalled` in the payout dictionary, `WinnerPayouts`, `AmountWon`, or the hand-history winner list.

The refund is not a layer.

Worked result for the hand above: Alice 1000, Bob 100, Carol 300, all contesting. Uncalled is 700. Alice's contribution becomes 300. Her stack becomes 700.

### Step C — cap folded chips at the contesting maximum

Still inside `Build`, using contributions after the refund.

```text
cap = maximum Contributed among contesting stakes
```

For every stake, `matched = min(Contributed, cap)`. A folded player can have `Contributed > cap` only if the stored totals are inconsistent with a closed betting round. `excess = Contributed - matched` for those players. Sum it as `deadMoney`. It is added to the main pot in step E. It does not become its own pot. A folded player is never eligible.

If `cap == 0`, there is one layer of amount 0 and no refund. The handler's existing empty-pot behavior is fine.

### Step D — layers

Use `matched`, not the raw contribution.

Add `bool AllIn` to `Stake`. The handler sets it from the entity before any refund: `gamePlayer.IsAllIn || gamePlayer.ChipStack == 0`. `Build` keeps that flag on the player after it subtracts `Uncalled` from their contribution. A shove that is partly returned is still all-in at the reduced amount. Do not clear the flag because chips came back.

All-in levels are the distinct post-refund `matched` values of contesting stakes with `AllIn` true, where the value is greater than 0, sorted ascending.

If there is no all-in level, emit one layer:

- `PotOrder` 0, `PotType.Main`
- `Amount` = sum of every stake's `matched` + `deadMoney`
- One slice per stake with `matched > 0` or, for the folded player who produced `deadMoney`, one slice of `matched + excess`
- `Eligible` is true only for contesting stakes

That single layer is today's payout when nobody is all-in. Every contesting player is eligible for the whole pot, including folded chips.

If there is at least one all-in level:

```text
previous = 0
for each level in all-in levels:
    for each stake:
        slice = max(0, min(matched, level) - previous)
    layer amount = sum of slices
    eligible = contesting stakes whose matched >= level
    if layer amount > 0: emit the layer
    previous = level

if any matched > previous:
    slice = matched - previous
    emit that layer
    eligible = contesting stakes whose matched > previous
```

`slice` uses `min(matched, level) - previous`, not "only if `matched >= level`". A folded stake of 10 against a level of 60 contributes 10 to the first layer and 0 after that.

`PotOrder` starts at 0 and increases by 1 for each emitted layer. `PotOrder` 0 is `PotType.Main`. Later layers are `PotType.Side`. `MaxContributionPerPlayer` is `level - previous` for an all-in level, and `max(matched) - previous` for the final layer.

### Step E — dead money goes to the main pot

Add `deadMoney` to layer 0's `Amount`, and add it to the folded stake's slice on that layer. If layer 0 was not emitted, do not invent a side pot. Return no layers and let the handler fail the check in step F.

A layer with `Amount > 0` and no eligible slice must not be emitted. That chip total is `deadMoney` and belongs on the main pot. Do not drop it and do not mark a folded player eligible.

### Step F — the layers must conserve chips

`Build` takes pre-refund stakes and returns `Uncalled` plus layers of the reduced amounts. The handler applies `Uncalled` to the entity once and persists those layers. `Build` does not need the entities.

This identity must hold inside `Build`, and the handler must not persist a result that breaks it:

```text
Uncalled + sum(Layer.Amount) == sum(Stake.Contributed)   // pre-reduction contributions
sum(slices in a layer) == that layer's Amount
every Layer.Amount > 0 has at least one Eligible slice
```

If the handler sees a broken result, return `InvalidGameState` and persist nothing.

### Step G — replace the current-hand pots

Only the pots with `HandNumber == game.CurrentHandNumber`. Do not touch other hands.

- Delete their `PotContribution` rows through `context.PotContributions` (they may not be loaded).
- Remove the pot rows from `game.Pots`.
- Add one `Pot` per layer, with `GameId`, `HandNumber`, `PotOrder`, `PotType`, `Amount`, `MaxContributionPerPlayer`, `CreatedAt = now`, `IsAwarded = false`.
- Add one `PotContribution` per slice with `Amount > 0`: `GamePlayerId`, `Amount`, `IsEligibleToWin = slice.Eligible`, `ContributedAt = now`.

### Step H — award each pot

Walk the new pots by `PotOrder`.

Eligible players are the pot's contributions with `IsEligibleToWin` whose `GamePlayerId` has a `HoldemHand` in `playerHandEvaluations`. Match on `GamePlayer.Id`, not on name.

If a pot has eligible contribution rows but none of those players were evaluated, return `InvalidGameState` and do not save. Do not skip the pot. Skipping it deletes chips.

Winners for that pot:

- Strength is `HoldemHand.Strength` among those eligible evaluated players only.
- Walk `playersInHand` in its existing order and collect names whose evaluation is eligible and whose strength equals that maximum.
- Do not sort by seat. Do not sort the dictionary. `playersInHand` is `game.GamePlayers` filtered, with no `OrderBy`. That is the same order today's single-pot winner list uses, because the evaluation dictionary is filled by walking `playersInHand`.

Pay that pot:

```text
share = pot.Amount / winners.Count
remainder = pot.Amount % winners.Count
```

Each winner gets `share`. The first name in that winner list gets `remainder` as well. One pot's remainder is not carried to the next pot.

Add the per-pot amount into a `Dictionary<string, int>` keyed by `Player.Name`. A player who wins two pots has one entry, the sum.

Set on that pot only:

- `IsAwarded = true`, `AwardedAt = now`
- `WinReason` = that pot's winning `HoldemHand.Type` when there is one winner, or `"Split pot - {type}"` when that pot has more than one winner
- `WinnerPayouts` = JSON array of `{ playerId, playerName, amount }` for this pot's winners only, `amount` being this pot's payout, not the cross-pot sum

`playerId` is `GamePlayer.PlayerId` formatted with `ToString()`, the same anonymous shape the handler already writes. `TableStateBuilder` reads `playerName` and `amount`. Do not write the same JSON onto every pot.

### Step I — stacks, settlement, history

After every pot is awarded:

- `ChipStack +=` the dictionary value. The uncalled refund is already on the stack from step B. Do not add it again.
- `SettleHandAsync(game, payouts)` where `payouts` is that dictionary and does not contain the refund. Call it after `TotalContributedThisHand` has been reduced.
- Then the existing phase change, `MoveDealer`, `SaveChanges`, and hand history.

Hand history:

- `totalPot` is the sum of the new pot amounts. It does not include the refund.
- Winners are players whose dictionary value is greater than 0, with that value as `AmountWon`. A side-pot-only winner is a winner. A player who only received a refund is not.
- `IsSplitPot` is true only when some single pot had more than one winner. Two players winning different pots is not a split.
- `WinningHandDescription` is the main pot's `WinReason` (`PotOrder` 0).
- `NetChipDelta` already reads `TotalContributedThisHand` inside `RecordHandHistoryAsync`. Call it after the refund so the history delta matches the ledger.

Response `PlayerHands`:

- `IsWinner` is true when the dictionary value is greater than 0.
- `AmountWon` is that value, or 0. It does not include the refund.
- Keep the existing card, hand type, strength, and best-five-card index fields.

## Worked hands the code has to produce

Contributions below are `TotalContributedThisHand` after blinds and betting, which is the input. Stacks in the "before" column are the stacks once those chips have already left the player. `Build` sees the contributions. The handler then changes stacks.

### 1. Canonical all-in, two different amounts

Contributions: Alice 1000, Bob 100, Carol 300. All contesting. Alice and Bob and Carol are all-in. Pot row: 1400. Stacks before showdown: 0, 0, 0.

Refund 700 to Alice. Contributions become 300, 100, 300.

| PotOrder | Amount | Eligible | Winner |
|---|---|---|---|
| 0 | 300 | Alice, Bob, Carol | Bob (aces) |
| 1 | 400 | Alice, Carol | Carol (kings) |

Stacks after showdown: Alice 700, Bob 300, Carol 400.

`SettleHandAsync` dictionary:

| Player | Amount won | Contribution used | Net |
|---|---|---|---|
| Alice | 0 | 300 | -300 |
| Bob | 300 | 100 | +200 |
| Carol | 400 | 300 | +100 |

The nets sum to 0. Alice is absent from the payout dictionary or present as 0. Her `AmountWon` is 0 and `IsWinner` is false.

### 2. Heads-up uncalled shove

Alice 1000, Bob 100, both all-in, both contesting. Pot 1100.

Refund 900 to Alice. One pot of 200, both eligible. Bob wins. Stacks: Alice 900, Bob 200. Ledger: Alice -100, Bob +100.

### 3. Folded dead money stays in the main pot

Alice 110, Bob 60 all-in, Carol 110, Doug folded 10. Nobody has an uncalled amount (Alice and Carol are equal and highest). Pot 290.

| PotOrder | Amount | Eligible |
|---|---|---|
| 0 | 190 | Alice, Bob, Carol |
| 1 | 100 | Alice, Carol |

Doug's 10 is in pot 0. It is not in pot 1. Bob, winning pot 0, receives 190, not 180.

### 4. Three all-in amounts

A 20, B 50, C 100, D 100. All contesting. A and B all-in. Pot 270. No refund.

| PotOrder | Amount | Eligible |
|---|---|---|
| 0 | 80 | A, B, C, D |
| 1 | 90 | B, C, D |
| 2 | 100 | C, D |

### 5. No all-in

Alice 40, Bob 40, Carol 40, no one all-in, pot 120. `Uncalled` 0. One main pot of 120. All three eligible. The winner list and the remainder rule match the code this handler runs today: `120 / winners`, entire remainder to the first winner in `playersInHand` order.

### 6. Folded player contributed more than the players still in

Doug folded with 500. Bob and Carol contest with 100 each. No all-in. Cap is 100. `deadMoney` is 400. One main pot of 700. Bob and Carol eligible. Doug is not. The 400 is not a side pot.

### 7. Second showdown call

After hand 1 has saved, call the handler again. Stacks stay 700, 300, 400. `SettleHandAsync` is not called again. The response `Payouts` still read Bob 300 and Carol 400, taken from the stored per-pot `WinnerPayouts`.

### 8. Fold win

The existing test seeds a pot of 120 and folds Bob. Alice's stack increases by 120. No refund, no extra pot rows.

## Tests

Add `HoldEmPotLayersTests` next to the showdown handler tests:

`src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/HoldEmPotLayersTests.cs`

Call `HoldEmPotLayers.Build` directly. Cover hands 1 through 6 above, including Doug's 10 landing in the 190 pot and the folded 500 collapsing into one 700 pot. Assert the conservation identity on each case.

Extend `PerformShowdownCommandHandlerTests` using the existing in-memory `CardsDbContext` and `Substitute.For<IHandSettlementService>()`. The file already seeds Alice and Bob. Add Carol, contributions, `ChipStack` 0 where the player is all-in, one current-hand pot whose `Amount` equals the contribution sum, two hole cards each, and five community cards (`Location = Community`, `GamePlayerId = null`, `HandNumber` set).

Board for the ranking tests: `2c 7d 9c Js 3h`. Hole cards: Bob `As Ah`, Carol `Ks Kh`, Alice `Qs Qh`. Those are one pair each and do not interact with the board.

Handler assertions for hand 1:

- Success, `WonByFold` false.
- `Payouts` Bob 300, Carol 400, Alice absent or 0.
- Alice `AmountWon` 0, `IsWinner` false. Bob and Carol `IsWinner` true with 300 and 400.
- Reloaded stacks 700, 300, 400.
- Reloaded `TotalContributedThisHand` 300, 100, 300.
- Two current-hand pots, amounts 300 and 400, both `IsAwarded`, `PotOrder` 0 and 1.
- Pot 0 `WinnerPayouts` names Bob for 300. Pot 1 names Carol for 400. Pot 0's JSON does not contain 400.
- The settlement substitute received a dictionary whose nets are Alice -300, Bob +200, Carol +100. Assert the arguments `SettleHandAsync` was called with, not a reimplementation of the ledger.
- A second `Handle` call does not change stacks and does not call settlement again.

Also extend the handler tests with hand 3 (Doug's 10 must be inside the 190 pot that Bob wins) and hand 8 (the existing fold-win test still passes).

Run:

```text
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~HoldEm.v1.Commands.PerformShowdown"
```

## Done when

- Hands 1 through 8 produce the stacks, pot rows, payout dictionary, and settlement arguments above.
- The fold-win branch and the no-all-in single pot pay the same amounts they pay today.
- No file outside `PerformShowdownCommandHandler`, `HoldEmPotLayers`, and those two test files is modified.
- A second showdown call does not move chips and does not settle again.
