# Fix: side pots and uncalled chips paid to the best hand

This plan is the implementation spec for one defect. An LLM should follow it exactly. Do not fix other audit findings in the same change (Follow the Queen wild-card order, fixed-limit bet size, Tollbooth all-in runout, bring-in action order, ace-low bring-in).

The live Hold'em showdown is the behavior to copy. Do not invent a second layering algorithm.

## Outcome

For every contested showdown in the games listed below, chips already stored in `GamePlayer.TotalContributedThisHand` are split the way `HoldEmPotLayers.Build` splits them:

- The largest contesting contribution minus the second-largest contesting contribution is returned to that player. It is not part of any pot and is not `AmountWon`.
- Remaining chips are layers at each distinct contesting all-in amount.
- A layer is awarded only to players who matched that layer, did not fold, and are allowed to win that game's showdown.
- A folded player's chips stay in the pot. They are not returned.
- A player who did not match a layer does not receive that layer.
- After the hand, the sum of stack changes is zero. No chips are created or destroyed.
- A second showdown call does not refund or pay again.

Games:

| Game | Live showdown that writes stacks |
|---|---|
| Seven Card Stud | `src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| Razz | That stud handler returns immediately to generic showdown when `CurrentHandGameTypeCode ?? GameType.Code` is `RAZZ` |
| Baseball | `src/CardGames.Poker.Api/Features/Games/Baseball/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| Follow the Queen | `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| Good Bad Ugly | `src/CardGames.Poker.Api/Features/Games/GoodBadUgly/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` |
| Pair Pressure | Its showdown handler forwards to generic showdown |
| Tollbooth | Its showdown handler forwards to generic showdown |

Generic showdown is `src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`.

## Leave these alone

- Hand evaluation. Do not change which cards or wild ranks a handler uses. Razz stays on `RazzHand`. Baseball stays on `BaseballHand`. Follow the Queen stays on `FollowTheQueenHand`. Good Bad Ugly stays on `GoodBadUglyHand` plus `UGLY_ELIMINATED`. Pair Pressure stays on the generic `CreatePairPressureHand` branch.
- Betting handlers. They may keep adding every street's chips onto the single `PotOrder == 0` row. Layering happens at showdown from cumulative `TotalContributedThisHand`. Building side-pot rows during a street and layering again at showdown would pay twice.
- `HoldEmPotLayers` math. `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/HoldEmPotLayers.cs` and `HoldEmPotLayersTests` already cover uncalled refunds, folded dead money, and several all-in levels. Call `Build`. Do not reimplement it.
- Win-by-fold chip math, except as specified under "Win by fold" below. The last player already receives the full pot. That pot includes their own chips. Also returning an uncalled amount on that path creates chips.
- `PotContribution` rows written during the hand. Antes, bring-in, and baseball buy-cards write some of them. Later bets often do not. `IsEligibleToWin` is not a complete record. Do not derive layers from those rows.

## Why the current payout is wrong

Dedicated handlers do this after evaluating hands:

```csharp
var totalPot = currentHandPots.Sum(p => p.Amount);
var maxStrength = /* best hand among everyone still in */;
var payoutPerWinner = totalPot / winners.Count;
```

Everyone still in the hand shares `totalPot`, including chips they did not match.

Generic `AwardPotsAsync` does filter by `PotContribution.IsEligibleToWin`, but stud hands never rebuild those rows from the final contribution totals. `UsesContributionLayers` does not include `RAZZ`, `PAIRPRESSURE`, or `TOLLBOOTH`. One pot row therefore has either no usable eligibility or eligibility copied from the ante, and its `Amount` is the whole hand. The best hand receives it.

`TotalContributedThisHand` is already incremented in the places chips enter the pot:

- `ContinuousPlayBackgroundService.CollectAntesAsync` assigns the ante
- Each game's `CollectAntesCommandHandler` adds the ante
- `StudBringInPosting.PostBringInForThirdStreetAsync` adds the bring-in
- Bet, call, raise, and all-in in the stud, baseball, Follow the Queen, and Good Bad Ugly betting handlers add the chips moved
- `Baseball` `ProcessBuyCardCommandHandler` adds the buy price
- `Tollbooth` `ChooseCardCommandHandler` adds the toll

That total is the input. The pot row amount is only the checksum.

## Shared award

Add one type, `ContributionLayerAwarder`, under `src/CardGames.Poker.Api/Features/Games/Common/Showdown/`. Generic showdown and the four dedicated handlers must call it. Do not paste a second copy of the refund, the odd-chip sort, or the stack update into each handler.

### Inputs

- `Game` with `GamePlayers`, `Player`, and current-hand `Pots` loaded
- Current-hand pots (`HandNumber == game.CurrentHandNumber`)
- `DateTimeOffset now`
- `bool isAlreadyAwarded` (`currentHandPots.Any(p => p.IsAwarded)`)
- `IReadOnlyDictionary<Guid, long> strengthByGamePlayerId` for every player the handler already treats as still in the hand
- `Func<GamePlayer, bool> canWin`

`canWin` is the only game-specific hook.

- Seven Card Stud, Baseball, Follow the Queen, Razz, Pair Pressure, Tollbooth: `canWin` is true for every player already in `strengthByGamePlayerId`.
- Good Bad Ugly: a player with `VariantState == "UGLY_ELIMINATED"` (ordinal ignore case) cannot win if any player in `strengthByGamePlayerId` is not eliminated. If every player in that dictionary is eliminated, `canWin` is true for all of them. Folded players are not in the dictionary.

Do not express the ugly rule by setting `HoldEmPotLayers.Stake.Contesting` to false. An eliminated player who has not folded still contributed. Their chips still form layers. They are only barred from receiving a layer.

### Stake list

Build one `HoldEmPotLayers.Stake` for every `GamePlayer` on the game, including folded and sitting-out players:

- `GamePlayerId`: `gamePlayer.Id`
- `Name`: `gamePlayer.Player.Name`
- `Contributed`: `gamePlayer.TotalContributedThisHand`
- `Contesting`: `strengthByGamePlayerId` contains `gamePlayer.Id`
- `AllIn`: `Contesting && (gamePlayer.IsAllIn || gamePlayer.ChipStack == 0)`

`Contesting` must match the handler's existing "still in the hand" filter: not folded, and status `Active` or `IsAllIn`. Do not add a new filter.

### Fail closed before any stack or pot write

Let `potTotal` be the sum of current-hand pot `Amount`. Let `contributionSum` be the sum of `TotalContributedThisHand` across every `GamePlayer`.

If `potTotal != contributionSum`, return that handler's existing invalid-game-state error. Message shape, same as generic showdown:

`Pot total {potTotal} does not equal contributed chips {contributionSum}.`

Do not change stacks, pots, phase, or dealer. Do not fall back to paying `potTotal` to the best hand.

Then:

```csharp
var layers = HoldEmPotLayers.Build(stakes);
```

Reject the result with the same checks as generic `ValidateLayers`: `Uncalled + sum(layer.Amount)` must equal `contributionSum`; each layer's slice amounts must equal that layer's `Amount`; every layer amount must be positive and have at least one slice with `Eligible == true`. On failure, return invalid game state and write nothing.

`HoldEmPotLayers.Build` does not mutate the `Stake` records. The uncalled amount is still inside `TotalContributedThisHand` until the mutation step below.

### Unwinnable layers

After `Build`, and still before mutation, walk the layers in `PotOrder`.

A slice is a legal winner when `slice.Eligible` is true and `canWin` is true for that `GamePlayer`.

If a layer has at least one legal winner, keep it.

If a layer has none, do not award it and do not leave it in the pot. Add each slice's `Amount` to a refund map keyed by `GamePlayerId`. Drop the layer. This is the Good Bad Ugly case: an eliminated player put in more than any live player, the live player must not receive that extra, and the eliminated player must get that extra back because they are not allowed to win it.

Renumber kept layers so `PotOrder` is 0, 1, 2, ... with no gaps. The first kept layer is `PotType.Main`. The rest are `PotType.Side`.

Conservation before mutation:

`uncalled + unwinnable refunds + sum(kept layer amounts) == contributionSum`

If that fails, return invalid game state and write nothing.

### Mutation order

Only after the checks above, and only when `isAlreadyAwarded` is false:

1. Uncalled refund. If `layers.Uncalled > 0`, add it to that player's `ChipStack` and subtract it from `TotalContributedThisHand`. Do not put it in the payout dictionary.
2. Unwinnable-layer refunds. Same stack and `TotalContributedThisHand` update, per player, for the refund map. Do not put these amounts in the payout dictionary.
3. Replace current-hand pots. Reuse the generic `ReplaceCurrentHandPotsAsync` behavior: delete current-hand `PotContribution` rows and `Pot` rows, then insert one `Pot` per kept layer. Copy `Amount`, `PotOrder`, `PotType`, `MaxContributionPerPlayer`, and slices. A slice is stored even when `Eligible` is false (folded dead money). `IsEligibleToWin` on the new row is the slice's `Eligible` value after the `canWin` filter: an eliminated player who is barred has `IsEligibleToWin` false on every kept layer. `IsAwarded` stays false until step 5. Do not re-query the database for the new pots before awarding; award the in-memory pots just inserted. Generic showdown already depends on `pot.Contributions` being populated in memory.
4. Award each kept pot in `PotOrder`. Eligible recipients are contributions with `IsEligibleToWin` and a strength entry. Winner set is everyone whose strength equals the max strength in that eligible set. Split the pot amount by integer division. Give leftover chips one at a time to eligible winners ordered by seat clockwise from the dealer:

   ```csharp
   .OrderBy(seat => seat > game.DealerPosition ? 0 : 1)
   .ThenBy(seat => seat)
   ```

   That is the order in generic `AwardPotsAsync`. Do not give the whole remainder to `winners[0]` or to dictionary insertion order. A player's payout is the sum of their layer awards. It does not include refunds.
5. Mark each kept pot awarded: `IsAwarded`, `AwardedAt`, `WinReason`, `WinnerPayouts`. `WinReason` for the main pot stays the hand type string the handler already stores for a single winner, or `Split pot - {type}` when that layer has more than one winner. `WinnerPayouts` JSON keeps `playerId`, `playerName`, and `amount` for that pot only. Amounts in that JSON are layer awards, not refunds.
6. Add each payout to `ChipStack` once.
7. Call `IHandSettlementService.SettleHandAsync` only after steps 1, 2, and 6. It computes `amountWon - TotalContributedThisHand`. Refunds must already have been subtracted from `TotalContributedThisHand`, and they must not also be in `payouts`. Otherwise the cashier ledger creates or drops the refund.
8. Hand history `TotalPot` is the sum of kept layer amounts. It excludes the uncalled refund and unwinnable-layer refunds. `AmountWon` is the payout dictionary. `NetChipDelta` stays `amountWon - TotalContributedThisHand` using the post-refund contribution. Record history after the refunds.
9. One `SaveChanges` for the award. Do not save between the refund and the award.

### Already awarded

When any current-hand pot is `IsAwarded`, do not build layers, do not refund, and do not add chips. Rebuild the response payout dictionary from `WinnerPayouts` the way generic `AwardPotsAsync` does in its `isAlreadyAwarded` branch. A replayed showdown returns those amounts and leaves stacks unchanged.

### Win by fold

Keep the existing branch: one player remains, `ChipStack += totalPot`, pots marked awarded with reason `All others folded`, hole cards stay hidden.

Do not also run `HoldEmPotLayers` on that branch. With one contesting player, `Build` leaves `Uncalled` at 0 and puts their entire contribution into the pot. Awarding that pot is the same stack result as `ChipStack += totalPot`. Running both the current add and a refund would pay them twice.

The one-player branch still requires `potTotal == contributionSum` before it adds chips. On mismatch, return invalid game state and write nothing. A fold win of a pot that does not match contributions is not a reason to pay the pot anyway.

Good Bad Ugly: the last player wins the full pot even when `VariantState` is `UGLY_ELIMINATED`. Do not refund their extra chips on a fold win. Everyone else folded, so the last player is entitled to the pot.

### Generic handler

In `UsesContributionLayers`, add:

- `PokerGameMetadataRegistry.RazzCode`
- `PokerGameMetadataRegistry.PairPressureCode`
- `PokerGameMetadataRegistry.TollboothCode`

Do not add Seven Card Stud, Baseball, Follow the Queen, or Good Bad Ugly there as a substitute for editing their handlers. Those handlers never reach `UsesContributionLayers`. Their evaluation must stay in their own handler.

Point the generic layer block (the `UsesContributionLayers` section and the per-pot winner loop inside `AwardPotsAsync`) at `ContributionLayerAwarder` so Razz, Pair Pressure, and Tollbooth take the same refund and odd-chip path. Keep generic hand evaluation where it is. `strengthByGamePlayerId` for that call is `evaluation.Hand.Strength` for each player generic already evaluated. `canWin` is true for all of them.

Hold'em-family games already in `UsesContributionLayers` must keep today's results. Run the existing Hold'em showdown tests after the extraction. If a Hold'em assertion changes, the extraction is wrong. Put the Hold'em behavior back. Do not "fix" Hold'em in this change.

`AwardPotsAsync` today, when a pot has no eligible contribution, falls back to every evaluated player. After this change the fallback must not run for a pot that `ContributionLayerAwarder` just built. An empty eligible set is the unwinnable-layer case and is handled by the refund above. Leaving the fallback in place would pay a short all-in the side pot.

## Per-handler edit

Each dedicated handler keeps its evaluation, its response card list, and its win-by-fold branch.

Replace only the block that does `totalPot / winners.Count` and then adds that payout to every winner's stack. In the current files that block is the section commented "Calculate payouts" / "Update player chip stacks":

- Seven Card Stud: the block after `var maxStrength = playerHandEvaluations.Values.Max(...)` through the chip-stack loop. Razz has already returned through `DelegateRazzShowdownAsync`.
- Baseball: the same block after `var maxStrength = playerHandEvaluations.Values.Max(...)`.
- Follow the Queen: the same block. Do not change `faceUpCardsInOrder` or `FollowTheQueenHand` construction.
- Good Bad Ugly: the block that builds `winners` from max strength or, when every remaining player is `UGLY_ELIMINATED`, from every remaining player, then splits `totalPot`. Pass the `canWin` function described above instead of splitting `totalPot` once. Strength for an eliminated player is still that player's evaluated strength. It is used only when `canWin` is true.

The response `Payouts` and each `ShowdownPlayerHand.AmountWon` come from the awarder's payout dictionary. A player who only received a refund has `AmountWon` 0. Their stack still increased by the refund.

`isAlreadyAwarded` must gate the awarder. The handlers already compute `isAlreadyAwarded`. Wire it through. Do not add chips when it is true.

## Worked examples the tests must lock

Stacks below are after showdown. Starting stacks are the numbers before any contribution in that example. "Contributed" is `TotalContributedThisHand` at the start of showdown, and the single pot amount equals the sum of those contributions.

### 1. Short all-in has the best hand

Alice contributed 400, stack before showdown 600, strength high.
Bob contributed 50, stack before showdown 0, all-in, strength highest.
Carol contributed 400, stack before showdown 600, strength low.
Pot row amount 850.

`HoldEmPotLayers` keeps two layers: 150 (Alice 50, Bob 50, Carol 50, all eligible) and 700 (Alice 350, Carol 350). Uncalled is 0.

Bob wins 150. Alice wins 700. Carol wins 0.

| Player | Stack |
|---|---:|
| Alice | 1300 |
| Bob | 150 |
| Carol | 600 |

Bob must not finish at 850. Alice must not finish at 600.

### 2. Heads-up uncalled shove

Alice contributed 400, not all-in, best hand. Bob contributed 100, all-in, worse hand. Pot 500.

Uncalled refund to Alice: 300. Contested pot: 200. Alice wins 200.

Alice's `AmountWon` is 200, not 500. Her `TotalContributedThisHand` is 100 after the refund. Stack change from the start of the hand: she recovers the 300 and wins Bob's 100.

If Bob has the best hand instead, Alice's `AmountWon` is 0 and her stack still increases by 300. Bob's `AmountWon` is 200. Bob must not finish with 500.

### 3. Folded chips stay in the main pot

Doug contributed 10 and folded. Bob contributed 60, all-in. Alice and Carol each contributed 110 and are still in. This is the shape of `HoldEmPotLayersTests.Build_WhenFoldedPlayerMatchedPartOfTheMainPot`.

Main pot 190, eligible Alice, Bob, Carol. Side pot 100, eligible Alice and Carol. Doug's 10 is in the main pot with `IsEligibleToWin` false.

If Bob has the best hand and Alice the second best: Bob is paid 190, Alice is paid 100, Carol is paid 0, Doug is paid 0 and is not refunded.

### 4. All-in on a later street

Cumulative totals, not the last street alone:

Alice 100, all-in, best hand.
Bob 400.
Carol 400, second best.
Pot 900.

Main pot 300 to Alice. Side pot 600 to Carol. Same numbers as example 1 with the antes and earlier streets already inside the totals. The awarder must not look at `CurrentBet` (that field is cleared between streets).

### 5. Three all-in levels

Use the amounts in `HoldEmPotLayersTests.Build_WhenTwoPlayersAreAllInForLess`:

- A 20 all-in, best hand among everyone
- B 50 all-in, best hand among players who covered 50
- C 100, best hand among players who covered 100
- D 100, worst hand

Layers are 80, 90, and 100. A is paid only 80. B is paid only 90. C is paid 100. D is paid 0. A must not be paid 270.

### 6. Equal contributions

Alice, Bob, and Carol each contributed 40. Nobody all-in. Alice has the only best hand.

One layer, amount 120. Alice is paid 120. No refund. This is the path that must stay intact for hands that never all-in.

### 7. Good Bad Ugly, live player against a larger eliminated stack

Alice is not eliminated, contributed 100, best live hand, stack before showdown 0 if that 100 was her stack, or any remaining stack. The test should set stack-before-showdown to 500 so the result is obvious: she contributed 100 from a starting 600.
Bob is `UGLY_ELIMINATED`, contributed 300, all-in, and his raw hand strength is higher than Alice's.

Main pot 200. Alice is the only legal winner, so Alice is paid 200 even though Bob's hand is stronger.
Side pot 200 has only Bob eligible and `canWin` is false. Refund 200 to Bob. Do not pay it to Alice.

Alice `AmountWon` 200. Bob `AmountWon` 0. Bob's stack increases by 200. Bob's `TotalContributedThisHand` becomes 100.

### 8. Good Bad Ugly, every remaining player is eliminated

Alice contributed 100, all-in, eliminated.
Bob contributed 300, eliminated.
No live player remains.

Do not refund Bob's extra just because both are eliminated. `canWin` is true for both.

Main pot 200, both eligible. They split it under the clockwise odd-chip rule.
Side pot 200, only Bob eligible. Bob is paid 200.

Bob must receive his matched share of the 200 plus all of the 200. Alice must not receive any of Bob's unmatched 200.

### 9. Win by fold

Alice contributed 400. Bob contributed 100 and folded. Pot 500. One player remains.

Alice's stack increases by 500 once. `AmountWon` is 500. There is no second refund of 300. Calling showdown again does not add another 500.

### 10. Pot does not match contributions

Pot amount 500. Sum of `TotalContributedThisHand` is 490.

Showdown returns invalid game state. Alice's stack, the pot amount, `IsAwarded`, and the phase are unchanged.

### 11. Second showdown

Run example 1 to completion. Call the same handler again.

Stacks stay at the example 1 results. `IsAwarded` stays true. The response payouts still show Bob 150 and Alice 700.

### 12. Baseball buy and Tollbooth toll

Treat the buy price and the toll as ordinary contribution. They are already in `TotalContributedThisHand` and in the pot amount.

Alice contributed 100, of which 20 was a buy or toll, and she is all-in. Bob contributed 100. No side pot. The 20 is inside the single 200 pot. It is not refunded and it is not excluded from the layer. The winner of that pot receives 200.

If Alice then faces Carol, who contributed 300 with no all-in from Alice above 100: Alice can win at most 200 of a 400 pot (100 from each). Carol's other 200 is a side pot or, if nobody else matched it, an uncalled refund to Carol. The 20 buy is inside Alice's 100, not a separate pot she wins outright.

## Tests

Add handler tests that set `TotalContributedThisHand`, `ChipStack`, `IsAllIn`, `HasFolded`, one current-hand main pot whose `Amount` equals the contribution sum, and evaluated hands strong enough to force the winner named in the example. The smallest place that can do that without a full deck is the handler test, with cards chosen only so the existing evaluator ranks the players in the order the example states.

Update:

- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/SevenCardStud/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs` with examples 1, 2, 9, 10, and 11. Example 1 is the release case.
- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/GoodBadUgly/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs` with examples 7 and 8.
- Baseball and Follow the Queen: add the same style of test for example 1 if those projects already have a showdown handler fixture. If they do not, one shared test of `ContributionLayerAwarder` plus a handler test that the Baseball and Follow the Queen handlers call it is enough. Do not ship those two handlers on the old `totalPot / winners` split.
- Generic showdown, or the Razz delegation through the stud handler: example 1 where Bob's low hand is the winner and Alice's high hand wins only the side pot. Razz strength must come from `RazzHand`, not from a high `StudHand`.
- `HoldEmPotLayersTests` and the existing Hold'em `PerformShowdownCommandHandlerTests` must pass without edited assertions.

Also assert, in example 1, that the persisted pots are a main pot of 150 and a side pot of 700, Bob's side-pot contribution is absent or `IsEligibleToWin` is false, and `WinnerPayouts` on the side pot names Alice only.

## Done when

- Examples 1 through 12 pass.
- Existing Hold'em showdown tests pass without assertion edits.
- No dedicated stud, baseball, Follow the Queen, or Good Bad Ugly showdown path still executes `totalPot / winners.Count` for a contested pot.
- `RAZZ`, `PAIRPRESSURE`, and `TOLLBOOTH` are inside `UsesContributionLayers`.
- A contested showdown with `potTotal != contributionSum` writes nothing.
- A replayed showdown writes nothing.
