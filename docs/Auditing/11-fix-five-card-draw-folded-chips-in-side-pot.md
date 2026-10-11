# Fix: Five Card Draw's CalculateSidePots moves a folded player's chips to the wrong pot

## Defect

`FiveCardDraw/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.CalculateSidePots` rebuilds the current-hand pots when a betting round ends with someone all-in. A player is counted in a layer only when `TotalContributedThisHand >= level`. A folded player who put in less than the first all-in level matches no layer, so their chips are missing from the sum. This block then adds the gap to the pot with the highest `PotOrder`:

```csharp
var calculatedTotal = game.Pots.Where(p => p.HandNumber == game.CurrentHandNumber).Sum(p => p.Amount);
if (calculatedTotal != totalContributions && game.Pots.Any(p => p.HandNumber == game.CurrentHandNumber))
{
    var lastPot = game.Pots.Where(p => p.HandNumber == game.CurrentHandNumber)
        .OrderByDescending(p => p.PotOrder).First();
    lastPot.Amount += totalContributions - calculatedTotal;
}
```

That last pot is the side pot. The folded player's chips are awarded to whoever wins the side pot. No `PotContribution` row is written for those chips.

`AdvanceToNextPhase` calls `CalculateSidePots` when `playersInHand > 1` and some non-folded player has `IsAllIn`. The list it passes is `game.GamePlayers` with `Status == Active`, which still includes folded players. `PerformShowdownCommandHandler` then pays each pot's `Amount` to the rows with `IsEligibleToWin`. It does not rebuild the layers. Fixing the rows fixes the award.

The hand, as `TotalContributedThisHand` after the ante and the first betting round:

| Player | Contributed | Folded | All-in |
|---|---|---|---|
| Alice | 110 | no | no |
| Bob | 60 | no | yes |
| Carol | 110 | no | no |
| Doug | 10 | yes | no |

The pot before the rebuild is 290. Doug's 10 is the ante he folded behind.

| Pot | Expected | What the code saves |
|---|---|---|
| Main, `PotOrder` 0 | 190. Alice, Bob, and Carol eligible. Doug's 10 is in this pot and Doug is not eligible. | 180. Doug is absent. |
| Side, `PotOrder` 1 | 100. Alice and Carol eligible. | 110. The missing 10 was added here, with no row for Doug. |

If Bob has the best hand, Bob is paid 180 from the main pot. Bob should be paid 190. The side-pot winner is paid 110. They should be paid 100.

## Do not change

- `PerformShowdownCommandHandler`. It already awards `pot.Amount` to `IsEligibleToWin` rows and deals a remainder one chip at a time in the current winner-list order. Leave that remainder rule.
- The call in `AdvanceToNextPhase`. It already runs only when at least two players remain and a non-folded player is all-in. A fold win (`playersInHand <= 1`) returns before `CalculateSidePots`. Leave that.
- Betting actions, antes, and `TotalContributedThisHand`. They already put Doug's 10, Bob's 60, and Alice and Carol's 110 on the player and in the single pot. This method's job is to split that total.
- Uncalled bets. A layer above the highest all-in, with one eligible caller, stays a side pot. Showdown awards it to that player. Do not add a refund.
- `CardGames.Poker.Betting.PotManager`. The API does not call it. Its tests are not this bug.
- `TwosJacksManWithTheAxe/.../ProcessBettingActionCommandHandler.CalculateSidePots`. It is a copy of this method. Do not edit it in this change.
- Who is in `activePlayers`. Do not load `Status == Left` players into this method.

## The change

Replace the layer math inside `CalculateSidePots`. Keep the method where it is. Keep the existing delete-and-insert around it: delete current-hand `PotContribution` rows, remove those pots from `game.Pots`, then insert what the new math returns. Current-hand means `HandNumber == game.CurrentHandNumber`.

Put the math in a pure type so it can be tested without a database:

`src/CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/ProcessBettingAction/FiveCardDrawSidePots.cs`

```csharp
internal static class FiveCardDrawSidePots
{
    public readonly record struct Stake(
        Guid GamePlayerId,
        int Contributed,
        bool Folded,
        bool AllIn);

    public readonly record struct Slice(
        Guid GamePlayerId,
        int Amount,
        bool Eligible);

    public readonly record struct Layer(
        int PotOrder,
        int Amount,
        int MaxContributionPerPlayer,
        IReadOnlyList<Slice> Slices);

    public static IReadOnlyList<Layer> Build(IReadOnlyList<Stake> stakes) { ... }
}
```

`CalculateSidePots` maps each `activePlayers` entry to a `Stake` (`IsAllIn`, `HasFolded`, `TotalContributedThisHand`) and calls `Build`.

`Build` returns an empty list when fewer than two stakes are not folded, or when no non-folded all-in stake has `Contributed > 0`. `CalculateSidePots` must return immediately on an empty list. Do not delete the existing pots in that case. That is what the two early `return`s do today.

### Layers

All-in levels are the distinct `Contributed` values greater than 0 of stakes that are not folded and `AllIn`, sorted ascending.

```text
previous = 0
potOrder = 0
for each level in levels:
    for each stake:
        slice = max(0, min(Contributed, level) - previous)
    amount = sum of slices
    if amount > 0:
        emit a layer
        potOrder++
    previous = level

maxContributed = max Contributed among all stakes, or 0
if maxContributed > previous:
    slice = Contributed - previous for each stake where Contributed > previous
    emit that layer when the sum is greater than 0
```

