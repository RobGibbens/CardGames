# Fix: Generic showdown makes every player in the hand eligible for the one pot

## Defect

`AwardPotsAsync` awards each pot only to players with `PotContribution.IsEligibleToWin`. That loop is not what a live table runs for an uneven all-in.

Blind games never write `PotContribution` rows. Blinds and bets only increase `Pot.Amount` and `GamePlayer.TotalContributedThisHand`. The pot then hits this fallback, and every player still in the hand is eligible for all of it:

```csharp
if (eligiblePlayerIds.Count == 0)
    eligiblePlayerIds = playerHandEvaluations.Values
        .Select(e => e.GamePlayer.Id)
        .ToHashSet();
```

Ante games do write contribution rows, but only for the ante, with `IsEligibleToWin = true`. Later bets increase `Pot.Amount` and `TotalContributedThisHand` and do not add contribution rows. Every player who anted stays eligible for the whole pot.

The live callers are:

| Game | Showdown entry |
|---|---|
| Omaha, Nebraska, South Dakota, Bob Barker, Klondike | `TablePlay.TryLoadShowdownAsync` calls `GamesApiClient.GenericPerformShowdownAsync` |
| Irish Hold'em, Phil's Mom, Crazy Pineapple | same, through `IsIrishStyleHoldEm` |
| Hold the Baseball | same. Do not edit `HoldTheBaseball/.../PerformShowdownCommandHandler.cs`. The table does not call it |
| Razz | `RazzApiClientWrapper` and `SevenCardStud/.../PerformShowdownCommandHandler.DelegateRazzShowdownAsync` both send `Generic.PerformShowdownCommand` |
| Pair Pressure, Tollbooth | their showdown handlers call the Generic handler |

Betting for the blind games above is `HoldEm/.../ProcessBettingActionCommandHandler`. Razz and Pair Pressure use the Seven Card Stud betting handler. Tollbooth uses its own betting handler plus `ChooseCardCommandHandler`, which also adds the toll to `TotalContributedThisHand` and the main pot. All of those already record the chip total on the player. None of them build side pots.

## Do not change

- The betting handlers, `PostBlindAsync`, `CollectAntes` handlers, `StudBringInPosting`, or `ChooseCardCommandHandler`. They already put the same chip count on the player and in the main pot.
- The fold-win branch `HandleWinByFoldAsync`. One remaining player is paid `totalPot`, which already includes any uncalled amount. Do not also refund on that branch.
- `HandleDeadHandAsync`.
- The inline path at the top of `Handle` (`flowHandler.SupportsInlineShowdown`). Screw Your Neighbor, In Between, and the Kings and Lows flow return there before `AwardPotsAsync`. Do not call the new pot builder on that path.
- Hand evaluation, Bob Barker showcase selection, and `AwardAmount`. Bob Barker already splits whatever pot it is given: `mainHandAmount = (pot.Amount + 1) / 2`, the rest is the showcase half, and the showcase winner is chosen from that pot's `eligibleHands` only. After layering, each layer is split by that same code among that layer's eligible players. Do not change the formula.
- `AwardAmount`'s remainder rule. It already hands one chip at a time to winners in the current list order. Do not switch Generic to "the entire remainder goes to the first winner".
- The empty-contribution fallback inside `AwardPotsAsync`. Leave those three lines. This fix stops the contested path from needing them. Deleting them would change a pot that still has no rows.
- `eligibleHands.Count == 0` then `continue`. Leave it.
- `HandSettlementService`, `TableStateBuilder`, and `LeaveGameCommandHandler`.
- `CardGames.Poker.Betting.PotManager` and `FiveCardDraw/.../CalculateSidePots`. Do not call or copy either. Both drop a folded player's short contribution and then add the missing chips to the highest pot.
- The Hold'em showdown handler, except the one sharing step below if `HoldEmPotLayers` already exists.

## Where the fix goes

Add a pure type with no database and no hand ranking:

`src/CardGames.Poker.Api/Features/Games/Common/Showdown/ContestedPotLayers.cs`

