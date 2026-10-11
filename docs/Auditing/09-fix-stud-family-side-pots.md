# Fix: Seven Card Stud, Good Bad Ugly, Baseball, and Follow the Queen pay the whole pot to the best hand

## Defect

Each of these games has its own showdown handler, and each one does the same thing. It sums every current-hand pot into `totalPot` and pays that amount to the best hand or hands:

```csharp
var payoutPerWinner = totalPot / winners.Count;
var remainder = totalPot % winners.Count;
foreach (var winner in winners) payouts[winner] = payoutPerWinner;
if (remainder > 0 && winners.Count > 0) payouts[winners[0]] += remainder;
```

Antes, the bring-in, bets, calls, raises, all-ins, and a Baseball buy all increase `TotalContributedThisHand` and the single main-pot `Amount`. They do not build a side pot. Ante rows and Baseball buy rows, where they exist, mark every contributor `IsEligibleToWin` and do not include the later bets. The showdown never reads them.

A live table calls these handlers from `TablePlay.TryLoadShowdownAsync`:

| Game | Handler |
|---|---|
| Seven Card Stud | `SevenCardStud/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| Good Bad Ugly | `GoodBadUgly/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| Baseball | `Baseball/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| Follow the Queen | `FollowTheQueen/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |

The hand that shows it. Ante 5. Stacks before the hand: Alice 500, Bob 55, Carol 500.

- Antes: 5 each. Pot 15. Bob has 50 left.
- Third street: bring-in 5, and the other two call. Each player has 10 in. Bob has 45 left.
- Fourth street: Alice bets 100. Bob calls all-in for 45. Carol calls 100.

`TotalContributedThisHand`: Alice 110, Bob 55, Carol 110. One pot of 275. Bob has the best hand.

Bob is paid 275. Bob should be paid 165. Alice and Carol should contest the other 110.

## Do not change

- Betting handlers, `CollectAntes`, `StudBringInPosting`, and `Baseball/.../ProcessBuyCardCommandHandler`. A buy already adds its price to `TotalContributedThisHand` and to the main pot. The layer math picks that up from `TotalContributedThisHand`. Do not special-case a buy.
- The Razz branch at the top of the Seven Card Stud handler. `gameTypeCode == RAZZ` returns through `DelegateRazzShowdownAsync` before any of this runs. Do not layer pots in the stud handler for Razz.
- Fold wins. `playersInHand.Count == 1` still adds `totalPot` to the last player. That pot already contains any uncalled chips. Do not also refund on that branch.
- Hand evaluation. Do not change `StudHand`, `GoodBadUglyHand`, `BaseballHand`, `FollowTheQueenHand`, wild ranks, or which cards are hole, board, or seventh street.
- Good Bad Ugly's elimination rule. A player with `VariantState == "UGLY_ELIMINATED"` does not win a pot when any other player eligible for that pot is not eliminated. If every player eligible for that pot is eliminated, those players split that pot. Apply this per pot, not once to the whole hand. The current code applies it once, then pays `totalPot`.
- The remainder rule. For each pot, integer division, and the entire remainder goes to the first winner in that pot's winner list. The comment says "closest to dealer's left". The list is not seat order. It is the order produced by walking `playersInHand`, which is `game.GamePlayers` with no `OrderBy`. Keep that. Do not switch these games to one chip at a time.
- `HandSettlementService`, `TableStateBuilder`, and `LeaveGameCommandHandler`.
- `CardGames.Poker.Betting.PotManager` and `FiveCardDraw/.../CalculateSidePots`. Do not call or copy either. A folded player's chips that are below an all-in level must stay in the lower pot. Those two methods drop that amount and then add it to the highest pot.
- Hold'em, Generic, Twos/Jacks, and Five Card Draw showdowns, except the sharing step below if a duplicate of this layer math already exists.

## Shared types

`src/CardGames.Poker.Api/Features/Games/Common/Showdown/ContestedPotLayers.cs`

