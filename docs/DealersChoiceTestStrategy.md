# Dealer's Choice — Test Strategy

> **Author:** Basher (Tester)  
> **Requested by:** Rob Gibbens  
> **Date:** 2026-03-02

---

## 1. Existing Test Patterns (Reference)

All tests follow xUnit + FluentAssertions. Key conventions observed:

| Layer | Project | Namespace Root | Example File |
|-------|---------|----------------|--------------|
| **Domain unit tests** | `CardGames.Poker.Tests` | `CardGames.Poker.Tests.Games` | `FiveCardDrawGameTests.cs`, `KingsAndLowsGameTests.cs` |
| **Integration tests** (MediatR handlers, DB) | `CardGames.IntegrationTests` | `CardGames.IntegrationTests.Features.Commands` | `CreateGameCommandHandlerTests.cs`, `KingsAndLowsCommandTests.cs` |
| **End-to-end flow tests** | `CardGames.IntegrationTests` | `CardGames.IntegrationTests.EndToEnd` | `FiveCardDrawGameFlowTests.cs`, `KingsAndLowsGameFlowTests.cs` |
| **Service tests** | `CardGames.IntegrationTests` | `CardGames.IntegrationTests.Services` | `ContinuousPlayBackgroundServiceTests.cs` |
| **Game flow handler tests** | `CardGames.IntegrationTests` | `CardGames.IntegrationTests.GameFlow` | `GameFlowHandlerTests.cs` |
| **Registry tests** | Both | — | `PokerGameRegistryTests.cs`, `PokerGamePhaseRegistryTests.cs` |

**Key conventions:**
- `IntegrationTestBase` provides `DbContext`, `Mediator`, `FlowHandlerFactory`, in-memory DB, seeded `GameType` rows.
- `DatabaseSeeder.CreateCompleteGameSetupAsync(context, gameTypeCode, playerCount)` → returns `GameSetup(Game, Players, GamePlayers)`.
- `GetFreshDbContext()` for isolated assertion reads.
- Pattern: Arrange (seed DB) → Act (send MediatR command or call service method) → Assert (FluentAssertions on DB state + return values).
- `ContinuousPlayBackgroundServiceTests` uses fakes: `FakeServiceScopeFactory`, `FakeGameFlowHandlerFactory`, `FakeGameStateBroadcaster`, `FakeHandHistoryRecorder`.
- Result types are `OneOf<TSuccess, TError>` — asserted via `result.IsT0.Should().BeTrue()` / `result.AsT0`.

---

## 2. Proposed New Test Files & Locations

| File | Namespace | Purpose |
|------|-----------|---------|
| `src/Tests/CardGames.IntegrationTests/Features/Commands/CreateDealersChoiceGameTests.cs` | `CardGames.IntegrationTests.Features.Commands` | CreateGame for DC mode (null/empty game code) |
| `src/Tests/CardGames.IntegrationTests/Features/Commands/ChooseDealerGameCommandTests.cs` | `CardGames.IntegrationTests.Features.Commands` | ChooseDealerGame command validation & happy paths |
| `src/Tests/CardGames.IntegrationTests/EndToEnd/DealersChoiceGameFlowTests.cs` | `CardGames.IntegrationTests.EndToEnd` | Full DC lifecycle: create → pick → play → showdown → next pick |
| `src/Tests/CardGames.IntegrationTests/Services/DealersChoiceContinuousPlayTests.cs` | `CardGames.IntegrationTests.Services` | ContinuousPlayBackgroundService behavior in DC mode |
| `src/Tests/CardGames.IntegrationTests/GameFlow/DealersChoiceDealerRotationTests.cs` | `CardGames.IntegrationTests.GameFlow` | DC dealer rotation logic, K&L encapsulation |
| `src/Tests/CardGames.IntegrationTests/Features/Commands/CreateGameCommandHandlerTests.cs` | (add tests to existing file) | Regression: existing single-game-type creation unchanged |

---

## 3. Unit Test Scenarios

### 3A — CreateGame with Dealer's Choice (new file: `CreateDealersChoiceGameTests.cs`)

All tests inherit `IntegrationTestBase`.

