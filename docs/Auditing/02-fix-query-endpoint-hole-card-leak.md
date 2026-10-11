# Fix: query endpoints return other players' hole cards

## Defect

Three queries publish hole cards.

1. `GET /api/v1/games/{gameId}/players` has no `RequireAuthorization`. `GetGamePlayersMapper.MapCards` returns `Suit` and `Symbol` for every player's current-hand cards that are not discarded. `TablePlay.GetPlayersAndUpdateSeatsAsync` calls this when the table loads and copies every hand into seat state.

2. `GET /api/v1/games/{gameId}/draw-player` has no `RequireAuthorization`. `GetCurrentDrawPlayerMapper.MapCards` returns every non-discarded card of the player who must draw, with no `HandNumber` filter. `TablePlay` never calls it. `_currentDrawPlayer` is only read for a name, and nothing assigns it.

3. `GET /api/v1/games/{variant}/{gameId}/current-turn` already has `RequireAuthorization` on the variant group. Any authenticated caller receives the current actor's full hand. The handler also loads folded players' non-discarded cards and passes them to the odds calculator. The live web does not call this route. Pair Pressure and Tollbooth delegate to the Seven Card Stud handler. Five Card Draw, Baseball, Follow the Queen, and Twos Jacks Man with the Axe each have their own copy.

A response may contain the caller's own hidden cards, plus cards that are already face up. It must not contain another player's hidden cards. Odds computed from folded hidden cards are the same leak and go only to the actor.

## Do not change

- Do not add a fallback authorization policy in `Program.cs`.
- Do not call `RequireAuthorization` on the Common `mapGroup` in `Features/Games/Common/v1/V1.cs`. That group also maps `GetGames`, `GetGame`, `GetGameRules`, `GetHandHistory`, `GetRabbitHunt`, and `GetTableSettings`. Leave those routes as they are.
- Do not change `TablePlay.razor`. After this fix, `player.Hand` on the players response contains only face-up cards. Hole cards for the seated player still arrive on `PrivateStateUpdated`. Face-up cards for other seats still arrive on `TableStateUpdated`.
- Do not change `TableStateBuilder`, SignalR, or `HandVisibilityHelper`.
- Do not change Kings and Lows `StartHand` or `DrawCards` setting `IsVisible = true`. That is a separate defect. This plan must not rely on that flag for the draw-player response.
- Do not edit `Archive/CardGames.Poker.CLI`. It calls `GetCurrentDrawPlayerAsync` and `FiveCardDrawGetCurrentPlayerTurnAsync`. Those calls will no longer receive hidden cards unless the caller is the actor. Leave the archive alone.
- Do not regenerate Refit. `Hand` stays on the response records. Return an empty list, not a removed property.
- Do not put the caller's hidden cards on `GET .../players` or `GET .../draw-player`. Those responses are cached per game and shared. The owner's hidden cards stay on private state.

## 1. Players response: only face-up cards, and require login

### Authorization

File: `src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetGamePlayers/GetGamePlayersEndpoint.cs`

On the `MapGet("{gameId:guid}/players", ...)` chain, after `.MapToApiVersion(1.0)`, add `.RequireAuthorization()` with no policy name. That uses the default policy, which requires an authenticated user. The Blazor table is `[Authorize]`, and `IGamesApi` sends `X-Internal-User-Token` through `AuthenticationStateHandler`. Do not require the caller to be seated. A logged-in player who has not sat down still needs the roster.

### Cards

File: `src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetGamePlayers/GetGamePlayersMapper.cs`

In `MapCards`, keep the current-hand and not-discarded filters, and require `c.IsVisible`:

```csharp
.Where(c => !c.IsDiscarded && c.HandNumber == currentHandNumber && c.IsVisible)
```

`IsVisible == false` cards are omitted for every player, including the caller. Stud upcards (`Location = Board`, `IsVisible = true`) stay. Hole cards (`IsVisible = false`) do not.

### Cache

File: `src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetGamePlayers/GetGamePlayersQuery.cs`

`HybridCache` stores the mapped response under `CacheKey`. Entries written before this change still contain hole cards. Change the key so those entries are not reused:

```csharp
public string CacheKey =>
    $"{Feature.Name}:{Feature.Version}:{nameof(GetGamePlayersQuery)}:{GameId}:visible-cards-only";
```