If this file already exists, call it. Do not write a second copy. If `HoldEmPotLayers` exists and `ContestedPotLayers` does not, move the method into `ContestedPotLayers` and make `HoldEmPotLayers.Build` call it. If neither exists, create `ContestedPotLayers` with the contract and algorithm in the next section. Do not edit the Hold'em or Generic handlers except to retarget an existing duplicate `Build` method.

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

`src/CardGames.Poker.Api/Features/Games/Common/Showdown/ContestedShowdownAward.cs`

```csharp
public static class ContestedShowdownAward
{
    public readonly record struct RankedPlayer(
        Guid GamePlayerId,
        string Name,
        long Strength,
        bool CanWin,
        string HandTypeName);

    public readonly record struct PotAward(
        int PotOrder,
        int Amount,
        IReadOnlyList<(Guid GamePlayerId, string Name, int Amount)> Winners,
        string WinReason,
        bool Split);

    public readonly record struct AwardResult(
        IReadOnlyDictionary<Guid, int> PayoutsByGamePlayerId,
        IReadOnlyList<PotAward> Pots);

    public static AwardResult Award(
        IReadOnlyList<ContestedPotLayers.Layer> layers,
        IReadOnlyList<Guid> contestingOrder,
        IReadOnlyDictionary<Guid, RankedPlayer> ranks) { ... }
}
```

`contestingOrder` is `playersInHand` in its existing order, mapped to `GamePlayer.Id`. `ranks` contains only players who were actually evaluated. A contesting player who was skipped for having too few cards is absent from `ranks`.

## Layer algorithm

`Build` takes pre-refund stakes. It subtracts the uncalled amount from that one player's contribution internally, then cuts layers from the reduced amounts. The handler applies the refund once to the tracked entity and does not pass the reduced numbers back in.

`Contesting` means the player is in `playersInHand`: not folded, and `Status == Active` or `IsAllIn`. Pass every `GamePlayer`, including folded players.

`AllIn` is `gamePlayer.IsAllIn || gamePlayer.ChipStack == 0`, read before the refund. Keep that flag after the subtraction. A shove that is partly returned is still all-in at the reduced contribution.

Uncalled amount, contesting stakes only:

```text
ranked = contesting stakes ordered by Contributed descending
uncalled = ranked[0].Contributed - ranked[1].Contributed
```

This path has at least two contesting players. If `uncalled > 0`, one contesting player holds the maximum. Folded stakes do not cap the refund. The refund is not a pot and is not a payout.

The handler applies it once, before settlement:

- `ChipStack += Uncalled`
- `TotalContributedThisHand -= Uncalled`
- Do not change `IsAllIn`, `Status`, or `CurrentBet`
- Do not put `Uncalled` in `WinnerPayouts`, `AmountWon`, or the winner list

Then, on the post-refund contributions:

```text
cap = maximum Contributed among contesting stakes
matched = min(Contributed, cap) for each stake
deadMoney = sum of (Contributed - matched)
```

`deadMoney` is added to the main pot. A folded player is never eligible.

All-in levels are the distinct `matched` values greater than 0 of contesting stakes with `AllIn`, sorted ascending.

No all-in level: one layer, `PotOrder` 0, `Amount` = sum of `matched` + `deadMoney`. Every contesting stake is eligible. Folded chips, including `deadMoney`, are slices with `Eligible` false.

At least one all-in level:

```text
previous = 0
for each level:
    slice = max(0, min(matched, level) - previous) for each stake
    amount = sum of slices
    eligible = contesting stakes whose matched >= level
    if amount > 0: emit the layer
    previous = level

if any matched > previous:
    slice = matched - previous
    emit that layer
    eligible = contesting stakes whose matched > previous
```

Use `min(matched, level) - previous`. Do not require `matched >= level` before counting a slice. A folded 10 against a first level of 60 contributes 10 to the first layer and nothing above it.

`PotOrder` starts at 0. Order 0 is `PotType.Main`. Later orders are `PotType.Side`. `MaxContributionPerPlayer` is `level - previous` for an all-in level, and `max(matched) - previous` for the last layer.

