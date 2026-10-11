# Fix: A folded player who leaves is not charged

## Defect

`HandSettlementService.SettleHandAsync` posts `payout - TotalContributedThisHand` only for a `GamePlayer` whose `Status` is `Active`, or whose `IsAllIn` is true. The cashier balance moves only in `PlayerChipWalletService.RecordHandSettlementAsync`. `TryDebitForBuyInAsync` and `CreditForCashOutAsync` write ledger rows with `AmountDelta` 0. They do not change `PlayerChipAccount.Balance`.

`LeaveGameCommandHandler.IsPlayerInActiveHand` returns false on this line:

```csharp
if (gamePlayer.HasFolded) return false;
```

A folded player therefore takes the immediate-leave branch while the hand is still in progress, in `Handle` after the `isInActiveHand` check fails:

- `Status` becomes `Left`
- `CreditForCashOutAsync` writes a `CashOut` row with `AmountDelta` 0
- `ChipStack` is set to 0
- `FinalChipCount` is set to the stack from before that zeroing

Showdown still pays the pot, including that player's chips, to whoever wins. Settlement skips the folded leaver because `Status` is `Left` and `IsAllIn` is false. The winner's `HandSettlement` row is created. The leaver's loss is not. The ledger rows for the hand sum to the chips the leaver put in.

A player who leaves without folding, during a phase already listed in `activePhases`, is queued: `IsSittingOut = true`, `LeftAtHandNumber = CurrentHandNumber`, `Status` stays `Active`, `ChipStack` is not changed, and `CreditForCashOutAsync` is not called. Showdown charges them. `ContinuousPlayBackgroundService.StartNextHandAsync` later writes the audit cash-out and sets `Status` to `Left`. The folded leaver never reaches that queue.

The hand. Three players, 500 each. Blinds 5 and 10. `CurrentHandNumber` 1. Phase `PreFlop`. `Status` is `InProgress`.

| Player | Action | ChipStack | TotalContributedThisHand |
|---|---|---|---|
| Alice | small blind 5, then raises to 30 | 470 | 30 |
| Bob | big blind 10, then folds and stays | 490 | 10 |
| Carol | calls 10, then folds, then leaves | 490 before the leave | 10 |

The pot is 50. Live Hold'em fold-win pays Alice the whole pot. Table stacks if Carol is still seated: Alice 520, Bob 490, Carol 490. This fix does not change those table stacks. It changes the cashier.

Ledger if Carol is still `Active` at showdown, payout dictionary `{ "Alice": 50 }`:

| Player | Payout | Contributed | HandSettlement AmountDelta |
|---|---|---|---|
| Alice | 50 | 30 | +20 |
| Bob | 0 | 10 | −10 |
| Carol | 0 | 10 | −10 |

The three deltas sum to 0.

Today Carol's leave sets `Status` to `Left` and `ChipStack` to 0 before showdown. Settlement writes Alice +20 and Bob −10. Carol has no `HandSettlement` row. Her cashier balance does not move. The rows sum to +10, which is the 10 she called.

## Do not change

- `HandSettlementService`. The filter stays `Status == Active || IsAllIn`. Once Carol is still `Active` at showdown, that filter charges her. Widening it to `Left` also charges a player who already left between hands if a later settlement sees a stale `TotalContributedThisHand`.
- `RecordHandSettlementAsync`, `CreditForCashOutAsync`, and `TryDebitForBuyInAsync`. Cash-out stays an audit row with `AmountDelta` 0. Crediting `ChipStack` into the cashier on leave would create chips. The buy-in never debited the wallet.
- The queued-leave block already in `LeaveGameCommandHandler.Handle` (the `if (isInActiveHand)` branch). It sets `IsSittingOut` and `LeftAtHandNumber`, does not call `CreditForCashOutAsync`, and does not change `ChipStack` or `TotalContributedThisHand`. Folded players must enter this branch. Do not add a `SettleHandAsync` call inside `LeaveGame`.
- The immediate-leave block (the code after that `if`). It remains the path for a hand that is already over, and for a player who has no cards for `CurrentHandNumber`.
- `ContinuousPlayBackgroundService.StartNextHandAsync`, the loop `Status == Active && LeftAtHandNumber != -1`. It already calls `CreditForCashOutAsync`, sets `FinalChipCount` from the current `ChipStack`, sets `ChipStack` to 0, and sets `Status` to `Left`. Do not zero `ChipStack` at leave time.
- Every `StartHand` handler's pending-leave loop, including `HoldEm/.../StartHandCommandHandler.ProcessPendingLeaveRequests`. Those mark a queued player `Left` when the next hand starts. They do not cash out and they do not zero `ChipStack`. Leave them. A folded player who was queued uses that same existing path.
- Showdown payout math, side pots, uncalled bets, and who is dealt into the next hand.
- `IsSittingOut`. The queued branch already sets it. Settlement does not read it. Do not clear `HasFolded`.

