# Fix: Kings and Lows marks the owner's cards as public

## Defect

`IsVisible` means the card is face up for the table. `TableStateBuilder.BuildPrivateHand` copies it to `CardPrivateDto.IsPubliclyVisible`. `TableSeat.razor` uses that flag for the signed-in player:

- `IsPubliclyVisible` true renders the face-up public shell (`TableSeat.razor` lines 37–44).
- `IsPubliclyVisible` false, with a rank and suit, renders the private shell (lines 53–59). The owner still sees the card.
- Another player's seat shows a face only when `IsPubliclyVisible`, rank, and suit are all set (`ShouldShowRemoteCardFace`, lines 229–233).

Two Kings and Lows writes set `IsVisible = true` on hole cards.

1. `StartHandCommandHandler`, the loop that deals five cards, about line 353:

```csharp
card.IsVisible = true; // Cards visible to the player in their hand
```

The Web start button sends this command. The comment is wrong. Private state already gives the owner rank and suit. `IsVisible` does not control that.

2. `DrawCardsCommandHandler`, the loop that replaces discarded cards, about line 150:

```csharp
cardFromDeck.IsVisible = true;
cardFromDeck.IsDrawnCard = true;
```

Every draw uses this command, including auto-draw. `KingsAndLowsFlowHandler.SendDrawActionAsync` sends `DrawCardsCommand`. A player who keeps three and draws two then has two cards stored as public. On that player's own seat those two use the face-up public shell. Opponents still see backs, because Kings and Lows is not a stud game and `BuildSeatPublicDto` does not publish a hidden card's rank.

The background deal is already private. `KingsAndLowsFlowHandler.GetDealingConfiguration` sets `AllFaceDown = true`, and `BaseGameFlowHandler` sets `card.IsVisible = !config.AllFaceDown`. Do not change that path.

## Do not change

- `KingsAndLowsFlowHandler`. Initial cards from the background service are already `IsVisible = false`.
- Deck cards. These stay `IsVisible = true` because everyone sees the deck's hand:
  - `DrawCardsCommandHandler.DealDeckHandAsync`, about line 482.
  - `DeckDrawCommandHandler`, about lines 97 and 152. Those cards have `GamePlayerId = null` and `Location = Board`.
- `BuildPrivateHand`. It must keep `Rank` and `Suit` for the owner's cards and keep `IsPubliclyVisible = c.IsVisible`.
- `TableSeat.razor`. After the flag is false, the owner falls through to the private shell. No markup change.
- `BuildPlayerVsDeckStateAsync`. `StayingPlayerCards` and `DeckCards` are built with `IsFaceUp = true` and do not read `IsVisible`. During `PlayerVsDeck`, the staying player's hand stays face up on that DTO.
- Showdown publication. A contested showdown sets `IsFaceUp = true` for a player who has not folded, regardless of `IsVisible`. A fold-win still hides cards whose `IsVisible` is false. This change makes a Kings and Lows fold-win hide the drawn cards too, which is what a fold-win is supposed to do.
- `GetGamePlayers` and the other query endpoints. Those are a separate defect.

Do not rewrite cards already stored. The next deal and the next draw are the fix.

## Code

### Initial deal

File: `src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/Commands/StartHand/StartHandCommandHandler.cs`

In the five-card deal loop, set the dealt card private and replace the comment:

```csharp
card.Location = CardLocation.Hand;
card.GamePlayerId = player.Id;
card.IsVisible = false;
card.DealtAtPhase = nameof(Phases.Dealing);
```

Leave the deck loop above it alone. Those cards are already `IsVisible = false` and `Location = Deck`. Leave the later sort that rewrites `DealOrder`.

### Drawn cards

File: `src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/Commands/DrawCards/DrawCardsCommandHandler.cs`

In the replacement loop only, about line 150, set:

```csharp
cardFromDeck.GamePlayerId = gamePlayer.Id;
cardFromDeck.Location = CardLocation.Hand;
cardFromDeck.DealOrder = playerCards.Count + i + 1;
cardFromDeck.DealtAtPhase = "DrawPhase";
cardFromDeck.IsVisible = false;
cardFromDeck.IsDrawnCard = true;
cardFromDeck.DrawnAtRound = 1;
cardFromDeck.DealtAt = now;
```

Keep `IsDrawnCard = true`. The command response still returns `Suit` and `Symbol` to the acting player in `newCardInfos`. That response is the actor's own cards. Do not strip it.

Do not change `DealDeckHandAsync` in this file. Search the file for `IsVisible = true` after the edit. The only remaining assignment is the deck-hand loop, and it must stay true.

## Tests

Add facts to `src/Tests/CardGames.IntegrationTests/Features/Commands/KingsAndLowsCommandTests.cs`. That class already starts a Kings and Lows hand with `StartHandCommand` and `"KINGSANDLOWS"`.

### Start hand

After `Mediator.Send(new StartHandCommand(setup.Game.Id))`, reload `GameCards` for that game and hand.

- Each seated player has five cards with `Location == CardLocation.Hand` and `GamePlayerId` set.
- Every one of those cards has `IsVisible == false`.
- Cards still in `Location == CardLocation.Deck` have `IsVisible == false`.

Then call `TableStateBuilder.BuildPrivateStateAsync(game.Id, player.Email)`. The class can take `ITableStateBuilder` the same way `TableStateBuilderTests` does: `Scope.ServiceProvider.GetRequiredService<ITableStateBuilder>()`.

- The private hand has five cards.
- Each has a non-empty rank and `IsPubliclyVisible == false`.

Call `BuildPublicStateAsync(game.Id)`.

- No seat card has a non-null rank or suit.

`DatabaseSeeder.CreatePlayerAsync` sets `Email` to `{name}@test.com` when none is passed. `CreateCompleteGameSetupAsync` uses that. Pass the seeded player's `Email`.

### Draw

Use the same start, then move the loaded game to the draw:

- `CurrentPhase = nameof(Phases.DrawPhase)`.
- `CurrentDrawPlayerIndex` equals seat 0's `SeatPosition`.
- Seat 0's `HasDrawnThisRound` is false.
- `SaveChanges`.

Send `new DrawCardsCommand(game.Id, seat0.PlayerId, [0])`. `DrawCardsCommand` is in `CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards`. Index 0 discards one card and deals one replacement. One player drawing does not deal the deck hand, because the other players have not finished drawing.

Reload cards with a fresh context.

- The discarded card has `IsDiscarded == true` and `Location == CardLocation.Discarded`.
- The new hand card has `IsDrawnCard == true`, `DealtAtPhase == "DrawPhase"`, `Location == CardLocation.Hand`, and `IsVisible == false`.
- The other four hand cards are still `IsVisible == false`.
- `BuildPrivateStateAsync` for that player returns five cards, all with `IsPubliclyVisible == false`, all with a rank.
- `BuildPublicStateAsync` still has no seat rank or suit.

### Deck hand stays public

Do not implement a second deck deal. After the draw test, the file must still contain `IsVisible = true` only for board cards with `GamePlayerId == null` in `DealDeckHandAsync` and both assignments in `DeckDrawCommandHandler`. If a test fails because the deck hand is face down, those assignments were changed and must be put back.

## Verify

From `src/Tests`:

```bash
dotnet test CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~KingsAndLowsCommandTests|FullyQualifiedName~KingsAndLowsGameFlowTests"
```

`KingsAndLowsGameFlowTests` also sends `StartHandCommand`. It must still pass. It does not assert `IsVisible`.
