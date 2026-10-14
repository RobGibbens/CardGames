# Award each Deuces, Jacks, and Man with the Axe pot to the players who matched it

An LLM implements this plan. The table calls `TwosJacksManWithTheAxePerformShowdown`. That handler writes `ChipStack`. Change only that showdown award. Leave dealing, betting, `CalculateSidePots`, the generic showdown, Hold'em, and the Blazor route as they are.

`docs/Auditing/odd-chip.md` step 3 edits this same handler. It keeps the whole-pot `totalPot / 2` split and only replaces the seat walk. This plan deletes that whole-pot split. If step 3 has already landed, replace it anyway. Odd chips inside a layer still go through `OddChipOrder`. Do not edit `OddChipOrder.cs`.

## Outcome

Each current-hand pot is awarded on its own, in `PotOrder`.

A player can win a pot only when a `PotContribution` on that pot has `IsEligibleToWin` and that player is still in the evaluated showdown. Folded players stay ineligible. A short all-in does not win a side pot they did not match.

On each pot, the natural-sevens rule applies to that pot's amount, not to the sum of the hand:

- Sevens half is `pot.Amount / 2`. The high-hand half is `pot.Amount - sevensHalf`, so the odd chip starts in the high-hand half.
- Sevens winners are the eligible players whose `TwosJacksManWithTheAxeDrawHand.HasNaturalPairOfSevens()` is true. They are not the players who have sevens anywhere at the table.
- High-hand winners are the eligible players whose `hand.Strength` equals the maximum strength among those eligible players. The maximum is not the best hand in the whole showdown.
- When that pot has no eligible sevens winner, add the sevens half back into the high-hand half and pay the whole pot as high hand.
- Each half is split with `OddChipOrder.Distribute` after `OddChipOrder.OrderClockwiseFromDealer`. The dealer seat is `game.DealerPosition`. One leftover chip goes to each winner in that order. The dealer is last among the tied winners of that half.

`ChipStack` increases by the sum of the pots that player won. The sum of those increases equals the sum of the current-hand pot amounts. `IHandSettlementService.SettleHandAsync` still receives that gross dictionary. It subtracts `TotalContributedThisHand` itself.

This is the rule already implemented for the in-memory game by `PotManager.AwardPotsSplit`, which `TwosJacksManWithTheAxeGame.PerformShowdown` calls per pot. The API does not call `PotManager`. The rows written by `ProcessBettingActionCommandHandler.CalculateSidePots` are the layers to pay.

## The hand that fails today

`PerformShowdownCommandHandler.Handle` sets `totalPot` to the sum of every pot for `game.CurrentHandNumber`. It then splits that sum once. `IsEligibleToWin` is never read. `CalculateSidePots` has already stored the layers.

Alice is all-in for 100. Bob is all-in for 300. Carol calls 300. No one folded. No natural pair of sevens.

| Pot | Amount | Eligible |
| --- | --- | --- |
| Main, `PotOrder` 0 | 300 | Alice, Bob, Carol |
| Side, `PotOrder` 1 | 400 | Bob, Carol |

Hands, with no deuce, jack, or king of diamonds:

- Alice: As Ah Ad Ac Ks. Four aces.
- Bob: 9s 9h 9c 8s 8h. Nines full of eights.
- Carol: 6s 6h 5s 5h 4s. Two pair, sixes and fives.

Stacks after the bets, before showdown: Alice 0, Bob 0, Carol 700. They started at 100, 300, and 1000.

The live handler pays Alice 700. Bob stays at 0. Carol stays at 700.

After this fix: Alice 300, Bob 400, Carol 700. Both pots roll the sevens half into the high-hand half. Alice wins the main pot. Bob wins the side pot. `SevensPoolRolledOver` is true.

## Where the layers come from

`CollectAntesCommandHandler` creates the main pot and one `PotContribution` per seated player who posted, with `IsEligibleToWin = true`. A later fold sets `HasFolded` and does not clear that flag.

