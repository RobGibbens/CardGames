# Fix: hole cards are written to server logs

## Defect

A log line is a copy of a hidden card. These lines include rank and suit. They go to the Web or API process, and OpenTelemetry is already wired to collect logs. Anyone who can read the host logs can read the hand.

Do not log a card's rank, suit, or symbol at any level. `LogDebug` is not a fix. A debug level still records the card when that level is enabled. Counts, seat, phase, deal order, location, and `IsVisible` are safe. The face of the card is not.

`@key` strings in Razor and hand-description text shown to the player are not logs. Leave them.

## 1. Web private-state update

File: `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`
Method: the private-state handler, about lines 1852–1857.

This runs at Information for every private hand:

```csharp
Logger.LogInformation(
    "Updating seat {SeatIndex} cards from private state: {CardCount} cards, first card: {FirstCard}",
    targetSeatIndex,
    state.Hand.Count,
    $"{state.Hand[0].Rank} of {state.Hand[0].Suit}");
```

`state.Hand[0]` is that player's first hole card. Delete the first-card argument and the words `first card`. Keep the seat and the count:

```csharp
Logger.LogInformation(
    "Updating seat {SeatIndex} cards from private state: {CardCount} cards",
    targetSeatIndex,
    state.Hand.Count);
```

Do not replace it with a `LogDebug` that still formats `Rank` or `Suit`. The assignment to `_seats[targetSeatIndex].Cards` below this log stays as it is.

The nearby logs at about lines 1842, 4093, 4131, 4153, 4175, 4324, and 4601 do not print a card face. Leave them.

## 2. Delete the card-order debug block

File: `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`
Method: `BuildPublicStateAsync`, about lines 111–150.

Both calls are `LogInformation`, not Debug. For every stud player they print every card of the current hand, including hole cards (`IsVisible` false), as `Rank` plus suit initial. The second line prints the same face again as `Card`.

Delete the whole block, including the comment `DEBUG: Log card data before ordering for Seven Card Stud` and the `isSevenCardStudGame` local. That local is only used to guard these two logs. `OrderCardsForDisplay` still orders the cards later. Nothing else reads `orderKeys`.

## 3. Private-hand debug line

File: `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`
Method: `BuildPrivateHand`, about lines 769–775.

```csharp
_logger.LogDebug(
    "BuildPrivateHand for player {PlayerName}: {FilteredCount} cards for hand #{HandNumber}, ordered: [{OrderedCards}]",
    gamePlayer.Player.Name,
    orderedCards.Count,
    currentHandNumber,
    string.Join(", ",
        orderedCards.Select(c => $"{c.Symbol}{c.Suit}(DO={c.DealOrder},Phase={c.DealtAtPhase})")));
```

This is the owner's private hand. Drop the ordered-card argument. Keep the count:

```csharp
_logger.LogDebug(
    "BuildPrivateHand for player {PlayerName}: {FilteredCount} cards for hand #{HandNumber}",
    gamePlayer.Player.Name,
    orderedCards.Count,
    currentHandNumber);
```

`orderedCards` is still used to build the private DTO. Do not remove that variable.

## 4. Street deal logs

The same three messages are copied in:

- `src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1/Commands/DealHands/DealHandsCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/DealHands/DealHandsCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/GoodBadUgly/v1/Commands/DealHands/DealHandsCommandHandler.cs`

Each file has three logs that interpolate `$"{c.Symbol}{c.Suit}(DO={c.DealOrder})"`. Third street deals two hole cards, so the "dealt" line is an Information log of hole cards on every hand.

Apply the same edit in each file.

**Stale-card warning.** Keep the warning, the count, the player name, and the player id. Remove `Stale cards: [{Cards}]` and the `string.Join` that prints symbol and suit. The message can still say the stale cards are being deleted.

**Existing-cards Information log.** Keep player name, id, seat, existing count, starting deal order, and phase. Remove the `ExistingCards=[{ExistingCards}]` placeholder and its `string.Join`.

The query that builds `existingCards` selects `Symbol` and `Suit` only for those logs. After the logs no longer read them, drop `Symbol` and `Suit` from that `Select`. Keep `DealOrder` and `Location` only if something other than the log still reads them. If nothing does, select no card fields and use `CountAsync` instead of materializing the list. The stale-card delete below re-queries `GameCards` itself. Do not change that delete.

**Dealt-cards Information log.** It currently logs every dealt card's symbol and suit. Replace it with count and phase only:

```csharp
logger.LogInformation(
    "DealHands: Player {PlayerName} dealt {CardCount} cards on {Phase}",
    gamePlayer.Player.Name,
    dealtCards.Count,
    game.CurrentPhase);
```

`dealtCards` is still added to `playerHands`. Do not stop recording the dealt cards on the command result. Only the log changes.

## Do not change

- Hand evaluation, showdown text, and `HandDescriptionFormatter`. Those strings are the game result, not a server log.
- Razor `@key` values that contain rank and suit. They are not written to the log stream.
- Tests that format `Suit-Symbol` for an assertion.
- Logging of player name, seat, phase, card count, or `IsVisible`.

## Verify

From the repo root, this must print nothing. It looks for a log call whose message or following argument formats a card face:

```bash
rg -n --glob '!**/Tests/**' --glob '!**/Archive/**' 'Log(Information|Debug|Warning|Error|Trace)\(' -g '*.cs' -g '*.razor' src | rg -n 'Symbol|Suit|Rank of|first card'
```

Then confirm the three deal-handler files and `TableStateBuilder.cs` no longer contain `Symbol}{` inside a log. A normal search is enough:

```bash
rg -n 'Symbol\}\{' src/CardGames.Poker.Api src/CardGames.Poker.Web
```

The only remaining hits, if any, must be UI keys or hand-description code, not `LogInformation`, `LogDebug`, or `LogWarning`.

No new test project. These branches have no log assertions today. `dotnet build src/CardGames.Poker.Api/CardGames.Poker.Api.csproj` and `dotnet build src/CardGames.Poker.Web/CardGames.Poker.Web.csproj` must succeed after the unused `Select` fields are removed.