```text
Class: CreateDealersChoiceGameTests : IntegrationTestBase

[Fact] Handle_NullGameCode_CreatesDealersChoiceTable
  Arrange: CreateGameCommand with GameCode = null, valid players
  Act:     Mediator.Send(command)
  Assert:  result.IsT0 → game.IsDealersChoice == true (or game has DC GameType),
           game.CurrentPhase == "WaitingToStart",
           game.Ante == null (ante set per-hand by dealer),
           game.MinBet == null (min bet set per-hand by dealer)

[Fact] Handle_EmptyStringGameCode_CreatesDealersChoiceTable
  Arrange: CreateGameCommand with GameCode = "", valid players
  Assert:  Same as null — DC mode activated

[Fact] Handle_DealersChoice_GameTypeCreatedOrReused
  Arrange: Send DC create twice
  Assert:  Both games reference the same "DEALERSCHOICE" GameType row

[Fact] Handle_DealersChoice_SetsAnteAndMinBetToNull
  Assert:  game.Ante is null, game.MinBet is null —
           these are chosen per-hand by the DC dealer

[Fact] Handle_DealersChoice_PlayersCreatedNormally
  Assert:  All players seated, chip stacks correct — no difference from single-game
```

### 3B — ChooseDealerGame Command Validation (new file: `ChooseDealerGameCommandTests.cs`)

```text
Class: ChooseDealerGameCommandTests : IntegrationTestBase

[Fact] Handle_ValidChoice_SetsGameTypeAndAnteAndMinBet
  Arrange: DC game in "WaitingForDealerChoice" phase, dealer = Player 1
  Act:     ChooseDealerGameCommand(gameId, playerId: player1.Id,
             gameTypeCode: "FIVECARDDRAW", ante: 10, minBet: 20)
  Assert:  game.GameTypeId updated to FiveCardDraw type,
           game.Ante == 10, game.MinBet == 20,
           game.CurrentPhase == "CollectingAntes" (or first phase of chosen game)

[Fact] Handle_NonDealerPlayer_ReturnsError
  Arrange: DC game in WaitingForDealerChoice, dealer = Player 1
  Act:     ChooseDealerGameCommand(gameId, playerId: player2.Id, ...)
  Assert:  Error — "Only the current dealer may choose the game type"

[Fact] Handle_InvalidGameTypeCode_ReturnsError
  Act:     ChooseDealerGameCommand(..., gameTypeCode: "NOTAREALGAME", ...)
  Assert:  Error — unknown game code

[Fact] Handle_WrongPhase_ReturnsError
  Arrange: DC game in "FirstBettingRound"
  Act:     ChooseDealerGameCommand(...)
  Assert:  Error — game is not in WaitingForDealerChoice phase

[Theory]
[InlineData(0)]
[InlineData(-5)]
Handle_InvalidAnte_ReturnsError
  Assert:  Error — ante must be positive

[Theory]  
[InlineData(0)]
[InlineData(-1)]
Handle_InvalidMinBet_ReturnsError
  Assert:  Error — minBet must be positive

[Fact] Handle_AnteLargerThanMinChipStack_ReturnsError
  Arrange: Player with 15 chips, dealer chooses ante = 20
  Assert:  Error or warning — ante exceeds a player's stack (or auto-sit-out)

[Fact] Handle_ValidChoice_KingsAndLows_SetsMultiHandGameType
  Arrange: Dealer chooses KINGSANDLOWS
  Assert:  game.GameTypeId = K&L type, game transitions to K&L's first phase (Dealing)
```

### 3C — DC Dealer Rotation (new file: `DealersChoiceDealerRotationTests.cs`)

