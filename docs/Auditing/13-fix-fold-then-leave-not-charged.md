# Fix: A player who folds and then leaves mid-hand is never charged

## Defect

`HandSettlementService.SettleHandAsync` charges `payout - TotalContributedThisHand` only for a `GamePlayer` whose `Status` is `Active`, or whose `IsAllIn` is true. `PlayerChipWalletService` changes the cashier balance only in `RecordHandSettlementAsync`. `TryDebitForBuyInAsync` and `CreditForCashOutAsync` write audit rows with `AmountDelta` 0.

`LeaveGameCommandHandler.IsPlayerInActiveHand` returns false as soon as `HasFolded` is true. A folded player therefore takes the immediate-leave branch while the hand is still in progress:

- `Status` becomes `Left`
- `CreditForCashOutAsync` records the remaining `ChipStack` with `AmountDelta` 0
- `ChipStack` is set to 0

Showdown still pays the pot, including that player's chips, to whoever wins. Settlement skips the folded leaver. The winner's ledger row is created and the leaver's loss is not.

A player who leaves without folding is queued: `IsSittingOut = true`, `LeftAtHandNumber = CurrentHandNumber`, `Status` stays `Active`. Showdown charges them. `ContinuousPlayBackgroundService.StartNextHandAsync` then writes the audit cash-out and sets `Status` to `Left`. The folded leaver never reaches that path.

The hand. Three players, 500 each. Blinds 5 and 10. Current hand 1. Phase `PreFlop`.

| Player | Action | ChipStack | TotalContributedThisHand |
|---|---|---|---|
| Alice | small blind 5, then raises to 30 | 470 | 30 |
| Bob | big blind 10, then folds and stays | 490 | 10 |
| Carol | calls 10, then folds, then leaves | 490 before the leave | 10 |

The pot is 50. Live Hold'em fold-win pays Alice the whole pot. Stacks if Carol is still seated: Alice 520, Bob 490, Carol 490.

Ledger if Carol is still `Active` at showdown:

| Player | Payout | Contributed | Row |
|---|---|---|---|
| Alice | 50 | 30 | +20 |
| Bob | 0 | 10 | −10 |
| Carol | 0 | 10 | −10 |

The rows sum to 0.

Today Carol's leave sets `Status` to `Left` and `ChipStack` to 0 before showdown. Settlement writes Alice +20 and Bob −10. Carol has no `HandSettlement` row. Her cashier balance does not move. The rows sum to +10, which is the 10 she called.

## Do not change

- `HandSettlementService`. Once Carol is still `Active` at showdown, the existing filter charges her. Widening it to `Left` also charges a player who already left between hands if a later settlement sees a stale `TotalContributedThisHand`.
- `RecordHandSettlementAsync`, `CreditForCashOutAsync`, and `TryDebitForBuyInAsync`. Cash-out stays an audit row. Crediting `ChipStack` on leave would add chips, because the buy-in never debited the wallet.
- The queued-leave branch already in `LeaveGameCommandHandler` (`IsSittingOut`, `LeftAtHandNumber`, no cash-out, `ChipStack` unchanged). Folded players use this branch. Do not add a settlement call inside `LeaveGame`.
- The next-hand finalizer in `ContinuousPlayBackgroundService.NextHand.cs` (the loop over `Status == Active && LeftAtHandNumber != -1`). It already cashes out and marks `Left` after the hand. Do not zero `ChipStack` at leave time.
- `StartHand` handlers' `ProcessPendingLeaveRequests`. They already mark a queued player `Left` when the next hand starts.
- Showdown payout math, side pots, and uncalled bets.
- A leave while `CurrentPhase` is `Complete`, `WaitingForPlayers`, `WaitingToStart`, or `WaitingForDealerChoice`. That hand is already settled or has not started. `LeaveGame_BetweenHands_WritesAuditOnlyCashOutLedger` stays an immediate leave.

## Who is still in the hand

In `IsPlayerInActiveHand`, delete this line:

```csharp
if (gamePlayer.HasFolded) return false;
```

A fold means the player is done betting. It does not mean the chips already in the pot have been settled.

Add these names to `activePhases`. They are in-hand phases that sit after a fold and before settlement. A folded player who leaves during one of them must be queued:

- `Showdown`
- `DrawComplete`
- `PotMatching`
- `Reveal`
- `KlondikeReveal`
- `RevealTheGood`
- `RevealTheBad`
- `RevealTheUgly`
- `BuyCardOffer`
- `TollboothOffer`
- `KeepOrTrade`
- `InBetweenTurn`