Leave the handler's `GetOrCreateAsync` call in place. The new value is the same for every caller, so a shared game key is safe after the suffix change.

## 2. Draw-player response: no cards, and require login

### Authorization

File: `src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetCurrentDrawPlayer/GetCurrentDrawPlayerEndpoint.cs`

On the `MapGet("{gameId:guid}/draw-player", ...)` chain, after `.MapToApiVersion(1.0)`, add `.RequireAuthorization()` with no policy name.

### Cards

File: `src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetCurrentDrawPlayer/GetCurrentDrawPlayerMapper.cs`

`MapCards` must return an empty list and must not read `Suit` or `Symbol`:

```csharp
private static IReadOnlyList<DealtCard> MapCards(ICollection<GameCard> cards)
{
    return [];
}
```

Do not filter on `IsVisible`. Kings and Lows writes `IsVisible = true` on hole cards, and this route would then publish them. The route identifies who is drawing. Name, seat, stack, and flags stay. `Hand` is empty for every caller, including the player who is drawing.

Keep the `[MapProperty]` from `Cards` to `Hand`. Mapperly still needs `MapCards`.

### Cache

File: `src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetCurrentDrawPlayer/GetCurrentDrawPlayerQuery.cs`

Change `CacheKey` so a cached hand is not served:

```csharp
public string CacheKey =>
    $"{Feature.Name}:{Feature.Version}:{nameof(GetCurrentDrawPlayerQuery)}:{GameId}:no-cards";
```

## 3. Current-turn: hidden cards and odds only for the actor

Apply the same behavior in each handler that builds the response. Do not edit only the Stud handler.

| Handler | Notes |
| --- | --- |
| `SevenCardStud/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs` | Also serves Pair Pressure and Tollbooth. Those two handlers only forward to this one. |
| `FiveCardDraw/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs` | Uses `CalculateDrawOdds`. |
| `Baseball/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs` | Own copy. |
| `FollowTheQueen/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs` | Own copy. |
| `TwosJacksManWithTheAxe/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs` | Own copy. |

Leave the variant `V1.cs` files alone. They already call `.RequireAuthorization()` on the group.

### Remove the shared cache

Each of those five handlers wraps the whole response in `hybridCache.GetOrCreateAsync` with a key of game id only. The first caller would be stored and replayed to everyone else. Remove the `GetOrCreateAsync` wrapper and the `HybridCache` constructor parameter from these five handlers. Query the database directly, as the body of the factory already does. Do not add a new cache key. A boolean such as "caller is actor" is wrong: after the turn moves, that entry still holds the previous actor's cards.

Pair Pressure and Tollbooth construct `new SharedGetCurrentPlayerTurnQueryHandler(context, hybridCache)`. Update those two constructors and their unit-test helpers to match the Stud constructor.

### Who the actor is

Inject `IGamePlayerResolver` (`src/CardGames.Poker.Api/Services/IGamePlayerResolver.cs`). It is already registered in `Program.cs`.

After the current `gamePlayer` is loaded:

```csharp
var caller = await gamePlayerResolver.ResolveSeatedPlayerAsync(request.GameId, cancellationToken);
var callerIsActor = caller is not null && caller.Id == gamePlayer.Id;
```

`ResolveSeatedPlayerAsync` returns null when the caller is not authenticated or is not seated. Both cases are `callerIsActor == false`.

### Hand

`ToCurrentPlayerResponse` currently maps every non-discarded card, including other hands. `CurrentPlayerResponse` is a record, so replace `Hand` after mapping.

When `callerIsActor` is true, `Hand` is the actor's cards for `game.CurrentHandNumber` that are not discarded, hidden and face up. Build that list in the handler. Do not trust `MapCards` alone, because it has no hand-number filter.

When `callerIsActor` is false, `Hand` is only cards where `!IsDiscarded && HandNumber == game.CurrentHandNumber && IsVisible`. Stud upcards stay. Hole cards do not. Set:

```csharp
playerResponse = playerResponse with { Hand = visibleOrOwnCards };
```

Use the `DealtCard` type that the mapper already uses (`Suit`, `Symbol`, `DealOrder`).

### Odds

The dead-card query is the leak:

```csharp
var deadCards = await context.GameCards
    .Where(gc => gc.GamePlayer.GameId == request.GameId
                 && gc.GamePlayer.HasFolded
                 && !gc.IsDiscarded)
```

Run that query, and call `CalculateStudOdds` or `CalculateDrawOdds`, only when `callerIsActor` is true. Otherwise pass `handOdds: null` and do not query folded cards.

`AvailableActions` does not contain cards. Keep calculating it for every authenticated caller.

## Tests

### Anonymous HTTP

Add two facts to `src/Tests/CardGames.IntegrationTests/Api/TrustedIdentityAuthTests.cs`. That class already uses `StrictIdentityApiFactory` and an unauthenticated `HttpClient`.

- `GET api/v1/games/{Guid.NewGuid()}/players` returns 401.
- `GET api/v1/games/{Guid.NewGuid()}/draw-player` returns 401.

No seeded game is required. Authorization runs before the handler. Also send the forged headers used by `PostBettingAction_WithForgedEmailHeader_Returns401` (`X-User-Email` and `X-User-Authenticated`) and assert 401 for both routes.

### Players and draw-player mapping

Add handler tests next to the handlers, or in `CardGames.Poker.Tests` under the matching feature folder. Seed two seated players in one game, hand number 1. Give each player one `IsVisible = false` hole card and, for the players query, one `IsVisible = true` board card. Give one player an extra hole card with `HandNumber = 0`.

- `GetGamePlayersQueryHandler` returns both players. Each `Hand` contains the face-up card only. The JSON of the response does not contain the hidden card's symbol.
- `GetCurrentDrawPlayerQueryHandler`, with `CurrentPhase = "DrawPhase"` and `CurrentDrawPlayerIndex` set to that seat, returns `Hand` empty. The hidden card's symbol is absent. Name and seat are still set.

### Current turn

Update every existing `GetCurrentPlayerTurnQueryHandler` test that constructs `new GetCurrentPlayerTurnQueryHandler(context, hybridCache)`. Pass an `IGamePlayerResolver` that returns null. Those tests currently expect `HandOdds` not null (`Handle_ConcurrentRequests_ReturnConsistentResults` in the Stud, Five Card Draw, Baseball, Follow the Queen, and Twos Jacks tests). With no seated caller, `HandOdds` is null and `Hand` contains only `IsVisible` cards. Available actions and the player name stay.

Add these two facts on the Stud handler. The other four handlers share the same rule; one Stud pair is enough if the other handlers are the same code shape. If a handler diverges, add the pair there too.

Seed Alice at seat 0 as the current actor, with a hole card `IsVisible = false` (king of spades) and a board card `IsVisible = true` (ten of hearts), both `HandNumber` equal to `game.CurrentHandNumber`. Seed Bob folded, with a hidden card (ace of clubs).

1. Resolver returns Alice's `GamePlayer`. Response `Hand` contains both the king and the ten. `HandOdds` is not null.
2. Resolver returns Bob's `GamePlayer`, or returns null. Response `Hand` contains the ten only. The king and the ace are absent. `HandOdds` is null.

A fake resolver is enough. Do not spin up authentication:

```csharp
private sealed class FakeGamePlayerResolver(GamePlayer? seated) : IGamePlayerResolver
{
    public Task<GamePlayer?> ResolveSeatedPlayerAsync(Guid gameId, CancellationToken cancellationToken)
        => Task.FromResult(seated);

    public Task<GamePlayer?> ResolveSeatedPlayerAsync(Guid gameId, ClaimsPrincipal user,
        CancellationToken cancellationToken)
        => Task.FromResult(seated);

    public Task<bool> IsTableHostAsync(Guid gameId, CancellationToken cancellationToken)
        => Task.FromResult(false);
}
```

## Verify

From `src/Tests`:

```bash
dotnet test CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~GetCurrentPlayerTurnQueryHandlerTests|FullyQualifiedName~GetGamePlayers|FullyQualifiedName~GetCurrentDrawPlayer"
dotnet test CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~TrustedIdentityAuthTests"
```

Confirm `GET .../players` and `GET .../draw-player` are the only Common queries that gained `RequireAuthorization`. `rg "RequireAuthorization" Features/Games/Common/v1` should show the mutations group plus these two `MapGet` chains.
