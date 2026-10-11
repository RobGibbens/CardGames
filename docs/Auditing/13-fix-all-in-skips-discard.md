# Fix plan: all-in runout skips Irish, Phil's Mom, and Crazy Pineapple discards

**Audit finding:** “Irish Hold'em, Phil's Mom, and Crazy Pineapple skip the discards when everyone is all-in preflop.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** The Hold’em betting handler’s all-in branch, and the two methods in `ProcessDiscardCommandHandler` that advance the hand after the last discard. Do not change evaluators, the client, or other games.

The code that runs a hand is the spec. Where `IrishHoldEmRules` / `CrazyPineappleRules` phase lists disagree with the handlers below, follow the handlers.

---

## 1. Problem summary

These three games deal extra hole cards and then require discards down to 2 before the later streets are dealt. Showdown builds a `HoldemHand` from **every** non-discarded hole card. `HoldemHand` plays the best 0–2 of whatever it is given. If the discards never happen, each player chooses those 2 cards after seeing the river.

`AreAllPlayersAllIn` is true when fewer than 2 non-folded players can still bet (`playersWhoCanBet < 2`). That includes “everyone is all-in” and “exactly one player still has chips.” On that path `Handle` calls `DealRemainingCommunityCardsAsync`, which deals every remaining street and sets `CurrentPhase = "Showdown"`. It never enters `DrawPhase`.

### What a live hand actually does when people can still bet

| Game | Code | After preflop betting closes | After that discard | Later |
|------|------|------------------------------|--------------------|-------|
| Irish Hold’em | `IRISHHOLDEM` | Deal the flop (3). Set `DrawPhase`. No flop betting. Each player discards **exactly 2** of 4. | `StartTurnPhaseAsync` deals the turn and starts turn betting. | River betting, then showdown. |
| Crazy Pineapple | `CRAZYPINEAPPLE` | Deal the flop (3). Set `DrawPhase`. Each player discards **exactly 1** of 3. | `StartFlopPhaseAsync(..., dealFlopCards: false)` starts **flop betting**. It does not deal another flop. | Turn, river, showdown. |
| Phil’s Mom | `PHILSMOM` | Set `DrawPhase`. Deal **no** community cards. Each player discards **exactly 1** of 4. | `StartFlopPhaseAsync` deals the flop and starts flop betting. | Flop betting closes into a **second** `DrawPhase`. Each player discards exactly 1 of the remaining 3. Then turn, river, showdown. |

Discard counts and the 4 / 3 / 2 hole-card checks live in `ProcessDiscardCommandHandler` (`requiredDiscardCount`, `ResolveDrawRound`). Do not duplicate different numbers.

### Concrete hand (Irish, both all-in preflop)

- Alice A♠ A♦ 7♣ 8♣. Bob K♠ K♦ Q♥ 2♥. Both all-in preflop. Pot 400.
- Board if it were dealt in order: flop 9♣ 10♦ 2♠, turn J♥, river 3♣.
- Rules: each player discards 2 **after the flop and before the turn**. Keeping A♠ A♦ vs K♠ K♦, Alice wins the 400.
- Code today: phase `Showdown`, 5 community cards, 4 live hole cards each. Bob plays K♦ Q♥ for a king-high straight. Alice’s best straight is 7♣ 8♣. Bob wins the 400.

Phil’s Mom on this path keeps all 4 hole cards (both discards skipped). Crazy Pineapple keeps all 3.

### Same hole, one street later (Phil’s Mom only)

Phil’s Mom’s second discard happens after the flop betting round. An all-in that closes the **flop** (first discard already done, 3 hole cards left) also takes `DealRemainingCommunityCardsAsync` and skips that second discard. Irish and Crazy Pineapple do not have a discard left at that point: Crazy Pineapple’s flop betting is after its only discard; Irish has no flop betting.

---

## 2. Intended behavior

Players choose their own discards. Do not auto-pick cards. An all-in player still discards. `ProcessDiscardCommandHandler` already allows that: eligibility is `!HasFolded && !HasDrawnThisRound`. It does not look at `IsAllIn`.

