# Kings and Lows Implementation - Summary

## What Has Been Completed

This pull request lays the foundation for Kings and Lows integration into the CardGames system. Here's what's working:

### ✅ API Infrastructure
- **CreateGame Endpoint**: `POST /api/v1/games/kings-and-lows`
  - Location: `src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/Commands/CreateGame/`
  - Command, handler, and endpoint all implemented
  - Auto-creates GameType in database on first game creation
  - Uses proper constants and follows established patterns
  
- **Endpoint Registration**: Properly registered in API routing
  - `KingsAndLowsApiMapGroup.cs` maps the versioned API
  - Included in `MapFeatureEndpoints.cs`
  - Tagged for automatic Refitter client generation

### ✅ Database Schema
- **GameType Auto-Seeding**: Kings and Lows GameType is created automatically when the first game is created with:
  - Code: `KINGSANDLOWS`
  - Betting Structure: `AntePotMatch` (unique to this game)
  - Wild Card Rule: `LowestCard` (Kings + each player's lowest card)
  - Max Discards: 5 (players can discard all cards)
  - Min/Max Players: 2-5
  
### ✅ Domain Layer (Pre-existing)
- Complete game logic in `src/CardGames.Poker/Games/KingsAndLows/`
  - `KingsAndLowsGame.cs` - Full game implementation
  - `KingsAndLowsRules.cs` - Game rules metadata
  - `KingsAndLowsPhase.cs` - All game phases defined
  - Wild card evaluation and pot matching logic

### ✅ Game Metadata
- Registered in `PokerGameMetadataRegistry`
- Registered in `PokerGameRulesRegistry`
- Available via `/api/v1/games/rules/KINGSANDLOWS` endpoint

### ✅ UI Integration (Partial)
- Kings and Lows appears in game selection UI
- Shows correct metadata (name, description, player counts)
- Marked as "Coming Soon" until full implementation is complete

### ✅ Documentation
- `KingsAndLowsPlan.md` - Complete 20+ page implementation plan
- `IMPLEMENTATION_STATUS.md` - Current state and next steps guide
- API endpoint documentation in code comments

## What Still Needs to Be Done

To make Kings and Lows fully playable, the following work is required:

### 1. API Commands (High Priority)
Implement these command handlers following the CreateGame pattern:

- **StartHand**: Initializes a new hand, transitions phases
- **DropOrStay**: Records player decisions (core game mechanic)
- **DrawCards**: Handles card discarding and drawing
- **DeckDraw**: Special case for player-vs-deck scenario
- **AcknowledgePotMatch**: Processes pot matching from losers

Estimated time: 8-10 hours

### 2. Refitter Client Generation (Required)
The `IKingsAndLowsApi` client interface needs to be generated:

**Steps:**
1. Start the API locally: `dotnet run --project src/CardGames.Poker.Api`
2. Verify the OpenAPI spec includes Kings and Lows at: `https://localhost:7034/openapi/v1.json`
3. Build Refitter project: `dotnet build src/CardGames.Poker.Refitter`
4. The generated client will be copied to `src/CardGames.Contracts/RefitInterface.v1.cs`
5. Update CreateTable.razor to use `IKingsAndLowsApi` and mark game as available

Estimated time: 0.5 hours

### 3. SignalR Extensions (Medium Priority)
Extend SignalR DTOs for real-time game state:

**TableStatePublicDto additions:**
```csharp
public IReadOnlyList<string>? DroppedPlayerIds { get; init; }
public IReadOnlyList<CardInfo>? DeckHand { get; init; }
public IReadOnlyList<string>? LosersToMatchPot { get; init; }
public IReadOnlyDictionary<string, int>? PotMatchAmounts { get; init; }
```

**PrivateStateDto additions:**
```csharp
public bool IsMyTurnToDecideDropOrStay { get; init; }
public string? MyDropOrStayDecision { get; init; }
public IReadOnlyList<int>? MyWildCardIndices { get; init; }
```

**GameHub methods:**
- `DropOrStayAsync()`
- `DrawCardsAsync()`
- `DeckDrawAsync()`

**TableStateBuilder updates:**
- Populate Kings and Lows specific state
- Calculate wild card indices for each player

Estimated time: 3-4 hours

### 4. UI Components (Medium Priority)
Create three new overlay components:

- **DropOrStayOverlay.razor**: Modal for drop/stay decisions
- **PlayerVsDeckOverlay.razor**: Shows deck's hand in special scenario
- **PotMatchingOverlay.razor**: Displays pot matching requirements

Update **TablePlay.razor** with:
- Phase detection for Kings and Lows
- Action handlers for all commands
- Wild card highlighting (Kings + lowest card per player)

Estimated time: 4-6 hours

### 5. Testing (Low Priority but Important)
- Unit tests for each command handler
- Integration tests for API endpoints
- End-to-end game flow testing
- UI manual testing

Estimated time: 3-4 hours

**Total Estimated Time to Complete: 19-25 hours**

## How to Complete the Implementation

### Recommended Approach: Incremental Implementation

1. **Generate Refitter Client First** (30 minutes)
   - This allows UI testing as commands are built
   - Unblocks the CreateTable.razor integration
   
2. **Implement Commands One at a Time** (8-10 hours)
   - Start with StartHand (simplest)
   - Then DropOrStay (core mechanic)
   - Then DrawCards (standard draw)
   - Then DeckDraw (special case)
   - Finally AcknowledgePotMatch (completion)
   - Test each command via Swagger UI before moving to next
   
3. **Add SignalR Support** (3-4 hours)
   - Extend DTOs with Kings and Lows fields
   - Update TableStateBuilder to populate state
   - Add GameHub methods
   - Test with multiple connected clients
   
4. **Build UI Overlays** (4-6 hours)
   - Create DropOrStayOverlay first (most used)
   - Then PlayerVsDeckOverlay
   - Then PotMatchingOverlay
   - Wire up TablePlay.razor handlers
   - Add wild card highlighting
   
5. **Test End-to-End** (3-4 hours)
   - Play through complete game flows
   - Test all edge cases (all drop, single player stays, etc.)
   - Verify pot carrying over across hands
   - Test with 2-5 players

### Alternative: Faster Partial Implementation

If time is limited, implement only the essential commands for a minimal viable game:

1. StartHand - Required to begin
2. DropOrStay - Core mechanic
3. DrawCards - Required for standard play
4. Use simplified flow without player-vs-deck or pot matching

This would reduce implementation time to ~8-12 hours.

## Architecture Decisions Made

### Why Game-Specific Endpoints?
- Each game has unique mechanics (drop/stay, deck draw, pot matching)
- Different validation rules per action
- Cleaner separation of concerns
- Easier to test and maintain

### Why Auto-Seed GameType?
- Simplifies deployment (no migration needed)
- GameType is created on-demand when first game is played
- Follows pattern used by FiveCardDraw
- Ensures consistency between code and database

### Why "Coming Soon" in UI?
- Game is not playable without all commands implemented
- Prevents user confusion
- Easy to enable once Refitter client is generated and commands are ready

## Testing the Current Implementation

### API Test (via Swagger or Postman)
```bash
# Start the API
dotnet run --project src/CardGames.Poker.Api

# Navigate to Swagger UI
https://localhost:7034/swagger

# Execute: POST /api/v1/games/kings-and-lows
{
  "gameId": "01936e5a-1234-7890-abcd-000000000001",
  "gameName": "Test Kings and Lows",
  "ante": 5,
  "minBet": 10,
  "players": [
    { "name": "Alice", "startingChips": 1000 },
    { "name": "Bob", "startingChips": 1000 }
  ]
}

# Expected: 201 Created with game ID
```

### Database Verification
```sql
-- Check GameType was created
SELECT * FROM GameTypes WHERE Code = 'KINGSANDLOWS';

-- Check Game was created
SELECT * FROM Games WHERE Id = '01936e5a-1234-7890-abcd-000000000001';

-- Check Players were created/linked
SELECT * FROM GamePlayers WHERE GameId = '01936e5a-1234-7890-abcd-000000000001';
```

### UI Test
1. Start the Web project: `dotnet run --project src/CardGames.Poker.Web`
2. Navigate to `/table/create`
3. Verify Kings and Lows appears in game list
4. Verify it shows "Coming Soon" badge
5. Verify metadata displays correctly (2-5 players, description)

## Code Quality Notes

### Follows Established Patterns
- Command/Handler/Endpoint structure matches FiveCardDraw
- Uses MediatR for CQRS pattern
- OneOf return types for result handling
- Proper dependency injection

### Best Practices Applied
- Uses constants instead of magic strings
- Proper placeholder email format
- XML documentation on all public APIs
- Async/await throughout
- Cancellation token support

### No Breaking Changes
- Existing games continue to work
- New endpoints are additive only
- Database changes are backwards compatible

## Resources

### Implementation Guides
- `KingsAndLowsPlan.md` - Detailed implementation plan with code examples
- `IMPLEMENTATION_STATUS.md` - Current state and next steps
- `ADDING_NEW_GAMES.md` - General guide for adding games
- `ARCHITECTURE.md` - System architecture overview

### Reference Implementations
- `src/CardGames.Poker.Api/Features/Games/FiveCardDraw/` - Complete game example
- `src/CardGames.Poker.Api/Features/Games/TwosJacksManWithTheAxe/` - Another complete example
- `src/CardGames.Poker/Games/KingsAndLows/` - Domain logic (already complete)

### Related Documentation
- `DYNAMIC_UI_PLAN.md` - UI adaptation strategy
- `IMPLEMENTATION_SUMMARY.md` - Game-agnostic architecture explanation

## Next Immediate Action Items

If you want to continue this implementation, here are the next steps in priority order:

1. ✅ **Review and Merge This PR**
   - Lays foundation for Kings and Lows
   - No breaking changes
   - API and domain layer integration complete

2. 🔧 **Generate Refitter Client**
   - Required before any UI work can be tested
   - Takes 30 minutes
   - See instructions in "Refitter Client Generation" section above

3. 🏗️ **Implement StartHand Command**
   - Simplest command to start with
   - Good warm-up for the pattern
   - Follow CreateGame as a template
   - Test via Swagger UI

4. 🎮 **Implement DropOrStay Command**
   - Core game mechanic
   - Most important for gameplay
   - Requires SignalR state extensions
   - Enables UI overlay testing

5. 🎨 **Create DropOrStayOverlay**
   - First UI component
   - Most frequently used
   - Tests the overlay pattern

Continue from there following the recommended approach outlined above.

## Questions or Issues?

If you encounter problems during implementation:

1. **API won't build**: Check that PokerGameMetadataRegistry is accessible
2. **Refitter fails**: Ensure API is running and OpenAPI spec is accessible
3. **UI compilation errors**: Verify IKingsAndLowsApi was generated by Refitter
4. **Database errors**: Check connection string and ensure migrations are applied

For architecture questions, refer to ARCHITECTURE.md and ADDING_NEW_GAMES.md.

## Conclusion

This PR provides a solid foundation for Kings and Lows integration:
- ✅ API endpoint exists and works
- ✅ Database integration complete
- ✅ Domain logic is fully implemented
- ✅ Game appears in UI
- ✅ Comprehensive documentation provided

To make it fully playable, implement the remaining commands, extend SignalR, and create the UI overlays following the detailed plans in `KingsAndLowsPlan.md` and `IMPLEMENTATION_STATUS.md`.

**Estimated effort to complete: 19-25 hours of development time.**
