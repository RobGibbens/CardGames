# Fix: Twos, Jacks and the Man with the Axe showdown ignores the side pots betting built

## Defect

`TwosJacksManWithTheAxe/.../ProcessBettingActionCommandHandler.CalculateSidePots` runs when a betting round ends with someone all-in. It writes main and side pots and `PotContribution` rows.

`TwosJacksManWithTheAxe/.../PerformShowdownCommandHandler.Handle` does not read those rows. It sums every current-hand pot and splits that one number:

```csharp
var sevensPool = totalPot / 2;
var handPool = totalPot - sevensPool;
```

Anyone with a natural pair of sevens, and anyone with the best hand at the table, is paid from that sum. A short all-in is paid from chips they did not put in.

The hand. Contributions after the ante and the betting: Alice 110, Bob 55 all-in, Carol 110. Pot 275. Bob has a natural pair of sevens and the best high hand.

Bob is paid 275. Bob should be paid the main pot only, 165. The side pot, 110, belongs to Alice and Carol.

`CardGames.Poker/Games/TwosJacksManWithTheAxe/TwosJacksManWithTheAxeGame` already calls `PotManager.AwardPotsSplit` per eligible set. The API does not run that class. A live table runs the showdown handler above, from `TablePlay.TryLoadShowdownAsync` through `TwosJacksManWithTheAxePerformShowdownAsync`.

## Do not change

- `CalculateSidePots` and the rest of the betting handler. Showdown replaces the current-hand pots before it awards them. Those betting rows are not the award. They also drop a folded player's chips when `contribution < level` and then add the missing amount to the highest pot. Do not copy that.
- `CardGames.Poker.Betting.PotManager`. Do not call it. Its `CalculateSidePots` has the same last-pot adjustment, and the API does not use it.
- The fold-win branch, `playersInHand.Count == 1`. The last player receives `totalPot`. Do not also refund an uncalled bet there, and do not split that pot into sevens and high hand.
- `HasNaturalPairOfSevens`, `TwosJacksManWithTheAxeDrawHand`, and wild-card rules. A natural pair is two or more sevens in the evaluated cards. Sevens are not wild.
- How a single pot is split. `sevensPool = amount / 2`. `handPool = amount - sevensPool`, so the odd chip of that pot is in the high-hand half. Each half then gives leftover chips out one at a time.
- The `orderedPlayerNames` loop. It treats `DealerPosition` as an index into a list of size `GamePlayers.Count`. That order is wrong when seats are not `0 .. Count-1`. Leave the loop. Use its result as the remainder order. Do not sort by seat.
- `HandSettlementService`, `TableStateBuilder`, and `LeaveGameCommandHandler`. `TableStateBuilder` already sums `amount`, `sevensAmount`, and `highHandAmount` across pots. Per-pot JSON makes that sum correct.

## Where the fix goes

All of the new award behavior is in `PerformShowdownCommandHandler.Handle`, contested branch only (`playersInHand.Count > 1`, `isAlreadyAwarded` false), after hands are evaluated and after `orderedPlayerNames` is built.

Rebuild the layers from `TotalContributedThisHand`. Do not award the pots `CalculateSidePots` saved.

Use `ContestedPotLayers.Build` if that file already exists at `src/CardGames.Poker.Api/Features/Games/Common/Showdown/ContestedPotLayers.cs`. If it does not exist, create it with the contract and algorithm below. Do not edit the Hold'em, Generic, or stud showdown handlers except to retarget an existing duplicate `Build` method at this type.

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

Put the sevens split in a new pure type, no database and no cards:

`src/CardGames.Poker.Api/Features/Games/TwosJacksManWithTheAxe/v1/Commands/PerformShowdown/TwosJacksPotSplit.cs`

```csharp
public static class TwosJacksPotSplit
{
    public readonly record struct PlayerRank(
        Guid GamePlayerId,
        string Name,
        bool HasNaturalSevens,
        long Strength);

    public readonly record struct PotPayout(
        int PotOrder,
        int Amount,
        bool SevensRolledOver,
        IReadOnlyList<(Guid GamePlayerId, string Name, int Amount, int SevensAmount, int HighHandAmount)> Winners,
        string WinReason);

    public readonly record struct SplitResult(
        IReadOnlyDictionary<Guid, int> Payouts,
        IReadOnlyDictionary<Guid, int> SevensPayouts,
        IReadOnlyDictionary<Guid, int> HighHandPayouts,
        IReadOnlyList<PotPayout> Pots);

    public static SplitResult Split(
        IReadOnlyList<ContestedPotLayers.Layer> layers,
        IReadOnlyList<Guid> remainderOrder,
        IReadOnlyDictionary<Guid, PlayerRank> ranks) { ... }
}
```

