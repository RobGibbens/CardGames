# Leave Table Feature - Requirements and Design Document

## Executive Summary

This document provides a comprehensive specification for implementing the "Leave Table" feature in the Friday Night Poker application. This feature allows players to voluntarily exit a game table, with different behaviors depending on whether the game has started and whether the player is actively involved in a hand.

## Table of Contents

1. [Functional Requirements](#functional-requirements)
2. [User Stories](#user-stories)
3. [Technical Architecture](#technical-architecture)
4. [Data Model](#data-model)
5. [API Design](#api-design)
6. [Game Logic Integration](#game-logic-integration)
7. [UI/UX Specifications](#uiux-specifications)
8. [SignalR Real-Time Updates](#signalr-real-time-updates)
9. [Edge Cases and Error Handling](#edge-cases-and-error-handling)
10. [Testing Requirements](#testing-requirements)
11. [Implementation Plan](#implementation-plan)

---

## 1. Functional Requirements

### 1.1 Core Requirements

**FR-1: Leave Table Button**
- A "Leave Table" button must be visible at all times during gameplay on the TablePlay.razor page (already exists at line 52).
- The button should be accessible regardless of game phase or player state.

**FR-2: Pre-Game Departure (Game Not Started)**
- **Definition**: Game has not started when `Game.StartedAt == null` OR `Game.Status == GameStatus.WaitingForPlayers`.
- **Behavior**: 
  - Player record should be **completely deleted** from the `GamePlayers` table.
  - No hand history is preserved.
  - Seat becomes immediately available for other players.
  - All other players see the seat as empty instantly via SignalR.
  - Player is redirected to the lobby page.

**FR-3: Mid-Game Departure (Game Started, Not in Active Hand)**
- **Definition**: Game has started (`Game.StartedAt != null` AND `Game.Status == GameStatus.InProgress`) but player is NOT currently in an active hand (i.e., between hands, folded, or sitting out).
- **Behavior**:
  - Player record is **NOT deleted** but marked as `Status = GamePlayerStatus.Left`.
  - Set `GamePlayer.LeftAtHandNumber = Game.CurrentHandNumber`.
  - Set `GamePlayer.LeftAt = DateTimeOffset.UtcNow`.
  - Set `GamePlayer.FinalChipCount = GamePlayer.ChipStack`.
  - Set `GamePlayer.IsSittingOut = true` (for consistency with game logic).
  - Seat appears empty to all other players immediately via SignalR.
  - Player is redirected to the lobby page.
  - Player record remains in database for hand history and audit purposes.

**FR-4: Mid-Game Departure (Game Started, In Active Hand)**
- **Definition**: Game has started AND player is actively participating in the current hand (not folded, not sitting out, has cards).
- **Behavior**: 
  - Player cannot leave immediately.
  - System treats this like a "Sit Out Next Hand" request.
  - Player's leave request is queued/flagged internally.
  - Player continues to play the current hand to completion (must make all required actions).
  - After hand completes (showdown or fold), BEFORE next hand starts:
    - Apply same logic as FR-3 (mark as Left, preserve record, update fields).
    - Player is removed from the next hand's dealing phase.
    - Player is redirected to lobby after current hand completes.

**FR-5: Ante Collection Exclusion**
- Once a player has status `GamePlayerStatus.Left` or is flagged for leaving, they must NOT have antes collected from them in future hands.
- Ante collection logic in `CollectAntesCommandHandler` must filter out players with `Status == GamePlayerStatus.Left`.

**FR-6: Turn Skipping**
- Players with `Status == GamePlayerStatus.Left` must be skipped during all game actions:
  - Betting rounds (fold/check/bet/raise).
  - Decision phases (Drop or Stay for Kings and Lows).
  - Drawing phases (discard/draw).
  - Any other player action that requires input.
- Game engine should treat them as if they have auto-folded.

**FR-7: Card Dealing Exclusion**
- Players with `Status == GamePlayerStatus.Left` must NOT receive cards in any future dealing phases.
- Dealing logic in `DealHandsCommandHandler` must filter out players with `Status == GamePlayerStatus.Left`.

**FR-8: Immediate UI Feedback**
- When a player leaves:
  - All other connected players see that seat as empty immediately (via SignalR broadcast).
  - Seat pill shows as unoccupied.
  - No player name, chips, or cards are displayed for that seat.
  - Dealer button, current actor indicator, and other game state indicators update accordingly.

**FR-9: Lobby Redirection**
- After successfully leaving a table:
  - Player's browser navigates to `/lobby`.
  - SignalR connection to the game hub is gracefully terminated via `GameHubClient.LeaveGameAsync(GameId)`.

---

## 2. User Stories

**US-1: Pre-Game Exit**
```
As a player who joined a table but the game hasn't started,
I want to leave the table immediately,
So that I can join a different game without waiting.
```

**Acceptance Criteria:**
- GIVEN I am seated at a table where the game has not started
- WHEN I click the "Leave Table" button
- THEN I am immediately removed from the table
- AND my seat appears empty to all other players
- AND I am redirected to the lobby
- AND no record of my participation is retained in the database

**US-2: Between-Hands Exit**
```
As a player in an active game between hands,
I want to leave the table immediately,
So that I don't have to play another hand.
```

**Acceptance Criteria:**
- GIVEN I am seated at a table where the game has started
- AND I am NOT currently in an active hand (hand is complete or I folded)
- WHEN I click the "Leave Table" button
- THEN I am immediately removed from the table
- AND my seat appears empty to all other players
- AND I am redirected to the lobby
- AND my participation record is preserved with my final chip count and hand history

**US-3: Mid-Hand Exit Request**
```
As a player currently in an active hand,
I want to request to leave the table,
So that I can exit after completing my current obligations.
```

**Acceptance Criteria:**
- GIVEN I am seated at a table and actively playing a hand
- WHEN I click the "Leave Table" button
- THEN I receive a message that I will leave after the current hand completes
- AND I continue to play the current hand normally
- AND after the hand completes (showdown/fold), I am removed from the table
- AND my seat appears empty to all other players
- AND I am redirected to the lobby
- AND my participation record is preserved

**US-4: Observer Perspective**
```
As a player at a table,
I want to see when another player leaves,
So that I know who is still in the game.
```

**Acceptance Criteria:**
- GIVEN I am seated at a table
- WHEN another player leaves the table
- THEN I see their seat become empty in real-time
- AND the game continues normally with remaining players
- AND dealer button and turn indicators adjust appropriately

---

## 3. Technical Architecture

### 3.1 System Components

The Leave Table feature integrates with the following existing system components:

1. **API Layer** (`CardGames.Poker.Api`)
   - New command: `LeaveGameCommand` and `LeaveGameCommandHandler`
   - New endpoint: `POST /api/v1/games/{gameId}/leave`
   - Result types: `LeaveGameSuccessful`, `LeaveGameError`

2. **Data Layer** (`CardGames.Poker.Api.Data`)
   - Entity: `GamePlayer` (existing, modifications required)
   - Entity: `Game` (existing, no modifications needed)
   - DbContext: `CardsDbContext` (existing)

3. **Domain Layer** (`CardGames.Poker`)
   - Game logic modifications in all variant games (FiveCardDraw, TwosJacks, KingsAndLows, SevenCardStud)
   - Player filtering in betting, dealing, and decision phases

4. **SignalR Hub** (`CardGames.Poker.Api.Hubs`)
   - `GameHub` (existing)
   - New method: `PlayerLeft` event broadcast
   - Existing: `TableStateUpdated` (will include seat changes)

5. **Web Client** (`CardGames.Poker.Web`)
   - `TablePlay.razor` (existing, wire up button handler)
   - `GameHubClient` (existing, receive PlayerLeft events)
   - Navigation to lobby after leave

6. **Background Services**
   - `ContinuousPlayBackgroundService` (existing, may need modifications)
   - Logic to handle abandoned games (all players left)

### 3.2 Existing Infrastructure to Leverage

**MediatR Command Pattern:**
- Use `IRequest<OneOf<Success, Error>>` pattern (proven in `JoinGameCommand`)
- Command validation in handler
- OneOf result type for success/error scenarios
- Pipeline behaviors for state broadcasting

**SignalR Broadcasting:**
- Use `IGameStateBroadcaster.BroadcastTableStateAsync()` (existing)
- Custom event: `PlayerLeftDto` for immediate notification

**Authorization:**
- Use `ICurrentUserService.UserId` to identify the authenticated player
- Only allow players to leave their own seat (no admin override)

**Database Patterns:**
- EF Core with row versioning for concurrency
- Include tracking for related entities (Game, Player)
- Optimistic concurrency handling

---

## 4. Data Model

### 4.1 GamePlayer Entity (Existing - No Schema Changes Required)

The `GamePlayer` entity already has all required fields:

```csharp
public class GamePlayer : EntityWithRowVersion
{
    // Existing fields - NO CHANGES NEEDED
    public GamePlayerStatus Status { get; set; }          // Set to Left (2)
    public int LeftAtHandNumber { get; set; } = -1;       // Set to current hand #
    public DateTimeOffset? LeftAt { get; set; }           // Set to UTC now
    public int? FinalChipCount { get; set; }              // Set to ChipStack
    public bool IsSittingOut { get; set; }                // Set to true
    public int JoinedAtHandNumber { get; set; }           // Already set
    public DateTimeOffset JoinedAt { get; set; }          // Already set
    public int ChipStack { get; set; }                    // Current chips
    // ... other fields
}
```

**Key Fields Used:**
- `Status`: Set to `GamePlayerStatus.Left` (enum value 2)
- `LeftAtHandNumber`: Set to `Game.CurrentHandNumber`
- `LeftAt`: Set to `DateTimeOffset.UtcNow`
- `FinalChipCount`: Set to `GamePlayer.ChipStack` at time of leaving
- `IsSittingOut`: Set to `true` to ensure game logic skips this player

### 4.2 Game Entity (Existing - No Changes Needed)

```csharp
public class Game : EntityWithRowVersion
{
    public GameStatus Status { get; set; }           // Check for InProgress
    public DateTimeOffset? StartedAt { get; set; }   // Check if game started
    public int CurrentHandNumber { get; set; }       // For LeftAtHandNumber
    public string CurrentPhase { get; set; }         // For phase detection
    // ... other fields
}
```

### 4.3 GamePlayerStatus Enum (Existing - No Changes)

```csharp
public enum GamePlayerStatus
{
    Active = 0,         // Actively playing
    Eliminated = 1,     // No chips left
    Left = 2,           // ← USE THIS for voluntary leave
    Disconnected = 3,   // Connection lost
    SittingOut = 4      // Temporarily sitting out
}
```

---

## 5. API Design

### 5.1 New REST Endpoint

**Endpoint:** `POST /api/v1/games/{gameId}/leave`

**Path Parameters:**
- `gameId` (Guid) - The unique identifier of the game to leave

**Request Body:** None (empty)

**Authentication:** Required (Bearer token)

**Authorization:** Only the authenticated user can leave their own seat

**Response:**

**Success (200 OK):**
```json
{
  "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "playerId": "7f3e4d2c-8a1b-4c5d-9e6f-1a2b3c4d5e6f",
  "playerName": "user@example.com",
  "leftAtHandNumber": 5,
  "leftAt": "2025-01-15T03:00:00Z",
  "finalChipCount": 1250,
  "immediate": true
}
```

**Success Response Fields:**
- `immediate` (bool) - `true` if player left immediately, `false` if queued for end of hand

**Deferred Leave (200 OK):**
```json
{
  "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "playerId": "7f3e4d2c-8a1b-4c5d-9e6f-1a2b3c4d5e6f",
  "playerName": "user@example.com",
  "leftAtHandNumber": -1,
  "leftAt": null,
  "finalChipCount": null,
  "immediate": false,
  "message": "You will leave the table after the current hand completes."
}
```

**Error Responses:**

**404 Not Found:**
```json
{
  "message": "Game not found or you are not seated at this table."
}
```

**409 Conflict:**
```json
{
  "message": "You have already left this game."
}
```

**500 Internal Server Error:**
```json
{
  "message": "An error occurred while leaving the game."
}
```

### 5.2 Command and Handler Design

**Command:**
```csharp
namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;

/// <summary>
/// Command to leave a game table.
/// </summary>
/// <param name="GameId">The unique identifier of the game to leave.</param>
public sealed record LeaveGameCommand(Guid GameId) 
    : IRequest<OneOf<LeaveGameSuccessful, LeaveGameError>>, 
      IGameStateChangingCommand;
```

**Success Result:**
```csharp
/// <summary>
/// Result when a player successfully leaves a game.
/// </summary>
public sealed record LeaveGameSuccessful(
    Guid GameId,
    Guid PlayerId,
    string PlayerName,
    int LeftAtHandNumber,
    DateTimeOffset? LeftAt,
    int? FinalChipCount,
    bool Immediate,
    string? Message = null);
```

**Error Result:**
```csharp
/// <summary>
/// Errors that can occur when leaving a game.
/// </summary>
public sealed record LeaveGameError(string Message);
```

**Command Handler Pseudocode:**

```csharp
public class LeaveGameCommandHandler 
    : IRequestHandler<LeaveGameCommand, OneOf<LeaveGameSuccessful, LeaveGameError>>
{
    public async Task<OneOf<LeaveGameSuccessful, LeaveGameError>> Handle(
        LeaveGameCommand command, 
        CancellationToken cancellationToken)
    {
        // 1. Get the current authenticated user
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
            return new LeaveGameError("User not authenticated");

        // 2. Load the game with related entities
        var game = await _context.Games
            .Include(g => g.GamePlayers)
            .ThenInclude(gp => gp.Player)
            .Include(g => g.GamePlayers)
            .ThenInclude(gp => gp.Cards)
            .FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

        if (game == null)
            return new LeaveGameError("Game not found");

        // 3. Find the player's GamePlayer record
        var gamePlayer = game.GamePlayers
            .FirstOrDefault(gp => gp.PlayerId == currentUserId && gp.Status != GamePlayerStatus.Left);

        if (gamePlayer == null)
            return new LeaveGameError("You are not seated at this table or have already left");

        // 4. Check if game has started
        var gameStarted = game.StartedAt.HasValue && game.Status == GameStatus.InProgress;

        // 5. Handle pre-game departure (complete deletion)
        if (!gameStarted)
        {
            _context.GamePlayers.Remove(gamePlayer);
            await _context.SaveChangesAsync(cancellationToken);

            return new LeaveGameSuccessful(
                GameId: game.Id,
                PlayerId: gamePlayer.PlayerId,
                PlayerName: gamePlayer.Player.Email,
                LeftAtHandNumber: -1,
                LeftAt: DateTimeOffset.UtcNow,
                FinalChipCount: null,
                Immediate: true);
        }

        // 6. Check if player is in an active hand
        var isInActiveHand = IsPlayerInActiveHand(gamePlayer, game);

        if (isInActiveHand)
        {
            // 7. Queue leave for end of hand (flag player but don't update Status yet)
            // Store a flag in VariantState or add a new field like "PendingLeave"
            // For simplicity, we'll use IsSittingOut as a proxy
            // and check again at end of hand in the background service or showdown logic

            // Mark player to sit out next hand (they'll finish current hand)
            gamePlayer.IsSittingOut = true;

            await _context.SaveChangesAsync(cancellationToken);

            return new LeaveGameSuccessful(
                GameId: game.Id,
                PlayerId: gamePlayer.PlayerId,
                PlayerName: gamePlayer.Player.Email,
                LeftAtHandNumber: -1,
                LeftAt: null,
                FinalChipCount: null,
                Immediate: false,
                Message: "You will leave the table after the current hand completes.");
        }

        // 8. Handle mid-game departure (not in active hand)
        gamePlayer.Status = GamePlayerStatus.Left;
        gamePlayer.LeftAtHandNumber = game.CurrentHandNumber;
        gamePlayer.LeftAt = DateTimeOffset.UtcNow;
        gamePlayer.FinalChipCount = gamePlayer.ChipStack;
        gamePlayer.IsSittingOut = true;

        await _context.SaveChangesAsync(cancellationToken);

        return new LeaveGameSuccessful(
            GameId: game.Id,
            PlayerId: gamePlayer.PlayerId,
            PlayerName: gamePlayer.Player.Email,
            LeftAtHandNumber: gamePlayer.LeftAtHandNumber,
            LeftAt: gamePlayer.LeftAt,
            FinalChipCount: gamePlayer.FinalChipCount,
            Immediate: true);
    }

    private bool IsPlayerInActiveHand(GamePlayer gamePlayer, Game game)
    {
        // Player is in an active hand if:
        // 1. Game is in progress (not between hands)
        // 2. Player has cards dealt to them in current hand
        // 3. Player has not folded
        // 4. Current phase is not Complete/Showdown/WaitingToStart

        if (game.Status != GameStatus.InProgress)
            return false;

        if (gamePlayer.HasFolded)
            return false;

        // Check if current phase is an "active" phase (betting, drawing, decision)
        var activePhases = new[] { "Dealing", "PreFlop", "Flop", "Turn", "River", 
            "FirstBettingRound", "SecondBettingRound", "DrawPhase", "Drawing",
            "DropOrStay", "PlayerVsDeck" };

        if (!activePhases.Contains(game.CurrentPhase))
            return false;

        // Check if player has cards for current hand
        var hasCardsInCurrentHand = gamePlayer.Cards
            .Any(c => c.HandNumber == game.CurrentHandNumber);

        return hasCardsInCurrentHand;
    }
}
```

### 5.3 Endpoint Mapping

```csharp
namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;

public sealed class LeaveGameEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/games/{gameId:guid}/leave", async (
                Guid gameId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new LeaveGameCommand(gameId);
                var result = await sender.Send(command, cancellationToken);

                return result.Match<IResult>(
                    success => Results.Ok(success),
                    error => Results.NotFound(new { message = error.Message }));
            })
            .WithTags("Games")
            .WithName("LeaveGame")
            .WithOpenApi()
            .RequireAuthorization()
            .Produces<LeaveGameSuccessful>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);
    }
}
```

---

## 6. Game Logic Integration

### 6.1 Ante Collection

**File:** `CollectAntesCommandHandler.cs` (all variants)

**Modification:** Filter out players with `Status == GamePlayerStatus.Left`

```csharp
// In CollectAntesCommandHandler.Handle()
var activePlayers = game.GamePlayers
    .Where(p => p.Status == GamePlayerStatus.Active && !p.IsSittingOut)
    .ToList();

foreach (var player in activePlayers)
{
    // Collect ante only from active players
    player.ChipStack -= game.Ante;
    player.TotalContributedThisHand += game.Ante;
    // ...
}
```

### 6.2 Card Dealing

**File:** `DealHandsCommandHandler.cs` (all variants)

**Modification:** Filter out players with `Status == GamePlayerStatus.Left`

```csharp
// In DealHandsCommandHandler.Handle()
var eligiblePlayers = game.GamePlayers
    .Where(p => p.Status == GamePlayerStatus.Active && !p.IsSittingOut)
    .OrderBy(p => p.SeatPosition)
    .ToList();

foreach (var player in eligiblePlayers)
{
    // Deal cards only to eligible players
    var cards = deck.Deal(numberOfCards);
    foreach (var card in cards)
    {
        player.Cards.Add(new GameCard { /* ... */ });
    }
}
```

### 6.3 Betting Rounds

**File:** `ProcessBettingActionCommandHandler.cs` (all variants)

**Modification:** Skip players with `Status == GamePlayerStatus.Left` when determining next actor

```csharp
// In betting round logic
var activePlayers = bettingRound.Players
    .Where(p => !p.HasFolded && 
                !p.IsAllIn && 
                p.Status == GamePlayerStatus.Active)
    .ToList();

// Determine next player to act
var nextPlayer = GetNextActivePlayer(currentPlayerIndex, activePlayers);
```

### 6.4 Drawing Phase

**File:** `ProcessDrawCommandHandler.cs` (FiveCardDraw, TwosJacks)

**Modification:** Skip players with `Status == GamePlayerStatus.Left`

```csharp
// When determining next draw player
var eligibleDrawPlayers = game.GamePlayers
    .Where(p => p.Status == GamePlayerStatus.Active && 
                !p.HasFolded && 
                !p.IsSittingOut)
    .OrderBy(p => p.SeatPosition)
    .ToList();
```

### 6.5 Decision Phases (Drop or Stay for Kings and Lows)

**File:** `DropOrStayCommandHandler.cs` (KingsAndLows)

**Modification:** Skip players with `Status == GamePlayerStatus.Left`

```csharp
// Check if all eligible players have decided
var eligiblePlayers = game.GamePlayers
    .Where(p => p.Status == GamePlayerStatus.Active && !p.IsSittingOut)
    .ToList();

var allDecided = eligiblePlayers.All(p => p.DropOrStayDecision != null);
```

### 6.6 Showdown

**File:** `PerformShowdownCommandHandler.cs` (all variants)

**Modification:** Exclude players with `Status == GamePlayerStatus.Left` from showdown evaluation

```csharp
// Only include active players in showdown
var playersInShowdown = game.GamePlayers
    .Where(p => !p.HasFolded && 
                p.Status == GamePlayerStatus.Active)
    .ToList();
```

### 6.7 Continuous Play Background Service

**File:** `ContinuousPlayBackgroundService.cs`

**Modifications:**

1. **Check for abandoned games** (all players left):
```csharp
private async Task ProcessAbandonedGamesAsync(
    CardsDbContext context, 
    IGameStateBroadcaster broadcaster,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // Find games where all players have left
    var abandonedGames = await context.Games
        .Where(g => g.Status == GameStatus.InProgress &&
                    g.GamePlayers.All(p => p.Status == GamePlayerStatus.Left))
        .ToListAsync(cancellationToken);

    foreach (var game in abandonedGames)
    {
        game.Status = GameStatus.Cancelled;
        game.EndedAt = now;
        
        _logger.LogInformation(
            "Game {GameId} marked as Cancelled - all players left", 
            game.Id);
    }

    if (abandonedGames.Any())
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
```

2. **Process pending leaves at end of hand:**
```csharp
// In ProcessGamesReadyForNextHandAsync(), after showdown completes
// Check for players who requested to leave during the hand
var playersToRemove = game.GamePlayers
    .Where(p => p.IsSittingOut && p.Status == GamePlayerStatus.Active)
    .ToList();

foreach (var player in playersToRemove)
{
    // Check if player requested leave (could use a PendingLeave flag)
    // For now, treat all sitting out players as potentially leaving
    // A more robust solution would add a PendingLeave field to GamePlayer
    
    player.Status = GamePlayerStatus.Left;
    player.LeftAtHandNumber = game.CurrentHandNumber;
    player.LeftAt = now;
    player.FinalChipCount = player.ChipStack;
}
```

### 6.8 Game State Builder

**File:** `TableStateBuilder.cs`

**Modification:** Treat players with `Status == GamePlayerStatus.Left` as empty seats in SignalR state

```csharp
private SeatPublicDto BuildSeatDto(GamePlayer? gamePlayer, int seatIndex)
{
    // If player has left, treat seat as empty
    if (gamePlayer == null || gamePlayer.Status == GamePlayerStatus.Left)
    {
        return new SeatPublicDto
        {
            SeatIndex = seatIndex,
            IsOccupied = false,
            // All other fields null/default
        };
    }

    // Normal seat building logic
    return new SeatPublicDto
    {
        SeatIndex = seatIndex,
        IsOccupied = true,
        PlayerName = gamePlayer.Player.Email,
        // ...
    };
}
```

---

## 7. UI/UX Specifications

### 7.1 Leave Table Button

**Location:** Already exists in `TablePlay.razor` at line 52

**Current Code:**
```html
<button class="btn btn-secondary btn-sm leave-table-btn" @onclick="LeaveTableAsync">
    <i class="fa-regular fa-arrow-left"></i>
    Leave Table
</button>
```

**Behavior:**

1. **Always visible** - No conditional rendering
2. **Always enabled** - No disabled state
3. **Click handler:** `LeaveTableAsync()` method

### 7.2 LeaveTableAsync Implementation

**File:** `TablePlay.razor.cs` (code-behind section)

**Current Implementation (line 1445-1450):**
```csharp
private async Task LeaveTableAsync()
{
    // Leave SignalR game group before navigating away
    await GameHubClient.LeaveGameAsync(GameId);
    NavigationManager.NavigateTo("/lobby");
}
```

**Updated Implementation:**
```csharp
private async Task LeaveTableAsync()
{
    if (_isLeavingTable)
        return; // Prevent double-clicks

    _isLeavingTable = true;

    try
    {
        // Call the new Leave Game API endpoint
        var response = await GamesApiClient.LeaveGameAsync(GameId);

        if (response.IsSuccessStatusCode && response.Content != null)
        {
            var result = response.Content;

            if (!result.Immediate)
            {
                // Player is in active hand - show toast message
                await ShowToastAsync(
                    result.Message ?? "You will leave after the current hand completes.",
                    "info",
                    durationMs: 6000);
                
                // Stay on page until hand completes
                // Background service will handle actual removal
                return;
            }

            // Immediate leave - disconnect from SignalR and navigate to lobby
            await GameHubClient.LeaveGameAsync(GameId);
            NavigationManager.NavigateTo("/lobby");
        }
        else
        {
            // Show error toast
            var errorMessage = response.Error?.Content ?? "Failed to leave the table.";
            await ShowToastAsync(errorMessage, "error");
        }
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error leaving table {GameId}", GameId);
        await ShowToastAsync("An error occurred while leaving the table.", "error");
    }
    finally
    {
        _isLeavingTable = false;
    }
}
```

**New State Variable:**
```csharp
private bool _isLeavingTable = false;
```

### 7.3 Toast Notifications

**Scenarios:**

1. **Queued Leave (Mid-Hand):**
   - Message: "You will leave the table after the current hand completes."
   - Type: Info (blue)
   - Duration: 6 seconds

2. **Error - Not Seated:**
   - Message: "You are not seated at this table."
   - Type: Error (red)
   - Duration: 4 seconds

3. **Error - Already Left:**
   - Message: "You have already left this game."
   - Type: Error (red)
   - Duration: 4 seconds

4. **Error - General:**
   - Message: "An error occurred while leaving the table."
   - Type: Error (red)
   - Duration: 4 seconds

### 7.4 Seat Display Updates

**Component:** `TableCanvas.razor`

**Current Behavior:** Already handles empty seats correctly

**Expected Behavior:** When a player leaves:
- Seat pill transitions to empty state (no animation needed)
- Player name, chips, cards, and avatar disappear
- Seat background shows empty seat styling
- Dealer button moves to next valid player if needed

**SignalR Update:** `TableStatePublicDto.Seats` will contain `IsOccupied = false` for left players

### 7.5 Leaderboard Updates

**Component:** `LeaderboardSection.razor`

**Expected Behavior:** Players who have left should be removed from the leaderboard immediately.

**Implementation:** Filter out players with `Status == Left` in `GetLeaderboardPlayers()` method:

```csharp
private IReadOnlyList<LeaderboardSection.LeaderboardPlayer> GetLeaderboardPlayers()
{
    return _seats
        .Where(s => s.IsOccupied && s.PlayerName is not null)
        .Select(s => new LeaderboardSection.LeaderboardPlayer(
            PlayerId: s.SeatIndex.ToString(),
            PlayerName: s.PlayerName!,
            Chips: s.Chips))
        .OrderByDescending(p => p.Chips)
        .ToList();
}
```

**Note:** Since `IsOccupied` will be `false` for left players, no additional filtering needed.

---

## 8. SignalR Real-Time Updates

### 8.1 PlayerLeft Event (New)

**Purpose:** Notify all players immediately when someone leaves the table.

**Event Definition:**
```csharp
namespace CardGames.Contracts.SignalR;

/// <summary>
/// Notification sent when a player leaves a game table.
/// </summary>
public sealed record PlayerLeftDto
{
    public Guid GameId { get; init; }
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; }
    public int SeatIndex { get; init; }
    public string Message { get; init; }
    public DateTimeOffset LeftAt { get; init; }
}
```

**Hub Method (Server):**
```csharp
// In GameHub.cs
public async Task NotifyPlayerLeft(PlayerLeftDto notification)
{
    await Clients.Group(notification.GameId.ToString())
        .SendAsync("PlayerLeft", notification);
}
```

**Broadcaster Integration:**
```csharp
// In GameStateBroadcaster.cs
public async Task BroadcastPlayerLeftAsync(
    Guid gameId, 
    Guid playerId,
    string playerName,
    int seatIndex)
{
    var notification = new PlayerLeftDto
    {
        GameId = gameId,
        PlayerId = playerId,
        PlayerName = playerName,
        SeatIndex = seatIndex,
        Message = $"{playerName} has left the table.",
        LeftAt = DateTimeOffset.UtcNow
    };

    await _hubContext.Clients.Group(gameId.ToString())
        .SendAsync("PlayerLeft", notification);
}
```

**Client Handler (Blazor):**
```csharp
// In TablePlay.razor
protected override async Task OnInitializedAsync()
{
    // Existing subscriptions...
    GameHubClient.OnPlayerLeft += HandlePlayerLeftAsync;
}

private async Task HandlePlayerLeftAsync(PlayerLeftDto notification)
{
    if (notification.GameId != GameId)
        return;

    Logger.LogDebug(
        "Player {PlayerName} left seat {SeatIndex}", 
        notification.PlayerName, 
        notification.SeatIndex);

    // Show toast notification to other players
    if (notification.PlayerId != _currentPlayerId)
    {
        await ShowToastAsync(notification.Message, "info");
    }

    // TableStateUpdated will handle seat display updates
}
```

### 8.2 TableStateUpdated Event (Existing)

**Usage:** Continue using for full state synchronization after player leaves.

**Expected Changes:**
- `Seats` array will show left player's seat as `IsOccupied = false`
- `CurrentActorSeatIndex` may change if it was the leaving player
- `DealerSeatIndex` may change if dealer left

**No Client Code Changes Needed:** Existing logic already handles seat state changes.

---

## 9. Edge Cases and Error Handling

### 9.1 Edge Case: Last Player Leaves

**Scenario:** All players leave a game that has started.

**Behavior:**
- Background service detects abandoned game (all players with `Status == Left`)
- Game status set to `GameStatus.Cancelled`
- `Game.EndedAt` set to current time
- No new hands are started

**Implementation:** See Section 6.7 (Continuous Play Background Service)

### 9.2 Edge Case: Player Leaves While Being Dealer

**Scenario:** Current dealer clicks "Leave Table".

**Behavior:**
- If game hasn't started: dealer leaves normally
- If game started and not in hand: dealer leaves, dealer button moves to next active player
- If game started and in hand: dealer finishes hand, then leaves, button moves on next hand

**Implementation:**
- Dealer button calculation in `StartHandCommandHandler` already filters active players
- No special handling needed beyond status check

### 9.3 Edge Case: Player Leaves While It's Their Turn

**Scenario:** It's the player's turn to act, and they click "Leave Table".

**Behavior:**
- Player is queued to leave (deferred)
- Current turn continues (player must act)
- After hand completes, player is removed

**Alternative (Aggressive):**
- Auto-fold the player immediately
- Mark as leaving
- Continue to next player

**Recommended:** Use deferred approach (first option) to avoid disrupting game flow.

### 9.4 Edge Case: Network Disconnection vs. Voluntary Leave

**Scenario:** Player's connection drops.

**Behavior:**
- `GamePlayer.IsConnected` set to `false` (existing logic)
- Player status remains `Active` (not `Left`)
- Player can reconnect and resume
- Separate from voluntary leave

**No Conflict:** Leave action is explicit API call, disconnection is SignalR event.

### 9.5 Edge Case: Double-Click Leave Button

**Scenario:** Player clicks "Leave Table" multiple times rapidly.

**Behavior:**
- First click initiates leave request
- Subsequent clicks are ignored (UI state flag `_isLeavingTable`)

**Implementation:** See Section 7.2 (LeaveTableAsync with flag)

### 9.6 Error: Player Not Found

**Scenario:** User is authenticated but not seated at the requested game.

**HTTP Response:** 404 Not Found
```json
{
  "message": "You are not seated at this table."
}
```

### 9.7 Error: Already Left

**Scenario:** Player tries to leave after already leaving (e.g., via API call outside UI).

**HTTP Response:** 409 Conflict
```json
{
  "message": "You have already left this game."
}
```

### 9.8 Error: Game Not Found

**Scenario:** GameId doesn't exist in database.

**HTTP Response:** 404 Not Found
```json
{
  "message": "Game not found."
}
```

### 9.9 Error: Concurrency Conflict

**Scenario:** Another operation modified the GamePlayer row simultaneously.

**Behavior:** EF Core throws `DbUpdateConcurrencyException`

**Handling:**
```csharp
catch (DbUpdateConcurrencyException ex)
{
    _logger.LogWarning(ex, "Concurrency conflict when leaving game {GameId}", command.GameId);
    return new LeaveGameError("Another operation is in progress. Please try again.");
}
```

**HTTP Response:** 409 Conflict

---

## 10. Testing Requirements

### 10.1 Unit Tests

**Test Class:** `LeaveGameCommandHandlerTests.cs`

**Test Cases:**

1. **Pre-Game Leave - Success**
   - GIVEN a game that hasn't started
   - WHEN a seated player executes LeaveGameCommand
   - THEN GamePlayer record is deleted from database
   - AND response indicates immediate leave

2. **Mid-Game Leave (Not in Hand) - Success**
   - GIVEN a game in progress
   - AND player is not in an active hand (folded or between hands)
   - WHEN player executes LeaveGameCommand
   - THEN GamePlayer.Status set to Left
   - AND LeftAtHandNumber, LeftAt, FinalChipCount populated
   - AND response indicates immediate leave

3. **Mid-Game Leave (In Active Hand) - Deferred**
   - GIVEN a game in progress
   - AND player is in an active hand
   - WHEN player executes LeaveGameCommand
   - THEN GamePlayer.IsSittingOut set to true
   - AND GamePlayer.Status remains Active (for now)
   - AND response indicates deferred leave with message

4. **Error - Player Not Seated**
   - GIVEN a game exists
   - AND user is authenticated but not seated
   - WHEN user executes LeaveGameCommand
   - THEN error response with "not seated" message

5. **Error - Player Already Left**
   - GIVEN a game exists
   - AND player has Status == Left
   - WHEN player executes LeaveGameCommand again
   - THEN error response with "already left" message

6. **Error - Game Not Found**
   - GIVEN a non-existent GameId
   - WHEN user executes LeaveGameCommand
   - THEN error response with "game not found" message

### 10.2 Integration Tests

**Test Class:** `LeaveGameEndpointTests.cs`

**Test Cases:**

1. **API Endpoint - Pre-Game Leave**
   - POST /api/v1/games/{gameId}/leave
   - VERIFY 200 OK response
   - VERIFY player removed from database

2. **API Endpoint - Mid-Game Leave**
   - POST /api/v1/games/{gameId}/leave
   - VERIFY 200 OK response
   - VERIFY player marked as Left in database

3. **API Endpoint - Authorization**
   - POST without authentication
   - VERIFY 401 Unauthorized

4. **API Endpoint - Not Seated**
   - POST by user not in game
   - VERIFY 404 Not Found

### 10.3 E2E Tests (Manual or Automated)

**Test Scenarios:**

1. **Pre-Game Leave**
   - Player joins table, game not started
   - Player clicks "Leave Table"
   - VERIFY player redirected to lobby
   - VERIFY seat appears empty to other players

2. **Mid-Game Leave (Between Hands)**
   - Player in game, hand completes, waiting for next hand
   - Player clicks "Leave Table"
   - VERIFY player redirected to lobby immediately
   - VERIFY seat appears empty to other players
   - VERIFY next hand starts without that player

3. **Mid-Game Leave (During Hand)**
   - Player in game, actively in a hand
   - Player clicks "Leave Table"
   - VERIFY toast message shown
   - VERIFY player stays on page
   - VERIFY player can continue playing hand
   - AFTER hand completes, VERIFY player redirected to lobby

4. **Multiple Players Leave**
   - 3 players seated, game in progress
   - 2 players leave (not in hand)
   - VERIFY game continues with 1 player
   - VERIFY seats appear empty
   - 1 remaining player leaves
   - VERIFY game marked as Cancelled

5. **SignalR Updates**
   - 2 players seated, different browsers
   - Player 1 leaves
   - VERIFY Player 2 sees seat empty in real-time
   - VERIFY Player 2 sees toast notification

### 10.4 Performance Tests

**Test Cases:**

1. **Concurrent Leaves**
   - 8 players all click "Leave Table" simultaneously
   - VERIFY all requests processed successfully
   - VERIFY no data corruption

2. **Rapid Leave/Join**
   - Player joins, leaves, joins again rapidly
   - VERIFY all operations succeed
   - VERIFY correct state in database

---

## 11. Implementation Plan

### Phase 1: Backend API (Priority 1)

**Tasks:**
1. Create `LeaveGameCommand`, `LeaveGameCommandHandler`, `LeaveGameEndpoint` ✓
2. Create result types: `LeaveGameSuccessful`, `LeaveGameError` ✓
3. Implement handler logic (pre-game, mid-game, deferred) ✓
4. Add unit tests for command handler ✓
5. Register endpoint in `GamesApiMapGroup` ✓

**Files to Create/Modify:**
- `Features/Games/Common/v1/Commands/LeaveGame/LeaveGameCommand.cs` (new)
- `Features/Games/Common/v1/Commands/LeaveGame/LeaveGameCommandHandler.cs` (new)
- `Features/Games/Common/v1/Commands/LeaveGame/LeaveGameEndpoint.cs` (new)
- `Features/Games/Common/v1/Commands/LeaveGame/LeaveGameSuccessful.cs` (new)
- `Features/Games/Common/v1/Commands/LeaveGame/LeaveGameError.cs` (new)
- `Features/Games/Common/GamesApiMapGroup.cs` (modify - add endpoint)
- `Tests/CardGames.Poker.Tests/Features/LeaveGameCommandHandlerTests.cs` (new)

**Estimated Effort:** 4-6 hours

### Phase 2: SignalR Events (Priority 1)

**Tasks:**
1. Add `PlayerLeftDto` to contracts ✓
2. Add `OnPlayerLeft` event to `GameHub` ✓
3. Add `BroadcastPlayerLeftAsync` to `GameStateBroadcaster` ✓
4. Update `TableStateBuilder` to show left players as empty seats ✓
5. Test SignalR event delivery ✓

**Files to Create/Modify:**
- `Contracts/SignalR/PlayerLeftDto.cs` (new)
- `Hubs/GameHub.cs` (modify - add PlayerLeft method)
- `Services/GameStateBroadcaster.cs` (modify - add BroadcastPlayerLeftAsync)
- `Services/TableStateBuilder.cs` (modify - handle Left status in seat building)

**Estimated Effort:** 2-3 hours

### Phase 3: Game Logic Filtering (Priority 2)

**Tasks:**
1. Update `CollectAntesCommandHandler` (all variants) - filter Left players ✓
2. Update `DealHandsCommandHandler` (all variants) - filter Left players ✓
3. Update `ProcessBettingActionCommandHandler` (all variants) - skip Left players ✓
4. Update `ProcessDrawCommandHandler` (draw variants) - skip Left players ✓
5. Update `DropOrStayCommandHandler` (KingsAndLows) - skip Left players ✓
6. Update `PerformShowdownCommandHandler` (all variants) - exclude Left players ✓
7. Add unit tests for each modified handler ✓

**Files to Modify:**
- `Features/Games/FiveCardDraw/v1/Commands/CollectAntes/CollectAntesCommandHandler.cs`
- `Features/Games/FiveCardDraw/v1/Commands/DealHands/DealHandsCommandHandler.cs`
- `Features/Games/FiveCardDraw/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`
- `Features/Games/FiveCardDraw/v1/Commands/ProcessDraw/ProcessDrawCommandHandler.cs`
- `Features/Games/FiveCardDraw/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`
- (Repeat for TwosJacksManWithTheAxe, KingsAndLows, SevenCardStud variants)

**Estimated Effort:** 6-8 hours (multiple variants)

### Phase 4: Continuous Play Integration (Priority 2)

**Tasks:**
1. Add abandoned game detection to `ContinuousPlayBackgroundService` ✓
2. Add pending leave processing at hand completion ✓
3. Test background service logic ✓

**Files to Modify:**
- `Services/ContinuousPlayBackgroundService.cs`

**Estimated Effort:** 3-4 hours

### Phase 5: Web Client Integration (Priority 1)

**Tasks:**
1. Update `LeaveTableAsync` in `TablePlay.razor` ✓
2. Add `_isLeavingTable` state flag ✓
3. Wire up toast notifications for deferred leave ✓
4. Add `OnPlayerLeft` SignalR event handler ✓
5. Test UI flow (leave button, toasts, navigation) ✓

**Files to Modify:**
- `Poker.Web/Components/Pages/TablePlay.razor` (modify LeaveTableAsync method)
- `Poker.Web/Services/GameHubClient.cs` (add OnPlayerLeft event subscription)

**Estimated Effort:** 3-4 hours

### Phase 6: API Client Generation (Priority 3)

**Tasks:**
1. Regenerate Refit client interfaces ✓
2. Update Blazor project to use new `LeaveGameAsync` method ✓

**Files to Generate/Modify:**
- `Contracts/RefitInterface.v1.cs` (regenerate)
- `Poker.Refitter/Output/RefitInterface.v1.cs` (regenerate)

**Estimated Effort:** 1 hour

### Phase 7: Testing (Priority 1)

**Tasks:**
1. Unit tests (command handler, game logic) ✓
2. Integration tests (API endpoint) ✓
3. Manual E2E tests (UI flow, SignalR updates) ✓
4. Performance tests (concurrent leaves) ✓

**Estimated Effort:** 6-8 hours

### Phase 8: Documentation (Priority 3)

**Tasks:**
1. Update API documentation (Swagger/OpenAPI) ✓
2. Update ARCHITECTURE.md if needed ✓
3. Update README.md with new feature ✓

**Estimated Effort:** 1-2 hours

---

## Total Estimated Effort

**Development:** 25-35 hours
**Testing:** 6-8 hours
**Documentation:** 1-2 hours

**Total:** 32-45 hours (4-6 days for a single developer)

---

## Appendix A: Related Files Reference

### Key Existing Files

**Entities:**
- `/src/CardGames.Poker.Api/Data/Entities/GamePlayer.cs` - Player participation record
- `/src/CardGames.Poker.Api/Data/Entities/Game.cs` - Game state
- `/src/CardGames.Poker.Api/Data/Entities/GameCard.cs` - Card dealing records

**Commands (Example - FiveCardDraw):**
- `/src/CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/StartHand/StartHandCommandHandler.cs`
- `/src/CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/CollectAntes/CollectAntesCommandHandler.cs`
- `/src/CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/DealHands/DealHandsCommandHandler.cs`

**Services:**
- `/src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs` - Hand progression
- `/src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs` - SignalR broadcasting
- `/src/CardGames.Poker.Api/Services/TableStateBuilder.cs` - State DTO construction

**SignalR:**
- `/src/CardGames.Poker.Api/Hubs/GameHub.cs` - SignalR hub
- `/src/CardGames.Poker.Web/Services/GameHubClient.cs` - Blazor client

**UI:**
- `/src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` - Main game page
- `/src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor` - Seat display

---

## Appendix B: Database Schema Reference

**GamePlayer Table (Existing):**

| Column               | Type              | Description                                    |
|----------------------|-------------------|------------------------------------------------|
| Id                   | Guid              | Primary key                                    |
| GameId               | Guid              | Foreign key to Game                            |
| PlayerId             | Guid              | Foreign key to Player                          |
| SeatPosition         | int               | Zero-based seat index (0-7)                    |
| ChipStack            | int               | Current chip count                             |
| StartingChips        | int               | Initial chips                                  |
| Status               | int (enum)        | 0=Active, 1=Eliminated, 2=Left, 3=Disconnected, 4=SittingOut |
| IsSittingOut         | bool              | Currently sitting out flag                     |
| HasFolded            | bool              | Folded in current hand                         |
| JoinedAtHandNumber   | int               | Hand number when joined                        |
| LeftAtHandNumber     | int               | Hand number when left (-1 if still active)     |
| JoinedAt             | DateTimeOffset    | Timestamp of join                              |
| LeftAt               | DateTimeOffset?   | Timestamp of leave (null if still active)      |
| FinalChipCount       | int?              | Chip count at time of leaving                  |
| RowVersion           | byte[]            | Concurrency token                              |

**No schema migration needed** - all required fields already exist.

---

## Appendix C: Success Criteria

The Leave Table feature is considered successfully implemented when:

1. ✅ A player can leave a table before the game starts, and their record is completely removed.
2. ✅ A player can leave a table during the game (between hands), and their record is preserved with status=Left.
3. ✅ A player in an active hand can request to leave and will exit after the hand completes.
4. ✅ Players with status=Left do not receive antes, cards, or turn actions.
5. ✅ All connected players see the leaving player's seat as empty immediately via SignalR.
6. ✅ When all players leave, the game is marked as Cancelled by the background service.
7. ✅ The Leave Table button works reliably without double-click issues.
8. ✅ Appropriate toast messages are shown for deferred leaves and errors.
9. ✅ Players are redirected to the lobby after successfully leaving.
10. ✅ All unit, integration, and E2E tests pass.

---

## Document Revision History

| Version | Date       | Author    | Changes                          |
|---------|------------|-----------|----------------------------------|
| 1.0     | 2025-01-15 | AI Agent  | Initial comprehensive design     |

---

**END OF DOCUMENT**
