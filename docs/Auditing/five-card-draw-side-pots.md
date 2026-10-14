# Put a short folded Five Card Draw contribution in the layer it reached

An LLM implements this plan. Five Card Draw showdown pays each stored pot to the players with `IsEligibleToWin`. The wrong chips are written earlier, by `CalculateSidePots`, when a betting round closes with someone all-in. Fix that builder. Leave showdown award, odd-chip order, Deuces/Jacks/Axe, and the Blazor route as they are.

`ProcessBettingActionCommandHandler.CalculateSidePots` in the Deuces, Jacks, and Man with the Axe feature is a copy of this method and has the same last-pot adjustment. Do not edit it in this change. `docs/Auditing/twos-jacks-side-pots.md` pays whatever rows that copy stored.

## Outcome

A player's chips occupy every layer from zero up to `TotalContributedThisHand`, and no layer above that.

For all-in levels `L1 < L2 < ... < Ln` among players who have not folded, the slices are:

- `(0, L1]`, `(L1, L2]`, ..., and one final slice above `Ln` when someone contributed more than `Ln`.
- A player with contribution `c` puts `min(c, level) - previousLevel` into a slice when `c > previousLevel`.
- A player who folded puts that slice into the pot and is stored with `IsEligibleToWin = false`.
- A player who has not folded and whose contribution reaches the slice is stored with `IsEligibleToWin = true`.

The sum of the new pot amounts equals the sum of `TotalContributedThisHand` for the players passed into the calculator. The sum of each pot's contribution rows equals that pot's `Amount`. No code adds a leftover onto the highest `PotOrder`.

## The hand that fails today

`CalculateSidePots` only counts a player in a level when `TotalContributedThisHand >= level`. A folded player who put in less than that level is omitted. After the levels are built, this block runs:

```csharp
var lastPot = game.Pots.Where(p => p.HandNumber == game.CurrentHandNumber)
    .OrderByDescending(p => p.PotOrder).First();
lastPot.Amount += totalContributions - calculatedTotal;
```

`PotOrder` descending is the highest side pot. The omitted chips are added there, with no contribution row.

Dave folded after contributing 10. Alice is all-in for 50. Bob and Carol each contributed 150. Nobody else folded. Total 360.

The level loop writes:

| Pot | Amount today | Eligible today |
| --- | --- | --- |
| Main, through 50 | 150 | Alice, Bob, Carol |
| Side, 50 through 150 | 200, then adjusted to 210 | Bob, Carol |

Dave's 10 is the adjustment on the side pot. Alice covered that 10 and cannot win the side pot. Bob can. Five Card Draw showdown then pays the side pot's `Amount` to the best eligible hand, so Bob is paid 210 instead of 200 and Alice is paid 150 instead of 160.

The same block is wrong for a fold that sits between two all-in levels when a still-higher pot exists. Dave contributed 80 and folded. Alice is all-in for 50. Bob is all-in for 150. Carol contributed 200. Total 480. Dave's first 50 is in the main pot. The next 30 is omitted because `80 < 150`, and the adjustment adds 30 to Carol's pot above 150. Bob covered 150, so he covered that 30. Carol's exclusive pot is only her last 50.

Moving the adjustment from the last pot to the main pot does not fix this hand. Dave's 30 is above Alice's 50, so Alice must not win it either.

## Step 1. Extract the layer math

Add `src/CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/ProcessBettingAction/FiveCardDrawSidePots.cs`.

```csharp
namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;

public static class FiveCardDrawSidePots
{
    public readonly record struct Stake(
        Guid GamePlayerId,
        int Contributed,
        bool HasFolded,
        bool IsAllIn);

    public readonly record struct Slice(Guid GamePlayerId, int Amount, bool Eligible);

    public readonly record struct Layer(
        int PotOrder,
        int Amount,
        int MaxContributionPerPlayer,
        IReadOnlyList<Slice> Slices);

    public static IReadOnlyList<Layer> Build(IReadOnlyList<Stake> stakes)
    {
        // ...
    }
}
```

`Build` uses every stake, folded or not. It does not read seat, name, or cards.

All-in levels are the distinct `Contributed` values where `IsAllIn` is true, `HasFolded` is false, and `Contributed > 0`, sorted ascending. If that list is empty, return an empty layer list. The caller keeps the existing single pot.

`previous` starts at 0. For each level:

- A stake is in the slice when `Contributed > previous`.
- Its amount is `Math.Min(Contributed, level) - previous`.
- `Eligible` is `!HasFolded`.
- Skip a slice whose amount is 0.
- If the slice amounts sum to 0, write no layer and still set `previous = level`.
- Otherwise append a layer. `PotOrder` is the number of layers already appended. `Amount` is the sum of the slice amounts. `MaxContributionPerPlayer` is `level - previous`. `previous` becomes `level`.

