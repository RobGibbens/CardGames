# Close Hold the Baseball's second showdown door

An LLM implements this plan. `TablePlay.TryLoadShowdownAsync` already calls `GenericPerformShowdown` for Hold the Baseball. A second endpoint, `HoldTheBaseballPerformShowdown`, still runs its own award and writes `ChipStack`. Whichever command saves first sets `IsAwarded`, and the other command will not correct the stacks. Delete the dedicated award. Keep the route, and make it send the generic showdown command.

`docs/Auditing/odd-chip.md` step 4 adds `OddChipOrder` inside the dedicated handler and leaves that award in place. This plan replaces that step. Do not add `OddChipOrder` to the dedicated handler. If step 4 has already landed, delete that payout block anyway.

Do not edit `ContributionLayerAwarder`, `OddChipOrder`, or `TablePlay.razor`. The table stays on `GenericPerformShowdownAsync`.

## Outcome

`POST` for Hold the Baseball showdown and `POST` for generic showdown run one handler: `CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownCommandHandler`.

That handler evaluates with `HoldTheBaseballHandEvaluator` and pays with `ContributionLayerAwarder`. Layers come from `TotalContributedThisHand`. Each layer is paid to the best hand allowed to win it. A short all-in does not win the chips above that all-in. Tied winners of a layer receive leftover chips one at a time, clockwise from `DealerPosition`, through `OddChipOrder`. The dealer is last among the tied winners.

The dedicated route remains `HoldTheBaseballPerformShowdown` in `PerformShowdownEndpoint`. Refit keeps `HoldTheBaseballPerformShowdownAsync`. The response type stays the Hold the Baseball `PerformShowdownSuccessful`. It is a mapping of the generic success, not a second calculation.

## The hands that fail today

The dedicated handler sums every current-hand pot into `totalPot`, ignores `TotalContributedThisHand`, and pays that sum to the best `HoldTheBaseballHand`. On a tie it does this:

```csharp
var payoutPerWinner = totalPot / winners.Count;
var remainder = totalPot % winners.Count;
foreach (var winner in winners) payouts[winner] = payoutPerWinner;
if (remainder > 0 && winners.Count > 0) payouts[winners[0]] += remainder;
```

`winners` is dictionary order from `playersInHand`. `DealerPosition` is not read. The entire remainder is added to one player. The same full-pot JSON is copied onto every pot, and those pots are marked awarded.

Seats are not packed. Insert Alice, then Bob, then Carol:

| Player | Seat | Role |
| --- | --- | --- |
| Alice | 0 | short all-in in the side-pot hand |
| Bob | 3 | dealer (`Game.DealerPosition` is 3) |
| Carol | 5 | first seat clockwise from Bob |

No card is a three or a nine. Those ranks are wild in Hold the Baseball.

### A short all-in does not win the side pot

Stacks and contributions are the values already stored at showdown. The pot row is the single pre-showdown pot. Its amount equals the sum of `TotalContributedThisHand`.

| Player | ChipStack | TotalContributedThisHand | All-in |
| --- | --- | --- | --- |
| Alice | 0 | 50 | `IsAllIn` true, `ChipStack` 0 |
| Bob | 100 | 150 | no |
| Carol | 100 | 150 | no |

One current-hand main pot, amount 350, `IsAwarded` false.

Board, `CardLocation.Community`, `GamePlayerId` null, `DealOrder` 1 through 5: Ks Qs Js Ts 2c.

Hole cards, `CardLocation.Hole`, two each:

- Alice: Ah Ad. Best five are Ah Ks Qs Js Ts, a broadway straight.
- Bob: 8h 7h. Best five are the king-high board.
- Carol: 8d 7d. Same king-high board.

`ContributionLayerAwarder` builds two layers. The 50 layer is 150 and all three can win it. Alice wins it. The layer above 50 is 200 and only Bob and Carol can win it. They tie, so each receives 100.

Stacks after the generic award: Alice 150, Bob 200, Carol 200. The sum of the increases is 350.

The dedicated handler pays Alice 350. Bob stays at 100. Carol stays at 100. A later generic call sees `IsAwarded` and reconstructs Alice's 350 from `WinnerPayouts`. It does not move the chips back.

### One leftover chip goes to Carol

| Player | ChipStack | TotalContributedThisHand | In the hand |
| --- | --- | --- | --- |
| Alice | 100 | 5 | yes |
| Bob | 100 | 5 | yes, dealer |
| Carol | 100 | 5 | yes |
| Dave | 99 | 1 | folded, no cards |

