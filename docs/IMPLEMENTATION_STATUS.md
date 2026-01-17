# Kings and Lows - Implementation Status

## ✅ Completed

### API Layer
- **CreateGame Endpoint**: `POST /api/v1/games/kings-and-lows`
  - Creates a new Kings and Lows game
  - Auto-creates GameType in database on first use
  - Follows same pattern as existing games
  - Fully tested and working

### UI Layer
- **CreateTable Integration**: Kings and Lows now appears in the game selection UI
  - Users can select Kings and Lows when creating a table
  - Properly wired to use IKingsAndLowsApi client
  - Game marked as available (not "Coming Soon")

### Database
- **GameType Auto-Creation**: GameType record is automatically created when first Kings and Lows game is created
  - Code: KINGSANDLOWS
  - Betting Structure: AntePotMatch (3)
  - Wild Card Rule: LowestCard (3)
  - Max Discards: 5 (all cards can be discarded)
  - Min/Max Players: 2-5

### Domain Layer (Already Existed)
- Complete game logic in `KingsAndLowsGame.cs`
- Game rules metadata in `KingsAndLowsRules.cs`
- All phases defined in `KingsAndLowsPhase.cs`
- Wild card evaluation logic

## 🚧 In Progress / Not Yet Implemented

### API Commands Still Needed

To make the game fully playable, the following API endpoints need to be implemented:

#### 1. StartHand
- `POST /api/v1/games/kings-and-lows/{gameId}/start-hand`
- Initializes a new hand, transitions from WaitingToStart to CollectingAntes

#### 2. DropOrStay Decision
- `POST /api/v1/games/kings-and-lows/{gameId}/drop-or-stay`
- Records each player's drop or stay decision
- Advances game when all players have decided

#### 3. DrawCards
- `POST /api/v1/games/kings-and-lows/{gameId}/draw`
- Allows players to discard and draw replacement cards
- Handles turn-based drawing for staying players

#### 4. DeckDraw (Player vs Deck)
- `POST /api/v1/games/kings-and-lows/{gameId}/deck-draw`
- Allows dealer to choose discards for the deck when only one player stays
- Special case handler for player-vs-deck scenario

#### 5. AcknowledgePotMatch
- `POST /api/v1/games/kings-and-lows/{gameId}/acknowledge-pot-match`
- Processes pot matching from losers
- Continues to next hand or ends game

### SignalR Integration

The following SignalR extensions are needed for real-time gameplay:

#### TableStatePublicDto Extensions
```csharp
// Kings and Lows specific public state
public IReadOnlyList<string>? DroppedPlayerIds { get; init; }
public IReadOnlyList<CardInfo>? DeckHand { get; init; }
public IReadOnlyList<string>? LosersToMatchPot { get; init; }
public IReadOnlyDictionary<string, int>? PotMatchAmounts { get; init; }
```

#### PrivateStateDto Extensions
```csharp
// Kings and Lows specific private state
public bool IsMyTurnToDecideDropOrStay { get; init; }
public string? MyDropOrStayDecision { get; init; }
public IReadOnlyList<int>? MyWildCardIndices { get; init; }
```

#### GameHub Methods
- `DropOrStayAsync(Guid gameId, string decision)`
- `DrawCardsAsync(Guid gameId, int[] discardIndices)`
- `DeckDrawAsync(Guid gameId, int[] discardIndices)`

### UI Components

The following UI components are needed for Kings and Lows specific interactions:

#### 1. DropOrStayOverlay.razor
- Modal overlay for drop/stay decision
- Shows current pot amount
- Drop and Stay buttons
- Waiting indicator for other players

#### 2. PlayerVsDeckOverlay.razor
- Displays deck's hand (visible to all)
- Shows dealer controls for selecting discards
- Indicates single player vs deck scenario

#### 3. PotMatchingOverlay.razor
- Shows losers and amounts they must match
- Displays who won and who lost
- Continue button to proceed to next hand

#### 4. TablePlay.razor Updates
- Phase detection for Kings and Lows phases
- Action handlers for drop/stay, draw, deck draw
- Wild card highlighting (Kings + lowest card)
- Status indicators for multi-hand game flow