After the last level, if any stake has `Contributed > previous`, append one more layer for `Contributed - previous`. `MaxContributionPerPlayer` is the largest contribution minus `previous`. `Eligible` is still `!HasFolded`. This is the pot above every all-in.

Do not add a remainder to any layer. If the sum of layer amounts is not the sum of `Contributed`, throw `InvalidOperationException` naming both totals. `CalculateSidePots` must not catch it.

`CalculateSidePots` keeps its current guards: return immediately when fewer than two non-folded players remain, and return immediately when `Build` returns no layers. It still deletes the current hand's `PotContribution` rows through `context.PotContributions` and removes the current hand's pots from `game.Pots`. It then persists one `Pot` per layer:

- `PotType.Main` when `PotOrder` is 0, otherwise `PotType.Side`.
- `Amount`, `PotOrder`, and `MaxContributionPerPlayer` come from the layer.
- One `PotContribution` per slice with `GamePlayerId`, `Amount`, `IsEligibleToWin = slice.Eligible`, and `ContributedAt` set to the same timestamp used today.

Delete the block that loads `lastPot` and adds `totalContributions - calculatedTotal` to it. After the pots are attached to `game.Pots`, if their amounts for this `HandNumber` do not equal `activePlayers.Sum(p => p.TotalContributedThisHand)`, throw `InvalidOperationException` with both numbers. That replaces the adjustment. A mismatch must fail the betting action before `SaveChangesAsync`.

Do not change `AdvanceToNextPhase`, `ExecuteAction`, or the showdown handler. `AdvanceToNextPhase` already calls `CalculateSidePots` only when two or more players remain and one of them has `IsAllIn` and has not folded. A fold that leaves one player goes to showdown without this method, and that player still receives the whole pot.

## Step 2. Unit tests

Add `src/Tests/CardGames.Poker.Tests/Api/Features/Games/FiveCardDraw/v1/Commands/ProcessBettingAction/FiveCardDrawSidePotsTests.cs`.

These tests call `FiveCardDrawSidePots.Build` only. Use fixed `Guid`s named Dave, Alice, Bob, and Carol. Assert layer count, `PotOrder`, `Amount`, each slice's `Amount` and `Eligible`, and that every layer's slices sum to its `Amount`. The layers' amounts sum to the stakes' contributions.

1. The failing hand. Dave contributed 10, folded, not all-in. Alice 50, all-in, not folded. Bob 150, not folded, not all-in. Carol 150, not folded, not all-in. Layers are 160 then 200. The 160 layer contains Dave 10 ineligible, and Alice, Bob, and Carol 50 eligible. The 200 layer contains Bob 100 and Carol 100, both eligible, and does not contain Dave or Alice.

2. Chips between two all-ins, with a pot above the fold. Dave 80, folded, not all-in. Alice 50, all-in. Bob 150, all-in. Carol 200, not all-in. Layers are 200, 230, and 50. Dave's slices are 50 ineligible in the first layer and 30 ineligible in the second. Dave is absent from the third. The third layer is Carol 50 eligible and nobody else. Alice is eligible only in the first layer. Bob is eligible in the first two layers and absent from the third.

3. No folded short money. Alice 100 all-in. Bob 300 all-in. Carol 300 not all-in. Layers are 300 and 400. The 300 layer is 100 from each player, all eligible. The 400 layer is Bob 200 and Carol 200, both eligible. Alice is absent from it.

4. No all-in. Alice 40, Bob 40, neither all-in, neither folded. `Build` returns an empty list.