```csharp
public static class ContestedPotLayers
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

If `HoldEmPotLayers` already exists, move this logic into `ContestedPotLayers` and make `HoldEmPotLayers.Build` call it. Do not leave two copies of the layer math. If `HoldEmPotLayers` does not exist, do not create it and do not edit the Hold'em handler.

Call `Build` from `Generic/.../PerformShowdownCommandHandler.Handle` only after all of these are true:

- `flowHandler.SupportsInlineShowdown` was false, so execution reached the card-evaluation path
- `playersInHand.Count > 1`
- `isAlreadyAwarded` is false
- hands were evaluated (`playerHandEvaluations` is already built)

That is immediately before the existing `AwardPotsAsync` call. Do not rebuild on the already-awarded path. That path already sums stored `WinnerPayouts`.

## Order of work

Evaluate cards before moving chips. On a failed check, return `PerformShowdownErrorCode.InvalidGameState` and do not award, settle, save, or record history.

### Step A — pot total must already match contributions

```text
potSum = current-hand pots.Sum(Amount)
contributionSum = game.GamePlayers.Sum(TotalContributedThisHand)
```

Current-hand pots are `HandNumber == game.CurrentHandNumber`, which `currentHandPots` already is.

If `potSum != contributionSum`, return `InvalidGameState`. The message must contain both numbers. Do not add the difference to a pot.

`OmahaShowdownHardeningTests` seeds a pot of 100 and leaves both players at `TotalContributedThisHand == 0`. That test will fail this check. Set those two players to 50 and 50, leave `IsAllIn` false, and leave the cards and the expected payout (`Player 2` wins 100) as they are. Do not special-case "all contributions are zero".

### Step B — `Build` takes pre-refund stakes

`Contesting` is true when the player is in `playersInHand`: not folded, and `Status == Active` or `IsAllIn`.

`AllIn` is `gamePlayer.IsAllIn || gamePlayer.ChipStack == 0`, read before any refund.

`Contributed` is the current `TotalContributedThisHand`. Pass every `GamePlayer`, including folded players and players who are not contesting.

`Build` subtracts the uncalled amount from that one player's contribution internally, then builds layers from the reduced amounts. The handler does not pass the reduced numbers back in.

Uncalled amount, from contesting stakes only:

```text
ranked = contesting stakes ordered by Contributed descending
uncalled = ranked[0].Contributed - ranked[1].Contributed
```

There are at least two contesting players on this path. If `uncalled > 0`, exactly one contesting player has the maximum. `UncalledPlayerId` is that `GamePlayerId`. Folded stakes do not cap the refund.

The handler applies that result once:

- `ChipStack += Uncalled`
- `TotalContributedThisHand -= Uncalled`
- Do not change `IsAllIn`, `Status`, or `CurrentBet`
- Do not put `Uncalled` in the payout dictionary, `WinnerPayouts`, `AmountWon`, or the winner list

A refund must not become a one-player pot. Bob Barker would split that pot into a high-hand half and a showcase half.

Keep the `AllIn` flag after the subtraction. A shove that is partly returned is still all-in at the reduced contribution.

### Step C — cap folded chips, then cut layers

Still inside `Build`, on the post-refund contributions.

```text
cap = maximum Contributed among contesting stakes
matched = min(Contributed, cap) for each stake
deadMoney = sum of (Contributed - matched)
```

`deadMoney` is added to the main pot in step D. A folded player is never eligible.

All-in levels are the distinct `matched` values greater than 0 of contesting stakes with `AllIn`, sorted ascending.

If there is no all-in level, emit one layer:

- `PotOrder` 0
- `Amount` = sum of `matched` + `deadMoney`
- a slice for each stake with `matched > 0`; the folded stake that produced `deadMoney` has slice `matched + excess`
- `Eligible` is true only for contesting stakes

If there is at least one all-in level:

```text
previous = 0
for each level:
    for each stake:
        slice = max(0, min(matched, level) - previous)
    amount = sum of slices
    eligible = contesting stakes whose matched >= level
    if amount > 0: emit the layer
    previous = level

if any matched > previous:
    slice = matched - previous
    emit that layer
    eligible = contesting stakes whose matched > previous
