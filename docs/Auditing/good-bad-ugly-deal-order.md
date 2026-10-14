# Fix Good Bad Ugly deal order

The first hand and every later hand must take cards from the shuffled deck in the same order. Today they do not.

`GoodBadUglyGame.StartHand` deals three table cards, then `DealThirdStreet` deals four hole cards to each player. `GoodBadUglyFlowHandler.DealCardsAsync` does the same, and `ContinuousPlayBackgroundService` calls that method for every hand after the first. The hand the host starts goes through `GoodBadUgly` `DealHandsCommandHandler`, which deals four hole cards to each player and only then deals The Good, The Bad, and The Ugly.

Same deck, two players, dealer in seat 0, deal starts at seat 1:

| Cards in deck order | Domain and `DealCardsAsync` | `DealHandsCommandHandler` (the hand the host starts) |
|---|---|---|
| A♠ K♠ Q♠ J♠ 2♣ 3♣ 4♣ 5♣ 9♥ T♥ J♥ | Table: A♠ K♠ Q♠. Seat 1: J♠ 2♣ 3♣ 4♣. Seat 0: 5♣ 9♥ T♥ J♥ | Seat 1: A♠ K♠ Q♠ J♠. Seat 0: 2♣ 3♣ 4♣ 5♣. Table: 9♥ T♥ J♥ |

Counts stay 4 hole + 3 community either way. The cards each seat receives do not.

## Done when

For one shuffled deck and the same seats, both of these paths assign the same suit and symbol to The Good, The Bad, The Ugly, and to each seat’s four hole cards, in the same player order:

- `DealHandsCommandHandler.Handle` after the host’s start (`TablePlay` calls Seven Card Stud start, Seven Card Stud collect antes, then Good Bad Ugly `POST .../hands/deal`).
- `GoodBadUglyFlowHandler.DealCardsAsync` (the background next-hand path).

That order is the domain order:

1. The next three remaining deck cards, in deck order, become The Good, The Bad, and The Ugly. `Location = Community`, `GamePlayerId = null`, `IsVisible = false`, `DealtAtPhase` is `TheGood`, then `TheBad`, then `TheUgly`.
2. Each active player then receives four hole cards, all four before the next player. Players are ordered from the left of the dealer. `Location = Hole`, `IsVisible = false`, `DealtAtPhase = ThirdStreet`.

Face-down stays face-down. No extra card is dealt. The stud branches in this handler stay as they are for a non-Good-Bad-Ugly game.

## Change one loop

Edit `src/CardGames.Poker.Api/Features/Games/GoodBadUgly/v1/Commands/DealHands/DealHandsCommandHandler.cs`.

The community block at the `if (isGoodBadUgly)` after the player `foreach` (the loop that sets `DealtAtPhase` from `{ "TheGood", "TheBad", "TheUgly" }`) runs too late. Move that block so it runs after `deckIndex` is set to 0 and before the player `foreach`. Leave the four-hole loop inside the player `foreach`. Delete the old community block so the three table cards are dealt once.

Keep these assignments exactly:

- Community `DealOrder` stays `index + 1` (1, 2, 3).
- Hole `DealOrder` stays `playerDealOrder` starting at 1 for each player.
- `IsVisible = false` on all seven cards per player-equivalent (4 hole + the shared 3).
- Player sort stays `OrderBy(gp => gp.SeatPosition > game.DealerPosition ? gp.SeatPosition : gp.SeatPosition + 1000)`.
- `FindFirstActivePlayerAfterDealer` stays the first actor. There is no bring-in.

Do not edit `GoodBadUglyFlowHandler.DealCardsAsync`. Its community `DealOrder` values are 0, 1, 2 and its hole `DealOrder` values start at 10. Those integers are not what this bug assigns. The Good, The Bad, and The Ugly are found by `DealtAtPhase`, not by a shared `DealOrder` sequence. Matching the flow handler’s integers would make the two paths look the same in the database and is not required for the same cards to be dealt.

Do not change `GoodBadUglyGame`. It is already community-then-holes.

## Tests

`src/Tests/CardGames.Poker.Tests/Api/Features/Games/GoodBadUgly/v1/Commands/DealHands/DealHandsCommandHandlerTests.cs`

`Handle_WhenThirdStreetContainsStaleCards_CleansThemUpAndResetsDealOrder` checks hole `DealOrder` 1–4 and community phases. Those assertions stay true. Add one assertion on the seeded deck order from `CreateDeckCard`: `Symbol = (CardSymbol)(cardIndex % 13 + 2)` and `Suit = (CardSuit)(cardIndex % 4)`, cards ordered by `DealOrder`. After `Handle`:

- Community cards ordered by `DealOrder` are deck indexes 0, 1, 2, with phases `TheGood`, `TheBad`, `TheUgly`.
- The first player in deal order (seat greater than the dealer, or the lowest seat when the dealer is last) owns deck indexes 3, 4, 5, 6 as hole cards in `DealOrder` 1–4.
- The other player owns deck indexes 7, 8, 9, 10.

`Handle_WhenDeckDoesNotContainEnoughCards_ReturnsInvalidGameState` seeds 10 deck cards. Two players need 11 cards. The failure remains an `InvalidGameState` whose message contains `Not enough cards in deck`. That test does not need a new message.

`src/Tests/CardGames.E2E/Infrastructure/GoodBadUglyTable.cs`

`PlayAsync` scripts the clicked start with `communityFirst: false` and the following hand with `communityFirst: true`. Both calls pass `communityFirst: true`.

`src/Tests/CardGames.E2E/Infrastructure/GoodBadUglyDeckScript.cs`

The comment that says the clicked start deals holes and then the table is now false. Describe one order: three table cards, then four hole cards per seat from the left of the dealer.

`src/Tests/CardGames.E2E/GoodBadUglyFiveMaxCatalogTests.cs`

`EveryScript_ConservesChips_AndBuildsADeckFromEveryButton` builds `opening` with `communityFirst: false` and expects holes in front. Build the start-path deck with `communityFirst: true` as well. For every dealer seat, the first three cards are `script.Community`, and the next four are `script.HoleCards[HoldEmPosition.SmallBlind]`.

## Proof

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~GoodBadUgly"
dotnet test src/Tests/CardGames.E2E/CardGames.E2E.csproj --filter "FullyQualifiedName~GoodBadUglyFiveMaxCatalogTests"
```

Both exit 0. The new handler assertion is the proof that the API consumes the deck the way `DealCardsAsync` does. The catalog assertion is the proof that the script used for the clicked start matches the script used for the next hand.
