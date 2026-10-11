# Fix: Kings and Lows player-against-the-deck is shown as a win by fold

## Defect

`HandVisibilityHelper.IsWinByFoldHand` returns true for a Kings and Lows player-against-the-deck hand. `TableStateBuilder` then publishes `Showdown.WonByFold = true`, clears the staying player's `HandDescription` and `HandRanking`, and hides any of that player's cards whose `IsVisible` is false.

The server already treats this hand as a showdown. `KingsAndLows` `PerformShowdownCommandHandler` returns `WonByFold = false` and `IsPlayerVsDeck = true`. Hand history is recorded with `wonByFold: false`. The public snapshot disagrees with that.

`IsWinByFoldHand` has two true paths:

1. Exactly one player remains, and an awarded pot for this hand has `WinReason` equal to `"All others folded"`.
2. Exactly one player remains, `CurrentPhase` is `Showdown` or `Complete`, and anyone has folded.

Player-against-the-deck hits path 2. Dropped players have `HasFolded = true`, so one player remains. The deck's win reason is not `"All others folded"`.

Two moments broadcast this bad snapshot:

- `DeckDrawCommandHandler` sets `CurrentPhase = Showdown` after the deck has five cards, and saves before `PerformShowdown` awards the pot. Path 2 is already true. `WinReason` is still null.
- `PerformShowdown` then sets `CurrentPhase = Complete` when the deck wins, in either standalone or Dealer's Choice, and when the player beats the deck inside Dealer's Choice. `WinReason` is `Lost to deck's {description} - pot carries forward` or `Beat the deck with {description}`. Path 2 is still true.

A standalone player win sets `CurrentPhase = Ended`. `BuildShowdownPublicDtoAsync` returns null unless the phase is `Showdown`, `Complete`, or `PotMatching`, so `Ended` is not this bug. Do not add `Ended` to that phase list.

## Do not change

- `PerformShowdownCommandHandler`. Its HTTP result, pot reasons, chip movement, and hand history are already the player-against-the-deck result.
- `ShowdownOverlay.razor` and `TablePlay.razor`. The overlay already treats a hand as player-against-the-deck when a player result is named `"The Deck"`. It says "wins by fold" only because `WonByFold` is true. Fixing the flag fixes the verb, the hidden cards, and the missing hand description.
- Kings and Lows `StartHand` and `DrawCards` writing `IsVisible = true`. Once this hand is not a fold-win, showdown publication sets `IsFaceUp = true` for every card of a player who has not folded, including cards stored with `IsVisible = false`.
- The true Kings and Lows fold-win. That is branch 6b: fewer than five deck cards, `WinReason = "All others dropped"`, `CurrentPhase = Complete`. It must stay `WonByFold = true`, with hidden cards unpublished.
- Hold'em and every other game. One player left at `Showdown` before the pot is awarded, with no Kings and Lows deck hand, must stay a fold-win.

## The rule

A Kings and Lows deck hand is five cards for this hand, not discarded, `GamePlayerId == null`, `Location == CardLocation.Board`. `DrawCardsCommandHandler.DealDeckHandAsync` writes them that way, and `BuildKingsAndLowsDeckOutcomeAsync` reads them with the same filter. `PerformShowdown` takes the player-against-the-deck branch only when that count is at least 5. Branch 6b is the same hand with fewer than five such cards.

If that deck hand exists, `IsWinByFoldHand` is false. Check this before the `Showdown` / `Complete` fallback. The pre-award broadcast has no win reason yet, so a win-reason check alone misses it. The cards are still on the board after the award, so the same check covers `Complete`.

## Code

### Helper

File: `src/CardGames.Poker.Api/Services/HandVisibilityHelper.cs`

Add a fourth parameter. Default it to false so the existing unit tests keep their meaning.

```csharp
internal static bool IsWinByFoldHand(
    Game game,
    IReadOnlyList<GamePlayer> gamePlayers,
    IReadOnlyList<Pot> currentHandPots,
    bool hasKingsAndLowsDeckHand = false)
{
    var playersInHand = gamePlayers
        .Count(gp => !gp.HasFolded
                     && (gp.Status == GamePlayerStatus.Active || gp.IsAllIn));

    if (playersInHand != 1)
        return false;

    if (hasKingsAndLowsDeckHand)
        return false;

    if (currentHandPots.Any(p => p.IsAwarded
                                 && (string.Equals(p.WinReason, "All others folded",
                                         StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(p.WinReason, "All others dropped",
                                         StringComparison.OrdinalIgnoreCase))))
        return true;

    if (string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase)
        || string.Equals(game.CurrentPhase, "Complete", StringComparison.OrdinalIgnoreCase))
        return gamePlayers.Any(gp => gp.HasFolded);

    return false;
}
```

`"All others dropped"` is the branch-6b reason. Listing it next to `"All others folded"` does not change a `Complete` hand that already matches the phase fallback. It keeps that fold-win classified if the phase fallback is later narrowed. Do not treat a reason that starts with `"Beat the deck"` or `"Lost to deck"` as a fold-win. The deck-hand flag already returns false for those hands.

### Call site

File: `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`
Method: `BuildPublicStateAsync`
Current call, about line 170:

```csharp
var isWinByFold = HandVisibilityHelper.IsWinByFoldHand(game, gamePlayers, currentHandPots);
```

This is the only production call. Both `BuildSeatPublicDto` and `BuildShowdownPublicDtoAsync` receive the same bool. Do not add a second check in either method.

