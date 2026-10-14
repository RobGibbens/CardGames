# Fix the deal-response hole-card leak

The HTTP body of every deal-hands success names the rank and suit of every card just dealt, including cards persisted with `IsVisible = false`. Any seated player can call the endpoint (`IRequiresHostOrSeatedPlayer`). The host’s browser receives that body on the first hand. Later streets dealt through MediatR stay on the server, but the same success object is what `Results.Ok` serializes when a client calls the endpoint directly.

Private cards already reach the owning player through SignalR `PrivateStateUpdated`. Face-up cards already reach everyone through `TableStateUpdated`. `TablePlay.razor` uses the deal call only as a status check (`IsSuccessStatusCode`). It does not read `PlayerHands`.

## Done when

Serialize each affected success body with the API’s HTTP JSON options (`JsonStringEnumConverter`, camelCase). For a two-player hand:

- A card persisted with `IsVisible = false` does not contribute its suit name or rank name anywhere in that JSON.
- A card persisted with `IsVisible = true` and owned by a player is still present as `suit`, `symbol`, and `dealOrder` under that seat.
- Persisted `GameCard` rows are unchanged: same count, suit, symbol, location, `IsVisible`, and `DealOrder`.
- Stud and Razz bring-in still selects the door. Existing deal-handler tests that assert the bring-in seat still pass.
- `GameId`, `CurrentPhase`, `HandNumber`, `CurrentPlayerIndex`, `CurrentPlayerName`, and one `playerHands` entry per dealt player are still present. `playerHands` stays a required array. Hidden cards are omitted from `cards`, not replaced with null suits.

## Leave these alone

- Showdown `PlayerHands`. Those are a different DTO and are out of this fix.
- `ProcessDraw` discarded and replacement cards.
- `GetCurrentPlayerTurn`.
- `TableStateBuilder`, SignalR, and `TablePlay.razor`.
- Deck order, `IsVisible` on insert, bring-in rules, and chip movement.
- `src/CardGames.Contracts/RefitInterface.v1.cs` and `src/CardGames.Poker.Refitter/Output/RefitInterface.v1.cs`. The JSON shape of `DealtCard` stays `suit`, `symbol`, `dealOrder`. Empty `cards` arrays are already valid. Do not regenerate Refit.

## Where the body is built

`Results.Ok(success)` writes the handler return value. Filter that value. Filtering only one endpoint leaves the wrappers leaking, because they return the inner handler’s object.

| HTTP caller | Handler that puts suits into `PlayerHands` |
|---|---|
| Seven Card Stud, Razz (`SevenCardStudDealHands`) | `SevenCardStud/.../DealHands/DealHandsCommandHandler.cs` |
| Pair Pressure | same stud handler, returned by `PairPressure/.../DealHands/DealHandsCommandHandler.cs` |
| Tollbooth | same stud handler, returned by `Tollbooth/.../DealHands/DealHandsCommandHandler.cs` after it moves two deck cards to face-up `Community` |
| Baseball | `Baseball/.../DealHands/DealHandsCommandHandler.cs` (two `return new DealHandsSuccessful`) |
| Follow the Queen | `FollowTheQueen/.../DealHands/DealHandsCommandHandler.cs` |
| Good Bad Ugly | `GoodBadUgly/.../DealHands/DealHandsCommandHandler.cs` |
| Five Card Draw | `FiveCardDraw/.../DealHands/DealHandsCommandHandler.cs` |
| Deuces/Jacks/Man with the Axe | `TwosJacksManWithTheAxe/.../DealHands/DealHandsCommandHandler.cs` |

Five Card Draw’s `PlayerDealtCards` / `DealtCard` live in `src/CardGames.Poker.Api/Models/`. The other handlers each define their own copies. Do not merge those types.

Stud, Follow the Queen, and Baseball choose the bring-in door with `playerHand.Cards.LastOrDefault()` **before** the return. On Third Street the last entry is the door only while the hole cards are still in that list. Build the filtered list after that call, and pass the filtered list only into `DealHandsSuccessful.PlayerHands`.