Add `deadMoney` to layer 0. Do not emit a layer that has chips and nobody eligible. If that happens, `Build` returns no layers and the handler fails the check below.

This identity uses the pre-refund contributions. If it fails, return `InvalidGameState` and persist nothing:

```text
Uncalled + sum(Layer.Amount) == sum(Stake.Contributed)
sum(slices in a layer) == that layer's Amount
every Layer.Amount > 0 has at least one Eligible slice
```

## Award algorithm

`ContestedShowdownAward.Award` does not touch the database.

For each layer, in `PotOrder`:

1. Eligible ids are slices with `Eligible` true.
2. Ranked eligible players are those ids that exist in `ranks`. If an eligible id has no rank, the handler must not call `Award`. Return `InvalidGameState` before `Award`, and do not save. Do not skip the layer.
3. Candidates are the ranked eligible players. If any candidate has `CanWin` true, drop the ones with `CanWin` false. If every candidate has `CanWin` false, keep them all. Seven Card Stud, Baseball, and Follow the Queen pass `CanWin: true` for every evaluated player, so this filter does nothing there.
4. Walk `contestingOrder`. Collect candidates whose strength equals the maximum strength among the candidates that survived step 3. That walk order is the winner list. Do not sort by seat.
5. `share = layer.Amount / winners.Count`. `remainder = layer.Amount % winners.Count`. Each winner gets `share`. The first winner in that list also gets `remainder`.
6. `WinReason` is `HandTypeName` of the first winner when there is one winner. It is `"Split pot - {HandTypeName}"` when there is more than one and at least one has `CanWin` true. It is `"Split pot - all remaining players were eliminated by The Ugly"` when every winner has `CanWin` false.
7. `Split` is true when that pot has more than one winner.

`PayoutsByGamePlayerId` sums a player's pots. A player who wins nothing is absent. The refund is not an entry.

## What each handler does

Do this only when `playersInHand.Count > 1` and `isAlreadyAwarded` is false. Fold win has already returned. Razz has already returned on the stud handler.

The four handlers do not `Include` contributions. Load them with a query on the current-hand pot ids. The stud handler's `Include` stops at `Pots`.

1. Evaluate hands exactly as the handler does today. Build `ranks` from `playerHandEvaluations`.
   - Seven Card Stud, Baseball, Follow the Queen: `CanWin` true, `Strength` from the hand, `HandTypeName` from `hand.Type.ToString()`.
   - Good Bad Ugly: `CanWin` is false when `VariantState` equals `"UGLY_ELIMINATED"` ordinal-ignore-case. Otherwise true. Strength and hand type come from the evaluated hand, including a player who cannot win. The all-eliminated fallback needs those strengths.
2. Check `currentHandPots.Sum(Amount) == game.GamePlayers.Sum(TotalContributedThisHand)`. On mismatch, return that handler's `InvalidGameState` and include both numbers in the message. Do not add the difference to a pot.
3. `Build` from every `GamePlayer`.
4. Apply the refund to the one player.
5. Check the conservation identity. On failure, return `InvalidGameState` and do not save.
6. If any eligible slice has no rank, return `InvalidGameState` and do not save.
7. `Award`.
8. Replace current-hand pots (`HandNumber == game.CurrentHandNumber` only):
   - `context.PotContributions.RemoveRange` for those pot ids, including ante rows and Baseball buy rows
   - remove the pots from `game.Pots`
   - add one `Pot` per `PotAward`: `GameId`, `HandNumber`, `PotOrder`, `PotType`, `Amount`, `MaxContributionPerPlayer` from the matching layer, `CreatedAt = now`, `IsAwarded = true`, `AwardedAt = now`, `WinReason` from the pot award
   - `WinnerPayouts` is a JSON array of `{ playerId, playerName, amount }` for that pot only. `playerId` is `GamePlayer.PlayerId.ToString()`. `amount` is that pot's payout, not the cross-pot sum
   - one `PotContribution` per slice with `Amount > 0`: `IsEligibleToWin = slice.Eligible`
