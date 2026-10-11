# Fix plan: Five Card Draw runs out of cards on the draw

**Audit finding:** “Five Card Draw can run out of cards on the draw, even at its own 6-seat limit.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** `ProcessDrawCommandHandler` for Five Card Draw only. Do not change Twos/Jacks, Kings and Lows, the domain `FiveCardDrawGame`, discard limits, or showdown.

---

## 1. Problem summary

Five Card Draw deals 5 cards to each player from a 52-card deck. The draw then replaces what each player discards. A player may discard up to 3 cards, or up to 4 if they hold an ace. The live handler deals those replacements only from rows with `Location == Deck` and `GamePlayerId == null`. If that stub is short, it returns `ProcessDrawErrorCode.InsufficientDeckCards` and does not save. The discards stay in the hand. The player cannot draw the number of cards the rules allow.

Discarded cards are not put back. `IsDiscarded` is set to true and `Location` stays `Hole` (the handler never sets `CardLocation.Discarded`). They are not in the stub query.

### Why 6 seats is enough to hit it

`FiveCardDrawRules` allows 6 players. The deal uses 30 cards and leaves a stub of 22.

Worst legal draw at that table: five players each hold an ace and discard 4 (20 cards). The stub has 2 left. The sixth player discards 3. The handler needs 3 deck rows, finds 2, and returns `InsufficientDeckCards`. The 20 cards already discarded are enough to fill that draw. They are never used.

An 8-seat Dealer’s Choice hand uses the same handler. The deal leaves 12 cards. The stub runs out on the fourth 4-card draw. Same bug, earlier in the round. Do not special-case the seat count. Rebuilding the stub from discards fixes both.

### What is wrong with the current error

The error text says a short stub is an inconsistent deck. It is not. A 6-handed hand with four-card draws is a legal deck. `InsufficientDeckCards` remains the right code only when the stub, the earlier discards, and the cards just discarded in this call together still cannot fill the draw.

---

## 2. Intended behavior

1. Deal replacements from the existing stub first, in `DealOrder` ascending. Do not shuffle the stub.
2. If the stub is shorter than the number of cards actually discarded, fill the rest from **earlier** discards (saved `IsDiscarded` cards from this hand, not the cards this call just discarded).
3. Shuffle only that returned pile. Park every card in it at the end of the stub, then deal from the front until the draw is filled. Cards from that pile that this player does not take stay on the stub, in that shuffled order, for the next player.
4. If earlier discards are still not enough, shuffle the cards **this call** just discarded and append those the same way. A player receives their own discard only when the stub and every earlier discard together cannot fill the draw.
5. A draw that the stub can already fill does not move any discarded card.
6. Standing pat (`discardedCards.Count == 0`) does not read the deck and does not shuffle anything.
7. After a successful draw the player has 5 live cards: `Location == Hole`, `IsDiscarded == false`, `GamePlayerId` set to that player. No card id is both in their hand and on the stub. No card id is dealt to two players.
8. The ace rule is unchanged: more than 3 discards without an ace is still `TooManyDiscards`. More than 4 is still `TooManyDiscards`.

Deal `discardedCards.Count` replacements, not `command.DiscardIndices.Count`. The discard loop already uses `Distinct()`. A duplicate index must not deal an extra card.

---

## 3. Root cause

**File:** `src/CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/ProcessDraw/ProcessDrawCommandHandler.cs`  
**Method:** `Handle`, step 8, lines 147–196.

```csharp
var availableDeckCards = await context.GameCards
    .Where(gc => gc.GameId == game.Id
        && gc.HandNumber == game.CurrentHandNumber
        && gc.GamePlayerId == null
        && gc.Location == CardLocation.Deck)
    .OrderBy(gc => gc.DealOrder)
    .Take(command.DiscardIndices.Count)
    .ToListAsync(cancellationToken);

if (availableDeckCards.Count >= command.DiscardIndices.Count)
{
    // deal those rows
}
else
{
    return new ProcessDrawError
    {
        Code = ProcessDrawErrorCode.InsufficientDeckCards
    };
}
```

