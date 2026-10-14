# Fix the all-in runout hole-card leak

An LLM implements this plan. Change only the public projection. Leave dealing, `IsVisible`, payouts, and the Blazor animation alone.

## Outcome

While a hand is in `Showdown`, `GameSettings` says this hand is an all-in runout, and no pot for this hand is awarded, every public card follows the stored `GameCard.IsVisible` flag.

- `IsVisible == false`: `IsFaceUp` is false, `Rank` is null, and `Suit` is null.
- `IsVisible == true`: `IsFaceUp` is true, and rank and suit are published as they are today.

After any pot for the current hand is awarded, or the phase is `Complete`, a contested showdown still reveals every non-folded player's cards, including hole cards whose `IsVisible` is still false. That final reveal is the existing rule. This plan does not remove it.

The viewer's private state still contains that viewer's own hidden cards.

## Why both sites leak

`DealRemainingStreetsAsync` in these handlers stores seventh street as `CardLocation.Hole` with `IsVisible = false`, then sets `GameSettings.allInRunout`, `runoutStreets`, and `runoutHandNumber`, then sets the phase to `Showdown`:

- `src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` (`isVisible = street != SeventhStreet`)
- `src/CardGames.Poker.Api/Features/Games/Baseball/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/GoodBadUgly/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/Tollbooth/TollboothRunout.cs` (`IsVisible = !isSeventhStreet`), reached from the Seven Card Stud betting handler

Hold'em-style runouts use the same `allInRunout` flag and deal community cards with `IsVisible = true`. Their hole cards stay `IsVisible = false`.

`TableStateBuilder.BuildPublicStateAsync` then publishes three copies of those hidden cards:

1. `BuildAllInRunoutStateAsync` sets `IsFaceUp = c.IsVisible` but always sets `Rank` and `Suit` from the entity. A face-down seventh-street card leaves the server as `IsFaceUp: false, Rank: "3", Suit: "Hearts"`.
2. `BuildSeatPublicDto` sets `shouldShowCardsForContestedShowdown` for every non-folded player when the phase is `Showdown`, `Complete`, or `PotMatching` and the hand is not a fold win. That forces `IsFaceUp` true and fills rank and suit for hole cards that are still `IsVisible == false`.
3. `BuildShowdownPublicDtoAsync` maps showdown cards with `IsFaceUp = isWinByFoldHand ? c.IsVisible : true` and fills rank and suit unless it is a fold win. The same method publishes a Bob Barker showcase card and dealer card face up.

`TablePlay` already hides a face-down runout card when rank and suit are null (`CreateRunoutCardInfo`). `TablePlayCountdownTests.CreateRunoutCardInfo_KeepsFaceDownRunoutCardsHidden` locks that client behavior. The client also keeps the previous seat backs while `_runoutAnimationInProgress` is true, and it does not call `TryLoadShowdownAsync` during that animation. The broadcast still contains the identities. Fix the broadcast.

`CardPublicDto` in `src/CardGames.Contracts/SignalR/TableStatePublicDto.cs` already states the contract: when face-down, `Rank` and `Suit` are null.

## Step 1. Name the window

Add this method to `src/CardGames.Poker.Api/Services/HandVisibilityHelper.cs`.

```csharp
internal static bool IsUnawardedAllInRunout(Game game, IReadOnlyList<Pot> currentHandPots)
```

Return true only when all of these hold:

- `game.CurrentPhase` equals `Showdown`, ordinal ignore case.
- `game.GameSettings` is non-empty JSON.
- The root has `allInRunout` with boolean value `true`.
- If the root has `runoutHandNumber`, its integer value equals `game.CurrentHandNumber`. A missing `runoutHandNumber` does not fail the check. That matches `BuildAllInRunoutStateAsync`.
- No pot in `currentHandPots` has `IsAwarded == true`.

Return false when the JSON is missing, the flag is absent or false, the hand number belongs to another hand, any current-hand pot is awarded, or `JsonDocument.Parse` throws `JsonException`. Swallow only `JsonException`. Do not log card symbols.

Parse with `JsonDocument`, the same way `BuildAllInRunoutStateAsync` does. Do not deserialize `GameSettings` into `Dictionary<string, object>`.

