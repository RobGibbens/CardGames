# Fix: Bob Barker fold-win reveals the winner's showcase card

## Defect

On a Bob Barker hand won by fold, `TableStateBuilder.BuildShowdownPublicDtoAsync` puts the winner's showcase card into the public showdown payload with rank and suit. That payload is broadcast to the table group. `ShowdownOverlay.razor` paints it in the Showcase Results panel for every viewer.

The showcase card is a face-down hole card (`Location = Hand`, `IsVisible = false`). A fold-win does not play the showcase contest. `PerformShowdownCommandHandler.HandleWinByFoldAsync` awards the whole pot with `WinReason = "All others folded"` and returns `WinByFoldResponseBuilder.HiddenCards`. The HTTP response is already hidden. The live table does not use that response for the overlay. It uses `TableStatePublicDto.Showdown`.

The other four hole cards are already hidden on this path. Only `ShowcaseCard` and `ShowcaseCardValue` bypass the fold-win gate.

## Do not change

- `HandVisibilityHelper.IsWinByFoldHand`. It already returns true for this hand, both before the pot is awarded (`CurrentPhase` is `Showdown`, exactly one player has not folded, someone has folded) and after (`WinReason` is `All others folded`).
- `BuildSeatPublicDto`. Bob Barker is not a stud game. Fold-win seats already publish every hole card with `IsFaceUp = false` and null rank and suit, because `IsVisible` is false.
- `BuildPrivateHand`. The owner's private state must still include the showcase card, with rank, suit, and `IsShowcaseCard = true`.
- `HandleWinByFoldAsync` and `GetBobBarkerShowcaseWinners`. Payouts and the contested-showdown HTTP response are not the leak.
- `isBobBarkerShowdownReveal` (community cards, around lines 211–232) and `BobBarker.DealerCard` (around lines 1739–1753). Those reveal the hidden dealer card on a fold-win. That card belongs to no player. Leave it face up. A test below locks that in so this fix does not hide it.
- `ShowdownOverlay.razor`. It already renders a player showcase card only when `player.ShowcaseCard is not null` (around lines 162–176). Once the DTO is null, the card disappears. Do not hide the Showcase Results section on `WonByFold`. That section still shows the dealer card.
- Hand history, rabbit hunt, and Kings and Lows visibility.

## The change

File: `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`
Method: `BuildShowdownPublicDtoAsync`
Block: the `showcaseCard` lookup inside the `playerResults` projection, currently:

```csharp
var selectedShowcaseDealOrder = isBobBarker
    ? BobBarkerVariantState.GetSelectedShowcaseDealOrder(gp)
    : null;
var showcaseCard = isBobBarker && selectedShowcaseDealOrder.HasValue
    ? gp.Cards.FirstOrDefault(c =>
        !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber &&
        c.DealOrder == selectedShowcaseDealOrder.Value)
    : null;
```

Add `&& !isWinByFoldHand` to the `showcaseCard` condition, and nowhere else:

```csharp
var showcaseCard = isBobBarker && !isWinByFoldHand && selectedShowcaseDealOrder.HasValue
    ? gp.Cards.FirstOrDefault(c =>
        !c.IsDiscarded && c.HandNumber == game.CurrentHandNumber &&
        c.DealOrder == selectedShowcaseDealOrder.Value)
    : null;
```

Leave `selectedShowcaseDealOrder` as it is. The `Cards` projection below it excludes that deal order from the hand list. On a fold-win the excluded card must not reappear in `Cards` with a rank. On a contested showdown it must still be excluded there, because it is published separately as `ShowcaseCard`.

`ShowcaseCard` and `ShowcaseCardValue` are already `null` when `showcaseCard` is null:

```csharp
ShowcaseCard = showcaseCard is null
    ? null
    : new CardPublicDto
    {
        IsFaceUp = true,
        Rank = MapSymbolToRank(showcaseCard.Symbol),
        Suit = GetCardSuitString(showcaseCard.Suit),
        DealOrder = showcaseCard.DealOrder
    },
ShowcaseCardValue = showcaseCard is null
    ? null
    : GetBobBarkerCardValue(showcaseCard.Symbol, bobBarkerDealerCard?.Symbol == CardSymbol.Ace),
```

Do not add a second condition there. Do not change `IsFaceUp = true` on the contested branch. A contested showdown must still publish the showcase card face up.

`isWinByFoldHand` is already a parameter of this method. `BuildPublicStateAsync` passes `HandVisibilityHelper.IsWinByFoldHand(...)`.

## Why this covers both moments the table broadcasts

1. Betting ends with one player left. The Hold'em `ProcessBettingAction` handler sets `CurrentPhase = "Showdown"` before `PerformShowdown` runs. No pot is awarded yet. `IsWinByFoldHand` is still true because the phase is `Showdown`, one player remains, and someone has folded. `BuildPublicStateAsync` runs on that save.
2. `HandleWinByFoldAsync` then sets `CurrentPhase = "Complete"`, `pot.IsAwarded = true`, and `pot.WinReason = "All others folded"`. `IsWinByFoldHand` is true because of the win reason.

Both calls go through the same `showcaseCard` lookup.

## Tests

Add tests to `src/Tests/CardGames.IntegrationTests/Services/TableStateBuilderTests.cs`. That class already builds Bob Barker games with `DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BOBBARKER", ...)`. `BobBarkerVariantState` is usable from this test project; `PerformShowdownCommandHandlerTests` already calls `SetSelectedShowcaseDealOrder`. Matching the existing private-state test in this file is also fine: set `VariantState` to the JSON `{"SelectedShowcaseDealOrder":1}`.