9. `ChipStack +=` the summed payout. The refund is already on the stack. Do not add it again.
10. `SettleHandAsync` with a dictionary keyed by `Player.Name`, values from the summed payouts, not including the refund. Call it after `TotalContributedThisHand` was reduced.
11. The existing phase change, `MoveDealer`, one `SaveChanges`, and hand history.

Hand history:

- `totalPot` is the sum of the new pot amounts. It does not include the refund.
- Winners are every player whose summed payout is greater than 0, with that sum as `AmountWon`. A side-pot-only winner is a winner. A refund-only player is not.
- `IsSplitPot` is true only when some `PotAward.Split` is true. Two players winning different pots is not a split. Each handler sets `isSplitPot` from `winners.Count > 1` inside `RecordHandHistoryAsync`. Pass a bool instead, or stop deriving it from the winner count.
- `WinningHandDescription` is the `WinReason` of `PotOrder` 0.
- `NetChipDelta` reads `TotalContributedThisHand`. History is recorded after the refund, so it matches settlement.

Response:

- `IsWinner` is true when that player's summed payout is greater than 0. Do not use the old single `winners` list. A player who has the best hand at the table but is not eligible for the side pot is a winner only for the pots they actually received.
- `AmountWon` is the summed payout, or 0. It does not include the refund.
- Leave cards, hand type, strength, and best-card indexes as they are.

When `isAlreadyAwarded` is true, do not call `Build`, do not move chips, and do not settle. The payout block currently sits outside `if (!isAlreadyAwarded)` and would recompute a single-pot split from the sum of the already-layered pots. Move the money calculation so the already-awarded path only sums stored `WinnerPayouts` (`playerName`, `amount`) across current-hand pots. `IsWinner` and `AmountWon` come from that sum. Evaluation still runs so the response can show cards.

## Worked hands

### 1. The reported stud hand

Contributions: Alice 110, Bob 55 all-in, Carol 110. All contesting. Pot 275. No refund.

| PotOrder | Amount | Eligible |
|---|---|---|
| 0 | 165 | Alice, Bob, Carol |
| 1 | 110 | Alice, Carol |

Bob has the best hand. Alice's hand beats Carol's. Bob receives 165. Alice receives 110. Carol receives 0.

Stacks before showdown are 0 for an all-in player and `starting stack - contributed` for the others. After showdown: Bob's stack increases by 165, Alice's by 110, Carol's by 0.

Settlement payouts: Bob 165, Alice 110. Contributions stay 55, 110, and 110. Nets: Bob +110, Alice 0, Carol −110.

### 2. Uncalled shove

Alice 200 all-in, Bob 40 all-in, both contesting. Pot 240. Refund 160 to Alice. One pot of 80, both eligible. Bob wins it. Alice's `AmountWon` is 0. Alice's stack increases by 160 only.

### 3. Folded dead money

Alice 110, Bob 60 all-in, Carol 110, Doug folded 10. Pot 290. No refund.

| PotOrder | Amount | Eligible |
|---|---|---|
| 0 | 190 | Alice, Bob, Carol |
| 1 | 100 | Alice, Carol |

Doug's 10 is in pot 0. It is not in pot 1.

### 4. Good Bad Ugly, per pot

Contributions: Alice 100, Bob 40 all-in, Carol 100. Pot 240. Bob's `VariantState` is `UGLY_ELIMINATED`. Bob has the highest strength. Alice's strength beats Carol's. Neither Alice nor Carol is eliminated.

| PotOrder | Amount | Eligible | Who can win | Winner |
|---|---|---|---|---|
| 0 | 120 | Alice, Bob, Carol | Alice, Carol | Alice |
| 1 | 120 | Alice, Carol | Alice, Carol | Alice |

Bob is eligible for pot 0 and receives nothing from it. Bob is not eligible for pot 1.

If Alice, Bob, and Carol are all `UGLY_ELIMINATED` and all three are eligible for a pot of 120, they split it 40/40/40. `WinReason` is `"Split pot - all remaining players were eliminated by The Ugly"`.