1. **Irish, preflop closes with fewer than 2 players who can bet.** Phase becomes `DrawPhase`. Exactly 3 community cards, all `DealtAtPhase == "Flop"`. No turn, no river. Each non-folded player still has 4 live hole cards (`Location == Hand`, `!IsDiscarded`). `HasDrawnThisRound` is false. `CurrentDrawPlayerIndex` is the first non-folded seat left of the dealer, **including** all-in seats.
2. **Crazy Pineapple, same trigger, and each non-folded player still has 3 live hole cards.** Same as Irish, except they will discard 1 card, not 2.
3. **Phil’s Mom, preflop closes with fewer than 2 who can bet, 4 live hole cards.** Phase `DrawPhase`. **Zero** community cards. 4 live hole cards.
4. **Phil’s Mom, flop closes with fewer than 2 who can bet, 3 live hole cards.** Phase `DrawPhase`. The 3 flop cards stay. No turn, no river. `HasDrawnThisRound` is false again so the second discard is accepted.
5. **After the last required discard, if fewer than 2 players can bet:** do not open a betting round. Deal only the streets that have not been dealt, then set `Showdown`.
   - Irish, and Phil’s Mom after the second discard: deal turn (`DealOrder` 4) and river (`DealOrder` 5). Board size 5. Each non-folded player has 2 live hole cards.
   - Crazy Pineapple after its discard: flop is already dealt. Deal turn and river. Do not deal a second flop. Board size 5. 2 live hole cards.
   - Phil’s Mom after the **first** discard: deal the flop only, then open the second `DrawPhase` (rule 4). Do not deal turn or river in that request.
6. **If 2 or more players can still bet,** keep today’s post-discard behavior: Phil’s Mom and Crazy Pineapple start flop betting; Irish and Phil’s Mom’s second discard start turn betting.
7. **Turn or river all-in, discards already done (2 live hole cards).** Keep today’s runout. Do not insert `DrawPhase`.
8. **Hold’em and every other game on this handler.** Unchanged: all-in runout deals the remaining streets and sets `Showdown`. No `DrawPhase`.

`GameSettings["allInRunout"]` must not be set when entering `DrawPhase`. `TableStateBuilder` hides community cards whose `DealtAtPhase` is listed in `runoutStreets`. The flop has to be visible while they discard. Set the runout keys only on the request that deals turn and river together:

- `allInRunout` = true
- `runoutStreets` = `["Turn", "River"]`
- `runoutHandNumber` = `game.CurrentHandNumber`
- `runoutTimestamp` = `now.ToString("O")`

Copy the JSON read/write already in `DealRemainingCommunityCardsAsync` (about lines 867–876). Do not add `"Flop"` to that list.

---

## 3. Root cause

### 3.1 All-in branch never looks at these games

**File:** `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`  
**Method:** `Handle`, `allPlayersAllIn` branch, lines 197–236.

```csharp
if (allPlayersAllIn)
{
    await DealRemainingCommunityCardsAsync(game, game.CurrentPhase, now, cancellationToken);
    // then SaveChanges and return Showdown
}
```

`AreAllPlayersAllIn` (lines 639–653) returns true when `playersWhoCanBet < 2`, after ignoring a fold-win (`playersInHand <= 1`).

The normal path that this branch skips:

- Lines 253–291: Irish or Crazy Pineapple, phase just became `Flop` → deal the flop, set `DrawPhase`, return.
- Lines 293–326: phase is `DrawPhase` (Phil’s Mom after preflop, and Phil’s Mom again after the flop) → clear `HasDrawnThisRound`, set `CurrentDrawPlayerIndex`, return.
- `AdvanceToNextPhase` lines 683–697: Phil’s Mom preflop goes to `DrawPhase`; Irish and Phil’s Mom flop goes to `DrawPhase`.

`FindFirstActivePlayerAfterDealer` (lines 587–603) skips `IsAllIn`. If everyone who is still in the hand is all-in, it returns **-1**. Do not use it to pick the first discard seat. `ProcessDiscard` will then have no actor.

`game.GameCards` is not loaded in `Handle` (the query at lines 41–46 includes players, type, and pots only). Count hole cards with a query.

`IsIrishHoldEmGame` / `IsPhilsMomGame` / `IsCrazyPineappleGame` (lines 721–728) read only `game.GameType.Code`. `ProcessDiscardCommandHandler` line 68 uses `game.CurrentHandGameTypeCode ?? game.GameType?.Code`. A Dealer’s Choice hand sets `CurrentHandGameTypeCode` in `ChooseDealerGameCommandHandler`. The new branch must use that same expression or a Dealer’s Choice hand of these games still run out.

### 3.2 After the last discard, “nobody can bet” skips the rest of the board