`ProcessBettingAction` for stud, baseball, and Follow the Queen reads `CurrentPlayerIndex`, `CurrentPlayerName`, and `CurrentPhase` from this result. It does not read `PlayerHands`. Tollbooth’s display cards are `Community` rows, not `PlayerHands`. Leave that block as it is.

## Implementation

1. In each handler above, keep the list used for bring-in and first-actor selection intact until that selection has finished.
2. Immediately before each `return new DealHandsSuccessful`, set `PlayerHands` to a copy whose `Cards` contain only the cards added for an assignment with `isVisible: true`.
   - Record that fact at the `dealtCards.Add` site (a side set of `(seat, dealOrder)`, or a second list). `DealtCard` has no visibility field. Do not add one. A new JSON property would ship on every `DealtCard`, including turn queries that share the stud record.
   - Five Card Draw and Deuces/Jacks set `IsVisible = false` on every dealt card. Their returned `Cards` arrays are empty. Keep the sort that writes `DealOrder` on the persisted rows.
   - Good Bad Ugly hole cards are face down. The three community cards are face down and are not part of `PlayerHands`. Returned `Cards` arrays are empty.
   - Stud-family Third Street and Fourth/Fifth/Sixth keep the face-up board card. Seventh Street’s new card is a hole card (`isVisible: false` in `AssignCardToPlayer`). Omit it.
   - Baseball keeps the face-up board card, including a four that creates a buy offer. Omit the hole cards. The buy offer is stored from the `GameCard`, not from `PlayerHands`.
3. Apply the same projection to both Baseball return statements.
4. Do not log suit or symbol while doing this.
5. Update `src/Tests/CardGames.Poker.Tests/Api/Features/Games/GoodBadUgly/v1/Commands/DealHands/DealHandsCommandHandlerTests.cs`. It currently expects `playerHand.Cards.Count == 4`. Expect two player entries with empty `Cards`, and keep the database assertions (8 player cards, deal orders 1–4, three community cards).

## Proof

Add tests that call the handler, then serialize `result.AsT0` with:

```csharp
new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() }
};
```

Use a deck whose hidden cards have suits and ranks that do not appear on any face-up card in that response.

| Case | Hidden cards absent from JSON | Still present |
|---|---|---|
| Stud Third Street, two players. Doors are K♥ and J♣. Hole cards are A♠, 2♦ and 9♣, 3♥ | `Spades`, `Ace`, `Diamonds`, `Deuce`, `Clubs`+`Nine`, `Hearts`+`Three` | `Hearts`+`King`, `Clubs`+`Jack` |
| Same handler with `CurrentHandGameTypeCode = RAZZ` and the same doors | the same hole names | the same doors. Bring-in seat is the higher door (ace low): K♥ brings in over J♣ |
| Stud Seventh Street, one new hole card Q♠ | `Spades`, `Queen` | no new `cards` entry for that Q♠ |
| Five Card Draw, five hole cards each | every dealt suit and rank | `playerHands` length equals the player count, each `cards` length 0, rows still in the database |
| Good Bad Ugly | the four hole ranks and the three community ranks (`TheGood` / `TheBad` / `TheUgly` are face down and are not in this DTO) | empty `cards`, community rows still face down in the database |
| Baseball Third Street | hole ranks | the door, including when the door is a four |
| Follow the Queen Third Street | hole ranks | the door |
| Deuces/Jacks | every hole rank | empty `cards` |

Pair Pressure and Tollbooth need no separate projection. One stud test plus a Tollbooth test that the returned `PlayerHands` cards are only doors is enough to lock the wrapper. Tollbooth’s two display cards stay face-up `Community` rows and stay out of `PlayerHands`.

Run:

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~DealHands"
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~DealHands"
```

Both commands exit 0. The integration deal tests assert persisted cards and bring-in seats, not the HTTP body. If one of them asserts a hole card inside `PlayerHands`, point that assertion at `GameCards` and keep the bring-in assertion on the seat index.