Production file this change may modify:

- `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/LeaveGame/LeaveGameCommandHandler.cs`

Test file:

- `src/Tests/CardGames.IntegrationTests/Features/Commands/JoinLeaveWalletCommandTests.cs`

No other file.

## The change

Edit only `IsPlayerInActiveHand`.

Delete this line:

```csharp
if (gamePlayer.HasFolded) return false;
```

A fold means the player is done betting. It does not mean the chips already in the pot have been settled.

Replace the comment above the method body. It currently says the player must not have folded. The comment should say a folded player is still in the hand until the hand is settled, as long as the other three checks pass.

Add these names to the `activePhases` array. They are phases that occur after a fold and before `SettleHandAsync`. A folded player who leaves during one of them must be queued. Use these exact strings. `nameof(Phases.X)` equals the identifier, and `CurrentPhase` is stored as that identifier.

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

Leave every name already in the array. That includes `SecondBettingRound`, `ThirdBettingRound`, `FourthBettingRound`, and `Drawing`. `SecondBettingRound` is the post-draw betting phase in Five Card Draw and in Deuces/Jacks. A player who folded on the first betting round and leaves during `SecondBettingRound` must stay queued. Do not remove those four strings.

Do not add `Complete`, `WaitingForPlayers`, `WaitingToStart`, `WaitingForDealerChoice`, `Ended`, `CollectingAntes`, or `CollectingBlinds`.

`CollectingAntes` and `CollectingBlinds` run before a fold. A player who has not been dealt a card for `CurrentHandNumber` still leaves immediately, because of the card check below. That includes someone who posted a blind and leaves before hole cards exist. Do not change that in this fix.

Leave the rest of `IsPlayerInActiveHand` as it is:

1. `game.Status != GameStatus.InProgress` returns false.
2. `CurrentPhase` must be in `activePhases`, ordinal-ignore-case.
3. `gamePlayer.Cards` must contain a card with `HandNumber == game.CurrentHandNumber`. A folded player still has those cards. The leave query already includes `GamePlayers.ThenInclude(gp => gp.Cards)`.

No other change in `Handle`. When `IsPlayerInActiveHand` returns true, execution stays in the block that sets `IsSittingOut = true` and `LeftAtHandNumber = game.CurrentHandNumber`, saves, and returns `LeaveGameSuccessful` with `Immediate: false`, `LeftAt: null`, `FinalChipCount: null`, the response `LeftAtHandNumber` argument hardcoded to `-1`, and `Message` equal to `"You will leave the table after the current hand completes."`

That `-1` is the response value. The entity's `LeftAtHandNumber` is `game.CurrentHandNumber`. Tests must assert those separately. `PlayerName` on the response is `gamePlayer.Player.Email ?? gamePlayer.Player.Name`. The seeder sets an email, so the response name is the email. Do not assert that it equals `"Carol"`.

## Worked hand

Same blinds and actions as above. Carol's leave runs while `HasFolded` is true, `Status` is `Active`, `IsAllIn` is false, phase is `PreFlop`, `CurrentHandNumber` is 1, `game.Status` is `InProgress`, `StartedAt` is set, and she has at least one `GameCard` with `HandNumber` 1.

After `LeaveGame`, Carol's row:

| Field | Value |
|---|---|
| Status | `Active` |
| HasFolded | true |
| IsAllIn | false |
| IsSittingOut | true |
| LeftAtHandNumber | 1 |
| LeftAt | null |
| ChipStack | 490 |
| FinalChipCount | null |
| TotalContributedThisHand | 10 |
| CashOut ledger row | none |
| HandSettlement row | none yet |

Response: `Immediate` false, `LeftAtHandNumber` −1, `FinalChipCount` null, `Message` `"You will leave the table after the current hand completes."`