`BuildPublicStateAsync` already has `currentHandPots` for `game.CurrentHandNumber` before it calls `HandVisibilityHelper.IsWinByFoldHand`. Compute the flag once there:

```csharp
var isUnawardedAllInRunout = HandVisibilityHelper.IsUnawardedAllInRunout(game, currentHandPots);
```

Pass that boolean into `BuildSeatPublicDto` and `BuildShowdownPublicDtoAsync`.

Completion: a unit test can force each boundary below without a database.

## Step 2. Publish a face-down card without an identity

In `TableStateBuilder`, add one private mapper and use it for the runout list:

```csharp
private static CardPublicDto ToRunoutPublicCard(GameCard card)
{
    var faceUp = card.IsVisible;
    return new CardPublicDto
    {
        IsFaceUp = faceUp,
        Rank = faceUp ? MapSymbolToRank(card.Symbol) : null,
        Suit = faceUp ? GetCardSuitString(card.Suit) : null,
        DealOrder = card.DealOrder
    };
}
```

Replace the object initializer in `BuildAllInRunoutStateAsync` with `runoutCards.Select(ToRunoutPublicCard)`. Leave the query, the phase gate, `runoutStreets`, and the empty-dictionary behavior as they are. `BuildAllInRunoutStateAsync` still returns null unless the phase is `Showdown`.

`GetCardSuitString` returns the same names the enum `ToString()` returns for the four suits (`Hearts`, `Diamonds`, `Clubs`, `Spades`). Face-up runout cards stay readable by the existing client.

Completion: a face-down runout row cannot carry a rank or a suit. A face-up runout row still carries both.

## Step 3. Stop the showdown override during that window

`BuildSeatPublicDto` takes a new `bool isUnawardedAllInRunout` argument.

Keep the existing fold-win and upcard rules. Change only the contested-showdown term:

```csharp
var shouldShowCardsForContestedShowdown =
    isShowdownPhase && !gamePlayer.HasFolded && !isWinByFoldHand && !isUnawardedAllInRunout;
```

`IsStudGame` already includes Seven Card Stud, Razz, Baseball, Follow the Queen, Pair Pressure, and Tollbooth. Visible upcards still match `(isSevenCardStud && card.IsVisible)`. Hidden hole cards no longer match the contested-showdown term while the flag is true.

In `BuildShowdownPublicDtoAsync`, thread the same flag through. Where player-result cards are projected, replace the fold-win ternary with:

```csharp
var showCard = c.IsVisible || (!isWinByFoldHand && !isUnawardedAllInRunout);
```

Set `IsFaceUp = showCard`. Set `Rank` and `Suit` only when `showCard` is true. Use `MapSymbolToRank` and `GetCardSuitString`, which this projection already uses.

Apply the same `showCard` rule to the Bob Barker showcase card and the Bob Barker dealer card in that method. A hidden showcase or dealer card stays `IsFaceUp` false with null rank and suit while `isUnawardedAllInRunout` is true. When the flag is false, those two objects stay face up with rank and suit, which is what `BuildPublicStateAsync_BobBarker_ContestedShowdown_PublishesShowcaseCards` requires.

Do not change `BuildPrivateHand`, `BuildPrivateStateFromSeatAsync`, or any `*PrivateDto`.

Do not set `IsVisible = true` on seventh street when the phase becomes `Showdown`. The stored flag stays false. The `Complete` reveal works because `shouldShowCardsForContestedShowdown` becomes true again once the runout is no longer unawarded.

Completion: during the window, `Seats[].Cards`, `Showdown.PlayerResults[].Cards`, showcase, and the Bob Barker dealer card omit rank and suit for every `IsVisible == false` card. Face-up upcards and face-up community cards still publish rank and suit.

## Step 4. Tests

### Unit tests

Extend `src/Tests/CardGames.Poker.Tests/Api/Services/HandVisibilityHelperTests.cs`.

`IsUnawardedAllInRunout` returns true for phase `Showdown`, settings `{"allInRunout":true,"runoutStreets":["SeventhStreet"],"runoutHandNumber":1}`, `CurrentHandNumber` 1, and a pot with `IsAwarded` false.

It returns false for each of these, one case at a time:

- phase `Complete`
- phase `FourthStreet`
- `allInRunout` false
- `runoutHandNumber` 2 while `CurrentHandNumber` is 1
- missing `GameSettings`
- invalid JSON
- a current-hand pot with `IsAwarded` true
- missing `runoutHandNumber` still returns true when the other true conditions hold

### Integration tests

Add these to `src/Tests/CardGames.IntegrationTests/Services/TableStateBuilderTests.cs`. Seed with the same style as `BuildPublicStateAsync_SevenCardStud_ShowsBoardCards`: a real game row, two active players who have not folded, `CurrentHandNumber` 1, and `GameCards` rows. Use Seven Card Stud. One test covers the shared builder, so Baseball, Follow the Queen, Good Bad Ugly, and Tollbooth do not each need a copy.

Unawarded runout at `Showdown`:

- Alice seat 0: `Ah` and `Ad` at `CardLocation.Hole`, `DealtAtPhase` `ThirdStreet`, `IsVisible` false; `7c` at `CardLocation.Board`, `DealtAtPhase` `FifthStreet`, `IsVisible` true; `3h` at `CardLocation.Hole`, `DealtAtPhase` `SeventhStreet`, `IsVisible` false.
- Bob seat 1: the same pattern with `Kh`, `Kd`, `2s`, and `9c`.
- `GameSettings` is `{"allInRunout":true,"runoutStreets":["FifthStreet","SixthStreet","SeventhStreet"],"runoutHandNumber":1}`.
- One unawarded current-hand pot.
- Phase `Showdown`.

Assert all of the following on `BuildPublicStateAsync`:

- Alice's runout entry for the `7c` is face up with rank `7` and suit `Clubs`.
- Alice's runout entry for the `3h` is face down with null rank and null suit. Bob's `9c` entry is the same.
- Alice's seat contains a face-up `7` and also contains face-down cards with null rank and suit for the aces and the `3h`.
- Bob's seat does not contain rank `A`, `3`, `K`, or `9`.
- `Showdown.PlayerResults` for both players follows the same face-up and face-down split.
- `Showdown.WonByFold` is false.

Awarded hand at `Complete`:

- Same cards and the same `GameSettings`.
- Phase `Complete`.
- The current-hand pot has `IsAwarded` true.
- `AllInRunout` is null, because `BuildAllInRunoutStateAsync` returns null outside `Showdown`.
- Both seats and `Showdown.PlayerResults` publish `Ah`, `3h`, `Kh`, and `9c` face up. This is the reveal that must survive.

Normal showdown, no runout flag:

- Do not add a new test if `BuildPublicStateAsync_BobBarker_ContestedShowdown_PublishesShowcaseCards` and `BuildPublicStateAsync_SouthDakotaShowdown_BestCardIndexesUseThreeHoleAndTwoCommunity` already run. They must still pass. They are the guard that a `Showdown` phase without this flag still reveals.

### Commands

Run:

- `dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter HandVisibilityHelperTests`
- `dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter TableStateBuilderTests`
- `dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter TablePlayCountdownTests`

Completion: those three filters pass. The new unawarded-runout test fails on the current code and passes after the mapper and the flag.

## Out of scope

- Do not change `TablePlay.razor`, `TableSeat.razor`, or `CreateRunoutCardInfo`.
- Do not change when betting handlers set `Showdown` or `allInRunout`.
- Do not flip `IsVisible` on seventh street at deal time or at showdown.
- Do not hide a card whose `IsVisible` is already true. Community-card runouts and stud upcards stay public.
- Do not change fold-win hiding. `IsWinByFoldHand` still suppresses contested reveal, and `BuildPublicStateAsync_BobBarker_FoldWinAtShowdown_HidesShowcaseCard` must still pass.
- Do not change `GetCurrentPlayerTurn` odds. That is a different leak.
- Do not change odd-chip payouts.

## Done

The work is done when:

- An unawarded `Showdown` broadcast for an all-in runout contains no rank and no suit for any card with `IsVisible` false, in `AllInRunout`, `Seats`, and `Showdown`.
- The same cards are face up with rank and suit once the phase is `Complete` or a current-hand pot is awarded.
- Face-up runout streets still publish rank and suit during the window.
- The viewer's private hand still includes that viewer's hidden cards.
- The three test filters above pass, including the existing Bob Barker, South Dakota, stud upcard, and fold-win visibility tests.
