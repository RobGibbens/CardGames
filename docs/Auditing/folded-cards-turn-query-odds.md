# Fix folded cards inside the turn-query odds

An LLM implements this plan. The odds calculator may receive only cards the caller can already see are out of play. Leave the probability payload, the caller's own hand, and the Blazor odds display as they are.

## Outcome

`HandOdds` on `GetCurrentPlayerTurn` is still a list of hand-type probabilities. It never includes a rank or a suit.

When the caller is the player to act, the cards passed as dead cards are the current hand's non-discarded cards that belong to a folded player and have `IsVisible == true`. A folded hole card stays out of that list. A card from another hand stays out. A card belonging to a player who is still in the hand stays out.

When the caller is not the player to act, `HandOdds` stays null. `MapHand` already returns another player's card only when `IsVisible` is true. Leave `MapHand` as it is.

## The hand that fails today

Seven Card Stud, hand 2. Alice is the player to act. Bob has folded. Carol is still in the hand.

| Card | Owner | Hand | `IsVisible` | `IsDiscarded` | Dead-card input today | Dead-card input after the fix |
| --- | --- | --- | --- | --- | --- | --- |
| 9♣, Bob's upcard | Bob, folded | 2 | true | false | included | included |
| 3♥, Bob's hole card | Bob, folded | 2 | false | false | included | excluded |
| A♠, Bob's other hole card | Bob, folded | 2 | false | false | included | excluded |
| K♦, Bob's upcard from the previous hand | Bob, folded | 1 | true | false | included | excluded |
| T♦, Carol's upcard | Carol, still in | 2 | true | false | excluded | excluded |

Alice calls the Seven Card Stud current-turn endpoint. `callerIsActor` is true, so the handler loads every non-discarded card of every folded player and passes that list to `OddsCalculator.CalculateStudOdds`. Bob's 3♥ and A♠ are removed from the simulated deck. Alice's probabilities move as if she knows those two hole cards. The JSON still contains only probabilities, so the identities are not printed, but they decide the numbers.

`TablePlay.CalculateHandOdds` already builds dead cards from folded seats whose cards are face up. The Blazor page does not call `GetCurrentPlayerTurn`. The API is the leak. Match the table's folded-and-face-up rule. Do not start treating a live opponent's upcard as dead in this change.

## Where the query is copied

Each of these handlers, inside `if (callerIsActor)`, loads dead cards with the same predicate: `GamePlayer.GameId` matches, `GamePlayer.HasFolded`, and `!IsDiscarded`. None of them require `IsVisible` or the current hand number.

- `src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/Baseball/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/TwosJacksManWithTheAxe/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`

Pair Pressure and Tollbooth forward to the Seven Card Stud handler. They pick up this fix when the stud handler changes. Do not edit those two handlers.

Razz uses the stud handler when `Game.GameType.Code` is `RAZZ`. The same dead-card list is passed to `CalculateRazzOdds`. One stud fix covers Razz.

Each handler then calls its own `HandOddsCalculationService`. Leave those calls, including the Razz boolean, as they are. The services pass the list through to `OddsCalculator`. Do not change `OddsCalculator`.

## Step 1. One loader for known dead cards

Add `src/CardGames.Poker.Api/Services/KnownDeadCards.cs`:

```csharp
internal static class KnownDeadCards
{
    internal static Task<List<GameCard>> LoadAsync(
        CardsDbContext context,
        Guid gameId,
        int handNumber,
        CancellationToken cancellationToken)
    {
        return context.GameCards
            .Where(card => card.GamePlayer.GameId == gameId
                           && card.HandNumber == handNumber
                           && card.GamePlayer.HasFolded
                           && card.IsVisible
                           && !card.IsDiscarded)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
```

Use the `GamePlayer.GameId` navigation the handlers already use, so a card with a null `GamePlayerId` stays out. `HandNumber` keeps a previous hand's cards out after the deck is reshuffled. `IsVisible` keeps a folded hole card out and keeps a folded stud upcard in. `IsDiscarded` keeps a mucked card out.

`CardGames.Poker.Api` already exposes internals to `CardGames.Poker.Tests`.

Completion: a unit test can force each row of the table above without calling `OddsCalculator`.

## Step 2. Call the loader from each of the five handlers

Replace the `deadCards` query in each of the five handlers with:

```csharp
var deadCards = await KnownDeadCards.LoadAsync(
    context,
    request.GameId,
    game.CurrentHandNumber,
    cancellationToken);
```

Leave the following line that passes `deadCards` into `CalculateStudOdds`, `CalculateBaseballOdds`, or `CalculateDrawOdds`. The stud call still passes the Razz boolean.

In each `HandOddsCalculationService` next to those handlers, change the `deadCards` parameter comment that says folded players' cards are known to be out of play. State that the list is the current hand's face-up, non-discarded cards of folded players. Baseball's service has no such sentence; leave it.

Completion: each of the five handlers contains `KnownDeadCards.LoadAsync`. None of those five files still contains `GamePlayer.HasFolded` inside the odds query.

## Step 3. Tests

Add `src/Tests/CardGames.Poker.Tests/Api/Services/KnownDeadCardsTests.cs`. Use an in-memory `CardsDbContext` the same way `GetCurrentPlayerTurnQueryHandlerTests.CreateContext` does in the Seven Card Stud test project.

Seed hand number 2 as the game's `CurrentHandNumber`, with the five cards in the table:

- Bob `HasFolded = true`. 9♣ on hand 2, `IsVisible = true`, `IsDiscarded = false`, `Location = Board` or `Hole` (the loader ignores location). 3♥ and A♠ on hand 2, `IsVisible = false`. K♦ on hand 1, `IsVisible = true`.
- Carol `HasFolded = false`. T♦ on hand 2, `IsVisible = true`.
- Alice `HasFolded = false`. Any hole card on hand 2, `IsVisible = false`. She is not part of the assertion except to prove an active player's card is absent.

Call `KnownDeadCards.LoadAsync` with the game id and hand number 2. The result contains one card, the 9♣. It does not contain the 3♥, the A♠, the K♦, the T♦, or Alice's card.

Add a discarded upcard for Bob on hand 2 (`IsVisible = true`, `IsDiscarded = true`) in the same seed. It is absent from the result.

Do not assert probability values. `OddsCalculator` shuffles with an unseeded `Random`, so a comparison of `HandOdds` percentages is not a stable test.

Run the existing turn-query tests as well. They already expect `HandOdds` to be non-null for the actor and null for everyone else, and they seed folded hole cards with `IsVisible = false`. Those cards stop affecting the calculation; the null / non-null assertions still hold.

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~KnownDeadCardsTests|FullyQualifiedName~GetCurrentPlayerTurnQueryHandlerTests"
```

Completion: that filter passes.

## Out of scope

- `MapHand` and the caller's own hidden cards on `Player.Hand`.
- `TablePlay.CalculateHandOdds`. It already uses folded face-up cards only.
- Live opponents' face-up cards. The table does not count them as dead, and this plan does not add them.
- `OddsCalculator`, simulation count, and the choice of stud, Razz, baseball, or draw math.
- Pair Pressure and Tollbooth handler bodies. They delegate to Seven Card Stud.
- Showing folded hole cards at showdown. That is a different path.

## Done

A folded player's hidden cards, and every card from a previous hand, are absent from the dead-card list that `GetCurrentPlayerTurn` passes to the odds calculator. A folded player's current face-up card is still in that list. The response still contains probabilities only.