**File:** `src/CardGames.Poker.Api/Features/Games/IrishHoldEm/v1/Commands/ProcessDiscard/ProcessDiscardCommandHandler.cs`

`StartFlopPhaseAsync` lines 325–332 and `StartTurnPhaseAsync` lines 380–387:

```csharp
if (firstActorIndex < 0)
{
    game.CurrentPhase = nameof(Phases.Showdown);
    game.CurrentPlayerIndex = -1;
    game.CurrentDrawPlayerIndex = -1;
    return;
}
```

`FindFirstActivePlayerAfterDealerForBetting` (lines 463–478) also skips all-in players. After an all-in discard round this returns -1, and the method returns **before** dealing:

- Phil’s Mom first discard (`dealFlopCards: true`): showdown with **no flop**, and the second discard never starts. `HasDrawnThisRound` is still true, so a later discard would be `AlreadyDiscarded`.
- Crazy Pineapple (`dealFlopCards: false`): showdown with a flop and **no turn or river**.
- Irish and Phil’s Mom second discard (`StartTurnPhaseAsync`): showdown with **no turn or river**.

That early return is dead on today’s preflop all-in path, because the hand never reaches `ProcessDiscard`. It becomes the live path once section 4.1 enters `DrawPhase`. Replacing it is required.

`DealCommunityCardsAsync` (lines 427–457) does not re-filter tracked cards. Calling it twice in one request (turn, then river) can select the turn card again: the store still has `Location = Deck` until `SaveChangesAsync`, and EF returns the tracked instance. Deal turn and river from **one** in-memory list. This is the same re-filter `DealCommunityCardsForPhaseAsync` already does at lines 771–775 of the betting handler.

---

## 4. Required code change

### 4.1 Betting handler: enter `DrawPhase` instead of running out

**File:** `ProcessBettingActionCommandHandler.cs`, inside `if (allPlayersAllIn)`, **before** `DealRemainingCommunityCardsAsync`.

Add a private method:

```csharp
private async Task<bool> TryEnterOwedDiscardPhaseAsync(
    Game game,
    List<GamePlayer> activePlayers,
    DateTimeOffset now,
    CancellationToken cancellationToken)
```

Return `true` only when this method has set `DrawPhase` and the caller must save and return. Return `false` when the caller must keep the existing runout.

**Game type**

```csharp
var gameTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code;
```

Compare with `PokerGameMetadataRegistry.IrishHoldEmCode`, `PhilsMomCode`, and `CrazyPineappleCode`, ordinal ignore case. Any other code, including `HOLDEM`, returns `false` immediately. Do not deal cards in that case.

**Live hole-card counts**

Query `GameCards` for this `GameId` and `game.CurrentHandNumber`, `Location == CardLocation.Hand`, `GamePlayerId != null`, `!IsDiscarded`. Count per `GamePlayerId`.

For each active player with `!HasFolded`, the count is the query result or **0** if that player has no row. Folded players are ignored.

If those counts are not all equal, return `ProcessBettingActionError` from `Handle` (do not return `false` and run out):

- `Code = ProcessBettingActionErrorCode.InvalidGameState`
- Message includes the game type code and each seat’s count.

Return that error **before** `SaveChangesAsync`. The betting mutation is only in the change tracker; not saving drops it, and the client can retry. Do not call `DealRemainingCommunityCardsAsync` on this error.

**When a discard is owed**

| `gameTypeCode` | Every non-folded count | Enter `DrawPhase` | Community cards before returning |
|----------------|------------------------|-------------------|----------------------------------|
| `IRISHHOLDEM` | 4 | yes | Exactly 3, all `DealtAtPhase == "Flop"`. If there are 0, call `DealCommunityCardsForPhaseAsync(game, "Flop", now, cancellationToken)` once. If there are already 3, deal nothing. Any other count: the `InvalidGameState` error above. |
| `CRAZYPINEAPPLE` | 3 | yes | Same flop rule as Irish. |
| `PHILSMOM` | 4 | yes | Deal nothing. If any community card already exists for this hand, `InvalidGameState`. |
| `PHILSMOM` | 3 | yes | Flop must already be exactly 3 cards. If it is 0, deal the flop with `DealCommunityCardsForPhaseAsync`. If it is not 0 or 3, `InvalidGameState`. Do not deal turn or river. |
| any of the three | 2 | no (`false`) | Do not deal here. Existing runout. |
| any of the three | any other uniform count | error | `InvalidGameState`. |