### Refitter Client Generation

The IKingsAndLowsApi client interface needs to be generated:

**Steps:**
1. Start the API (`dotnet run --project src/CardGames.Poker.Api`)
2. Verify OpenAPI spec includes Kings and Lows endpoints
3. Build Refitter project (`dotnet build src/CardGames.Poker.Refitter`)
4. Copy generated client to CardGames.Contracts

**Note:** Refitter is configured with `multipleInterfaces: "ByTag"`, so it will automatically generate `IKingsAndLowsApi` based on the "KingsAndLows" tag on the endpoints.

## 📝 Implementation Approach

### For Each Command (StartHand, DropOrStay, etc.)

1. **Create Command Files**
   - `{CommandName}Command.cs` - The command record
   - `{CommandName}CommandHandler.cs` - MediatR handler
   - `{CommandName}Endpoint.cs` - HTTP endpoint mapping
   - Result DTOs (Success/Conflict/etc.)

2. **Register Endpoint in V1.cs**
   - Add `.Map{CommandName}()` to the endpoint group

3. **Test Command**
   - Unit tests for handler
   - Integration tests for API endpoint
   - Manual testing via Swagger/Postman

4. **Extend SignalR**
   - Add necessary state to DTOs
   - Update TableStateBuilder to populate state
   - Broadcast state changes after command execution

5. **Create UI Components**
   - Build overlay/panel component
   - Add to TablePlay.razor with phase detection
   - Wire up action handlers

### Recommended Implementation Order

1. **StartHand** - Simplest, just transitions phase
2. **DropOrStay** - Core mechanic, needed before draw
3. **DrawCards** - Standard draw mechanics
4. **DeckDraw** - Special case, but builds on DrawCards
5. **AcknowledgePotMatch** - Pot matching and hand completion

### Testing Strategy

For each command:
1. Unit test the command handler
2. Test via Swagger UI (API running locally)
3. Test via UI once all commands are implemented
4. Test full game flow end-to-end

### Time Estimates

Based on FiveCardDraw implementation complexity:

- **Each Command**: 1-2 hours (implementation + testing)
- **SignalR Extensions**: 2-3 hours (DTOs + TableStateBuilder)
- **UI Components**: 4-6 hours (3 overlays + TablePlay integration)
- **Refitter Generation**: 0.5 hours (build + verify)
- **Testing & Debugging**: 3-4 hours (full game flow)

**Total**: ~15-20 hours for complete implementation

## 🎯 Current State: Minimal Viable Product

**What works now:**
- Users can create a Kings and Lows game from the UI
- Game record is created in the database
- GameType is automatically seeded
- Foundation is in place for additional commands

**What's needed for gameplay:**
- All game phase commands (StartHand, DropOrStay, DrawCards, DeckDraw, AcknowledgePotMatch)
- SignalR real-time state broadcasting
- UI overlays for game-specific interactions
- Wild card highlighting
- Multi-hand pot tracking

## 📚 Reference

See these files for implementation patterns:
- `src/CardGames.Poker.Api/Features/Games/FiveCardDraw/` - Complete game implementation
- `src/CardGames.Poker/Games/KingsAndLows/KingsAndLowsGame.cs` - Domain logic
- `KingsAndLowsPlan.md` - Complete implementation plan with API specs

## ✅ Next Immediate Steps

To continue implementing Kings and Lows:

1. Generate Refitter clients so IKingsAndLowsApi is available in UI
2. Implement StartHand command (simplest one first)
3. Test CreateGame + StartHand flow end-to-end
4. Implement DropOrStay command (core mechanic)
5. Add SignalR extensions for drop/stay state
6. Create DropOrStayOverlay.razor
7. Test drop/stay flow
8. Continue with remaining commands...

## 🚀 Alternative: Use Existing Game Flow

If time is limited, consider leveraging the Common game endpoints that might work across games:
- Check if `/api/v1/games/{gameId}/action` generic endpoint exists
- See if SignalR GameHub has generic action methods
- This could reduce implementation from 20 hours to ~8-10 hours
