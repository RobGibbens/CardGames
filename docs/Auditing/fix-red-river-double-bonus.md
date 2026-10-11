# Fix plan: Red River deals a second bonus card on all-in runouts

**Audit finding:** “Red River deals a second bonus card on all-in runouts.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** Red River (`REDRIVER`) community-card dealing inside `ProcessBettingActionCommandHandler` only. Do not change betting amounts, phase order, showdown routing, evaluation, or any other game.

---

## 1. Problem summary

A Red River board is 5 community cards, plus **one** sixth card when the river suit is hearts or diamonds. That sixth card is stored as a `GameCard` with `Location = Community`, `DealtAtPhase = "RedRiverBonus"`, and `DealOrder = 6`.

On the normal path this is correct: the bonus is dealt once, in the same request that deals the river, and later requests see it in the database.

On an all-in runout that **deals the river in the same request**, the handler deals the bonus and then deals it again. Both rows are saved. The board has 7 cards (flop 3, turn, river, bonus, bonus). Both bonus rows use `DealOrder` 6.

This happens when the runout starts in `PreFlop`, `Flop`, or `Turn` **and** the river card just dealt is red. About half of those runouts. An all-in that starts on `River` does not hit it, because that river and its bonus were saved in an earlier request.

The extra card is visible to every client and is included in the hand. Hold’em showdown loads every non-discarded community card into `HoldemHand`, which takes the best 5 of 2 hole cards plus **every** board card. A seventh board card changes who wins.

### Concrete hand (heads-up)

- Seat 0 Alice is the button and small blind. Seat 1 Bob is the big blind. 200 chips each. Blinds 5/10.
- Bob is dealt Q♥ Q♦. Alice is dealt A♠ K♠.
- Alice is first to act. Alice moves all-in. Bob calls. Pot is 400. Phase at the call is `PreFlop`.
- Runout: flop 2♣ 7♦ 9♠, turn J♣, river 5♥, bonus 3♣, **second bonus A♦**.
- Rules result: 6-card board, Bob’s queens win, stacks Alice 0 / Bob 400.
- Code result: 7-card board, Alice’s aces win, stacks Alice 400 / Bob 0, once a showdown endpoint runs.

### Why the existing unit test does not catch it

`Handle_WhenRedRiverRunoutDealsRiverAndBonusInSameUnitOfWork_UsesDistinctDeckCards` seeds **two** deck cards. The first bonus consumes the second card. The duplicate call finds an empty deck. `DealCommunityCardsForPhaseAsync` stops when `i < deckCards.Count` fails, so it deals nothing and the test passes. A live hand has 52 cards, so the duplicate call always finds another card.

---

## 2. Intended behavior

1. A Red River board at showdown has **5** community cards when the river is clubs or spades.
2. It has **6** community cards when the river is hearts or diamonds: exactly **one** row with `DealtAtPhase == "RedRiverBonus"`.
3. That bonus card is the next deck card after the river (`DealOrder` 6). No later deck card is moved to `Community` for this rule.
4. Calling the bonus rule twice in one `SaveChanges` deals one card, not two.
5. These paths stay as they are:
   - Normal street-by-street play: bonus dealt once when the river is dealt (red) or when the river round closes (the already-saved card is detected and not dealt again).
   - All-in on the river street when the bonus row is already saved: still exactly one bonus card.
   - Black river: no bonus card, on a normal close or a runout.
   - Hold’em, Omaha, and the other games that share this handler: no `RedRiverBonus` card.

`GameSettings["runoutStreets"]` is not proof. The duplicate deal happens **after** `DealRemainingCommunityCardsAsync` builds that list, so the list can contain `"RedRiverBonus"` once while two bonus rows exist. Assert on `GameCard` rows.

---

## 3. Root cause

**File:** `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`

### 3.1 The runout already deals the bonus

`DealRemainingCommunityCardsAsync` deals every remaining street by mutating tracked `GameCard`s (it does not save). For Red River it then calls the bonus method before it sets `CurrentPhase = "Showdown"`:

```861:864:src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs
		if (IsRedRiverGame(game) && await DealRedRiverBonusCommunityCardIfNeededAsync(game, now, cancellationToken))
		{
			dealtPhases.Add("RedRiverBonus");
		}
```

That call is the correct one. The river row is tracked and `Modified`, so the river lookup below finds it. No bonus row is in the database yet, so it deals one card.

### 3.2 Handle calls the bonus method again

After that method returns, `CurrentPhase` is already `"Showdown"`. Handle calls the bonus method a second time, then saves:

```206:216:src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs
				// Deal all remaining community cards and go to showdown
				await DealRemainingCommunityCardsAsync(game, game.CurrentPhase, now, cancellationToken);

				// Safety check: ensure Red River bonus-card rule is applied on all-in runouts at River.
				if (IsRedRiverGame(game) && string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase))
				{
					await DealRedRiverBonusCommunityCardIfNeededAsync(game, now, cancellationToken);
				}

				game.UpdatedAt = now;
				await context.SaveChangesAsync(cancellationToken);
```