`remainderOrder` is `orderedPlayerNames` mapped back to `GamePlayer.Id`, in that list's order.

## Order of work

Evaluate cards before moving chips. On a failed check, return `PerformShowdownErrorCode.InvalidGameState` and do not award, settle, save, or record history.

### Step A — the pot must match the contributions

```text
potSum = current-hand pots.Sum(Amount)
contributionSum = game.GamePlayers.Sum(TotalContributedThisHand)
```

Current-hand pots are `HandNumber == game.CurrentHandNumber`. If the two sums differ, return `InvalidGameState` and put both numbers in the message. Do not add the difference to a pot.

### Step B — rebuild layers, including the uncalled refund

`Contesting` means the player is in `playersInHand`. Pass every `GamePlayer`.

`AllIn` is `gamePlayer.IsAllIn || gamePlayer.ChipStack == 0`, read before any refund.

`Build` takes those pre-refund stakes. It subtracts the uncalled amount inside itself, then cuts layers from the reduced contributions. The handler applies the refund once and does not pass the reduced numbers back in.

Uncalled amount, contesting stakes only:

```text
ranked = contesting stakes ordered by Contributed descending
uncalled = ranked[0].Contributed - ranked[1].Contributed
```

If `uncalled > 0`, one contesting player holds the maximum. Folded stakes do not cap it. The handler then:

- `ChipStack += Uncalled`
- `TotalContributedThisHand -= Uncalled`
- does not change `IsAllIn`, `Status`, or `CurrentBet`
- does not put `Uncalled` in `Payouts`, `SevensPayouts`, `HighHandPayouts`, `AmountWon`, or `WinnerPayouts`

A refund must not be split into a sevens half and a high-hand half.

`Build` then caps every contribution at the maximum contesting contribution. `deadMoney` is the sum of what folded players had above that cap. It is added to the main pot. A folded player is never eligible.

All-in levels are the distinct post-refund matched amounts greater than 0 of contesting stakes with `AllIn`, sorted ascending. Keep the `AllIn` flag after the refund. A shove that is partly returned is still all-in at the reduced amount.

No all-in level: one main pot for the matched chips plus `deadMoney`. Every contesting player is eligible.

Otherwise:

```text
previous = 0
for each level:
    slice = max(0, min(matched, level) - previous) for each stake
    emit the layer when the sum of slices is greater than 0
    eligible = contesting stakes whose matched >= level
    previous = level

if any matched > previous:
    emit a layer of (matched - previous)
    eligible = contesting stakes whose matched > previous
```

Use `min(matched, level) - previous`. Do not require `matched >= level` before counting the slice. A folded 10 against a first level of 60 stays in the first layer.

`PotOrder` 0 is `PotType.Main`. Later orders are `PotType.Side`. Do not emit a layer that has chips and nobody eligible. If layer 0 was not emitted, fail step C.

This identity uses the pre-refund contributions. Fail step C when it does not hold:

```text
Uncalled + sum(Layer.Amount) == sum(Stake.Contributed)
sum(slices in a layer) == that layer's Amount
every Layer.Amount > 0 has at least one Eligible slice
```

### Step C — split each layer

Call `TwosJacksPotSplit.Split`. If an eligible slice has no entry in `ranks`, return `InvalidGameState` before `Split` and do not save. A player skipped for having fewer than five cards is absent from `ranks`.

For each layer:

1. Eligible ids are slices with `Eligible` true that exist in `ranks`.
2. Sevens winners are those ids whose `HasNaturalSevens` is true.
3. High-hand winners are those ids whose `Strength` equals the maximum strength among the eligible ids.
4. Order each winner set by walking `remainderOrder`. Drop anyone not in that walk. If a non-empty winner set becomes empty after that walk, treat it as empty and roll or fail as below. Do not divide by zero.
5. `sevensPool = layer.Amount / 2`. `handPool = layer.Amount - sevensPool`.
6. If there is no sevens winner, add `sevensPool` to `handPool`, set `sevensPool` to 0, and set `SevensRolledOver` true for this pot. The rolled chips stay in this pot. They do not move to another pot, and they are not paid to a sevens holder who is not eligible for this pot.
7. If `handPool > 0` and there is no high-hand winner, return `InvalidGameState`. Do not save.
8. Pay each pool with integer division. Walk that pool's ordered winners and give one leftover chip to each until the remainder is gone.
9. A player's pot `amount` is `sevensAmount + highHandAmount`.
10. `WinReason` for a pot that paid sevens is `7s: {names}; High: {names}`, names in that pot's winner order. For a pot whose sevens half rolled, `WinReason` is the high-hand `hand.Type` when one player wins it, or `"Split pot - {type}"` when more than one does. Use the first high-hand winner's evaluated type.