```

Use `min(matched, level) - previous`. Do not require `matched >= level` before counting the slice. A folded contribution of 10 against a first level of 60 puts 10 in the first layer and nothing above it.

`PotOrder` starts at 0. Order 0 is `PotType.Main`. Later orders are `PotType.Side`. `MaxContributionPerPlayer` is `level - previous` for an all-in level, and `max(matched) - previous` for the last layer.

### Step D — dead money and conservation

Add `deadMoney` to layer 0's `Amount` and to the folded stake's slice on that layer. Do not emit a layer that has chips and no eligible slice. If layer 0 was not emitted, `Build` returns no layers and the handler fails step E.

This identity has to hold, using the pre-refund contributions:

```text
Uncalled + sum(Layer.Amount) == sum(Stake.Contributed)
sum(slices in a layer) == that layer's Amount
every Layer.Amount > 0 has at least one Eligible slice
```

If it does not, the handler returns `InvalidGameState` and persists nothing.

### Step E — replace the current-hand pots

Only `HandNumber == game.CurrentHandNumber`.

- Delete those pots' `PotContribution` rows through `context.PotContributions`. They are loaded (`Include` already has `ThenInclude(p => p.Contributions)`), and ante rows are among them. Remove the loaded rows as well as querying, so the tracked ante rows cannot be saved again.
- Remove the pot entities from `game.Pots`.
- Add one `Pot` per layer: `GameId`, `HandNumber`, `PotOrder`, `PotType`, `Amount`, `MaxContributionPerPlayer`, `CreatedAt = now`, `IsAwarded = false`.
- Add one `PotContribution` per slice with `Amount > 0`: `GamePlayerId`, `Amount`, `IsEligibleToWin = slice.Eligible`, `ContributedAt = now`.

Set the local `currentHandPots` list to these new pots before `AwardPotsAsync`. Set the `totalPot` value that `RecordHandHistoryAsync` receives to `currentHandPots.Sum(p => p.Amount)` after the replacement. Do not pass the pre-refund sum. The fold-win branch has already returned, so this assignment does not change a fold win.

### Step F — the existing award

Call `AwardPotsAsync` with the replaced pots. Do not rewrite it.

It will see contribution rows, so it will not take the "everyone in the hand" fallback. Each pot is ranked only among its eligible evaluated players. Bob Barker splits each of those pots with the existing half-and-half formula. `AwardAmount` still distributes one leftover chip at a time.

Then the existing code adds `payouts` to `ChipStack`, settles, saves once, and records history. The refund is already on the stack and already subtracted from `TotalContributedThisHand` before settlement and before `RecordHandHistoryAsync` reads that field. `payouts` does not include the refund.

`IsWinner` stays "name is in `allWinners`". A player who only received a refund is not added there. `AmountWon` is the dictionary value.

A second call sees `IsAwarded`, skips `Build`, and reconstructs payouts from `WinnerPayouts`. Each pot's JSON is written by the existing `potPayoutsList` and contains only that pot's amounts. Do not write one combined JSON onto every pot.

## Worked hands

Stacks in the "before" column are the stacks after the chips have left the player. `Contributed` is `TotalContributedThisHand`.

### 1. Omaha all-in, the reported hand

Contributions: Player 1 (seat 0) 1000, Player 2 (seat 1) 100, Player 3 (seat 2) 300. All contesting, all all-in, stacks 0. One pot of 1400. No contribution rows. Board `2c 7d 9c Js 3h`.

Omaha uses exactly two hole cards. Give each player four hole cards whose pair is two hole cards, plus two unmatched low cards that are not on the board:

| Player | Hole cards | Made hand |
|---|---|---|
| Player 2 | `As Ah 4c 5d` | pair of aces |
| Player 3 | `Ks Kh 4d 5c` | pair of kings |
| Player 1 | `Qs Qh 4h 5s` | pair of queens |

Refund 700 to Player 1. Contributions become 300, 100, 300.

| PotOrder | Amount | Eligible | Winner |
|---|---|---|---|
| 0 | 300 | Player 1, Player 2, Player 3 | Player 2 |
| 1 | 400 | Player 1, Player 3 | Player 3 |

Stacks: Player 1 = 700, Player 2 = 300, Player 3 = 400.

`SettleHandAsync` payouts: Player 2 = 300, Player 3 = 400, Player 1 absent or 0. Contributions at settlement: 300, 100, 300. Nets: Player 1 −300, Player 2 +200, Player 3 +100.

`AmountWon` for Player 1 is 0 and `IsWinner` is false.

### 2. Stale ante rows must be ignored

Same totals and cards as hand 1, but the single pot also has three contribution rows of 5 with `IsEligibleToWin = true`. That is the ante-game shape: the rows do not add up to the pot.

The result is the same as hand 1. Those three rows are deleted. Player 2 does not receive 1400.

### 3. Pair Pressure / Tollbooth shape

Contributions after a 5 ante and the betting: A 205, B 50 all-in, C 205. Pot 460. B has the best hand.

No refund (A and C are tied for the most).

| PotOrder | Amount | Eligible |
|---|---|---|
| 0 | 150 | A, B, C |
| 1 | 310 | A, C |

B wins 150, not 460. A and C contest the 310.

### 4. Folded chips stay in the lowest layer

A 110, B 60 all-in, C 110, D folded 10. Pot 290. No refund.

| PotOrder | Amount | Eligible |
|---|---|---|
| 0 | 190 | A, B, C |
| 1 | 100 | A, C |

D's 10 is in pot 0. If B wins pot 0, B receives 190.

### 5. Folded player put in more than the players still contesting

D folded with 500. B and C contest with 100 each. No all-in. Cap is 100. `deadMoney` is 400. One main pot of 700. B and C are eligible. D is not.

### 6. No all-in

Three contesting players, 40 each, pot 120, none all-in. One main pot of 120. All three eligible. The winner takes 120. This is the `OmahaShowdownHardeningTests` shape once contributions sum to the pot.

### 7. Second call

After hand 1 has saved, call the handler again. Stacks stay 700, 300, 400. Settlement is not called again. Response payouts still come from the stored per-pot JSON: Player 2 = 300, Player 3 = 400.

### 8. Fold win

A pot of 120 and one player left. That player gains 120. No refund and no extra pot rows.

## Tests

`src/Tests/CardGames.Poker.Tests/Api/Features/Games/Common/Showdown/ContestedPotLayersTests.cs`

Call `ContestedPotLayers.Build` with no database. Cover hands 1 and 3 through 6, including D's 10 landing in the 190 pot and the folded 500 collapsing into one 700 pot. Assert the conservation identity on each case.

Add a handler test to `src/Tests/CardGames.IntegrationTests/Features/Commands/OmahaShowdownHardeningTests.cs`. Copy that file's handler construction (`FlowHandlerFactory`, `HandEvaluatorFactory`, `FakeHandHistoryRecorder`, `FakeHandSettlementService`) and its card helpers. Seed three players with `DatabaseSeeder.CreateCompleteGameSetupAsync(..., OmahaCode, 3, ...)`, then overwrite `ChipStack`, `IsAllIn`, and `TotalContributedThisHand` to the hand-1 figures and add one pot of 1400. Assert hand 1's payouts, stacks, contributions, two awarded pots, and the settlement dictionary. Then call `Handle` again and assert stacks and that `SettledHands` did not grow.

Add a second handler test with the same cards and totals plus three `PotContribution` rows of 5 on that pot, all `IsEligibleToWin = true`. Assert the same payouts as hand 1, and that the reloaded pots are 300 and 400 rather than one pot of 1400.

Update the existing `Handle_OmahaShowdown_UsesExactlyTwoHoleCardsForWinnerResolution` so the two players' `TotalContributedThisHand` values sum to 100 and neither is all-in. Player 2 still wins 100.

Run:

```text
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~ContestedPotLayersTests"
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~OmahaShowdownHardeningTests"
```

If another existing Generic showdown test fails only because its pot amount is not the sum of `TotalContributedThisHand`, set the seeded contributions so they sum to that pot and nobody is all-in, unless that test is already an all-in case. Do not drop the equality check.

## Done when

- Hands 1 through 8 produce the stacks, pot rows, and settlement dictionary above.
- A contested Omaha hand with no contribution rows no longer pays the whole pot to the best hand when the all-in amounts differ.
- Ante contribution rows that do not account for later bets are replaced before the award.
- Fold wins, dead hands, and `SupportsInlineShowdown` games are unchanged.
- Bob Barker still uses `(pot.Amount + 1) / 2` per pot, and a refund is not run through that split.
- `AwardAmount` still gives leftover chips out one at a time.