Before the call, compute the flag only for a Kings and Lows hand. Dealer's Choice keeps its own `Game.GameType.Code` and stores the chosen game on `CurrentHandGameTypeCode`. Use that first:

```csharp
var handTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code;
var hasKingsAndLowsDeckHand = false;
if (IsKingsAndLowsGame(handTypeCode))
{
    var deckHandCount = await _context.GameCards
        .AsNoTracking()
        .CountAsync(c => c.GameId == game.Id
                         && c.HandNumber == game.CurrentHandNumber
                         && !c.IsDiscarded
                         && c.GamePlayerId == null
                         && c.Location == CardLocation.Board,
            cancellationToken);
    hasKingsAndLowsDeckHand = deckHandCount >= 5;
}

var isWinByFold = HandVisibilityHelper.IsWinByFoldHand(
    game, gamePlayers, currentHandPots, hasKingsAndLowsDeckHand);
```

`IsKingsAndLowsGame` is a private method on `TableStateBuilder`. Do not query board cards for other game types. Stud and Baseball board cards have a `GamePlayerId`.

Do not change `BuildKingsAndLowsDeckOutcomeAsync`. With `isWinByFoldHand` false and one player result, it already appends `"The Deck"`.

## Tests

### Helper

File: `src/Tests/CardGames.Poker.Tests/Api/Services/HandVisibilityHelperTests.cs`

Keep the existing theory. Those calls use the default `hasKingsAndLowsDeckHand: false`.

Add cases:

| Players still in | Someone folded | Phase | Awarded win reason | Deck hand | Expected |
| --- | --- | --- | --- | --- | --- |
| 1 | yes | Showdown | null | true | false |
| 1 | yes | Complete | `Lost to deck's pair of Jacks - pot carries forward` | true | false |
| 1 | yes | Complete | `Beat the deck with a pair of Sevens` | true | false |
| 1 | yes | Complete | `All others dropped` | false | true |
| 1 | yes | Showdown | null | false | true |
| 2 | yes | Showdown | null | true | false |

The last row is a contested hand. `playersInHand != 1` is false before the deck-hand check.

### Public snapshot

Add tests to `src/Tests/CardGames.IntegrationTests/Services/TableStateBuilderTests.cs`. `CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 3, ante: 0)` works. `IntegrationTestBase` seeds that game type. Set `CurrentHandNumber = 1`, `CurrentHandGameTypeCode = "KINGSANDLOWS"`, and `Status = GameStatus.InProgress`.

**Deck wins at Complete.** Seats 1 and 2 have `HasFolded = true`. Seat 0 has not folded. All three are `GamePlayerStatus.Active`.

Seat 0's five cards, `Location = Hand`, `HandNumber = 1`, `GamePlayerId` set, not discarded:

| DealOrder | Card | IsVisible |
| --- | --- | --- |
| 1 | 7♥ | false |
| 2 | 7♦ | false |
| 3 | 2♣ | false |
| 4 | 3♠ | true |
| 5 | 4♥ | true |

The mixed `IsVisible` values are what the bug hides. After the fix, all five are face up.

Five deck cards, `Location = Board`, `GamePlayerId = null`, `HandNumber = 1`, `IsVisible = true`, `DealtAtPhase = "PlayerVsDeck"`: J♥ J♦ 9♣ 8♠ 5♦. Deal orders 1 through 5.

Main pot for hand 1: `IsAwarded = true`, `WinReason = "Lost to deck's pair of Jacks - pot carries forward"`. `CurrentPhase = nameof(Phases.Complete)`.

`BuildPublicStateAsync` then asserts:

- `Showdown` is not null.
- `Showdown.WonByFold` is false.
- The staying player's result has a non-null `HandDescription` and `HandRanking`.
- That result's five cards are `IsFaceUp` true, and the 7♥ is present with rank `"7"` and suit `"Hearts"`.
- A result named `"The Deck"` exists, `IsWinner` is true, and its five cards are face up, including J♥.
- Seat 0's five cards are face up, including both sevens. Seats 1 and 2 do not show the staying player's ranks.

**Same cards at Showdown, pot not awarded.** `CurrentPhase = nameof(Phases.Showdown)`, pot `IsAwarded = false`, `WinReason = null`. `WonByFold` is false, the 7♥ is face up on the staying player's showdown cards, and `"The Deck"` is present. This is the broadcast `DeckDraw` saves before `PerformShowdown`.

**Branch 6b stays a fold-win.** Same three players and the same five player cards. No board cards. Pot awarded with `WinReason = "All others dropped"`. Phase `Complete`. Assert `WonByFold` is true, the staying player's `HandDescription` is null, the three `IsVisible = false` cards have null rank and suit, and no result is named `"The Deck"`.

**Hold'em fold-win is unchanged.** One existing public-state test is enough if it already covers a fold-win. If none does, add a short one: game type `"HOLDEM"` or `"TEXASHOLDEM"` (use whichever `IntegrationTestBase.SeedBaseDataAsync` registers; the file uses codes such as `"FIVECARDDRAW"`), phase `Showdown`, one player not folded, one folded, no board cards, unawarded pot. `WonByFold` is true.

## Verify

From `src/Tests`:

```bash
dotnet test CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~HandVisibilityHelperTests"
dotnet test CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~TableStateBuilderTests"
```

Do not change the overlay, the Kings and Lows showdown command, or card `IsVisible` flags. If the deck-win test still has `WonByFold` true, the new flag is not being passed from `BuildPublicStateAsync`.