`ProcessBettingActionCommandHandler.ExecuteAction` adds the chips to `TotalContributedThisHand` and to the pot whose `PotOrder` is 0. It does not rewrite contribution rows.

`AdvanceToNextPhase` calls `CalculateSidePots` only when at least two players remain and one of them has `IsAllIn`. That method deletes the current-hand pots and contributions and inserts one pot per all-in level, plus a final pot for chips above the highest all-in. Eligible players get `IsEligibleToWin = true`. Folded players who reached that level get `IsEligibleToWin = false`. Showdown trusts those rows. Do not rebuild the layers in the showdown handler.

`PerformShowdownCommandHandler` currently loads pots with `.Include(g => g.Pots)` and does not include `Contributions`. There is no lazy loading. Add:

```csharp
.Include(g => g.Pots)
.ThenInclude(p => p.Contributions)
```

Five Card Draw's showdown handler already includes contributions this way.

## Step 1. Replace the whole-pot award

In `src/CardGames.Poker.Api/Features/Games/TwosJacksManWithTheAxe/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`, leave steps 1 through 7 as they are, including the win-by-fold return. A single remaining player still receives `totalPot` once, and the fold response still uses `WinByFoldResponseBuilder.HiddenCards`.

Delete the `orderedPlayerNames` loop (the `Skip((DealerPosition + i) % GamePlayers.Count)` walk) and the block that sets `sevensPool` and `handPool` from `totalPot`.

After `playerHandEvaluations` is filled, award in `PotOrder`:

```csharp
var payouts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var sevensPayouts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var highHandPayouts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var sevensWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var highHandWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var sevensPoolRolledOver = false;
var potAwards = new Dictionary<Guid, List<PotAward>>();

foreach (var pot in currentHandPots.OrderBy(p => p.PotOrder))
{
    if (pot.Amount == 0)
        continue;

    var eligible = EligibleForPot(pot, playerHandEvaluations);
    if (eligible.Count == 0)
        return new PerformShowdownError
        {
            Message = $"Pot {pot.PotOrder} of {pot.Amount} has no eligible winner.",
            Code = PerformShowdownErrorCode.InvalidGameState
        };

    var sevensHere = eligible
        .Where(entry => entry.Value.hand.HasNaturalPairOfSevens())
        .Select(entry => entry.Value.gamePlayer)
        .ToList();
    var maxStrength = eligible.Max(entry => entry.Value.hand.Strength);
    var highHere = eligible
        .Where(entry => entry.Value.hand.Strength == maxStrength)
        .Select(entry => entry.Value.gamePlayer)
        .ToList();

    var sevensHalf = pot.Amount / 2;
    var highHalf = pot.Amount - sevensHalf;
    if (sevensHere.Count == 0)
    {
        highHalf += sevensHalf;
        sevensHalf = 0;
        sevensPoolRolledOver = true;
    }

    var awards = new List<PotAward>();
    if (sevensHalf > 0)
        awards.AddRange(AwardHalf(sevensHere, sevensHalf, game.DealerPosition, isSevens: true));
    if (highHalf > 0)
        awards.AddRange(AwardHalf(highHere, highHalf, game.DealerPosition, isSevens: false));

    foreach (var award in awards)
    {
        Add(payouts, award.PlayerName, award.Amount);
        if (award.IsSevens)
        {
            Add(sevensPayouts, award.PlayerName, award.Amount);
            sevensWinners.Add(award.PlayerName);
        }
        else
        {
            Add(highHandPayouts, award.PlayerName, award.Amount);
            highHandWinners.Add(award.PlayerName);
        }
    }

    potAwards[pot.Id] = awards;
}

if (payouts.Values.Sum() != currentHandPots.Sum(p => p.Amount))
    return new PerformShowdownError
    {
        Message = "Awarded chips do not equal the current hand's pots.",
        Code = PerformShowdownErrorCode.InvalidGameState
    };
```