`slice` does not require `Contributed >= level`. Doug at 10, with the first level 60 and `previous` 0, contributes `min(10, 60) - 0 = 10` to the first layer and `min(10, next) - 60 = 0` after that. He contributes nothing to the side pot.

A slice greater than 0 becomes one `Slice`:

- `Eligible` is true only when the stake is not folded and `Contributed >= level` for an all-in level.
- For the final layer, `Eligible` is true only when the stake is not folded and `Contributed > previous`.
- A folded stake is never eligible, including when `Contributed` is above the level.

`Amount` is the sum of that layer's slices. `MaxContributionPerPlayer` is `level - previous` for an all-in level, and `maxContributed - previous` for the final layer. `PotOrder` 0 is the first emitted layer. `CalculateSidePots` saves `PotOrder` 0 as `PotType.Main` and every later order as `PotType.Side`, which it already does.

One `PotContribution` per slice with `Amount > 0`: `GamePlayerId`, `Amount` equal to the slice (not the player's whole `TotalContributedThisHand`), `IsEligibleToWin = slice.Eligible`, `ContributedAt = now`. Do not write a second loop that decides folded rows with `contribution >= level`. The slices are the only source of rows, so `Pot.Amount` equals the sum of its rows.

### Delete the adjustment

Remove the block that adds `totalContributions - calculatedTotal` to the highest `PotOrder`. After `Build`, this identity holds:

```text
sum(Layer.Amount) == sum(Stake.Contributed)
sum(slices in a layer) == that layer.Amount
```

`CalculateSidePots` must not change `Amount` after it copies a layer onto a `Pot`. If the identity failed, adding the gap to the side pot is the bug this change removes.

## Worked hands

`Build` sees only the stakes. It does not see blinds, streets, or cards.

### 1. The reported hand

Stakes: Alice 110, Bob 60 all-in, Carol 110, Doug folded 10.

| PotOrder | Amount | Slices | Eligible |
|---|---|---|---|
| 0 | 190 | Alice 60, Bob 60, Carol 60, Doug 10 | Alice, Bob, Carol |
| 1 | 100 | Alice 50, Carol 50 | Alice, Carol |

Doug's row on pot 0 has `IsEligibleToWin` false. Doug has no row on pot 1. The amounts sum to 290.

Showdown, unchanged, then pays pot 0's 190 to the best hand among Alice, Bob, and Carol. If that is Bob, Bob receives 190. Pot 1's 100 goes to the better hand among Alice and Carol.

### 2. Folded player covered more than the all-in and less than the callers

Alice 110, Bob 60 all-in, Carol 110, Doug folded 80.

| PotOrder | Amount | Doug's slice | Eligible |
|---|---|---|---|
| 0 | 240 | 60, not eligible | Alice, Bob, Carol |
| 1 | 170 | 20, not eligible | Alice, Carol |

Doug's 80 is not added as a lump to pot 1. Pot 1 is Alice 50 + Carol 50 + Doug 20.

### 3. Two all-in amounts

Ann 20 all-in, Bob 50 all-in, Carol 100, Doug 100. Nobody folded.

| PotOrder | Amount | Eligible |
|---|---|---|
| 0 | 80 | Ann, Bob, Carol, Doug |
| 1 | 90 | Bob, Carol, Doug |
| 2 | 100 | Carol, Doug |

### 4. No all-in

Alice 40, Bob 40, Carol 40, none all-in. `Build` returns an empty list. `CalculateSidePots` returns without deleting the existing pot.

### 5. Only one player has not folded

`Build` returns an empty list. The caller already returns before `CalculateSidePots` in that case. The empty list is the guard inside `Build`.

## Tests

`src/Tests/CardGames.Poker.Tests/Api/Features/Games/FiveCardDraw/v1/Commands/ProcessBettingAction/FiveCardDrawSidePotsTests.cs`

Call `FiveCardDrawSidePots.Build` with no database. Cover hands 1 through 5. For hand 1, assert pot 0 is 190, pot 1 is 100, Doug's only slice is 10 on pot 0 with `Eligible` false, and the amounts sum to 290. For hand 2, assert Doug's 20 is on pot 1 and pot 1 is 170, not 190 with a 20-chip lump added on top of a 100-chip side pot.

There is no existing test that calls this handler's `CalculateSidePots`. Do not add a full betting-round integration test. The pure function is the behavior this method persists, and `CalculateSidePots` must copy `Layer.Amount` onto `Pot.Amount` without a later adjustment. A reviewer can see that by the deleted block.

Run:

```text
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~FiveCardDrawSidePotsTests"
```

## Done when

- Hand 1 produces pots of 190 and 100. Doug's 10 is a non-eligible row on the 190 pot and is not part of the 100 pot.
- The block that adds `totalContributions - calculatedTotal` to the highest pot is gone.
- `Pot.Amount` for each saved pot equals the sum of its `PotContribution.Amount` rows.
- Showdown is unchanged. A main-pot win for Bob on hand 1 pays 190 because that is now `pot.Amount`.
- No file outside `CalculateSidePots`, `FiveCardDrawSidePots.cs`, and `FiveCardDrawSidePotsTests.cs` is modified.