Do not key this off `CurrentPhase` alone. The hole-card count is what `ResolveDrawRound` uses. Phase is still only `PreFlop`, `Flop`, `Turn`, or `River` here (`ValidBettingPhases`).

**Setting the phase** (same fields as lines 296–303)

```csharp
foreach (var player in activePlayers)
{
    player.HasDrawnThisRound = false;
}

var firstDrawPlayerIndex = FindFirstPlayerInHandAfterDealer(game, activePlayers);
game.CurrentPhase = "DrawPhase";
game.CurrentDrawPlayerIndex = firstDrawPlayerIndex;
game.CurrentPlayerIndex = firstDrawPlayerIndex;
```

`FindFirstPlayerInHandAfterDealer` is `FindFirstActivePlayerAfterDealer` with the `!player.IsAllIn` check removed. Still skip `HasFolded`. Still use `totalPlayers = activePlayers.Max(p => p.SeatPosition) + 1` and start at `(game.DealerPosition + 1) % totalPlayers`. If it returns -1, return `InvalidGameState` instead of saving `DrawPhase` with no actor.

Do not add a `BettingRound`. Do not set `allInRunout`. Do not call `DealRemainingCommunityCardsAsync`.

**Caller**

```csharp
if (allPlayersAllIn)
{
    var enteredDiscard = await TryEnterOwedDiscardPhaseAsync(game, activePlayers, now, cancellationToken);
    if (enteredDiscard)
    {
        game.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        var nextDrawPlayer = activePlayers.FirstOrDefault(p => p.SeatPosition == game.CurrentDrawPlayerIndex);
        return new ProcessBettingActionSuccessful
        {
            GameId = game.Id,
            RoundComplete = true,
            CurrentPhase = game.CurrentPhase, // "DrawPhase"
            Action = /* same BettingActionResult already built above */,
            PlayerSeatIndex = currentPlayer.SeatPosition,
            NextPlayerIndex = game.CurrentDrawPlayerIndex,
            NextPlayerName = nextDrawPlayer?.Player.Name,
            PotTotal = game.Pots.Sum(p => p.Amount),
            CurrentBet = bettingRound.CurrentBet
        };
    }

    await DealRemainingCommunityCardsAsync(game, game.CurrentPhase, now, cancellationToken);
    // existing Red River safety block, save, and return stay here
}
```

`TryEnterOwedDiscardPhaseAsync` cannot both return `bool` and return the error. Use this shape instead: a private method that returns `OneOf<bool, ProcessBettingActionError>`, `true` meaning “DrawPhase is set”, `false` meaning “run out”, error meaning “stop”. `Handle` already returns `OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>`.

Leave the Red River block that follows `DealRemainingCommunityCardsAsync` on the `false` path only. Do not call it on the discard path. There is no river yet.

### 4.2 Process discard: finish the board, or open Phil’s Mom’s second discard

**File:** `ProcessDiscardCommandHandler.cs`.

Add:

```csharp
private static bool FewerThanTwoCanBet(List<GamePlayer> activePlayers)
{
    return activePlayers.Count(p => !p.HasFolded && !p.IsAllIn && p.ChipStack > 0) < 2;
}
```

This matches `AreAllPlayersAllIn` without the fold-win exception. Both callers already have at least the players who just discarded.

**Replace** the `firstActorIndex < 0` early return in `StartFlopPhaseAsync` and in `StartTurnPhaseAsync`. Do not leave a return that sets `Showdown` before the deal.

`StartFlopPhaseAsync`:

1. Keep the `CurrentBet = 0` loop.
2. If `dealFlopCards` is true, deal the flop only when this hand has no `DealtAtPhase == "Flop"` community card. Use the existing `DealCommunityCardsAsync(game, nameof(Phases.Flop), 3, 1, ...)`. One call.
3. If `FewerThanTwoCanBet(activePlayers)` and `dealFlopCards` is true (Phil’s Mom, first discard just finished):
   - Set every `activePlayers` entry `HasDrawnThisRound = false`.
   - Set `CurrentPhase = nameof(Phases.DrawPhase)`.
   - Set `CurrentDrawPlayerIndex` and `CurrentPlayerIndex` with a finder that skips folded players and **includes** all-in players (same seat walk as `FindFirstActivePlayerAfterDealerForBetting`, without `!player.IsAllIn`).
   - Return. Do not add a `BettingRound`. Do not deal turn or river.