The comment is wrong. The first call already applied the rule. This block is not limited to runouts that started on the river. `DealRemainingCommunityCardsAsync` sets `Showdown` for every non-Klondike runout, so the second call runs for every Red River all-in runout.

### 3.3 The “already dealt” check cannot see the card it just dealt

`DealRedRiverBonusCommunityCardIfNeededAsync` (starts at line 897):

- The **river** lookup is correct for a same-request deal. It reads `context.GameCards.Local` for a tracked `Added` or `Modified` river, and only then falls back to `AsNoTracking()`.
- The **bonus** lookup is only `AsNoTracking().AnyAsync(...)`. `AsNoTracking` reads the store. The bonus row is still `Location = Deck` in the store until `SaveChangesAsync`. The check returns false.
- `DealCommunityCardsForPhaseAsync` (line 746) loads `Location == Deck`, then **re-filters the tracked instances** (`deckCards.Where(gc => gc.Location == CardLocation.Deck)`). The first bonus card is no longer `Deck`, so the second call takes the next deck card and sets `DealtAtPhase = "RedRiverBonus"`, `DealOrder = 6`.

The same miss happens on the EF in-memory provider used by the unit tests and on SQL Server. Unsaved mutations are not in the store either way.

### 3.4 Other callers (do not remove these)

| Lines | When | Why it is not the bug |
|------:|------|------------------------|
| 112–115 | Start of `Handle` when phase is already `River` | Bonus from the river-deal request is already saved. `AsNoTracking` finds it. |
| 242–247 | Normal advance from `River` to `Showdown` | Same: bonus was saved when the river was dealt. |
| 333–338 | Just after dealing the river on the normal path | First deal. No bonus exists yet. Must stay. |
| 861–864 | Inside the runout | First deal of the runout bonus. Must stay. |

---

## 4. Required code change

Both edits are required. Removing the second call fixes the runout. The tracked-state check makes the method safe if it is ever called twice before `SaveChangesAsync`. Doing only one of them leaves the other hole.

### 4.1 Delete the second call

In `Handle`, in the `allPlayersAllIn` branch, delete the whole “Safety check” `if` (the comment and the call at lines 209–213).

Leave the call at line 861 inside `DealRemainingCommunityCardsAsync`. Leave the `SaveChangesAsync` that follows the deleted block.

The all-in branch should be:

```csharp
if (allPlayersAllIn)
{
    logger.LogInformation(
        "All players are all-in after {Phase} for game {GameId}. Dealing remaining community cards and proceeding to showdown.",
        game.CurrentPhase, game.Id);

    await DealRemainingCommunityCardsAsync(game, game.CurrentPhase, now, cancellationToken);

    game.UpdatedAt = now;
    await context.SaveChangesAsync(cancellationToken);

    return new ProcessBettingActionSuccessful
    {
        // unchanged
    };
}
```

Do not add a new call anywhere else.

### 4.2 See an unsaved bonus card

In `DealRedRiverBonusCommunityCardIfNeededAsync`, replace the `hasBonusCard` query (lines 939–952) so a tracked bonus card counts **before** the database query.

Match this shape. `EntityState` is already available: the file imports `Microsoft.EntityFrameworkCore`, and the river lookup above already uses `EntityState.Added` and `EntityState.Modified`.

```csharp
var hasBonusCard = context.GameCards.Local.Any(gc =>
    gc.GameId == game.Id
    && gc.HandNumber == game.CurrentHandNumber
    && gc.Location == CardLocation.Community
    && !gc.IsDiscarded
    && gc.DealtAtPhase == "RedRiverBonus"
    && context.Entry(gc).State != EntityState.Deleted);

if (!hasBonusCard)
{
    hasBonusCard = await context.GameCards
        .AsNoTracking()
        .AnyAsync(
            gc => gc.GameId == game.Id
                && gc.HandNumber == game.CurrentHandNumber
                && gc.Location == CardLocation.Community
                && !gc.IsDiscarded
                && gc.DealtAtPhase == "RedRiverBonus",
            cancellationToken);
}

if (hasBonusCard)
{
    return false;
}
```

Why `Local` is first: after the card is mutated, `AsNoTracking` still returns the store row (`Location = Deck`). A store query must not be the only check.

Why not only `Added` or `Modified`: a bonus loaded earlier in this context can be `Unchanged`. Those must count too. `Deleted` must not count.

Do not change `DealCommunityCardsForPhaseAsync`. Its tracked re-filter is what makes the second deal take a different card; it is also what keeps the river and the first bonus distinct. Leave it.

Do not change the river lookup.

---

## 5. Tests

### 5.1 Fix the test that hides the bug

**File:** `src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandlerTests.cs`  
**Test:** `Handle_WhenRedRiverRunoutDealsRiverAndBonusInSameUnitOfWork_UsesDistinctDeckCards`

It seeds a red river candidate (`dealOrder` 1, hearts ace) and one bonus candidate (`dealOrder` 2, clubs king), phase `Turn`, second player already all-in, actor `AllIn`.

Add a third deck card **before** `Handle`, with a higher `DealOrder` than the other two, for example diamonds ace via the existing `CreateDeckCard`.