Sum `Payouts`, `SevensPayouts`, and `HighHandPayouts` across pots. A player with nothing is absent.

`SevensPoolRolledOver` on the HTTP response is true only when every pot that had `Amount > 0` has `SevensRolledOver` true. One pot paying sevens and another rolling is false.

### Step D — replace the pots and pay

Delete current-hand `PotContribution` rows through `context.PotContributions`, then remove those pots from `game.Pots`. The handler does not `Include` contributions today. Query them. Remove tracked rows too, so a pot built by `CalculateSidePots` cannot be saved again.

Add one `Pot` per `PotPayout`: `GameId`, `HandNumber`, `PotOrder`, `PotType`, `Amount`, `MaxContributionPerPlayer` from the matching layer, `CreatedAt = now`, `IsAwarded = true`, `AwardedAt = now`, `WinReason`.

`WinnerPayouts` is a JSON array of `{ playerId, playerName, amount, sevensAmount, highHandAmount }` for this pot only. `playerId` is `GamePlayer.PlayerId.ToString()`. The amounts are this pot's amounts, not the cross-pot totals. Do not write the same JSON onto every pot.

Add one `PotContribution` per slice with `Amount > 0`: `IsEligibleToWin = slice.Eligible`.

Then:

- `ChipStack +=` the summed payout. The refund is already on the stack.
- `SettleHandAsync` with the summed payout dictionary keyed by `Player.Name`. Call it after the refund reduced `TotalContributedThisHand`. The dictionary does not include the refund.
- The existing phase change, `MoveDealer`, one `SaveChanges`, and hand history.

Hand history `totalPot` is the sum of the new pot amounts, not the pre-refund sum. Winners are players whose summed payout is greater than 0. `NetChipDelta` reads `TotalContributedThisHand`, so record history after the refund. `WinningHandDescription` is the `WinReason` of `PotOrder` 0.

Response fields:

- `Payouts`, `SevensPayouts`, and `HighHandPayouts` are the summed dictionaries.
- `SevensWinners` is the names with a sevens payout greater than 0. `HighHandWinners` is the names with a high-hand payout greater than 0. A player can be in both. A player who has sevens but was not eligible for any pot that paid sevens is in neither list.
- `IsSevensWinner`, `IsHighHandWinner`, `SevensAmountWon`, and `HighHandAmountWon` match those sums.
- `IsWinner` is true when the summed payout is greater than 0. `AmountWon` is that sum. A refund alone is not a win.
- Leave cards, hand type, description, strength, and `WildCardIndexes` as they are.

### Already awarded

The split currently runs even when `isAlreadyAwarded` is true, and only the chip update is guarded. After the first save the pots are layers. A second call must not add those layers back together and split the sum.

When `isAlreadyAwarded` is true, do not call `Build` or `Split`, do not move chips, and do not settle. Sum stored `WinnerPayouts` across current-hand pots into `amount`, `sevensAmount`, and `highHandAmount`. Build `Payouts`, `SevensPayouts`, `HighHandPayouts`, and the response lists from those sums. `SevensPoolRolledOver` is true when the summed sevens amount is 0 and the summed pot amount is greater than 0.

## Worked hands

### 1. The reported hand

Contributions: Alice 110, Bob 55 all-in, Carol 110. Pot 275. No refund.

Bob's cards: `7h 7d As Ah Ad`. Natural sevens, and the best high hand.
Alice's cards: `Ks Kh Kd Qc 9d`. Three kings. No sevens.
Carol's cards: `Qs Qh Qd Tc 9c`. Three queens. No sevens.

| PotOrder | Amount | Eligible | Sevens half | High half | Paid |
|---|---|---|---|---|---|
| 0 | 165 | Alice, Bob, Carol | 82 to Bob | 83 to Bob | Bob 165 |
| 1 | 110 | Alice, Carol | rolled, no sevens | 110 to Alice | Alice 110 |

`SevensPoolRolledOver` is false. Bob's `SevensAmountWon` is 82 and `HighHandAmountWon` is 83. Alice's `HighHandAmountWon` is 110 and `SevensAmountWon` is 0. Carol is not a winner.