4. If `FewerThanTwoCanBet(activePlayers)` and `dealFlopCards` is false (Crazy Pineapple, discard just finished):
   - Call the runout method below.
   - Return.
5. Otherwise keep the existing flop betting-round creation (`firstActorIndex`, `BettingRound` with `Street = Flop`, phase `Flop`). If `firstActorIndex < 0` on this branch, return `Invalid` by not creating a round with `CurrentActorIndex` -1: leave phase unchanged and surface `ProcessDiscardErrorCode.NoEligiblePlayers` from `Handle`. That state is not reachable when two players can bet.

`StartTurnPhaseAsync`:

1. Keep the `CurrentBet = 0` loop.
2. If `FewerThanTwoCanBet(activePlayers)`: call the runout method and return. Do not add a `BettingRound`.
3. Otherwise keep the existing turn deal (`DealCommunityCardsAsync` turn, count 1, `firstDealOrder` 4) and the turn `BettingRound`.

**Runout method** (one deck read):

```csharp
private async Task RunOutTurnAndRiverAsync(
    Game game,
    DateTimeOffset now,
    CancellationToken cancellationToken)
```

- Load deck cards for this game and hand with `Location == Deck`, `OrderBy(DealOrder)`.
- Re-filter: `deckCards = deckCards.Where(gc => gc.Location == CardLocation.Deck).ToList()`.
- If a community card with `DealtAtPhase == "Turn"` already exists, do not deal another turn. Same for `"River"`.
- From the filtered list, deal a missing turn first (`DealtAtPhase = "Turn"`, `DealOrder = 4`, `Location = Community`, `IsVisible = true`, `GamePlayerId = null`, `DealtAt = now`), then a missing river (`DealOrder = 5`, `DealtAtPhase = "River"`). Advance the index so the river is not the turn card.
- If a missing street has no deck card left, do not set `Showdown`. `Handle` returns `ProcessDiscardError` with `ProcessDiscardErrorCode.InsufficientCards` and does not need a partial save. Throw nothing. Easiest plumbing: have `RunOutTurnAndRiverAsync` return `bool`, `false` when a card is missing, and `Handle` returns the error **before** `SaveChangesAsync` (the discard mutations are unsaved and will be dropped). Say that in a comment so the next edit does not save first.
- On success: `CurrentPhase = nameof(Phases.Showdown)`, `CurrentPlayerIndex = -1`, `CurrentDrawPlayerIndex = -1`.
- Write the four `GameSettings` keys from section 2. Preserve any other keys already in `game.GameSettings`. `DealRemainingCommunityCardsAsync` shows the deserialize/serialize pattern. Duplicate that block here. Do not make it a new shared service.

`Handle` already calls `SaveChangesAsync` once at the end. Do not add a second save.

### 4.3 Comments that would undo the fix

In `StartFlopPhaseAsync` and `StartTurnPhaseAsync`, delete the comment that says no one who can bet means skip straight to showdown. Replace it with one line: fewer than two players who can bet means deal the streets this method owes, or open Phil’s Mom’s second discard; do not skip the deal.

Do not rewrite `IrishHoldEmFlowHandler`’s class comment. It is wrong (“after the flop betting round”) but nothing reads it.

---

## 5. Tests

Run the existing lifecycle tests. They cover the “someone can still bet” path and must stay green:

- `IrishHoldEmHandLifecycleTests` (preflop check-down lands on `DrawPhase` with a flop, discard 2, then turn)
- `PhilsMomHandLifecycleTests.Lifecycle_OneDiscardBeforeFlop_OneDiscardAfterFlop_ThenTurnRiverShowdown`
- `CrazyPineappleHandLifecycleTests.Lifecycle_PreFlopDiscardThenFlopTurnRiverShowdown` (after the discard, phase is `Flop`, not `Showdown`, and the community count stays 3)

### 5.1 New integration tests

Use the existing setup helpers (`CreateDealt*`, `CheckAllPlayersThrough`, `Mediator.Send(new ProcessBettingActionCommand(...))`, `ProcessDiscardCommand`). Two players, `dealerPosition: 0`, starting chips 1000. Close preflop with `BettingActionType.AllIn` from the current actor, then `AllIn` or `Call` from the other, until `CurrentPhase` changes. Assert `IsT0` on each action.