Run:

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter FiveCardDrawSidePotsTests
```

## Step 3. Integration tests

Add `src/Tests/CardGames.IntegrationTests/Games/FiveCardDraw/FoldedContributionSidePotTests.cs`, subclassing `IntegrationTestBase`.

Drive the live handlers with `Mediator`. `DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, startingChips, ante)` creates seats 0 through 3 named `Player 1` through `Player 4`, with `DealerPosition` 0. Set the four `ChipStack` values before the hand starts. Then:

```csharp
await Mediator.Send(new StartHandCommand(game.Id));
await Mediator.Send(new CollectAntesCommand(game.Id));
await Mediator.Send(new DealHandsCommand(game.Id));
```

`DealHands` opens `FirstBettingRound`. The first actor is the occupied seat after the dealer, seat 1. Every `ProcessBettingActionCommand` sets `BoundSeat` to the live `BettingRounds` row where `IsComplete` is false and `HandNumber` is the current hand. Read that `CurrentActorIndex` from a fresh context before each action. `ActionType` is `CardGames.Poker.Api.Data.Entities.BettingActionType`. A raise's `Amount` is the street total, which is `TotalContributedThisHand` after the action minus the ante already posted, because antes reset `CurrentBet` to 0.

After the round closes, `CalculateSidePots` has run and the phase is `DrawPhase`. Reload pots with `GetFreshDbContext`, including `Contributions`, for `HandNumber == 1`, ordered by `PotOrder`. Assert the amounts and eligibility below before showdown.

Then replace the five `GameCard` rows (`Location == CardLocation.Hand`, `HandNumber == 1`, `IsDiscarded == false`) of each player who has not folded. Set `CurrentPhase` to `Showdown` and save. Call `FiveCardDraw.v1.Commands.PerformShowdown.PerformShowdownCommand`. Showdown pays `pot.Amount` to the best eligible hand. Assert `ChipStack` from a fresh context. The four stacks sum to the chips that existed before the hand.

### Test A. Folded ante below the short all-in

Starting stacks: Player 1 (seat 0, Carol) 500, Player 2 (seat 1, Dave) 100, Player 3 (seat 2, Alice) 50, Player 4 (seat 3, Bob) 500. Ante 10. Sum 1,150.

Actions, in actor order:

1. Seat 1 folds.
2. Seat 2 all-in. That posts the remaining 40. Total contribution 50. Street bet 40.
3. Seat 3 raises to street total 140. Total contribution 150.
4. Seat 0 calls. Total contribution 150.

The round is complete because the only players who can still act have matched 140 and the all-in player cannot act. Phase is `DrawPhase`.

Pots before showdown:

| PotOrder | Amount | Eligible names | Ineligible |
| --- | --- | --- | --- |
| 0 | 160 | Player 1, Player 3, Player 4 | Player 2 for 10 |
| 1 | 200 | Player 1, Player 4 | none |

There is no third pot. Pot amounts sum to 360. Each pot's contribution amounts sum to its `Amount`.

Replace hole cards. Player 3: As Ah Ad Ac Ks. Player 4: 9s 9h 9c 8d 8h. Player 1: 6s 6h 5s 5h 4c. No deuces are required. Four aces beats nines full, which beats two pair.

After showdown: Player 3 has 160, Player 4 has 550, Player 1 has 350, Player 2 has 90. Sum 1,150. Player 4's 550 is 350 remaining after contributing 150, plus the 200 side pot. Player 3's 160 is the whole main pot.

### Test B. Folded bet between two all-in levels

Starting stacks: Player 1 (Carol) 500, Player 2 (Dave) 200, Player 3 (Alice) 50, Player 4 (Bob) 150. Ante 0. Pass `ante: 0` to `CreateCompleteGameSetupAsync`. Sum 900. `CollectAntes` with a zero ante posts nothing.

Actions:

1. Seat 1 bets 80.
2. Seat 2 all-in. That posts 50, which is less than 80.
3. Seat 3 all-in. That posts 150.
4. Seat 0 raises to street total 200.
5. Seat 1 folds. The 80 stays contributed.
6. The all-in seats cannot act. The round completes.

Pots before showdown:

| PotOrder | Amount | Eligible names | Ineligible |
| --- | --- | --- | --- |
| 0 | 200 | Player 1, Player 3, Player 4 | Player 2 for 50 |
| 1 | 230 | Player 1, Player 4 | Player 2 for 30 |
| 2 | 50 | Player 1 | none |

Pot amounts sum to 480. Player 2's 30 is on pot 1, not pot 0 and not pot 2.

Hole cards: Player 3 As Ah Ad Ac Ks, Player 4 9s 9h 9c 8d 8h, Player 1 6s 6h 5s 5h 4c.

After showdown: Player 3 has 200, Player 4 has 230, Player 1 has 350, Player 2 has 120. Sum 900. Player 1's 350 is 300 left after contributing 200, plus the 50 pot that only Player 1 can win. Player 4 wins the 230 middle pot, including Dave's 30. Player 3 wins only the 200 main pot.

Run:

```bash
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter FoldedContributionSidePotTests
```

Both tests fail on the current builder. Test A sees a side pot of 210 and a main pot of 150. Test B sees 30 chips on pot 2. After the fix, both pass. A unit test that only checks test 1 does not prove test B.

## Out of scope

Do not change `PerformShowdownCommandHandler`, `OddChipOrder`, or the Deuces, Jacks, and Man with the Axe copy of `CalculateSidePots`. Do not award a pot from inside the betting handler.