Settlement payouts: Bob 165, Alice 110. Contributions stay 55, 110, and 110. Nets: Bob +110, Alice 0, Carol −110.

Stacks before showdown are 0 for Bob and `starting stack - contributed` for Alice and Carol. After showdown Bob's stack is 165 higher, Alice's is 110 higher, and Carol's is unchanged.

### 2. Side-pot sevens stay with the player who covered it

Same contributions. Bob's cards stay the full house. Alice's cards are `7s 7c Kh Kd 9d` (natural sevens, two pair). Carol's stay three queens.

Main pot: sevens half 82 to Bob and Alice, one chip at a time in `orderedPlayerNames` order. High half 83 to Bob, because the full house beats Alice and Carol.

Side pot: Bob is not eligible. Sevens half 55 to Alice. High half 55 to Carol, because three queens beat Alice's two pair.

Bob is not paid from the 110.

### 3. No all-in, nobody has sevens

Three contesting players, 40 each, none all-in. Pot 120. One main pot. Sevens half rolls. The best hand is paid 120. `SevensPoolRolledOver` is true. This is today's result for an even pot with no short all-in.

### 4. Uncalled shove

Alice 200 all-in, Bob 40 all-in. Pot 240. Refund 160 to Alice. One pot of 80, both eligible. That 80 is split by the sevens rule. The 160 is not in `SevensAmountWon` or `HighHandAmountWon`.

### 5. Folded chips

Alice 110, Bob 60 all-in, Carol 110, Doug folded 10. Pot 290. No refund. Main pot 190, eligible Alice, Bob, Carol. Side pot 100, eligible Alice and Carol. Doug's 10 is in the 190, not the 100.

### 6. Second call

After hand 1 has saved, call the handler again. Stacks stay where hand 1 left them. Settlement is not called again. The response still shows Bob 165, of which 82 is sevens and 83 is high hand, and Alice 110 of high hand, read from the two pot JSON values added together.

### 7. Fold win

One player left, pot 275. That player gains 275. No sevens split and no refund.

## Tests

`src/Tests/CardGames.Poker.Tests/Api/Features/Games/TwosJacksManWithTheAxe/v1/Commands/PerformShowdown/TwosJacksPotSplitTests.cs`

Call `Split` with no database. Cover hands 1, 2, and 3. For hand 2, pass a `remainderOrder` and assert which of Alice and Bob receives the odd sevens chip of the 82. `82 / 2 = 41`, remainder 0, so each gets 41 if both are sevens winners. Use a sevens pool of 1 in a separate case: one chip goes to the first of the two names in `remainderOrder`.

If `ContestedPotLayersTests` already exists, add hand 5 there if it is missing. If it does not, add `src/Tests/CardGames.Poker.Tests/Api/Features/Games/Common/Showdown/ContestedPotLayersTests.cs` and cover hands 4 and 5 plus the conservation identity.

Add a handler test beside `TwosJacksPotSplitTests`. There is no existing showdown handler test for this game. Build `PerformShowdownCommandHandler` with an in-memory `CardsDbContext`, `Substitute.For<IHandHistoryRecorder>()`, and `Substitute.For<IHandSettlementService>()`, following `HoldEm/.../PerformShowdownCommandHandlerTests` for the context setup. Seed three players, five hole cards each, `Location = Hand`, contributions 110, 55, and 110, stacks 0, `IsAllIn` true, `CurrentPhase = Showdown`, and one pot of 275. Use the cards from hand 1. Assert the payouts, the sevens and high-hand amounts, two awarded pots, the settlement dictionary, and that a second `Handle` does not settle again.

`TwosJacksManWithTheAxeDrawHand` lives in `CardGames.Poker`. The handler test project already references it if the Hold'em showdown tests do. Five cards are enough. `cards.Count < 5` skips the player, so do not seed fewer.

Run:

```text
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~TwosJacksPotSplitTests|FullyQualifiedName~TwosJacksManWithTheAxe.v1.Commands.PerformShowdown|FullyQualifiedName~ContestedPotLayersTests"
```

## Done when

- Hand 1 pays Bob 165 and Alice 110, and the saved pots are those two amounts.
- Bob is not paid the side pot. Alice is not paid a sevens share of the side pot in hand 1, because Alice has no sevens.
- In hand 2, Alice's sevens are paid only from pots Alice is eligible for.
- The pots written by `CalculateSidePots` are replaced before the award. Folded chips below an all-in level are not added to the highest pot.
- Fold wins and the `orderedPlayerNames` loop are unchanged.
- A second showdown call does not move chips and does not settle again.