**Irish** — `IrishHoldEmHandLifecycleTests`:

- After both are all-in preflop: `CurrentPhase == "DrawPhase"`, 3 community cards, all `DealtAtPhase == "Flop"`, 0 turn, 0 river, 4 non-discarded `Hand` cards per player, `HasDrawnThisRound == false`.
- Each player sends `ProcessDiscardCommand` with indices `{ 0, 1 }`. Follow the seat order the handler already uses (`CurrentDrawPlayerIndex`). Discarding out of turn must not be required.
- After the second discard: `CurrentPhase == "Showdown"`, 2 live hole cards per player, 5 community cards, exactly one `Turn` and one `River`, distinct card ids.

**Crazy Pineapple** — `CrazyPineappleHandLifecycleTests`:

- After preflop all-in: `DrawPhase`, 3 flop cards, 3 live hole cards, no turn/river.
- Each player discards index `{ 0 }`.
- Then `Showdown`, 2 live hole cards, 5 community cards, still exactly 3 flop cards. Phase must not be `Flop`.

**Phil’s Mom preflop** — `PhilsMomHandLifecycleTests`:

- After preflop all-in: `DrawPhase`, 0 community cards, 4 live hole cards.
- Each discards `{ 0 }`.
- Then `DrawPhase` again, 3 flop cards, 3 live hole cards, `HasDrawnThisRound == false`, no turn/river.
- Each discards `{ 0 }` again.
- Then `Showdown`, 2 live hole cards, 5 community cards.

**Phil’s Mom flop** — same class:

- Check preflop (existing helper), discard one card each (existing `DiscardOneCardForAllActivePlayers`), confirm phase `Flop` and 3 hole cards.
- Both all-in on the flop.
- Phase is `DrawPhase`, community count is still 3, live hole cards still 3.
- Second discard of `{ 0 }` each.
- `Showdown`, 2 live hole cards, 5 community cards.

**Crazy Pineapple flop, discard already done** — same class as the lifecycle test:

- Play to flop betting (check preflop, discard one each). Confirm 2 live hole cards and phase `Flop`.
- Both all-in.
- `Showdown`, still 2 live hole cards, 5 community cards. Must not be `DrawPhase`.

### 5.2 Hold’em regression

**File:** `src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandlerTests.cs`

Add a test next to the Red River runout test. `SeedGameAsync` defaults to `HOLDEM` and `PreFlop`. Use `secondPlayerAllIn: true`, `firstPlayerStack: 50`, action `BettingActionType.AllIn`. Seed 5 deck cards with `CreateDeckCard`.

Assert `CurrentPhase == "Showdown"` and 5 community cards. Assert it is not `DrawPhase`. This fails if the new branch treats every game as a discard game.

### 5.3 Commands

From the repo root:

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~ProcessBettingActionCommandHandlerTests"
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~IrishHoldEmHandLifecycleTests|FullyQualifiedName~PhilsMomHandLifecycleTests|FullyQualifiedName~CrazyPineappleHandLifecycleTests"
```

---

## 6. Out of scope

- Auto-discarding by rank, position, or “best hand.” The player sends `ProcessDiscardCommand`.
- `IrishHoldEmHandEvaluator`, `CrazyPineappleHandEvaluator`, and `HoldemHand`. With exactly 2 live hole cards they already play those 2.
- `FoldDuringDrawCommandHandler`. A timeout fold during the draw is existing behavior. A fold that leaves one player goes to showdown without a board; leave that.
- Red River’s second bonus card (`docs/Auditing/fix-red-river-double-bonus.md`). Do not remove or edit that call while editing the all-in branch.
- Client discard UI. `TablePlay` already treats `DrawPhase` as the player’s turn.
- Burn cards. No live path burns a card.
- Moving these games off `ProcessBettingActionCommandHandler`.

---

## 7. Done when

- Irish and Crazy Pineapple all-in preflop stop on `DrawPhase` with only the flop dealt, and reach `Showdown` with 2 hole cards and 5 community cards only after the discards.
- Phil’s Mom all-in preflop discards once with no board, sees only the flop for the second discard, then shows down with 2 hole cards and 5 community cards.
- Phil’s Mom all-in on the flop still forces the second discard before turn and river exist.
- Crazy Pineapple all-in after the discard still runs the board out and does not open another discard.
- A Hold’em all-in still goes straight to `Showdown`.
- A check-down of each game still follows the table in section 1.