Showdown's existing `SettleHandAsync` then writes Alice +20, Bob −10, Carol −10, using payout `{ "Alice": 50 }` and the contributions above. Carol's key is absent from the dictionary. `GetValueOrDefault` treats that as 0. Her `ChipStack` stays 490. Settlement does not move table stacks.

When the next hand starts through `StartNextHandAsync`, the existing finalizer sees Carol `Active` with `LeftAtHandNumber != -1`. It writes a `CashOut` row with `AmountDelta` 0 and a reason that contains the in-game stack 490, sets `FinalChipCount` to 490, sets `ChipStack` to 0, and sets `Status` to `Left`. Carol's cashier has moved by −10 and by nothing else. Do not add a test that drives `StartNextHandAsync`. That finalizer is unchanged.

The same leave during `Showdown`, `DrawComplete`, `PotMatching`, or `SecondBettingRound` queues her. Those phases have not settled the hand.

The same leave during `Complete`, including when `HasFolded` is still true from the hand that just finished, stays immediate. `Complete` is not in `activePhases`. Settlement for that hand has already run. `game.Status` in the existing between-hands test is `InProgress` while `CurrentPhase` is `Complete`. That combination must still take the immediate branch.

A folded player with no `GameCard` for `CurrentHandNumber` still leaves immediately, even during `PreFlop`.

## Tests

Add the tests below to `JoinLeaveWalletCommandTests`. Use `DatabaseSeeder.CreateCompleteGameSetupAsync` the way `LeaveGame_BetweenHands_WritesAuditOnlyCashOutLedger` does. Keep that existing test and its assertions. It does not set `HasFolded`, and its phase is `Complete`.

`CreateCompleteGameSetupAsync` names players `"Player 1"`, `"Player 2"`, `"Player 3"` and sets `CurrentHandNumber` to 0. After seeding, set the three `Player.Name` values to `"Alice"`, `"Bob"`, and `"Carol"`, set `game.CurrentHandNumber` to 1, set `game.StartedAt` to a non-null timestamp, and set `game.Status` to `GameStatus.InProgress`. `SetCurrentUser` must pass the seated player's `Name` as the user name. The handler matches `Player.Name`, not email.

`IntegrationTestBase` registers `FakeHandSettlementService` as `IHandSettlementService`. Resolving that interface from the test scope records a call and writes no ledger row. Do not resolve it. Construct the real types on `DbContext`:

```csharp
var wallet = new PlayerChipWalletService(DbContext);
var settlement = new HandSettlementService(wallet);
```

`RecordHandSettlementAsync` checks the database for an existing `HandSettlement` row. A second call in the same context, before `SaveChanges`, does not see the unsaved row and will post the loss again. The idempotency assertion must `SaveChanges` after the first `SettleHandAsync`, then call it again.

### 1. Folded leave during PreFlop

Seed three players as above, game type `"HOLDEM"`, starting chips 500. Carol is `setup.Players[2]` after the rename.

Set Carol: `HasFolded` true, `ChipStack` 490, `TotalContributedThisHand` 10, `Status` `Active`, `IsAllIn` false, `IsSittingOut` false, `LeftAtHandNumber` −1.

Add one `GameCard` and save it before sending the command. Minimum fields that satisfy the handler and the `GameCard` entity:

- `Id = Guid.NewGuid()`
- `GameId = game.Id`
- `GamePlayerId = carolGamePlayer.Id`
- `HandNumber = 1`
- `Location = CardLocation.Hole`
- `Suit = CardSuit.Clubs`
- `Symbol = CardSymbol.Ace`
- `DealOrder = 1`
- `IsVisible = false`
- `IsDiscarded = false`
- `DealtAt = DateTimeOffset.UtcNow`
- `DealtAtPhase = nameof(Phases.Dealing)`

`CurrentPhase = nameof(Phases.PreFlop)`.

`SetCurrentUser` with user name `"Carol"`. Send `new LeaveGameCommand(game.Id)`.

Assert:

- `result.IsT0` is true
- `result.AsT0.Immediate` is false
- `result.AsT0.LeftAtHandNumber` is −1
- `result.AsT0.FinalChipCount` is null
- `result.AsT0.Message` is `"You will leave the table after the current hand completes."`
- Reloaded Carol `GamePlayer`: `Status` is `Active`, `ChipStack` is 490, `TotalContributedThisHand` is 10, `HasFolded` is true, `IsSittingOut` is true, `LeftAtHandNumber` is 1, `LeftAt` is null
- No `PlayerChipLedgerEntry` for Carol with type `HandSettlement` or `CashOut`