The phases already in the array stay there: `Dealing`, `PreFlop`, `Flop`, `Turn`, `River`, the four `*BettingRound` phases, `DrawPhase`, `Drawing`, `DropOrStay`, `PlayerVsDeck`, and the five stud streets.

Do not add `Complete`, `WaitingForPlayers`, `WaitingToStart`, or `WaitingForDealerChoice`.

Leave the rest of the method as it is. `game.Status` must be `InProgress`. The player must have a `GameCard` with `HandNumber == game.CurrentHandNumber`. A folded player still has those cards. A player who never received cards this hand still leaves immediately.

No other change in `Handle`. With `IsPlayerInActiveHand` true, execution stays in the block that sets `IsSittingOut` and `LeftAtHandNumber` and returns `Immediate: false` with the message `"You will leave the table after the current hand completes."`

## Worked hand

Same blinds and actions as above. Carol's leave runs while `HasFolded` is true, `Status` is `Active`, phase is `PreFlop`, and she has hole cards for hand 1.

After `LeaveGame`:

| Field | Carol |
|---|---|
| Status | `Active` |
| HasFolded | true |
| IsSittingOut | true |
| LeftAtHandNumber | 1 |
| ChipStack | 490 |
| TotalContributedThisHand | 10 |
| CashOut ledger row | none |
| HandSettlement row | none yet |

Showdown's existing settlement then writes Alice +20, Bob −10, Carol −10.

When the next hand starts, the existing finalizer sees Carol `Active` with `LeftAtHandNumber != -1`. It writes a `CashOut` row with `AmountDelta` 0 and reason text that includes 490, sets `FinalChipCount` to 490, sets `ChipStack` to 0, and sets `Status` to `Left`. Carol's cashier balance has moved by −10 and nothing else.

The same leave during `Showdown`, `DrawComplete`, or `PotMatching` queues her. Those phases have not settled the hand.

The same leave during `Complete`, including when `HasFolded` is still true from the hand that just finished, stays immediate. Settlement for that hand has already run.

## Tests

Add these to `src/Tests/CardGames.IntegrationTests/Features/Commands/JoinLeaveWalletCommandTests.cs`. Use `DatabaseSeeder.CreateCompleteGameSetupAsync` the way `LeaveGame_BetweenHands_WritesAuditOnlyCashOutLedger` does. `SetCurrentUser` must use the seated player's name. The handler matches `Player.Name`.

1. Folded leave during `PreFlop`. Game `InProgress`, `CurrentHandNumber` 1, Carol `HasFolded` true, `ChipStack` 490, `TotalContributedThisHand` 10, and at least one `GameCard` on her `GamePlayer` with `HandNumber` 1. Send `LeaveGameCommand`. Assert `Immediate` is false, `Message` is `"You will leave the table after the current hand completes."`, `Status` is still `Active`, `ChipStack` is 490, `IsSittingOut` is true, `LeftAtHandNumber` is 1, and there is no `PlayerChipLedgerEntry` of type `HandSettlement` or `CashOut` for her.

2. Same folded player, phase `Showdown`, same assertions. This locks the phase that was missing from `activePhases`.

3. Folded player, phase `Complete`, `ChipStack` 490. Assert `Immediate` is true, `Status` is `Left`, and `ChipStack` is 0. This is the existing between-hands path with `HasFolded` left true so the deleted early return cannot queue a finished hand.

4. Charge at showdown. After test 1's leave state (Carol still `Active`, folded, contributed 10), run the Hold'em showdown for a fold win of the 50 pot, or call `IHandSettlementService.SettleHandAsync` with payouts `{ Alice's Player.Name: 50 }` on that tracked game. Assert one `HandSettlement` row for Carol with `AmountDelta` −10, and her `PlayerChipAccount.Balance` reduced by 10. Alice +20 and Bob −10 are the other two rows. A second `SettleHandAsync` for the same hand does not add a second Carol row.

Keep `LeaveGame_BetweenHands_WritesAuditOnlyCashOutLedger` passing. It does not set `HasFolded`, and its phase is `Complete`.

Run:

```text
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~JoinLeaveWalletCommandTests"
```

## Done when

- Carol's fold-then-leave during `PreFlop` or `Showdown` leaves her `Active` with her stack and contribution untouched, and showdown writes a `HandSettlement` of −10.
- Alice +20, Bob −10, and Carol −10 sum to 0 for the 50 pot.
- A folded player who leaves on `Complete` still leaves immediately, with an audit-only cash-out and no new hand-settlement row.
- `CreditForCashOutAsync` still writes `AmountDelta` 0.
- `HandSettlementService` still selects `Active` or `IsAllIn` only.
