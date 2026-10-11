# Fix: Baseball buy card is dealt face up

## Which side is wrong

The live table deals the bought card face up. The Baseball rule deals it face down.

`BaseballGame.ProcessBuyCardDecision` is the rules implementation. On accept it calls `gamePlayer.AddHoleCard(extraCard)` and states that extra cards are always dealt face down, so a bought 4 does not offer another buy (`src/CardGames.Poker/Games/Baseball/BaseballGame.cs`, about lines 457–467). The class summary says the same thing (about lines 16–17).

The hand that actually runs is `ProcessBuyCardCommandHandler`. About lines 159–166 it does the opposite, and the comment records that choice:

```csharp
deckCard.GamePlayerId = player.Id;
// Deal face up (Board) instead of face down (Hole)
deckCard.Location = CardLocation.Board;
deckCard.IsVisible = true;
deckCard.DealtAtPhase = currentOffer.Street;
deckCard.DealtAt = now;
deckCard.IsBuyCard = true;
deckCard.DealOrder = newDealOrder;
```

`IsVisible` is what `TableStateBuilder` shows to other players. Baseball is a stud game (`IsStudGame` includes `BASEBALL`), so a board card with `IsVisible` true is face up on every seat. Change the live deal. Do not edit `BaseballGame` to say the card is face up.

## Do not change

- `BaseballGame.cs`. Leave `AddHoleCard` and the face-down comments.
- Payment, offer queue, deal order, and `IsBuyCard`. The bought card is still inserted at `triggerCard.DealOrder + 1`, and later cards still shift up. `ProcessBuyCardSuccessful` still returns no card. The owner sees the rank through private state.
- `DealHandsCommandHandler` and `BaseballFlowHandler`. They offer a buy only for the street upcard just dealt, when that card is a 4. A face-down buy card is not that upcard, so it must not create an offer. That already matches the domain note that a bought 4 does not buy again.
- `GetExpectedCardsForStreet` and the existing-card count in `DealHandsCommandHandler` (about lines 93–108). That count is every non-deck card, so a buy card already increases it. `Location` is not part of the count. Do not exclude buy cards from it and do not change the expected totals (3, 4, 5, 6, 7).
- Showdown construction. `PerformShowdownCommandHandler` and `TableStateBuilder` both pass every `Location == Hole` card into `BaseballHand` as hole cards, and every `Location == Board` card as open cards. `StudHand` evaluates the combined set, so a hole buy card still counts. Do not add a special buy-card branch.
- `BuildPlayerVsDeckStateAsync`, stud street dealing, and seventh street. Seventh street is already `Hole` and `IsVisible = false`. The buy card uses the same pair of values, with `IsBuyCard = true` and `DealtAtPhase` left as `currentOffer.Street`.

## Code

File: `src/CardGames.Poker.Api/Features/Games/Baseball/v1/Commands/ProcessBuyCard/ProcessBuyCardCommandHandler.cs`

Replace the face-up assignment. Keep every other field in that block:

```csharp
deckCard.GamePlayerId = player.Id;
deckCard.Location = CardLocation.Hole;
deckCard.IsVisible = false;
deckCard.DealtAtPhase = currentOffer.Street;
deckCard.DealtAt = now;
deckCard.IsBuyCard = true;
deckCard.DealOrder = newDealOrder;
```

Delete the comment that says to deal it face up. One sentence is enough: the bought card is an extra hole card, same as `BaseballGame.ProcessBuyCardDecision`.

After the edit, the only `IsVisible = true` writes left in this handler must not be on the bought card. This handler should have none.

## What the table shows

- Other seats: `BuildSeatPublicDto` publishes a Baseball card only when `IsVisible` is true. The buy card has null rank and suit until showdown.
- Owner: `BuildPrivateHand` still sets rank and suit, with `IsPubliclyVisible = false`. `TableSeat` draws that as the owner's private card, not the public face-up shell.
- Contested showdown: `shouldShowCardsForContestedShowdown` reveals every card of a player who has not folded, including this hole card.
- Fold-win: the buy card stays hidden, because `IsVisible` is false.

No Web change.

## Tests

File: `src/Tests/CardGames.Poker.Tests/Api/Features/Games/Baseball/v1/Commands/ProcessBuyCard/ProcessBuyCardCommandHandlerTests.cs`

`Handle_WhenPlayerAcceptsOffer_PaysPotAndDealsCard` currently asserts:

```csharp
boughtCard.Location.Should().Be(CardLocation.Board);
boughtCard.IsVisible.Should().BeTrue();
```

Change those two lines to `CardLocation.Hole` and `false`. Keep `DealOrder` 4, chip stack 90, and pot 10.

In `Handle_WhenDecisionStartsFromStaleSnapshot_UsesFreshOfferState`, after `buyCards.Should().HaveCount(2)`, assert each buy card is `Location == CardLocation.Hole`, `IsVisible == false`, and `IsBuyCard == true`.

Add one public-state fact to `src/Tests/CardGames.IntegrationTests/Services/TableStateBuilderTests.cs`. `IntegrationTestBase` seeds `"BASEBALL"`.

- Two players, `CurrentHandNumber = 1`, `CurrentPhase = nameof(Phases.FourthStreet)`, `CurrentHandGameTypeCode = "BASEBALL"`.
- Seat 0 has two hole cards (`Location = Hole`, `IsVisible = false`) and one board 4 (`Location = Board`, `IsVisible = true`, `Symbol = CardSymbol.Four`).
- Seat 0 also has the bought card: `Location = Hole`, `IsVisible = false`, `IsBuyCard = true`, a distinct rank such as `CardSymbol.Ace`, `CardSuit.Spades`.
- Seat 1 has its own hole card, a different rank, `IsVisible = false`.

`BuildPublicStateAsync`:

- Seat 0's face-up cards include the 4 and do not include the ace. No seat card has rank `"A"` or suit `"Spades"`.
- Seat 1's hole card is not face up.

`BuildPrivateStateAsync` for seat 0's email:

- The hand includes the ace with `IsPubliclyVisible == false`.
- The 4 is present with `IsPubliclyVisible == true`.

`DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BASEBALL", 2, ante: 0)` follows the same shape as the other tests in that class. Set `CurrentHandGameTypeCode` as the Bob Barker tests do.

## Verify

From `src/Tests`:

```bash
dotnet test CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~ProcessBuyCardCommandHandlerTests"
dotnet test CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~TableStateBuilderTests"
```

Do not change `BaseballGame.cs`. If the unit test still expects `CardLocation.Board`, it was not updated.