`EligibleForPot` returns the slice of `playerHandEvaluations` that may win that pot:

- The hand has at least one `PotContribution`: keep evaluations whose `gamePlayer.Id` equals a contribution with `IsEligibleToWin`. A folded player is absent from `playerHandEvaluations`, so an ante row left eligible after a fold does not pay them. A row with `IsEligibleToWin == false` does not pay them either.
- The hand has no `PotContribution` rows: every entry in `playerHandEvaluations` is eligible. That is the single pot when nobody was all-in and antes were zero, so `CalculateSidePots` never ran. Do not use this fallback when any contribution row exists. An empty eligible set after filtering real rows is the error above, not a license to pay every player in the hand.

`AwardHalf` orders that half's winners and pays them:

```csharp
private static List<PotAward> AwardHalf(
    List<GamePlayer> winners,
    int amount,
    int dealerSeat,
    bool isSevens)
{
    var ordered = OddChipOrder.OrderClockwiseFromDealer(
        winners,
        dealerSeat,
        gamePlayer => gamePlayer.SeatPosition);
    var shares = OddChipOrder.Distribute(amount, ordered);
    var awards = new List<PotAward>(ordered.Count);
    for (var index = 0; index < ordered.Count; index++)
        awards.Add(new PotAward(ordered[index].Player.Name, shares[index], isSevens));
    return awards;
}
```

`PotAward` is a private record `(string PlayerName, int Amount, bool IsSevens)` in this handler. `Add` is the existing `GetValueOrDefault` accumulation. The file already imports `CardGames.Poker.Api.Features.Games.Common.Showdown`.

A player who takes both halves of one pot, or the same half of two pots, has one summed entry in `payouts`, `sevensPayouts`, and `highHandPayouts`.

`SevensWinners` and `HighHandWinners` contain only players who were paid that half. A player who holds sevens and is ineligible for every pot is in neither set. `SevensPoolRolledOver` is true when any pot rolled its sevens half, including a hand where every pot rolled. `AwardPotsSplit` uses that same sticky flag.

Return the error before `ChipStack` changes and before `SaveChangesAsync`.

## Step 2. Persist one award per pot

Keep the chip update where it is: inside `if (!isAlreadyAwarded)`, add each `payouts` value to that player's `ChipStack` once.

Replace the loop that writes the same `winnerPayoutsJson` onto every pot. For each pot, serialize only `potAwards[pot.Id]`, grouped by player name:

```csharp
new
{
    playerId = gamePlayer.PlayerId.ToString(),
    playerName = name,
    amount = sevensForThisPot + highForThisPot,
    sevensAmount = sevensForThisPot,
    highHandAmount = highForThisPot
}
```

`amount` is that pot only. `TableStateBuilder` reads every awarded pot's `WinnerPayouts` and adds `amount`, `sevensAmount`, and `highHandAmount` (`BuildShowdownPublicDtoAsync`, the `actualPayouts` loop). Writing the hand total onto each pot makes that sum count the same chips once per pot.

`WinReason` is per pot:

- Both halves paid: `7s: {names}; High: {names}` using that pot's winners.
- Sevens half rolled and one high-hand winner: that winner's `hand.Type`.
- Sevens half rolled and several high-hand winners: `Split pot - {hand.Type}`.

Then set `IsAwarded`, `AwardedAt`, phase `Complete`, `HandCompletedAt`, `NextHandStartsAt`, and `MoveDealer` as the method does now. Call `SettleHandAsync(game, payouts, ...)` with the summed gross dictionary. `RecordHandHistoryAsync` still receives `totalPot` and one `AmountWon` per player equal to that player's summed gross payout. A player with a payout of 0 is not a winner. Losers keep `NetChipDelta = -TotalContributedThisHand`.