### 5. No all-in

Three contesting players, 40 each, none all-in. One main pot of 120. All three eligible. The best hand takes 120. This is today's result when the contributions are equal.

### 6. Second call

After hand 1 has saved, call the same handler again. Stacks do not change. `SettleHandAsync` is not called again. The response pays Bob 165 and Alice 110 from the stored JSON.

### 7. Fold win

One player left, pot 275. That player gains 275. No refund and no extra pot rows.

### 8. Stale ante rows

Hand 1's totals, plus three contribution rows of 5 on the single pot, all `IsEligibleToWin = true`. Those rows are removed. The saved pots are 165 and 110, not one pot of 275.

## Tests

`src/Tests/CardGames.Poker.Tests/Api/Features/Games/Common/Showdown/ContestedPotLayersTests.cs`

If plan 08 already added this file, add the cases that are missing. Call `Build` with no database. Cover hands 1, 2, 3, and 5. Assert the conservation identity.

`src/Tests/CardGames.Poker.Tests/Api/Features/Games/Common/Showdown/ContestedShowdownAwardTests.cs`

Cover hand 4 with made-up strengths. Bob strength 300 and `CanWin` false. Alice 200 and `CanWin` true. Carol 100 and `CanWin` true. Alice receives both pots. A second case sets all three `CanWin` false and a 120 pot eligible to all three; each receives 40 and `WinReason` is the Ugly sentence.

Extend `src/Tests/CardGames.Poker.Tests/Api/Features/Games/SevenCardStud/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`. The file already builds the stud handler from the service provider and seeds seven-card hands with `AddStudHand`. Add a third player and these hands, all seven cards, with the file's hole/board/seventh-street layout:

| Player | Cards | Hand |
|---|---|---|
| Bob | `As Ah Ad Ac 2c 3d 4h` | four aces |
| Alice | `Ks Kh Kd 2s 3s 4s 5c` | three kings |
| Carol | `Qs Qh Qd 2h 3h 4c 5d` | three queens |

`ParseSymbol` in that file does not parse ace, three, or four. Extend it for the ranks this test uses. Set contributions 110, 55, 110, stacks 0, `IsAllIn` true, one pot of 275, `CurrentHandGameTypeCode` null and `GameType.Code` `SEVENCARDSTUD` so the handler does not delegate to Razz. Capture `SettleHandAsync`. Assert hand 1, then call `Handle` again and assert the settlement list did not grow and the stacks did not change.

Add one more stud test with the same cards and three `PotContribution` rows of 5, `IsEligibleToWin` true, on that pot. Assert hand 8.

Do not change `Handle_WhenRazz_AwardsBestLowAndDescribesLowHand`. It returns before this code. Its pot of 100 and zero contributions are a Generic-showdown fixture, not this defect.

The Good Bad Ugly, Baseball, and Follow the Queen handlers have the same payout block. The unit tests cover their money and the Ugly filter. One stud handler test covers wiring, including settlement and the second call. Do not copy the seven-card fixture into the other three test files.

Run:

```text
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~ContestedPotLayersTests|FullyQualifiedName~ContestedShowdownAwardTests|FullyQualifiedName~SevenCardStud.v1.Commands.PerformShowdown"
```

If an existing test of these four handlers reaches a contested showdown and fails only because the pot amount is not the sum of `TotalContributedThisHand`, set the seeded contributions so they sum to that pot. Do not drop the equality check. The Razz test must not be one of those edits.

## Done when

- Hand 1 pays Bob 165 and Alice 110, and the saved pots are 165 and 110.
- The same numbers come out of all four handlers, because they call `ContestedPotLayers` and `ContestedShowdownAward` instead of `totalPot / winners.Count`.
- Good Bad Ugly applies elimination inside each pot. An eliminated short stack does not take the main pot when a non-eliminated player is eligible for it.
- A refund is not marked as a win and is subtracted from `TotalContributedThisHand` before settlement.
- Fold wins, Razz delegation, and hand evaluation are unchanged.
- A second showdown call does not move chips and does not settle again.