After `Handle`, keep the current assertions (phase `Showdown`, exactly 2 community cards, distinct ids, river is the hearts ace, bonus is the clubs king). Add:

- Exactly one community card with `DealtAtPhase == "RedRiverBonus"`.
- The third card is still `Location == CardLocation.Deck`.
- No community card has that third card’s `Id`.

If the third card is omitted, this test still passes on the broken code.

### 5.2 Add these unit tests in the same class

Use `SeedGameAsync`, `CreateDeckCard`, and `CreateSut` already in that file. In-memory `CardsDbContext` is enough. Do not add a new test project.

1. **Black river runout does not deal a bonus and does not consume later deck cards.**  
   Same turn all-in seed. Three deck cards: spade (this becomes the river), then two more. Assert phase `Showdown`, exactly one community card, `DealtAtPhase == "River"`, suit spades, and the other two still `Location == Deck`.

2. **Preflop runout deals five board cards plus one bonus.**  
   `currentPhase: "PreFlop"`, same all-in shape (`secondPlayerAllIn: true`, actor stack > 0, `BettingActionType.AllIn`). Seed **seven** deck cards in `DealOrder` order. The fifth (index 4) must be red; that is the river. Assert six community cards, phases `Flop`×3, `Turn`×1, `River`×1, `RedRiverBonus`×1, the bonus is the sixth seeded card, and the seventh is still `Deck`.

3. **River-street all-in does not deal a second bonus when one is already saved.**  
   `currentPhase: "River"`, `gameTypeCode: "REDRIVER"`, same all-in shape. Seed a saved river (`Location = Community`, `DealtAtPhase = "River"`, `DealOrder = 5`, hearts) and a saved bonus (`Location = Community`, `DealtAtPhase = "RedRiverBonus"`, `DealOrder = 6`), plus one `Deck` card. `CreateDeckCard` builds a deck card; set `Location`, `DealtAtPhase`, and `DealOrder` on the two community cards before `SaveChangesAsync` / `Handle`. Assert still exactly two community cards, exactly one `RedRiverBonus`, and the spare card still `Deck`.

`CreateDeckCard` sets `DealtAtPhase = nameof(Phases.Dealing)` and `Location = Deck`. Community seeds must overwrite both.

### 5.3 Do not weaken these integration tests

**File:** `src/Tests/CardGames.IntegrationTests/Games/HoldEm/RedRiverHandLifecycleTests.cs`

- `RiverRound_WhenRiverIsRed_DealsBonusCommunityCardBeforeShowdown` — 6 community cards, one bonus. Normal path, not the bug.
- `RiverRound_WhenRiverIsBlack_DoesNotDealBonusCommunityCard` — 5 cards, no bonus.
- `RiverAllInRunout_WithControlledDeck_DealsDeterministicBonusCardWhenRiverIsRed` — all-in **on the river**, 6 cards, one bonus. This path was already correct. It must stay correct.

Run them. Do not change them unless a required edit makes a setup assumption false. It should not.

### 5.4 Commands

From the repo root:

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~ProcessBettingActionCommandHandlerTests"
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~RedRiverHandLifecycleTests"
```

The new unit test with three deck cards must fail on the current code (two bonus rows, or the third card moved to `Community`) and pass after the fix.

---

## 6. Doc correction (this file only, two sentences)

`docs/Auditing/fix-red-river-showdown-stall.md` says the bonus logic is fine. That is false for runouts. Update only these claims; do not implement that plan.

- Scope line (line 5): delete “Server-side betting/bonus-card logic is largely correct; the primary defect is **missing client routing**.” Replace with: “The primary defect is **missing client routing**. All-in runouts can also deal a second bonus card; that defect is `docs/Auditing/fix-red-river-double-bonus.md` and is out of scope here.”
- Section “What is **not** broken” (line 40): delete the bullet “Red River bonus card after red river suit (`DealRedRiverBonusCommunityCardIfNeededAsync`).”

The showdown stall is a separate bug. This plan does not add a Red River branch to `TablePlay.TryLoadShowdownAsync`.

---

## 7. Out of scope

- Client showdown routing and pot award (`fix-red-river-showdown-stall.md`).
- Irish Hold’em, Phil’s Mom, and Crazy Pineapple skipping discards on a preflop all-in. Same `allPlayersAllIn` branch, different bug. Do not add a discard step here.
- Burn cards. No live path burns a card.
- Moving Red River onto another handler, or splitting `DealRemainingCommunityCardsAsync`.
- Changing `HoldemHand` or showdown so it ignores a seventh card. The seventh card must not be dealt.

---

## 8. Done when

- A turn, flop, or preflop all-in runout with a red river saves **one** `RedRiverBonus` row, and the next deck card stays `Deck`.
- A black river runout saves no bonus row.
- A river-street all-in with a bonus already saved still has one bonus row.
- Normal red-river and black-river closes still have 6 and 5 community cards.
- The unit test seeds at least one spare deck card, so an empty deck cannot hide a second deal.
- No other game type gains a `RedRiverBonus` card.