One current-hand pot, amount 16. Dave's chip is in that pot. He is not eligible because he has folded and has no evaluated hand. Nobody is all-in, so the awarder builds one layer of 16 with three eligible players.

Board: 2c 4d 5h 6s 8c. Hole cards: Alice Ah Kd, Bob Ad Kc, Carol As Kh. All three are ace-king high. Assert the three `HoldTheBaseballHand.Strength` values are equal before asserting money.

Clockwise order is Carol, Alice, Bob. `Distribute(16, three winners)` pays Carol 6, Alice 5, Bob 5. Stacks become Carol 106, Alice 105, Bob 105. Dave stays at 99.

The dedicated handler pays 6, 5, and 5 as well, but the 6 goes to `winners[0]`. With this insert order that is Alice, so Alice's stack becomes 106 and Carol's becomes 105.

## Step 1. Forward the dedicated command

In `src/CardGames.Poker.Api/Features/Games/HoldTheBaseball/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`, delete the award. The class becomes a forwarder. It no longer takes `CardsDbContext`, `IHandHistoryRecorder`, `IHandSettlementService`, or `GameEventsTelemetry`.

```csharp
public class PerformShowdownCommandHandler(IMediator mediator)
    : IRequestHandler<PerformShowdownCommand, OneOf<PerformShowdownSuccessful, PerformShowdownError>>
{
    public async Task<OneOf<PerformShowdownSuccessful, PerformShowdownError>> Handle(
        PerformShowdownCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownCommand(
                command.GameId),
            cancellationToken);

        return result.Match<OneOf<PerformShowdownSuccessful, PerformShowdownError>>(
            success => new PerformShowdownSuccessful
            {
                GameId = success.GameId,
                WonByFold = success.WonByFold,
                CurrentPhase = success.CurrentPhase,
                Payouts = success.Payouts,
                PlayerHands = success.PlayerHands.Select(hand => new ShowdownPlayerHand
                {
                    PlayerName = hand.PlayerName,
                    PlayerFirstName = hand.PlayerFirstName,
                    Cards = hand.Cards.Select(card => new ShowdownCard
                    {
                        Suit = card.Suit,
                        Symbol = card.Symbol
                    }).ToList(),
                    HandType = hand.HandType,
                    HandDescription = hand.HandDescription,
                    HandStrength = hand.HandStrength,
                    IsWinner = hand.IsWinner,
                    AmountWon = hand.AmountWon,
                    BestCardIndexes = hand.BestCardIndexes
                }).ToList()
            },
            error => new PerformShowdownError
            {
                Message = error.Message,
                Code = error.Code == GenericPerformShowdownErrorCode.GameNotFound
                    ? PerformShowdownErrorCode.GameNotFound
                    : PerformShowdownErrorCode.InvalidGameState
            });
    }
}
```

Use the generic error enum's real name, `PerformShowdownErrorCode` in the generic namespace. Alias it in the using list so it does not collide with the Hold the Baseball enum.

Do not recompute a hand, a pot, or a stack in this file. Do not catch a generic failure and run the old award. `MoveDealer`, `SettleHandAsync`, and `WinnerPayouts` stay inside the generic handler.

Leave `PerformShowdownEndpoint` mapped. It still sends the Hold the Baseball `PerformShowdownCommand`. MediatR resolves the forwarder, and the forwarder sends the generic command. The endpoint's response type does not change.

`HoldTheBaseballFlowHandler.SupportsInlineShowdown` stays false. The generic handler then uses `ContributionLayerAwarder` rather than the flow handler's own showdown.

## Step 2. Unit tests

Update `src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldTheBaseball/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`.

Build the real generic handler on the same in-memory `CardsDbContext`:

- `new HandEvaluatorFactory()`
- `Substitute.For<IGameFlowHandlerFactory>()` whose `GetHandler("HOLDTHEBASEBALL")` returns `new HoldTheBaseballFlowHandler()`
- `Substitute.For<IHandHistoryRecorder>()`
- `Substitute.For<IHandSettlementService>()`
- `Substitute.For<ILogger<GenericPerformShowdownCommandHandler>>()`

Point the dedicated handler's `IMediator` at that generic handler:

```csharp
mediator.Send(
        Arg.Any<GenericPerformShowdownCommand>(),
        Arg.Any<CancellationToken>())
    .Returns(call => generic.Handle(
        call.Arg<GenericPerformShowdownCommand>(),
        call.Arg<CancellationToken>()));
```

The three existing facts stay, through this wiring. The missing-game fact still returns `GameNotFound`. The River fact still returns `InvalidGameState` and the message still names `River`. The one-hole-card seed has a pot of 120 and `TotalContributedThisHand` of 0, so the generic handler rejects it before paying anyone. Assert an error result and that both `ChipStack` values are still 200. The old sentence "No eligible hands could be evaluated" belongs to the deleted award. Do not keep that sentence by restoring the check.

Add two facts. Both set `CurrentHandGameTypeCode` and `Game.GameType.Code` to `HOLDTHEBASEBALL`, phase `Showdown`, `CurrentHandNumber` 1, `DealerPosition` 3. Insert the players in Alice, Bob, Carol order. Give each showdown player two hole cards and the five community cards from the matching section. The pot amount equals the sum of `TotalContributedThisHand`.

1. Side pot. `Payouts` is Alice 150, Bob 100, Carol 100. Stacks are Alice 150, Bob 200, Carol 200. Their sum is 450, which is the starting 200 plus the pot of 350. After the call, the current hand has two awarded pots: `PotOrder` 0 amount 150 whose `WinnerPayouts` names only Alice, and `PotOrder` 1 amount 200 whose `WinnerPayouts` names Bob 100 and Carol 100. Neither JSON contains 350. A second dedicated `Handle` leaves the three stacks unchanged. `SettleHandAsync` was received once, with the gross amounts 150, 100, and 100.

2. Leftover chip. Before `Handle`, construct three `HoldTheBaseballHand` values and assert the strengths are equal. `Payouts` is Carol 6, Alice 5, Bob 5. Stacks are Carol 106, Alice 105, Bob 105. Dave stays at 99. The awarded layer amount is 16. Its `WinnerPayouts` does not give Alice 6. A second `Handle` leaves every stack unchanged.

Run:

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~HoldTheBaseball.v1.Commands.PerformShowdown"
```

Fact 1 fails on the current handler: Alice's stack becomes 350. Fact 2 fails because Alice's stack becomes 106.

## Step 3. Integration tests

Add `src/Tests/CardGames.IntegrationTests/Games/HoldTheBaseball/SecondShowdownDoorTests.cs`, subclassing `IntegrationTestBase`.

Seed with `DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "HOLDTHEBASEBALL", 3, startingChips: 100, ante: 0)` for the side-pot hand, and 4 players for the leftover-chip hand. The seeder inserts `Player 1` at seat 0, then `Player 2`, then `Player 3`. Rename them Alice, Bob, and Carol, move the seats to 0, 3, and 5, and set `DealerPosition` to 3. Name the fourth player Dave, leave him folded, and give him no cards. Set phase `Showdown`, `CurrentHandNumber` 1, and `CurrentHandGameTypeCode` to `HOLDTHEBASEBALL`. Replace dealt cards and the ante pot with the cards, contributions, and single pot from the matching section. Do not call `StartHand` or betting commands.

`Mediator.Send` is the real mediator. It resolves the dedicated forwarder and the generic handler. Reload with `GetFreshDbContext` before every stack assertion.

1. Side pot, dedicated command. Stacks Alice 150, Bob 200, Carol 200. Two awarded pots, 150 and 200, with the winner JSON from the unit test. Then `Mediator.Send` the generic `PerformShowdownCommand` for the same game. The three stacks stay 150, 200, and 200.

2. The same side-pot seed, generic command only. Stacks are Alice 150, Bob 200, Carol 200. This is the same money as fact 1. The dedicated door is not allowed to differ.

3. Leftover chip, dedicated command. Stacks Alice 105, Bob 105, Carol 106, Dave 99. Then the generic command. Dave stays at 99 and the other three stay at 105, 105, and 106.

Run:

```bash
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter SecondShowdownDoorTests
```

Fact 1 calls the route's command, not a test-only helper. A unit test that only calls `OddChipOrder.Distribute` does not prove this door.

## Out of scope

Do not change generic layer math, Hold'em, or the other games that use `ContributionLayerAwarder`. Do not change wild-card ranks. Do not move `TablePlay` off `GenericPerformShowdown`. Do not remove the Hold the Baseball route or rename `HoldTheBaseballPerformShowdown`.