`Take` runs in the store, so the handler never sees the rest of the stub. The `else` branch is the whole bug. There is no second query for `IsDiscarded` cards.

The game load (lines 30–37) includes `GameCards` only where `!IsDiscarded`. Earlier discards are not on `game.GameCards`. Query `context.GameCards` directly.

This call marks the current discards `IsDiscarded = true` in the tracker and does not save until the end. A store query for `IsDiscarded == true` does not see them (same unsaved-state gap as the Red River bonus check). Keep the just-discarded cards in a list. Do not expect the query to return them.

The domain class `FiveCardDrawGame.ProcessDraw` deals from `FrenchDeckDealer` and throws when that deck is empty. The API does not call it. Leave it.

---

## 4. Required code change

Edit only `ProcessDrawCommandHandler.Handle`, in the block that deals replacements. Keep the discard loop, the ace check, `FindNextDrawPlayer`, and `StartSecondBettingRound` as they are.

### 4.1 How many cards to deal

After the discard loop, `discardedCards` is the list of cards actually marked. Use:

```csharp
var drawCount = discardedCards.Count;
```

Remember the entities, not just the `CardInfo` DTOs. Before the loop, `var justDiscarded = new List<GameCard>();` and add `cardToDiscard` inside the loop next to the `CardInfo` add.

If `drawCount == 0`, skip section 4.2 and leave `newCards` empty.

### 4.2 Build the replacement list

**Stub.** One query, no `Take`:

```csharp
var stub = await context.GameCards
    .Where(gc => gc.GameId == game.Id
        && gc.HandNumber == game.CurrentHandNumber
        && gc.GamePlayerId == null
        && gc.Location == CardLocation.Deck
        && !gc.IsDiscarded)
    .OrderBy(gc => gc.DealOrder)
    .ToListAsync(cancellationToken);

stub = stub.Where(gc => gc.GamePlayerId == null && gc.Location == CardLocation.Deck && !gc.IsDiscarded).ToList();
```

The second line is the tracked-state re-filter. EF can return a row that matched in the store after this context has already moved that instance off the deck. Do not drop it.

If `stub.Count >= drawCount`, the replacements are `stub.Take(drawCount)`. Do not query discards. Do not change any discarded card.

**Earlier discards**, only when `stub.Count < drawCount`:

```csharp
var justDiscardedIds = justDiscarded.Select(c => c.Id).ToHashSet();

var priorDiscards = await context.GameCards
    .Where(gc => gc.GameId == game.Id
        && gc.HandNumber == game.CurrentHandNumber
        && gc.IsDiscarded
        && gc.Location != CardLocation.Deck)
    .ToListAsync(cancellationToken);

priorDiscards = priorDiscards.Where(gc => !justDiscardedIds.Contains(gc.Id)).ToList();
```

Do not filter `Location == CardLocation.Discarded`. This handler never writes that location. Live discards stay `Location == Hole` with `IsDiscarded == true` and the old `GamePlayerId`.

Shuffle `priorDiscards` with one new `StandardRandomNumberGenerator` (namespace `CardGames.Core.Random`):

```csharp
var rng = new StandardRandomNumberGenerator();
for (var i = priorDiscards.Count - 1; i > 0; i--)
{
    var j = rng.Next(i + 1);
    (priorDiscards[i], priorDiscards[j]) = (priorDiscards[j], priorDiscards[i]);
}
```

`Next` is an exclusive upper bound, matching `System.Random.Next`. One generator per reshuffle. Do not use a static `Random`. Do not shuffle `stub`.

**Own discards**, only when `stub.Count + priorDiscards.Count < drawCount`. Shuffle `justDiscarded` with a second pass of that same generator and the same loop. Append that list after `priorDiscards`.