```text
Class: DealersChoiceDealerRotationTests : IntegrationTestBase

[Fact] AfterShowdown_DealerAdvancesToNextPlayer
  Arrange: 4-player DC game, DealerPosition = seat 0 (Player 1)
  Act:     Complete a hand (simulated showdown)
  Assert:  game.DealerPosition == seat 1 (Player 2),
           game.CurrentPhase == "WaitingForDealerChoice"

[Fact] DealerRotation_WrapsAround
  Arrange: 3-player DC game, DealerPosition = seat 2 (last seat)
  Act:     Complete a hand
  Assert:  game.DealerPosition == seat 0 (wraps to first player)

[Fact] DealerRotation_SkipsInactivePlayers
  Arrange: 4 players, seat 1 has left the table, dealer at seat 0
  Act:     Complete a hand
  Assert:  game.DealerPosition == seat 2 (skips seat 1)

[Fact] DealerRotation_SkipsSittingOutPlayers
  Arrange: 4 players, seat 1 is sitting out, dealer at seat 0
  Act:     Complete a hand  
  Assert:  game.DealerPosition == seat 2

[Fact] KingsAndLows_DCDealerDoesNotRotateDuringMultiHand
  Arrange: DC game, DcDealerPosition = seat 1 (Player 2)
           Dealer chooses Kings & Lows
           K&L internal dealer rotates per sub-hand
  Assert:  After K&L completes all sub-hands,
           DC dealer position still tracks seat 1 as the one who "chose" K&L,
           then NEXT DC dealer = seat 2

[Fact] KingsAndLows_AfterCompletion_DCDealerGoesToNextPlayer
  Arrange: DC DcDealerPosition = seat 1, K&L plays 3 sub-hands with
           internal dealer rotating among K&L players
  Act:     K&L concludes → game returns to DC mode  
  Assert:  DcDealerPosition = seat 2 (Player 3),
           NOT whoever was dealing in K&L's internal rotation

[Fact] KingsAndLows_InternalDealerRotation_DoesNotAffectDcDealerField
  Arrange: DC game, DcDealerPosition = seat 1
  Act:     During K&L, sub-hand 1 dealer = seat 0, sub-hand 2 dealer = seat 1...
  Assert:  game.DcDealerPosition stays at seat 1 throughout

[Fact] MultipleDCRounds_DealerRotatesCorrectly
  Arrange: 3-player DC game
  Act:     Hand 1 (Player 1 picks FCD) → Hand 2 (Player 2 picks 7CS) → Hand 3 (Player 3 picks K&L)
  Assert:  DC dealer cycles: seat 0 → seat 1 → seat 2 → seat 0
```

---

## 4. Integration Test Scenarios

### 4A — Full DC Lifecycle (new file: `DealersChoiceGameFlowTests.cs`)

```text
Class: DealersChoiceGameFlowTests : IntegrationTestBase

[Fact] FullLifecycle_CreateTable_PickGame_PlayHand_NextDealer
  1. CreateGameCommand(GameCode: null) → DC game created
  2. StartHand → phase = "WaitingForDealerChoice"
  3. ChooseDealerGameCommand(gameTypeCode: "FIVECARDDRAW", ante: 10, minBet: 20)
     → phase = "CollectingAntes"
  4. Simulate: CollectAntes → Deal → BettingRounds → Showdown → Complete
  5. After Complete → DealerPosition advances
  6. NextHandStartsAt triggers → phase = "WaitingForDealerChoice" (new dealer)
  Assert at each step: correct phase, correct dealer, correct game type

[Fact] GameTypeSwitching_RulesChangeCorrectly
  1. Hand 1: Dealer picks FIVECARDDRAW → game has draw phase, 5 hole cards
  2. Hand 2: Dealer picks SEVENCARDSTUD → game has street-based dealing, 2 hole + 1 up
  3. Hand 3: Dealer picks HOLDEM → game has community cards, 2 hole
  Assert: After each ChooseDealerGame, the game's effective metadata matches chosen type

[Fact] MultipleHands_AnteAndMinBetChangePerHand
  Hand 1: ante=10, minBet=20
  Hand 2: ante=25, minBet=50
  Assert: game.Ante and game.MinBet reflect dealer's choice for that hand

[Fact] KingsAndLows_FullDCRoundtrip
  1. Player 2 is DC dealer, picks K&L
  2. K&L plays through all sub-hands (drop/stay → draw → showdown per sub-hand)
  3. K&L concludes → DC mode resumes
  4. DC dealer advances to Player 3
  Assert: Player 3 is now in WaitingForDealerChoice, game type reset for next choice

[Fact] StartHand_InDCMode_TransitionsToWaitingForDealerChoice
  Arrange: DC game in "Complete" phase with NextHandStartsAt expired
  Act:     ContinuousPlayBackgroundService.ProcessGamesReadyForNextHandAsync
  Assert:  game.CurrentPhase == "WaitingForDealerChoice",
           game does NOT auto-start CollectingAntes
```