Game type code is `"BOBBARKER"`. Seeded game `CurrentHandNumber` is 0. Set it to 1 and use 1 on every card and pot.

Card shape used by the live deal (`BobBarkerFlowHandler`): player cards are `Location = CardLocation.Hand`, `IsVisible = false`, `IsDiscarded = false`, `DealtAtPhase = nameof(Phases.Dealing)`, `DealOrder` 1 through 5. The dealer card is `Location = CardLocation.Community`, `GamePlayerId = null`, `IsVisible = false`, `DealOrder = 0`, `DealtAtPhase = nameof(Phases.Dealing)`.

`BuildShowdownPublicDtoAsync` returns null unless `CurrentPhase` is `Showdown`, `Complete`, or `PotMatching`. Set the phase explicitly. Set `CurrentHandGameTypeCode = "BOBBARKER"` and `Status = GameStatus.InProgress`, same as the existing Bob Barker tests in this file.

### 1. Fold-win at Showdown, before the pot is awarded

Three players, seats 0, 1, 2. Seat 0 has not folded. Seats 1 and 2 have `HasFolded = true`. All three are `GamePlayerStatus.Active`.

Give seat 0 these cards, all `IsVisible = false`, `Location = Hand`, `HandNumber = 1`:

| DealOrder | Card |
| --- | --- |
| 1 | K♠ (`CardSymbol.King`, `CardSuit.Spades`) |
| 2 | 7♦ |
| 3 | 7♣ |
| 4 | 2♥ |
| 5 | 9♠ |

Set seat 0's showcase deal order to 1 (the king).

Give seats 1 and 2 five distinct face-down hand cards each, and set each showcase deal order to 1. Their cards must not appear anywhere in the public showdown or seat payloads with a rank. Folded players are omitted from `showdownPlayers`, but their seat cards are still built.

Add one dealer card: 8♥, community, `IsVisible = false`, `DealOrder = 0`.

Add a main pot for hand 1 with `IsAwarded = false` and `WinReason = null`.

Set `game.CurrentPhase = nameof(Phases.Showdown)`.

Call `TableStateBuilder.BuildPublicStateAsync(game.Id)`.

Assert:

- `result.Showdown` is not null.
- `result.Showdown.WonByFold` is true.
- Every `result.Showdown.PlayerResults` entry has `ShowcaseCard == null` and `ShowcaseCardValue == null`.
- Every card in every `PlayerResults[].Cards` has null `Rank` and null `Suit`.
- No seat card has a non-null `Rank` or `Suit`. Seat 0 still has 5 cards, all `IsFaceUp == false`.
- The string `"King"` and the string `"Spades"` do not appear on any seat card or any showdown player card. Check the serialized public DTO if that is easier than walking every field: `JsonSerializer.Serialize(result)` must not contain `King` together with a player showcase. The dealer card is allowed to contain `Eight` and `Hearts`.
- `result.Showdown.BobBarker.DealerCard` is not null, `IsFaceUp` is true, `Rank` is `"8"`, `Suit` is `"Hearts"`. This is the out-of-scope behavior. It must stay.
- `result.CommunityCards` contains that same 8♥ face up.

### 2. Fold-win at Complete, after the award

Same cards and folds as test 1. Differences:

- `game.CurrentPhase = nameof(Phases.Complete)`.
- The hand-1 main pot has `IsAwarded = true` and `WinReason = "All others folded"`.

Same assertions as test 1, including the dealer card still face up.

### 3. Contested showdown still publishes showcase cards

Two players, neither `HasFolded`. Phase `Showdown`. Pot `IsAwarded = false`, `WinReason = null`.

Each player has 5 face-down hand cards. Each showcase deal order is 5.

- Player 0's deal order 5 is 2♣ (`CardSymbol.Deuce`, `CardSuit.Clubs`).
- Player 1's deal order 5 is 10♥.

Dealer card: J♠, community, `IsVisible = false`, `DealOrder = 0`. No other community cards are required. `holeCoreCards.Count < 4` skips hand evaluation when fewer than 4 non-showcase cards exist; these hands have 4, so evaluation runs. Missing board cards do not matter for this assertion.

Assert:

- `result.Showdown.WonByFold` is false.
- Each `PlayerResults` entry has a non-null `ShowcaseCard` with `IsFaceUp == true`.
- Player 0's `ShowcaseCard.Rank` is `"2"` and `Suit` is `"Clubs"`.
- Player 1's `ShowcaseCard.Rank` is `"10"` and `Suit` is `"Hearts"`.
- `ShowcaseCardValue` is not null on either result.
- The showcase card is not duplicated inside that player's `Cards` list (no `Cards` entry with the same `DealOrder` as `ShowcaseCard`).

### 4. Private state still shows the owner their showcase card

Use the fold-win setup from test 1, phase `Complete`. Call `BuildPrivateStateAsync(game.Id, setup.Players[0].Email!)`.

Assert the returned hand has 5 cards, one with `IsShowcaseCard == true`, `Rank == "K"`, `Suit` equal to `CardSuit.Spades.ToString()` (`"Spades"`). `CardPrivateDto.Suit` is `c.Suit.ToString()`, not the public suit string.

Call `BuildPrivateStateAsync` for a folded player's email. That hand is their own cards only. It must not contain K♠.

## Verify

From `src/Tests`:

```bash
dotnet test CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~TableStateBuilderTests.BuildPublicStateAsync_BobBarker|FullyQualifiedName~TableStateBuilderTests.BuildPrivateStateAsync_BobBarker"
```

Name the new tests so that filter matches. The two existing Bob Barker private-state tests in this class must still pass.

Do not change production code outside the `showcaseCard` condition. If a test fails because the dealer card is hidden, the change went too far.