**Park the returned cards on the stub** before dealing. Let `returned` be `priorDiscards` plus, only if needed, the shuffled `justDiscarded`. Let `nextOrder` start at `(stub.Count == 0 ? 0 : stub.Max(c => c.DealOrder)) + 1`. For each card in `returned`, in list order:

- `Location = CardLocation.Deck`
- `GamePlayerId = null`
- `IsDiscarded = false`
- `DiscardedAtDrawRound = null`
- `IsVisible = false`
- `IsDrawnCard = false`
- `DrawnAtRound = null`
- `DealtAtPhase = null`
- `DealOrder = nextOrder++`

`GamePlayerId = null` is required. A prior discard still points at the player who folded it. Leaving that id set puts the card back into their live hand as soon as `IsDiscarded` is cleared, and the next stub query (`GamePlayerId == null`) will not see it.

**Deal.** `replacements = stub.Concat(returned).Take(drawCount)`. If that count is less than `drawCount`, return the existing `InsufficientDeckCards` error and do not reach `SaveChangesAsync`. Parking mutations are only in the tracker, so a return here leaves the database unchanged.

If the count is sufficient, run the existing assignment loop on `replacements` instead of `availableDeckCards`. Keep the fields it already sets (`GamePlayerId`, `Location = Hole`, `DealOrder = ++maxDealOrder`, `DealtAtPhase = DrawPhase`, `IsDrawnCard = true`, `DrawnAtRound = 1`, `DealtAt = now`). Also set `IsDiscarded = false` and `DiscardedAtDrawRound = null` on each dealt card. The parking step already clears those; setting them again keeps a card that was dealt from the original stub correct too.

`newCards` must follow `replacements` order: entire stub prefix first, then returned cards. The response the client shows is that order.

Unused entries in `returned` stay on the deck with the `DealOrder` values assigned above. The next call’s stub query orders by `DealOrder`, so it deals that shuffled remainder before anything appended later.

### 4.3 Save point

Do not add a `SaveChangesAsync` on the error path. The successful path already saves once at the bottom of `Handle`. Do not save between parking and dealing.

---

## 5. Tests

**File:** `src/Tests/CardGames.Poker.Tests/Api/Features/Games/FiveCardDraw/v1/Commands/ProcessDraw/ProcessDrawCommandHandlerTests.cs`

`SeedGameAsync` already takes `deckCardCount`. `CreateDeckCard` stores stub cards at `DealOrder` 100 + index. `CreatePlayerCard` stores hole cards at `DealOrder` 1–5, `Location = Hole`.

### 5.1 Replace the test that locks in the bug

Delete the expectation in `Handle_WhenDeckDoesNotContainEnoughReplacementCards_ReturnsInsufficientDeckCards`. A stub of 1 and a draw of 2, with no earlier discard, must succeed by dealing the stub card and exactly one of the two cards just discarded.

Assert:

- `result.IsT0`
- `NewCards` has 2 entries
- the stub card’s id is one of the dealt hole cards (`Location == Hole`, `GamePlayerId` is the acting player, `IsDiscarded == false`)
- exactly one of the two discarded ids is dealt back the same way
- the other discarded id stays `IsDiscarded == true` and is not the acting player’s live card
- the acting player has 5 live cards (`!IsDiscarded`, `GamePlayerId` set)

Do not assert which of the two discards came back. That choice is the shuffle.

### 5.2 Stub is used before any discard

Seed `deckCardCount: 5` and discard two cards. Assert both new cards are from those five deck rows, none of the acting player’s discarded ids are among the new cards, and the other three deck rows stay `Location == Deck`, `GamePlayerId == null`.

### 5.3 Earlier discards are used before the player’s own

Seed `deckCardCount: 1`. Before `Handle`, save one extra card: Bob’s id, `IsDiscarded = true`, `Location = Hole`, `GamePlayerId` Bob, `HandNumber` 1, a distinct suit and symbol. Alice discards two (`[0, 1]`).

Assert:

- success, 2 new cards
- the first dealt card is the stub card (the only original deck row)
- the second dealt card is Bob’s discarded card, now `Location == Hole`, `GamePlayerId` Alice, `IsDiscarded == false`
- both of Alice’s discarded ids stay `IsDiscarded == true`
- Bob has no live card with that id

One card in the returned pile makes the shuffle a no-op, so this assertion is stable.

### 5.4 The remainder stays on the stub for the next player

Seed `deckCardCount: 0` and two saved discards on Bob (`IsDiscarded = true`, `Location = Hole`, `GamePlayerId` Bob), distinct ids. Alice is seat 0 and the current draw index; Bob is seat 1 and has not drawn. Alice discards one card (`[0]`).

Assert Alice’s new card is one of those two Bob discards, and the other Bob discard is now `Location == Deck`, `GamePlayerId == null`, `IsDiscarded == false`, and its `DealOrder` is greater than any remaining stub `DealOrder`.

Then send a second `Handle` so Bob discards one card. His new card’s id must be that parked deck card, not Alice’s discard. Alice’s discarded card stays `IsDiscarded == true`.

`FindNextDrawPlayer` already moves `CurrentDrawPlayerIndex` to Bob after Alice’s draw. The second call uses that saved index. Reload is unnecessary if the same `CardsDbContext` is reused; `SaveChangesAsync` has run.

### 5.5 The error is still returned when the cards are not in this hand

Seed `deckCardCount: 0`. Do not add a prior discard. Before `Handle`, this case cannot use the acting player’s own cards as the only pile and still fail, because those cards are the pile.

Force the failure by making the draw need a card that is not among stub, prior discards, or `justDiscarded`: pass two discard indices, and after seeding, set both of those player cards’ `HandNumber` to `game.CurrentHandNumber + 1` and save. The hand-size check counts `game.GameCards` for the loaded hand. Cards with the wrong hand number are excluded by the include (`HandNumber == current` and `!IsDiscarded`), so `playerCards.Count < 5` returns `InvalidCardIndex` first.

That is the wrong code for this test. Instead seed a full 5-card hand on the current hand number, `deckCardCount: 0`, and discard two. Then the own-discard path succeeds. Cover `InsufficientDeckCards` by a direct arrangement that skips the include: not required if 5.3 and 5.4 hold.

Keep a failing-deck test only if it still returns `InsufficientDeckCards` without depending on the include. Do not leave the old test expecting that code for a stub of 1 and a draw of 2.

### 5.6 Command

From the repo root:

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~ProcessDrawCommandHandlerTests"
```

The old short-stub test must fail on the current handler if it still expects `InsufficientDeckCards`, and the new assertions must pass after the change.

---

## 6. Out of scope

- `TwosJacksManWithTheAxe/.../ProcessDrawCommandHandler.cs` returns `InvalidCardIndex` when `deckCards.Count < drawCount` (about lines 165–171). Same missing reshuffle. Do not edit it here.
- `KingsAndLows/.../DrawCardsCommandHandler.cs` returns `InsufficientCards` from its own deck query. Do not edit it here.
- `FiveCardDrawGame.ProcessDraw` in `CardGames.Poker`. The API hand does not call it.
- Changing the 3-card / ace-allows-4 limit.
- Burning a card before the draw. No live path burns a card.
- Dealing a fresh 52 and risking duplicate ranks. Reuse the existing `GameCard` rows.

---

## 7. Done when

- At 6 seats, after five 4-card draws, the sixth player’s 3-card draw succeeds. Two cards come from the stub and the third from an earlier discard, not from the two cards that player just threw if those earlier discards cover it.
- A stub that can fill the draw is unchanged aside from the cards dealt, and no discarded card comes back.
- A returned card that this player does not need is the next player’s first deck card.
- A card dealt back has `IsDiscarded == false`. Showdown ignores `IsDiscarded` cards (`PerformShowdownCommandHandler`), so leaving the flag set drops the replacement out of the hand.
- `TooManyDiscards`, bad indices, and the wrong phase still fail as they do now.