### 4B — ContinuousPlayBackgroundService in DC Mode (new file: `DealersChoiceContinuousPlayTests.cs`)

```text
Class: DealersChoiceContinuousPlayTests : IDisposable
  (mirrors existing ContinuousPlayBackgroundServiceTests pattern with fakes)

[Fact] ReadyForNextHand_DCGame_TransitionsToWaitingForDealerChoice
  Arrange: DC game, phase = "Complete", NextHandStartsAt elapsed
  Act:     ProcessGamesReadyForNextHandAsync
  Assert:  phase = "WaitingForDealerChoice", NOT "CollectingAntes"

[Fact] ReadyForNextHand_DCGame_MovesDealerBeforeWaiting
  Arrange: DC game, dealer at seat 0
  Act:     ProcessGamesReadyForNextHandAsync
  Assert:  DealerPosition advanced to seat 1,
           then phase = "WaitingForDealerChoice"

[Fact] ReadyForNextHand_NonDCGame_StillAutoStartsNormally
  Arrange: Standard FiveCardDraw game, phase = "Complete"
  Act:     ProcessGamesReadyForNextHandAsync
  Assert:  Phase transitions to "CollectingAntes" (unchanged behavior)

[Fact] DCGame_DoesNotAutoStartUntilDealerChooses
  Arrange: DC game in WaitingForDealerChoice, NextHandStartsAt elapsed
  Act:     ProcessGamesReadyForNextHandAsync (runs multiple times)
  Assert:  Phase stays "WaitingForDealerChoice" — service doesn't force-start

[Fact] DCGame_KingsAndLows_InProgress_ServiceDoesNotInterfere
  Arrange: DC game, K&L in progress (sub-hand active, phase = "DropOrStay")
  Act:     ProcessGamesReadyForNextHandAsync
  Assert:  Game is NOT touched — K&L sub-hand logic runs independently
```

---

## 5. Edge Cases

### 5A — Dealer Disconnection / Timeout

```text
[Fact] DealerDisconnects_DuringChoicePhase_GamePauses
  Arrange: DC game in WaitingForDealerChoice, dealer disconnects (IsConnected = false)
  Assert:  Game stays in WaitingForDealerChoice (no auto-advance)

[Fact] DealerDisconnects_Timeout_AdvancesToNextDealer
  (if timeout implemented)
  Arrange: DC game in WaitingForDealerChoice, dealer disconnected,
           DealerChoiceTimeoutAt expired
  Act:     Background service processes timeout
  Assert:  DealerPosition advances to next player,
           phase stays "WaitingForDealerChoice" for new dealer

[Fact] AllPlayersDisconnect_DuringChoicePhase_GameMarkedAbandoned
  Arrange: All players disconnected during WaitingForDealerChoice
  Act:     ProcessGamesReadyForNextHandAsync
  Assert:  game.Status = Completed (abandoned)
```

### 5B — Player Count Edge Cases

```text
[Fact] OnlyOnePlayerLeft_DCRotation_GameEnds
  Arrange: DC game, 2 players, one leaves after showdown,
           only 1 player remains
  Assert:  Game transitions to Completed, not WaitingForDealerChoice

[Fact] PlayerJoinsBetweenHands_DealerRotationUnaffected
  Arrange: 3-player DC game, new player joins at seat 3 between hands
  Assert:  DC dealer rotation continues: seat 0 → seat 1 → seat 2 →
           seat 3 (now included) → seat 0

[Fact] PlayerLeavesBetweenHands_DealerRotationSkipsThem
  Arrange: 4-player DC game, dealer = seat 0, Player at seat 1 leaves
  Assert:  After showdown, DC dealer = seat 2 (skips seat 1)

[Fact] CurrentDealerLeaves_NextDealerGetsChoice
  Arrange: DC game, dealer = seat 1, seat 1 player leaves during WaitingForDealerChoice
  Assert:  DC dealer advances to seat 2, WaitingForDealerChoice continues
           for new dealer
```

### 5C — Kings & Lows Encapsulation Edge Cases