### 2. Folded leave during Showdown

Same setup and assertions as test 1, with `CurrentPhase = nameof(Phases.Showdown)`. This is the phase that was missing from `activePhases`. Carol must be queued, not marked `Left`.

### 3. Folded leave during SecondBettingRound

Same setup and assertions as test 1, with `CurrentPhase = nameof(Phases.SecondBettingRound)`. This locks the post-draw phase that is already in the array and must stay there.

### 4. Folded leave on Complete stays immediate

Same Carol setup, including `HasFolded` true, `ChipStack` 490, and a current-hand hole card. `CurrentPhase = nameof(Phases.Complete)`. `game.Status` stays `InProgress`, matching `LeaveGame_BetweenHands_WritesAuditOnlyCashOutLedger`.

Assert:

- `Immediate` is true
- Reloaded `Status` is `Left`
- Reloaded `ChipStack` is 0
- Reloaded `FinalChipCount` is 490
- One `CashOut` row for Carol with `AmountDelta` 0
- No `HandSettlement` row for Carol
- Her `PlayerChipAccount.Balance` is unchanged

Seed her account at balance 100 before the command, the same way the between-hands test does. If the account is missing, `CreditForCashOutAsync` creates one at `CashierAccountInitializer.StartingChipAmount` (100000) and the balance assertion is wrong.

### 5. Folded player with no card for this hand leaves immediately

Phase `PreFlop`, Carol `HasFolded` true, `ChipStack` 490, `TotalContributedThisHand` 10, and no `GameCard` with `HandNumber == 1`. Assert `Immediate` is true and reloaded `Status` is `Left`. This is the card check, which stays.

### 6. Showdown charges the queued folded player

Start from test 1's saved leave state. Also set, before settlement:

- Alice `Status` `Active`, `TotalContributedThisHand` 30, `IsAllIn` false
- Bob `Status` `Active`, `TotalContributedThisHand` 10, `HasFolded` true, `IsAllIn` false
- `game.CurrentHandNumber` 1
- A `PlayerChipAccount` for each of the three, `Balance` 100

Reload the game with `Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)`. `SettleHandAsync` reads `gp.Player.Name`. Call the real `HandSettlementService` constructed above, not the fake from DI:

```csharp
await settlement.SettleHandAsync(
    game,
    new Dictionary<string, int> { ["Alice"] = 50 },
    CancellationToken.None);
await DbContext.SaveChangesAsync();
```

Assert one `HandSettlement` row each:

| Player | AmountDelta | Balance after |
|---|---|---|
| Alice | +20 | 120 |
| Bob | −10 | 90 |
| Carol | −10 | 90 |

Carol's reloaded `ChipStack` is still 490. Her `Status` is still `Active`.

Call `SettleHandAsync` again with the same dictionary and `SaveChanges` again. Carol still has one `HandSettlement` row, and her balance is still 90. The second call must happen after the first `SaveChanges`. `RecordHandSettlementAsync` will not see an unsaved row.

Do not call `PerformShowdownCommand`. The table-stack payout is a different change. This test only locks the cashier charge.

## Done when

- Carol's fold-then-leave during `PreFlop`, `Showdown`, or `SecondBettingRound` leaves her `Active`, with `ChipStack` 490 and `TotalContributedThisHand` 10, and no cash-out row.
- `SettleHandAsync` with `{ "Alice": 50 }` then writes Alice +20, Bob −10, and Carol −10. Those deltas sum to 0. A second call after `SaveChanges` does not write another Carol row.
- A folded player who leaves on `Complete` still leaves immediately: `Status` `Left`, `ChipStack` 0, one audit `CashOut` with `AmountDelta` 0, and no `HandSettlement` row.
- A folded player with no card for `CurrentHandNumber` still leaves immediately.
- `HandSettlementService` still selects `Active` or `IsAllIn` only.
- `LeaveGame_BetweenHands_WritesAuditOnlyCashOutLedger` still passes.
- The only production edit is `IsPlayerInActiveHand`.

Run:

```text
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~JoinLeaveWalletCommandTests"
```