When `isAlreadyAwarded` is true, do not change stacks, pots, dealer, or settlement. Build the response from the same per-pot calculation so a second call shows the awarded result. The first save still wins.

The response fields stay the same: `Payouts`, `SevensPayouts`, `HighHandPayouts`, `SevensWinners`, `HighHandWinners`, `SevensPoolRolledOver`, and each `ShowdownPlayerHand.AmountWon`, `SevensAmountWon`, `HighHandAmountWon`, `IsSevensWinner`, and `IsHighHandWinner`.

## Step 3. Tests

Add `src/Tests/CardGames.Poker.Tests/Api/Features/Games/TwosJacksManWithTheAxe/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`.

Follow `KingsAndLows` `PerformShowdownCommandHandlerTests`: EF Core in-memory `CardsDbContext`, `Substitute.For<IHandHistoryRecorder>()`, `Substitute.For<IHandSettlementService>()`. Seed `Game`, `Player`, `GamePlayer`, `Pot`, `PotContribution`, and five `GameCard` rows per showdown player (`Location = CardLocation.Hand`, `HandNumber` equal to the game, `IsDiscarded` false). Set `ChipStack` to the stack after the bets. Set `TotalContributedThisHand` to the amount each player put in. Set `DealerPosition` to the dealer's seat.

Assert these hands. Stacks are after showdown.

1. The failing hand above. No sevens. Main 300 eligible for all three. Side 400 eligible for Bob and Carol. Alice's cards are As Ah Ad Ac Ks, Bob's are 9s 9h 9c 8s 8h, Carol's are 6s 6h 5s 5h 4s. Starting post-bet stacks 0, 0, 700. Result: Alice 300, Bob 400, Carol 700. `Payouts` is Alice 300, Bob 400. `SevensPoolRolledOver` is true. `SevensPayouts` is empty. `HighHandPayouts` matches `Payouts`. Main `WinnerPayouts` contains only Alice for 300. Side `WinnerPayouts` contains only Bob for 400. Sum of `ChipStack` is 1400. A second `Handle` leaves the stacks at those values.

2. Sevens on the main pot only. Same money and eligibility. Alice's cards are 7s 7h 3c 4d 5s. Bob and Carol keep the hands from test 1. Result: Alice 150, Bob 550, Carol 700. `SevensPayouts` is Alice 150. `HighHandPayouts` is Bob 550. Alice is a sevens winner and not a high-hand winner. Bob is a high-hand winner and not a sevens winner. `SevensPoolRolledOver` is true because the side pot rolled. Alice's 150 is the entire main-pot sevens half. She receives none of the 400.

3. One pot, no all-in, no contribution rows. Pot amount 90. Alice's four aces, Bob's full house, both stacks 500 after posting 45, both in `playerHandEvaluations`. Alice's stack becomes 590. Bob's stays 500. `Payouts` is Alice 90.

4. One pot of 90 whose ante rows mark both the folder and the two callers `IsEligibleToWin`. The folder has `HasFolded` and no hole cards in the evaluation. The two callers tie with As Ah Ks Qc Jh and Ad Ac Kh Qs Jd. Dealer is Bob at seat 3. Alice is seat 0. Clockwise order is Alice then Bob. Alice receives 45, Bob receives 45. The folder's stack does not increase.

5. Main pot 5, all three eligible, all three the same hand strength, no sevens. Seats Alice 0, Bob 3, Carol 5. `DealerPosition` is 3. The pot pays Carol 2, Alice 2, Bob 1.

Do not add a test that goes through `CalculateSidePots`. These tests seed the rows showdown already receives.

## Out of scope

`CalculateSidePots` still adds chips that were below the first all-in level onto the last pot. A folded ante smaller than the short all-in can land in the side pot. This plan pays the stored rows and does not change that builder.

Do not change fold-win payout, dealer movement, draw, antes, or the route in `TablePlay.razor`.