```text
[Fact] KingsAndLows_PlayerDropsMidway_DCDealerPositionUnaffected
  Arrange: DC dealer = seat 1, K&L in progress, player at seat 1 drops from K&L sub-hand
  Assert:  DC knows next dealer is seat 2 regardless of K&L outcome

[Fact] KingsAndLows_AllPlayersDropExceptOne_KLEnds_DCDealerAdvances
  Arrange: K&L sub-hand, all but one player drops
  Assert:  K&L ends early, DC resumes, DC dealer = next seat after
           the player who chose K&L (seat 1 → seat 2)

[Fact] AllPlayersFold_DCRotation_StillAdvances
  Arrange: DC game, hand starts, all players fold except one (wins by default)
  Assert:  DC dealer still advances to next player for next hand,
           phase returns to WaitingForDealerChoice
```

---

## 6. Regression Tests

Add to the existing `CreateGameCommandHandlerTests.cs`:

```text
[Theory]
[InlineData("FIVECARDDRAW")]
[InlineData("SEVENCARDSTUD")]
[InlineData("KINGSANDLOWS")]
[InlineData("HOLDEM")]
[InlineData("GOODBADUGLY")]
Handle_ExistingGameCodes_StillWorkAfterDCIntroduction(string gameCode)
  Assert: Same behavior as before — game created with specified type,
          ante and minBet set on the game, phases work unchanged

[Fact] Handle_UnknownGameCode_StillReturnsConflict
  Assert: "UNKNOWNGAME" still returns conflict — DC didn't break validation

(Already exists, keep as-is — acts as regression)
```

Add to `ContinuousPlayBackgroundServiceTests.cs`:

```text
[Fact] ReadyForNextHand_NonDCGame_BehaviorUnchanged
  Arrange: Standard FIVECARDDRAW game in Complete phase
  Assert: Hand starts normally via ProcessGamesReadyForNextHandAsync
          (no WaitingForDealerChoice detour)
```

Add to `GameFlowHandlerTests.cs`:

```text
[Theory]
[InlineData("HOLDEM")]
[InlineData("FIVECARDDRAW")]
[InlineData("SEVENCARDSTUD")]
Factory_ExistingHandlers_StillResolveCorrectly(string code)
  Assert: GetHandler returns correct type after DC feature added
```

---

## 7. Entity / Schema Assumptions

The following new fields or concepts are assumed (to be confirmed during implementation):

| Entity | New Field | Type | Purpose |
|--------|-----------|------|---------|
| `Game` | `IsDealersChoice` | `bool` | Flag: is this a DC table? |
| `Game` | `DcDealerPosition` | `int?` | Separate DC dealer tracker (insulated from K&L internal rotation) |
| `GameType` | `DEALERSCHOICE` code | row | Sentinel game type for DC tables |
| New Command | `ChooseDealerGameCommand` | record | `(Guid GameId, Guid PlayerId, string GameTypeCode, int Ante, int MinBet)` |
| New Phase | `WaitingForDealerChoice` | string | Phase between hands in DC mode |

If the implementation uses a different approach (e.g., a nullable `GameCode` on the `Game` entity instead of `IsDealersChoice`), the test assertions adapt but the scenario coverage remains the same.

---

## 8. Execution Priority

| Priority | Tests | Why first |
|----------|-------|-----------|
| **P0** | CreateGame with null code, ChooseDealerGame validation, DC dealer rotation basic, Regression (existing game codes) | Core DC mechanics + backward compat |
| **P1** | Full lifecycle, K&L encapsulation, ContinuousPlay DC behavior, game type switching | Integration and multi-hand correctness |
| **P2** | Disconnect/timeout, player join/leave mid-DC, all-fold rotation | Edge cases after core is solid |

---

## 9. Test Infrastructure Additions

- **`DatabaseSeeder`** needs a new helper:
  ```csharp
  public static async Task<GameSetup> CreateDealersChoiceGameSetupAsync(
      CardsDbContext context, int numberOfPlayers, int startingChips = 1000)
  ```
  That creates a game with `IsDealersChoice = true` (or `GameCode = null`), no fixed ante/minBet.

- **`IntegrationTestBase.SeedBaseDataAsync`** should include a `DEALERSCHOICE` GameType row (if using a sentinel type).

- **Fake for timeout** (if implemented): `FakeDealerChoiceTimerService` to control and expire timeouts deterministically.
