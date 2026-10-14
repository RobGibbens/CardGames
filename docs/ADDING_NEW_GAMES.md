# Adding a New Game

Canonical playbook for a new poker variant. Discovery is automatic for **metadata** and **flow handlers**. **Behavior** is still wired explicitly through MediatR feature folders, duplicated or generic command handlers, `TableStateBuilder`, and `TablePlay` switches. `GameRules` drive some UI (phases, action lists) and do not drive every table action.

The architecture split is in [ARCHITECTURE.md](ARCHITECTURE.md#architecture-truth-table).

## 1. Quick orientation

The codebase mixes two patterns:

- Metadata-driven discovery for game registration (domain game class + `[PokerGameMetadata]` + `GetGameRules()`).
- Explicit wiring in API and web orchestration for behavior (betting, draw, street progression, start/deal/showdown routing).

A metadata-only change is enough for the lobby list and `GET /api/v1/games/rules/{gameTypeCode}`. A playable table also needs a flow handler and, when the hand sequence differs from an existing family, a `TablePlay.razor` branch.

## 2. Minimal path

Use this when the variant is a tweak of an existing flow family (Hold'em-derivative, draw-derivative, stud-derivative).

1. **Domain game and rules**
   - Add `src/CardGames.Poker/Games/<NewGame>/<NewGame>Game.cs` implementing `IPokerGame` with `[PokerGameMetadata(...)]`.
   - Add a parameterless constructor (or a public constructor the scanner can call with defaults) so `PokerGameRulesRegistry` can instantiate it.
   - Add `GetGameRules()` returning `<NewGame>Rules.CreateGameRules()`.
   - Add `src/CardGames.Poker/Games/<NewGame>/<NewGame>Rules.cs` with phases, dealing, betting, drawing, and showdown config.
   - The `code` argument and `GameRules.GameTypeCode` must match exactly (all-caps convention).
   - Phase IDs must be the strings handlers and the database store (`ThirdStreet`, `PreFlop`, `DrawPhase`, and so on). Copy them from a sibling `*Rules.cs` in the same family.

2. **Optional code constant**
   - `PokerGameMetadataRegistry` discovers the attribute by reflection. You do not edit a rules dictionary.
   - Add a `public const string` on `PokerGameMetadataRegistry` only when API or web conditionals need a shared name (the existing `PhilsMomCode` pattern).

3. **Flow handler**
   - Add `src/CardGames.Poker.Api/GameFlow/<NewGame>FlowHandler.cs` implementing `IGameFlowHandler`, usually by subclassing `BaseGameFlowHandler`.
   - Set `GameTypeCode` and override only what differs (blinds, initial phase, draw timeout).
   - `GameFlowHandlerFactory` scans the API assembly and registers the handler. There is no factory dictionary to edit.
   - The handler needs a constructor `Activator.CreateInstance` can call.

4. **Reuse command handlers when the request shape already exists**
   - Prefer an existing endpoint family and branch on game-type code.
   - Phil's Mom reuses Hold'em betting and Irish discard endpoints, with variant checks in `ProcessBettingActionCommandHandler`, `ProcessDiscardCommandHandler`, `PerformShowdownCommandHandler`, and `TableStateBuilder`.

5. **Web router and conditionals**
   - Add the code to `src/CardGames.Poker.Web/Services/IGameApiRouter.cs` for every action the variant uses.
   - Add an `Is<NewGame>` helper and a `StartGameAsync` branch in `TablePlay.razor`. An unknown code throws `NotSupportedException` (`No StartNewHand routing configured`).
   - Update draw constraints, Hold'em-style display toggles, and blind lists in create/edit/dealer-choice/table canvas when those apply.

6. **Image**
   - Add `src/CardGames.Poker.Web/wwwroot/images/games/<image>.png` and pass that file name as the attribute's `imageName`.

7. **One lifecycle test**
   - Add an integration test next to the family you reused.

If the new code is missing from a `switch` that defaults to Five Card Draw, the table will run the wrong command. If the switch throws, the hand will not start. Search for sibling game codes and add the new code beside them.

## 3. Full production path

1. Domain and metadata
   - [ ] `<NewGame>Game.cs` with the full `[PokerGameMetadata]` constructor.
   - [ ] `<NewGame>Rules.cs` with ordered phases and descriptors.
   - [ ] `GetGameRules()` returns that factory, and a unit test calls `PokerGameRulesRegistry.TryGet`.
2. Flow and streets
   - [ ] `<NewGame>FlowHandler.cs` for first phase and dealing.
   - [ ] Timer betting overrides `SendBettingActionAsync` when the game does not use the Five Card Draw betting command (see below).
   - [ ] Phase transitions in the reused handler match the new street list.
3. Command validation
   - [ ] Discard, betting normalization, and showdown inclusion are variant-aware.
   - [ ] New mutation endpoints call `RequireAuthorization()` the same way existing command groups do (`Features/Games/Common/v1/V1.cs` puts mutations on an authorized group). See `docs/Auditing/03-fix-trusted-identity-authz.md` before trusting a client-supplied player id.
4. State projection
   - [ ] `TableStateBuilder` evaluates transitional hole-card counts.
5. Web
   - [ ] `TablePlay` start, showdown, and action branches.
   - [ ] Create, edit, dealer-choice, and table canvas lists.
6. Router
   - [ ] `IGameApiRouter` betting and draw maps. Add a dedicated endpoint only when existing contracts cannot represent the action.
   - [ ] If contracts change, regenerate Refit clients with `dotnet build src/CardGames.Poker.Refitter`.
7. Tests
   - [ ] Lifecycle integration test, one invalid-action test, and a state-builder regression if the hand shape is nonstandard.

## 4. Registration

Registration is reflection. Do not add an entry to a rules dictionary.

| What | How it is found |
|------|-----------------|
| Lobby metadata | `[PokerGameMetadata]` on a concrete `IPokerGame` in `CardGames.Poker` (`PokerGameMetadataRegistry`) |
| Rules JSON | Same type, then `instance.GetGameRules()` (`PokerGameRulesRegistry`) |
| Hand setup and timer actions | `IGameFlowHandler` in the API assembly (`GameFlowHandlerFactory`) |

`GetGameRules()` on the instance is the wire. `*Rules.CreateGameRules()` is the usual implementation.

Confirm discovery with the pattern in `src/Tests/CardGames.Poker.Tests/Api/PokerGameRulesRegistryTests.cs`:

```csharp
var success = PokerGameRulesRegistry.TryGet("NEWCODE", out var rules);
success.Should().BeTrue();
rules!.GameTypeCode.Should().Be("NEWCODE");
```

Rules are served by `GetGameRulesEndpoint.MapGetGameRules` on the games group:

```http
GET /api/v1/games/rules/KINGSANDLOWS
GET /api/v1/games/{gameId}/rules
```

Mapped from `Features/Games/Common/v1/V1.cs` (`/api/v{version:apiVersion}/games`) plus `MapGet("rules/{gameTypeCode}")`. That query is not on the mutation group that calls `RequireAuthorization()`.

## 5. Metadata attribute

`PokerGameMetadataAttribute` requires every argument below. This block matches `SevenCardStudGame`:

```csharp
[PokerGameMetadata(
    "SEVENCARDSTUD",
    "Seven Card Stud",
    "A classic poker variant where players receive seven cards individually, with betting rounds after the third, fourth, fifth, and sixth cards.",
    2,                          // minimumNumberOfPlayers
    7,                          // maximumNumberOfPlayers
    2,                          // initialHoleCards
    1,                          // initialBoardCards
    0,                          // maxCommunityCards
    7,                          // maxPlayerCards
    false,                      // hasDrawPhase
    0,                          // maxDiscards
    WildCardRule.None,
    BettingStructure.AnteBringIn,
    "sevencardstud.png")]       // optional imageName
public class SevenCardStudGame : IPokerGame
{
    public SevenCardStudGame() { /* parameterless ctor so the registry can construct it */ }

    public GameRules GetGameRules() => SevenCardStudRules.CreateGameRules();
}
```

`BettingStructure` and `WildCardRule` live in `CardGames.Poker.Betting`.

## 6. Game rules

Phase `PhaseId` values are persisted and compared as strings. Use the ids already defined for that family:

- Seven Card Stud and stud-family variants: `WaitingToStart`, `CollectingAntes`, `ThirdStreet`, `FourthStreet`, `FifthStreet`, `SixthStreet`, `SeventhStreet`, `Showdown`, `Complete`.
- Hold'em (`HoldEmRules.cs`): `WaitingToStart`, `CollectingBlinds`, `Dealing`, `PreFlop`, `Flop`, `Turn`, `River`, `Showdown`, `Complete`.
- Five Card Draw (`FiveCardDrawRules.cs`): `WaitingToStart`, `CollectingAntes`, `Dealing`, `FirstBettingRound`, `DrawPhase`, `DrawComplete`, `SecondBettingRound`, `Showdown`, `Complete`.

`GamePhaseDescriptor` requires `PhaseId`, `Name`, `Description`, `Category`, and `RequiresPlayerAction`.

Guidelines:

- List phases in play order. Categories in use include `Setup`, `Dealing`, `Betting`, `Drawing`, `Decision`, `Resolution`, and `Special`.
- Set `RequiresPlayerAction` on decision phases and list `AvailableActions`.
- Mark the last phase `IsTerminal`.
- `CardDealing`, `Betting`, `Drawing`, and `Showdown` follow the sibling rules class. `SpecialRules` is the extension dictionary for flags such as `HasBringIn` or `WildCards`.

Stud `BringIn`, `SmallBet`, and `BigBet` on `Game` are not set by `CreateGameCommand` today. Rules metadata alone does not post a bring-in. See `docs/Auditing/10-fix-bring-in-never-posted.md`.

## 7. Flow handler and the action timer

`BaseGameFlowHandler.SendBettingActionAsync` sends the Five Card Draw `ProcessBettingAction` command. The action timer calls that method, not the HTTP route the player uses.

Override `SendBettingActionAsync` on any game whose betting command is not Five Card Draw. Stud-family games (`SEVENCARDSTUD`, `RAZZ`, `FOLLOWTHEQUEEN`, `PAIRPRESSURE`, `BASEBALL`, `GOODBADUGLY`, `TOLLBOOTH`) each send their own command type. Leaving the base method in place makes the timer send Five Card Draw's command, which rejects street phases and stalls the table. See `docs/Auditing/09-fix-stud-timer-auto-fold-stall.md`.

`GameFlowHandlerFactory` falls back to Five Card Draw when no handler matches the code. A missing handler is a silent wrong-game default, not a startup error.

## 8. Generic and dedicated MediatR handlers

`Features/Games/Generic/v1` maps `MapStartHand()` to the shared `StartHandCommandHandler`. Several variants also map their own `MapStartHand()` to a different handler type in the same assembly. `TablePlay` calls one or the other by branch. Both can exist during migration. Do not assume every game starts through Generic.

Showdown is the same shape: some codes call a variant `PerformShowdown`, and some call Generic. Red River must be included in the Hold'em showdown branch or the table stays in `Showdown`. See `docs/Auditing/04-fix-red-river-showdown-stall.md`.

## 9. When metadata is not enough

Use Bob Barker when the variant needs any of the following:

- A dedicated player action that does not fit an existing command, such as showcase selection before pre-flop betting.
- Variant-specific private or public state.
- Custom showdown settlement or display payloads.

Plan changes in:

- `src/CardGames.Poker.Api/Features/Games/{Variant}` and endpoint registration.
- Contracts under `src/CardGames.Contracts/`.
- `TableStateBuilder` and showdown shaping.
- `IGameApiRouter`.
- Localized UI (overlay or table section), not a broad UI rewrite.
- A `SendBettingActionAsync` override when the timer must not use the Five Card Draw command.

Tollbooth is the reference for a special phase between streets (a per-player card choice, display-only community cards, and its own endpoint family). Mapping notes are in [Tollbooth](#tollbooth-tollbooth) below.

## 10. What rules do and do not drive

| Driven by `GameRules` | Still hard-coded |
|-----------------------|------------------|
| Lobby name, player limits, image (via the attribute) | `TablePlay.StartGameAsync` API sequence |
| `GET` rules JSON | Betting command type |
| Phase label and `RequiresPlayerAction` on the table snapshot | Showdown endpoint |
| Some available-action lists | Timer `SendBettingActionAsync` target |
| | Create-game stake fields, including stud bring-in |

## 11. Orchestration matrix

Verified against `TablePlay.StartGameAsync`. Update this table when that method changes.

Dealer's Choice tables call `GenericStartHandAsync` and stop. The API moves to `WaitingForDealerChoice` instead of dealing.

| Game code | StartHand API (`TablePlay`) | Post-start path |
|-----------|-----------------------------|-----------------|
| `TWOSJACKSMANWITHTHEAXE` | `TwosJacksManWithTheAxeStartHandAsync` | Same API: `CollectAntes`, then `DealHands` |
| `PAIRPRESSURE` | `PairPressureStartHandAsync` | Same API: `CollectAntes`, then `DealHands` |
| `SEVENCARDSTUD` | `SevenCardStudStartHandAsync` | Stud API: `CollectAntes`, then `DealHands` |
| `RAZZ` | `SevenCardStudStartHandAsync` | Stud API: `CollectAntes`, then `DealHands` |
| `GOODBADUGLY` | `SevenCardStudStartHandAsync` | Stud `CollectAntes`, then `GoodBadUglyDealHandsAsync` |
| `BASEBALL` | `BaseballStartHandAsync` | Baseball `CollectAntes`, then `DealHands` |
| `FOLLOWTHEQUEEN` | `FollowTheQueenStartHandAsync` | Same API: `CollectAntes`, then `DealHands` |
| `TOLLBOOTH` | `TollboothStartHandAsync` | Same API: `CollectAntes`, then `DealHands` |
| `HOLDEM`, `REDRIVER`, `KLONDIKE` | `HoldEmStartHandAsync`, or `GenericStartHandAsync` when `IsLeagueTournament` | No separate collect/deal call from the page; blinds and dealing run inside the start handler and flow handler |
| `HOLDTHEBASEBALL` | `HoldTheBaseballStartHandAsync` | Start only |
| `OMAHA`, `NEBRASKA`, `SOUTHDAKOTA`, `BOBBARKER` | `GenericStartHandAsync` | Flow handler inside the generic start handler |
| `IRISHHOLDEM`, `PHILSMOM`, `CRAZYPINEAPPLE` | `GenericStartHandAsync` | Flow handler inside the generic start handler |
| `KINGSANDLOWS` | `KingsAndLowsStartHandAsync` | Start handler collects the ante and deals |
| `SCREWYOURNEIGHBOR` | `GenericStartHandAsync` | Generic start deals one card and moves to `KeepOrTrade` |
| `INBETWEEN` | `GenericStartHandAsync` | Generic start collects antes and moves to `InBetweenTurn` |
| `FIVECARDDRAW` | `FiveCardDrawStartHandAsync` | Same API: `CollectAntes`, then `DealHands` |
| Any other code | Throws `NotSupportedException` | Add an explicit branch |

## 12. Variant addenda

These notes record how existing variants were wired. Follow the same family when the new game is a small delta.

### Phil's Mom (`PHILSMOM`)

Irish Hold'em shape: 4 hole cards, one discard before the flop, one discard after the flop, then turn, river, and showdown. Reuses Hold'em betting and the Irish discard endpoint. Flow handler: `PhilsMomFlowHandler`. Lifecycle test: `PhilsMomHandLifecycleTests`.

### Crazy Pineapple (`CRAZYPINEAPPLE`)

Same endpoint reuse as Phil's Mom. Hold'em betting handler branches pre-flop to flop to a mandatory one-card discard. `IGameApiRouter` maps betting to Hold'em and discard to Irish.

### Red River (`REDRIVER`)

Hold'em betting. If the river card is hearts or diamonds, deal one extra community card and go to showdown with no extra betting round. `TablePlay` starts it on the Hold'em branch. `TryLoadShowdownAsync` must call the Hold'em showdown endpoint. `RedRiverApiClientWrapper` implements `PerformShowdownAsync` with `HoldEmPerformShowdownAsync`.

### South Dakota (`SOUTHDAKOTA`)

Nebraska hand shape (best 3 hole + 2 community) with a shorter board: 5 hole cards, pre-flop, flop of 2, turn of 1, no river. Include it anywhere Omaha/Nebraska is special-cased, and register evaluation with `NebraskaHandEvaluator`.

### Razz (`RAZZ`)

Reuses Seven Card Stud dealing. Action order is Razz, not stud: the highest door brings in (ace is the lowest door), the best ace-to-five board acts first on later streets, and an open pair on fourth street uses the big bet. Showdown must not build a `StudHand` inside the stud `PerformShowdown`. That endpoint delegates `RAZZ` (including a Dealer's Choice hand whose current type is `RAZZ`) to the generic showdown handler, which evaluates `RazzHand` as ace-to-five low.

### Pair Pressure (`PAIRPRESSURE`)

Stud street list with wild ranks taken from duplicate face-up ranks. Keep the stud flow-handler shape, add the wild-rank helper and `PairPressureHand`, and pass ordered face-up cards into showdown and `TableStateBuilder`.

### Tollbooth (`TOLLBOOTH`)

Stud streets with a `TollboothOffer` phase before 4th through 7th street. Each player chooses Furthest (free, lowest deal-order display card), Nearest (1× ante, highest deal-order display card), or Deck (2× ante). Two display community cards are visible and excluded from evaluation. Touchpoints: `TollboothFlowHandler`, `TollboothVariantState`, the Tollbooth `ChooseCard` endpoint, a stud betting-handler redirect into `TollboothOffer`, `TollboothOfferPrivateDto`, and `TollboothOfferOverlay.razor`.

## 13. Testing

```csharp
[Fact]
public void KingsAndLows_Rules_Should_Be_Retrievable_From_Registry()
{
    var success = PokerGameRulesRegistry.TryGet("KINGSANDLOWS", out var rules);

    Assert.True(success);
    Assert.NotNull(rules);
    Assert.Equal("Kings and Lows", rules.GameTypeName);
}
```

Registry suites already in the repo:

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~PokerGameRulesRegistry"
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~PokerGameRegistry"
```

Before a pull request:

```bash
dotnet build src/CardGames.sln
```

```bash
dotnet run --project src/CardGames.Poker.Api
dotnet run --project src/CardGames.Poker.Web
```

Check that the game appears in create/edit with the right stake fields, that a hand walks every phase, that discard limits match the API, and that showdown completes for the winner and the other seats.

`scripts/verify-doc-consistency.sh` fails if this guide, `ARCHITECTURE.md`, or `README.md` grow a manual registry-edit instruction again.

## 14. Troubleshooting

### Rules missing from `GET /api/v1/games/rules/{code}`

1. The class implements `IPokerGame`, is not abstract, and has `[PokerGameMetadata]` whose `code` matches `GameRules.GameTypeCode`.
2. `GetGameRules()` does not throw when the registry constructs the type.
3. The constructor is parameterless, or its parameters are value types and reference defaults the scanner can pass.

### Table does not start or uses the wrong deal sequence

1. `TablePlay.StartGameAsync` has a branch for the code (see the matrix above).
2. `GameFlowHandlerFactory` resolves your handler, not the Five Card Draw fallback.
3. Phase ids in `*Rules.cs` match the strings the command handler writes.

### Timer folds the wrong way or the street stalls

Override `SendBettingActionAsync` so the timer sends this variant's betting command.

### Build errors

Add `using CardGames.Poker.Games.GameFlow;` in the rules file and initialize every `required` member on `GameRules` and `GamePhaseDescriptor`.

## 15. Pre-PR checklist

- [ ] `[PokerGameMetadata]` uses the full constructor and `GetGameRules()` is discoverable.
- [ ] Flow handler exists and timer betting does not use the Five Card Draw default unless that is correct.
- [ ] `TablePlay` start and showdown branches exist.
- [ ] Router maps include the code.
- [ ] New endpoints require authorization.
- [ ] Image and create-table stake fields match the variant.
- [ ] Targeted tests pass.

## Resources

- [ARCHITECTURE.md](ARCHITECTURE.md)
- [PokerGameMetadataAttribute.cs](../src/CardGames.Poker/Games/PokerGameMetadataAttribute.cs)
- [PokerGameRulesRegistry.cs](../src/CardGames.Poker.Api/Games/PokerGameRulesRegistry.cs)
- [GameFlowHandlerFactory.cs](../src/CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs)
- [SevenCardStudGame.cs](../src/CardGames.Poker/Games/SevenCardStud/SevenCardStudGame.cs)
- [SevenCardStudRules.cs](../src/CardGames.Poker/Games/SevenCardStud/SevenCardStudRules.cs)
- [FiveCardDrawRules.cs](../src/CardGames.Poker/Games/FiveCardDraw/FiveCardDrawRules.cs)
- [KingsAndLowsRules.cs](../src/CardGames.Poker/Games/KingsAndLows/KingsAndLowsRules.cs)
