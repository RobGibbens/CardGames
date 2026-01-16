# Side Pots - Requirements, Architecture, and Design Document

## Executive Summary

This document provides a comprehensive specification for implementing side pots in poker games. Side pots are a critical feature in no-limit and pot-limit poker games that occur when one or more players are all-in for less than the full betting amount. This document outlines the functional requirements, technical architecture, data structures, algorithms, and implementation considerations for a robust side pot system.

## Table of Contents

1. [Overview](#overview)
2. [Functional Requirements](#functional-requirements)
3. [Core Concepts](#core-concepts)
4. [Algorithm and Logic](#algorithm-and-logic)
5. [Technical Architecture](#technical-architecture)
6. [Data Model](#data-model)
7. [API Design](#api-design)
8. [Game Logic Integration](#game-logic-integration)
9. [UI/UX Specifications](#uiux-specifications)
10. [Edge Cases and Error Handling](#edge-cases-and-error-handling)
11. [Testing Requirements](#testing-requirements)
12. [Implementation Plan](#implementation-plan)
13. [Implementation Guidance for LLM Code Generation](#implementation-guidance-for-llm-code-generation)
14. [References and Resources](#references-and-resources)

---

## 1. Overview

### 1.1 What Are Side Pots?

Side pots occur in **no-limit** and **pot-limit** poker games when **at least one player is all-in** for **less** than the full amount other players are betting. The fundamental principle is:

**A player can only win money they've matched ("covered") from each opponent.**

Any extra money that other players contribute above the all-in player's amount goes into one or more side pots, and the all-in player is not eligible to win those additional pots.

### 1.2 Core Rule: "You Can't Win What You Didn't Put In"

If Player A is all-in for $40, and other players continue betting up to $100 total, Player A can only win the portion of the pot that corresponds to $40 from each opponent. The remaining $60 from each continuing player is contested only by players who contributed to that level.

### 1.3 Why Side Pots Matter

Side pots are essential for:
- **Fairness**: Ensuring players can only win amounts they've matched
- **Game Integrity**: Preventing exploitation of all-in situations
- **Cash Game Flow**: Enabling proper handling of varying stack sizes
- **Tournament Play**: Managing elimination scenarios correctly
- **Multi-way Action**: Supporting complex scenarios with multiple all-ins

---

## 2. Functional Requirements

### 2.1 Core Requirements

**FR-1: Main Pot Creation**
- The system must create a main pot that includes contributions from all players who are in the hand.
- The main pot amount is determined by the smallest all-in amount multiplied by the number of contributing players.
- All players who have not folded are eligible for the main pot.

**FR-2: Side Pot Creation**
- When a player goes all-in for less than the current bet, and other players continue betting, the system must create one or more side pots.
- Each side pot contains contributions from players who bet beyond the all-in amount(s).
- Side pots are created in layers based on distinct all-in amounts.

**FR-3: Multiple All-In Handling**
- The system must correctly handle scenarios where multiple players are all-in for different amounts.
- Side pots must be created in ascending order based on all-in amounts.
- Each side pot must track its own set of eligible players.

**FR-4: Eligibility Tracking**
- Each pot (main and side) must maintain a list of eligible players.
- A player is eligible for a pot only if their total contribution meets or exceeds the threshold for that pot.
- Players who fold become ineligible for all pots.

**FR-5: Multi-Street Support**
- Side pots can be created on any betting street (preflop, flop, turn, river).
- The system must accumulate contributions across multiple betting rounds.
- All-in situations on later streets must integrate with existing pot structure.

**FR-6: Pot Award at Showdown**
- At showdown, pots must be awarded in order from main pot to final side pot.
- Each pot is judged independently among only its eligible players.
- Different players can win different pots based on hand strength among eligible competitors.

**FR-7: Contribution Tracking**
- The system must track each player's total contribution throughout the hand.
- Contributions must be tracked across all betting rounds and streets.
- Total pot amounts must always equal the sum of all player contributions.

**FR-8: Real-Time Updates**
- All connected clients must receive real-time updates when side pots are created.
- Pot amounts and eligibility must be visible to all players.
- UI must clearly distinguish between main pot and side pots.

### 2.2 Non-Functional Requirements

**NFR-1: Accuracy**
- Pot calculations must be 100% accurate with zero tolerance for chip loss or creation.
- All mathematical operations must use integer arithmetic to avoid floating-point errors.

**NFR-2: Performance**
- Side pot calculations must complete in O(n log n) time or better, where n is the number of players.
- Pot calculations should not introduce noticeable latency in game flow.

**NFR-3: Auditability**
- All pot creation and award decisions must be logged for audit purposes.
- Hand history must include complete pot breakdown and eligibility information.

**NFR-4: Testability**
- Side pot logic must be independently testable without requiring full game simulation.
- Edge cases must be covered by automated tests.

---

## 3. Core Concepts

### 3.1 Layered Pot Structure

Think of every player's total contribution as a stack of chips. Side pots are created by peeling off equal "layers" from all players still in the hand.

#### Example 1: Simple Three-Player Scenario

**Setup:**
- Rob: $100 committed
- Sam: $100 committed  
- Pat: $30 committed (all-in)

**Pot Construction:**

**Main Pot:**
- Pat: $30
- Rob: $30
- Sam: $30
- **Total: $90**
- **Eligible: Pat, Rob, Sam**

**Side Pot #1:**
- Rob: $70 (= $100 - $30)
- Sam: $70 (= $100 - $30)
- **Total: $140**
- **Eligible: Rob, Sam only**

**Showdown:**
1. Main pot ($90): Best hand among Pat, Rob, Sam
2. Side pot ($140): Best hand among Rob, Sam

### 3.2 Multiple All-Ins with Different Amounts

#### Example 2: Four Players, Multiple All-In Levels

**Setup:**
- Player A: $20 (all-in)
- Player B: $50 (all-in)
- Player C: $100
- Player D: $100

**Pot Construction:**

**Pot 1 - Main Pot (Layer: $0 to $20):**
- A: $20, B: $20, C: $20, D: $20
- **Total: $80**
- **Eligible: A, B, C, D**

**Pot 2 - Side Pot #1 (Layer: $20 to $50):**
- B: $30, C: $30, D: $30
- **Total: $90**
- **Eligible: B, C, D**

**Pot 3 - Side Pot #2 (Layer: $50 to $100):**
- C: $50, D: $50
- **Total: $100**
- **Eligible: C, D**

**Showdown:**
1. Pot 1 ($80): Best hand among A, B, C, D
2. Pot 2 ($90): Best hand among B, C, D
3. Pot 3 ($100): Best hand among C, D

### 3.3 All-In on Different Streets

Side pots can be created on any betting street. The system must track cumulative contributions across all streets.

#### Example 3: All-In on Flop with Continued Betting

**Preflop:**
- All three players see flop: Rob, Sam, Pat (each put in $10)

**Flop:**
- Pat goes all-in for $40 total ($30 additional)
- Sam calls $40 total
- Rob raises to $120 total ($110 additional)
- Sam calls the extra $80 (now at $120 total)

**Pot Construction:**
- **Main Pot**: $40 × 3 = $120 (Pat, Sam, Rob eligible)
- **Side Pot**: $80 × 2 = $160 (Sam, Rob eligible)

### 3.4 Key Principles

1. **No Forced Bets for All-In Players**: Once all-in, a player cannot be forced to act or contribute more.

2. **Continued Betting Among Active Players**: Players with chips can continue betting/raising against each other.

3. **Independent Pot Awards**: Each pot is awarded independently based on best hand among its eligible players.

4. **Contribution Tracking**: System must track total contributions per player across all streets.

5. **Eligibility Rules**: A player is eligible for a pot only if they contributed to that pot's layer.

---

## 4. Algorithm and Logic

### 4.1 Side Pot Calculation Algorithm

The algorithm for calculating side pots follows these steps:

#### Step 1: Collect All Contributions
```
For each player in hand:
    total_contribution[player] = sum of all bets across all streets
```

#### Step 2: Identify All-In Levels
```
all_in_levels = []
For each player in hand:
    If player is all-in AND player has not folded:
        Add total_contribution[player] to all_in_levels
        
Sort all_in_levels in ascending order
Remove duplicates
```

#### Step 3: Build Pots in Layers
```
pots = []
previous_level = 0

For each level in all_in_levels:
    pot_amount = 0
    eligible_players = []
    
    For each player in hand:
        If player has not folded AND total_contribution[player] >= level:
            contribution_to_this_pot = min(total_contribution[player], level) - previous_level
            pot_amount += contribution_to_this_pot
            eligible_players.add(player)
    
    If pot_amount > 0:
        pots.add(Pot(amount: pot_amount, eligible: eligible_players))
    
    previous_level = level
```

#### Step 4: Create Final Pot for Remaining Contributions
```
max_contribution = max(total_contribution.values)

If max_contribution > previous_level:
    final_pot_amount = 0
    eligible_for_final = []
    
    For each player in hand:
        If player has not folded AND total_contribution[player] > previous_level:
            contribution_to_final = total_contribution[player] - previous_level
            final_pot_amount += contribution_to_final
            eligible_for_final.add(player)
    
    If final_pot_amount > 0:
        pots.add(Pot(amount: final_pot_amount, eligible: eligible_for_final))
```

#### Step 5: Validate Total
```
total_from_pots = sum(pot.amount for pot in pots)
total_contributions = sum(total_contribution.values)

Assert total_from_pots == total_contributions
```

### 4.2 Practical Implementation Checklist

When implementing side pots:

1. ✅ Write each player's total committed amount
2. ✅ Sort committed amounts from smallest to largest
3. ✅ Filter out folded players from eligibility
4. ✅ Build pots in "tiers" using differences between amounts
5. ✅ Each tier pot is contested only by players who contributed to that tier
6. ✅ Validate that total pot amount equals sum of all contributions
7. ✅ Award pots in order at showdown
8. ✅ Each pot is judged only among its eligible players

### 4.3 Pot Award Algorithm at Showdown

```
For each pot in pots (in order from main to final side pot):
    If pot.amount == 0 OR pot.eligible_players.count == 0:
        Skip this pot
    
    winners = DetermineWinners(pot.eligible_players)
    
    If winners.count == 0:
        Skip this pot (shouldn't happen in normal play)
    
    share = pot.amount / winners.count
    remainder = pot.amount % winners.count
    
    For each winner in winners:
        payout = share
        If remainder > 0:
            payout += 1
            remainder -= 1
        
        award[winner] += payout
```

### 4.4 Handling Fold Actions

When a player folds:
```
For each pot in pots:
    pot.eligible_players.remove(folded_player)
```

This ensures the folded player cannot win any pot, even if they contributed to it.

---

## 5. Technical Architecture

### 5.1 Current Implementation Status

The repository already contains a `PotManager` class in `src/CardGames.Poker/Betting/PotManager.cs` with the following features:

**Existing Capabilities:**
- ✅ Basic pot management with main pot
- ✅ Contribution tracking per player
- ✅ Side pot calculation via `CalculateSidePots()` method
- ✅ Eligibility tracking per pot
- ✅ Pot award logic with winner determination
- ✅ Split pot support for dual-payout games (e.g., 7s rule)
- ✅ Fold handling via `RemovePlayerEligibility()`

**Current Implementation Highlights:**
```csharp
public class PotManager
{
    private readonly List<Pot> _pots;
    private readonly Dictionary<string, int> _contributions;
    
    // Tracks contributions and builds side pots when all-ins occur
    public void CalculateSidePots(IEnumerable<PokerPlayer> players)
    
    // Awards pots to winners
    public Dictionary<string, int> AwardPots(
        Func<IEnumerable<string>, IEnumerable<string>> determineWinners)
}
```

### 5.2 Architecture Integration Points

#### 5.2.1 Domain Layer
- **Location**: `CardGames.Poker/Betting/PotManager.cs`
- **Responsibility**: Core side pot calculation logic
- **Integration**: Used by game engines to manage pot state during hands

#### 5.2.2 Game Engines
- **Location**: `CardGames.Poker/Games/*/`
- **Responsibility**: Invoke pot manager at appropriate times
- **Key Events**:
  - After each betting round completion
  - When all-in situations occur
  - At showdown for pot awards

#### 5.2.3 API Layer
- **Location**: `CardGames.Poker.Api/`
- **Responsibility**: Expose pot state to clients
- **Endpoints**: Include pot information in game state responses

#### 5.2.4 Contracts
- **Location**: `CardGames.Contracts/`
- **Responsibility**: Define data transfer objects for pots
- **DTOs**: Pot amounts, eligibility lists, showdown results

#### 5.2.5 Web UI
- **Location**: `CardGames.Poker.Web/`
- **Responsibility**: Display pot information to players
- **Components**: Pot display, side pot indicators, showdown animations

### 5.3 System Flow

```
1. Betting Round Occurs
   ↓
2. Player(s) Go All-In
   ↓
3. PotManager.AddContribution() called for each bet
   ↓
4. After betting round completes:
   ↓
5. PotManager.CalculateSidePots() called with active players
   ↓
6. Side pots created and eligible players tracked
   ↓
7. SignalR broadcasts pot state to all clients
   ↓
8. UI updates to show main pot + side pot(s)
   ↓
9. Next street or showdown
   ↓
10. At showdown:
    ↓
11. PotManager.AwardPots() called with hand evaluator
    ↓
12. Each pot awarded to best hand among eligible players
    ↓
13. Chips distributed to winners
    ↓
14. Hand history recorded with pot breakdown
```

---

## 6. Data Model

### 6.1 Pot Class

**Current Implementation:**
```csharp
public class Pot
{
    public int Amount { get; private set; }
    public HashSet<string> EligiblePlayers { get; }
    
    public Pot()
    public Pot(int amount, IEnumerable<string> eligiblePlayers)
    public void Add(int amount)
    public void AddEligiblePlayer(string playerName)
}
```

**Purpose**: Represents a single pot (main or side) with amount and eligible players.

**Properties:**
- `Amount`: Total chips in this pot
- `EligiblePlayers`: Set of player names who can win this pot

### 6.2 PotManager State

**Internal State:**
```csharp
private readonly List<Pot> _pots;
private readonly Dictionary<string, int> _contributions;
```

**Properties:**
- `_pots`: Ordered list of pots (index 0 = main, 1+ = side pots)
- `_contributions`: Dictionary tracking total contribution per player

**Public Interface:**
- `TotalPotAmount`: Sum of all pot amounts
- `Pots`: Read-only list of pots

### 6.3 Contract DTOs

**Recommended DTO structure for API:**

```csharp
public class PotDto
{
    public int PotNumber { get; set; }         // 0 = main, 1+ = side pots
    public int Amount { get; set; }            // Chips in this pot
    public List<string> EligiblePlayers { get; set; }
    public string DisplayName { get; set; }    // "Main Pot", "Side Pot #1", etc.
}

public class PotStateDto
{
    public int TotalPotAmount { get; set; }
    public List<PotDto> Pots { get; set; }
    public Dictionary<string, int> PlayerContributions { get; set; }
}

public class ShowdownResultDto
{
    public Dictionary<string, int> PlayerPayouts { get; set; }
    public List<PotAwardDto> PotAwards { get; set; }
}

public class PotAwardDto
{
    public int PotNumber { get; set; }
    public int Amount { get; set; }
    public List<string> Winners { get; set; }
    public int SharePerWinner { get; set; }
}
```

### 6.4 Hand History

**Side pot information should be included in hand history:**

```csharp
public class HandHistoryEntry
{
    // ... existing fields ...
    
    public List<PotHistoryDto> Pots { get; set; }
    public Dictionary<string, int> FinalPlayerContributions { get; set; }
}

public class PotHistoryDto
{
    public int PotNumber { get; set; }
    public int Amount { get; set; }
    public List<string> EligiblePlayers { get; set; }
    public List<string> Winners { get; set; }
    public Dictionary<string, int> WinnerPayouts { get; set; }
}
```

---

## 7. API Design

### 7.1 Existing Integration

The PotManager is currently used internally by game engines. API endpoints should expose pot state as part of game state responses.

### 7.2 Recommended API Enhancements

#### Get Game State (Enhanced)
```
GET /api/games/{gameId}

Response includes:
{
  "gameId": "...",
  "currentHand": {
    "handNumber": 5,
    "potState": {
      "totalAmount": 350,
      "pots": [
        {
          "potNumber": 0,
          "displayName": "Main Pot",
          "amount": 150,
          "eligiblePlayers": ["Alice", "Bob", "Charlie"]
        },
        {
          "potNumber": 1,
          "displayName": "Side Pot",
          "amount": 200,
          "eligiblePlayers": ["Bob", "Charlie"]
        }
      ],
      "playerContributions": {
        "Alice": 50,
        "Bob": 150,
        "Charlie": 150
      }
    }
  }
}
```

#### Get Hand History (Enhanced)
```
GET /api/games/{gameId}/hands/{handNumber}

Response includes:
{
  "handNumber": 5,
  "pots": [
    {
      "potNumber": 0,
      "amount": 150,
      "eligiblePlayers": ["Alice", "Bob", "Charlie"],
      "winners": ["Alice"],
      "winnerPayouts": {
        "Alice": 150
      }
    },
    {
      "potNumber": 1,
      "amount": 200,
      "eligiblePlayers": ["Bob", "Charlie"],
      "winners": ["Charlie"],
      "winnerPayouts": {
        "Charlie": 200
      }
    }
  ]
}
```

### 7.3 SignalR Events

**Pot State Update Event:**
```
Event: "PotStateUpdated"
Payload: PotStateDto
When: After side pots are calculated
```

**Pot Award Event:**
```
Event: "PotsAwarded"
Payload: ShowdownResultDto
When: At showdown after determining winners
```

---

## 8. Game Logic Integration

### 8.1 When to Calculate Side Pots

Side pots should be calculated:

1. **After Each Betting Round Completes**
   - When all players have acted and betting is closed
   - If any player is all-in

2. **Before Moving to Next Street**
   - Ensures pot state is correct for next betting round
   - Allows UI to update before dealing next cards

3. **Before Showdown**
   - Final pot calculation before determining winners
   - Ensures all contributions are properly allocated

### 8.2 Integration with Betting Rounds

```csharp
public class BettingRound
{
    private PotManager _potManager;
    
    public void ProcessBet(string playerName, int amount, bool isAllIn)
    {
        // Record the bet
        _potManager.AddContribution(playerName, amount);
        
        // Update player state
        player.IsAllIn = isAllIn;
    }
    
    public void CompleteBettingRound(IEnumerable<PokerPlayer> players)
    {
        // Calculate side pots if anyone is all-in
        if (players.Any(p => p.IsAllIn && !p.HasFolded))
        {
            _potManager.CalculateSidePots(players);
        }
        
        // Broadcast pot state update
        BroadcastPotState(_potManager);
    }
}
```

### 8.3 Integration with Game Phases

Different game types (e.g., Five Card Draw, Texas Hold'em, Seven Card Stud) should all use the same PotManager:

```csharp
public class FiveCardDrawGame : IPokerGame
{
    private PotManager _potManager;
    
    public void PlayHand()
    {
        _potManager.Reset();
        
        // Antes
        CollectAntes();
        
        // Initial betting round
        PlayBettingRound();
        _potManager.CalculateSidePots(ActivePlayers);
        
        // Draw phase
        ProcessDraws();
        
        // Final betting round
        PlayBettingRound();
        _potManager.CalculateSidePots(ActivePlayers);
        
        // Showdown
        AwardPots();
    }
}
```

### 8.4 Integration with Showdown

```csharp
public void AwardPots()
{
    var payouts = _potManager.AwardPots(eligiblePlayers =>
    {
        // Evaluate hands for eligible players only
        var handRankings = EvaluateHands(eligiblePlayers);
        return handRankings
            .Where(r => r.Rank == handRankings.Max(x => x.Rank))
            .Select(r => r.PlayerName);
    });
    
    // Distribute chips to winners
    foreach (var (player, amount) in payouts)
    {
        player.ChipStack += amount;
    }
    
    // Record in hand history
    RecordHandHistory(payouts);
    
    // Broadcast results
    BroadcastShowdownResults(payouts);
}
```

---

## 9. UI/UX Specifications

### 9.1 Pot Display Requirements

**Location**: Center of the table (existing pot display area)

**Main Pot Display:**
- Prominent display of total main pot amount
- List of chips graphic representing pot size
- Label: "Main Pot"

**Side Pot Display:**
- Additional pot displays below/beside main pot
- Labels: "Side Pot", "Side Pot #2", etc.
- Distinct visual styling (different color, border, or icon)
- Smaller size than main pot display

**Example Layout:**
```
┌─────────────────────────┐
│     Main Pot: $150      │
│   ━━━━━━━━━━━━━━━━━     │
│ Eligible: Alice, Bob,   │
│          Charlie        │
└─────────────────────────┘

┌─────────────────────────┐
│   Side Pot: $200        │
│   ━━━━━━━━━━━━━━━━━     │
│ Eligible: Bob, Charlie  │
└─────────────────────────┘
```

### 9.2 Player Contribution Display

**Location**: Near each player's position

**Display Requirements:**
- Show total contribution for current hand
- Update in real-time as bets are made
- Highlight when player is all-in
- Different visual treatment for active bet vs. total contribution

**Example:**
```
Player: Alice
Chips: $450
Current Bet: $50 [ALL-IN]
Total Contributed: $50
```

### 9.3 All-In Indicator

**Visual Requirements:**
- Clear "ALL-IN" badge/indicator on player's position
- Remains visible throughout hand
- Distinct color (commonly red or orange)
- Should stand out from other player state indicators

### 9.4 Showdown Animation

**Pot Award Sequence:**
1. Reveal all active players' hands
2. Award pots in order (main pot first, then side pots)
3. For each pot:
   - Highlight the pot being awarded
   - Show winner(s) for that pot
   - Animate chips moving to winner(s)
   - Display amount awarded
4. Final chip stack update for all players

**Timing:**
- Minimum 2-3 seconds per pot to allow players to understand results
- Stagger animations for multiple pots (not simultaneous)

### 9.5 Pot Eligibility Tooltips

**Hover/Tap Behavior:**
- When hovering over a pot display, show tooltip with:
  - Full list of eligible players
  - Amount each player contributed to this pot
  - Brief explanation (e.g., "Only players who bet at least $50")

---

## 10. Edge Cases and Error Handling

### 10.1 Edge Case: Single All-In Player

**Scenario**: Only one player is all-in, others have equal stacks.

**Expected Behavior**:
- Main pot includes all-in player's matched contributions
- Side pot includes remaining contributions from other players
- All-in player eligible for main pot only

**Test Case**:
```
Players: A ($30 all-in), B ($100), C ($100)
All bet: A: $30, B: $100, C: $100

Expected Pots:
- Main: $90 (eligible: A, B, C)
- Side: $140 (eligible: B, C)
```

### 10.2 Edge Case: Multiple All-Ins at Same Amount

**Scenario**: Multiple players all-in for the same amount.

**Expected Behavior**:
- Treated as single all-in level
- One side pot created above that level (if others bet more)

**Test Case**:
```
Players: A ($50 all-in), B ($50 all-in), C ($100), D ($100)
All bet: A: $50, B: $50, C: $100, D: $100

Expected Pots:
- Main: $200 (eligible: A, B, C, D)
- Side: $100 (eligible: C, D)
```

### 10.3 Edge Case: All Players All-In

**Scenario**: All players are all-in for different amounts.

**Expected Behavior**:
- Multiple pots created based on all-in levels
- No further betting possible
- Immediate showdown

**Test Case**:
```
Players: A ($20 all-in), B ($50 all-in), C ($100 all-in)
All bet: A: $20, B: $50, C: $100

Expected Pots:
- Pot 1: $60 (eligible: A, B, C)
- Pot 2: $60 (eligible: B, C)
- Pot 3: $50 (eligible: C)
```

### 10.4 Edge Case: Player Folds After All-In

**Scenario**: Player A is all-in, then Player B folds after betting more.

**Expected Behavior**:
- Player B's contribution stays in pot
- Player B removed from eligibility
- Remaining players contest the pot

**Test Case**:
```
Initial: A ($50 all-in), B ($100), C ($100)
B bets $100, then folds
C calls $100

Expected Pots:
- Main: $150 (eligible: A, C) - B's $50 included but B not eligible
- Side: $100 (eligible: C only) - B's extra $50 included but B not eligible
```

### 10.5 Edge Case: Odd Chip Division

**Scenario**: Pot amount doesn't divide evenly among winners.

**Expected Behavior**:
- Divide pot as evenly as possible
- Award remainder chips to winner(s) in seat order (relative to dealer button)
- Total awarded must equal pot amount exactly

**Test Case**:
```
Pot: $100
Winners: A, B, C (tie)
Each gets: $33
Remainder: $1
Award remainder to first winner in seat order after dealer button
Final: A gets $34, B gets $33, C gets $33 (assuming A is first after dealer)
```

### 10.6 Edge Case: Empty Side Pot

**Scenario**: Side pot calculation results in zero amount (edge case in algorithm).

**Expected Behavior**:
- Skip empty pots
- Don't display empty pots in UI
- Don't include in showdown awards

### 10.7 Edge Case: All Eligible Players Fold from Pot

**Scenario**: All players eligible for a side pot fold.

**Expected Behavior**:
- Remaining player wins pot by default (no showdown needed for that pot)
- Award pot immediately when last competing player folds

**Test Case**:
```
Initial: Main pot eligible: A, B, C; Side pot eligible: B, C
B folds
C wins side pot immediately without showdown
Main pot still requires showdown between A and C (both have cards)
```

### 10.8 Error Handling

**EH-1: Pot Total Mismatch**
```csharp
var totalFromPots = _pots.Sum(p => p.Amount);
var totalContributions = _contributions.Values.Sum();

if (totalFromPots != totalContributions)
{
    _logger.LogError(
        "Pot calculation error: total from pots ({PotTotal}) " +
        "doesn't match total contributions ({ContributionTotal})",
        totalFromPots, totalContributions);
    
    // Adjust last pot to match (documented in current implementation)
    if (_pots.Count > 0)
    {
        _pots[^1].Add(totalContributions - totalFromPots);
    }
}
```

**EH-2: No Eligible Players**
```csharp
if (pot.EligiblePlayers.Count == 0)
{
    _logger.LogWarning(
        "Pot {PotNumber} has no eligible players. Amount: {Amount}",
        potIndex, pot.Amount);
    
    // Return chips to main pot or handle via house rules
}
```

**EH-3: Negative Contributions**
```csharp
if (amount < 0)
{
    throw new ArgumentException(
        "Contribution amount cannot be negative", 
        nameof(amount));
}
```

---

## 11. Testing Requirements

### 11.1 Unit Tests

**Test Coverage Requirements:**
- Minimum 95% code coverage for PotManager class
- All edge cases documented in section 10 must have tests
- Tests should be deterministic and repeatable

**Test Categories:**

#### Basic Side Pot Tests
```
✓ Test_SimpleSidePot_OneAllIn_TwoPlayers
✓ Test_SimpleSidePot_OneAllIn_ThreePlayers
✓ Test_NoSidePot_NoAllIns
✓ Test_NoSidePot_AllPlayersEqualStacks
```

#### Multiple All-In Tests
```
✓ Test_MultipleSidePots_TwoAllIns_DifferentAmounts
✓ Test_MultipleSidePots_ThreeAllIns_DifferentAmounts
✓ Test_MultipleSidePots_TwoAllIns_SameAmount
✓ Test_AllPlayersAllIn_DifferentAmounts
```

#### Fold Handling Tests
```
✓ Test_PlayerFolds_RemovedFromEligibility
✓ Test_AllInPlayerFolds_RemovedFromAllPots
✓ Test_MultipleFolds_CorrectEligibility
```

#### Award Tests
```
✓ Test_AwardMainPot_SingleWinner
✓ Test_AwardMainPot_TieMultipleWinners
✓ Test_AwardMultiplePots_DifferentWinners
✓ Test_AwardPot_OddChipDivision
```

#### Multi-Street Tests
```
✓ Test_AllInOnFlop_ContinuedBettingOnTurn
✓ Test_AllInOnTurn_ContinuedBettingOnRiver
✓ Test_MultipleAllIns_AcrossMultipleStreets
```

#### Edge Case Tests
```
✓ Test_EmptySidePot_SkippedInAward
✓ Test_AllEligiblePlayersFold_LastPlayerWins
✓ Test_NegativeContribution_ThrowsException
✓ Test_TotalMismatch_AdjustsLastPot
```

### 11.2 Integration Tests

**Test Scenarios:**
```
✓ IntegrationTest_FullHand_WithSidePots
✓ IntegrationTest_FiveCardDraw_AllInScenario
✓ IntegrationTest_TexasHoldEm_MultipleAllIns
✓ IntegrationTest_SignalR_PotStateUpdates
✓ IntegrationTest_API_PotStateEndpoint
✓ IntegrationTest_HandHistory_PotsRecorded
```

### 11.3 Manual Test Plan

#### Test Case 1: Basic Side Pot
**Setup**: Create 3-player game, one player short-stacked
**Steps**:
1. Short stack goes all-in
2. Other two players bet beyond all-in amount
3. Verify main pot and side pot created
4. Continue to showdown
5. Verify correct pot awards

**Expected Result**: 
- Main pot awarded to best hand among all three
- Side pot awarded to best hand between two non-all-in players

#### Test Case 2: Multiple All-Ins
**Setup**: Create 4-player game with varying stack sizes
**Steps**:
1. Player A all-in for $20
2. Player B all-in for $50
3. Players C and D both bet $100
4. Verify three pots created
5. Continue to showdown
6. Verify correct pot awards

**Expected Result**:
- Three pots created with correct amounts and eligibility
- Each pot awarded to best hand among eligible players

#### Test Case 3: All-In Then Fold
**Setup**: Create 3-player game
**Steps**:
1. Player A all-in for $50
2. Player B bets $100
3. Player C calls $100
4. Next street: Player B folds
5. Verify Player B removed from side pot eligibility
6. Continue to showdown
7. Verify awards

**Expected Result**:
- Main pot contested between A and C
- Side pot awarded to C by default (only eligible player)

### 11.4 Performance Tests

**Test Requirements:**
```
✓ Benchmark_SidePotCalculation_10Players
✓ Benchmark_SidePotCalculation_100Hands
✓ Benchmark_AwardPots_MultipleWinners
✓ LoadTest_ConcurrentPotCalculations
```

**Performance Targets:**
- Side pot calculation: < 10ms for 10 players
- Pot award: < 5ms per pot
- No memory leaks over 1000+ hands

---

## 12. Implementation Plan

### 12.1 Current Status

✅ **Already Implemented**:
- Core PotManager class exists in `src/CardGames.Poker/Betting/PotManager.cs`
- Basic side pot calculation algorithm implemented
- Contribution tracking functional
- Pot award logic with winner determination
- Split pot support for special game rules

### 12.2 Enhancement Recommendations

Even though core functionality exists, consider these enhancements:

#### Phase 1: Validation and Testing (Priority: High)
- [ ] Comprehensive unit test suite for PotManager
- [ ] Edge case test coverage
- [ ] Integration tests with game engines
- [ ] Performance benchmarks

#### Phase 2: API and Contracts (Priority: High)
- [ ] Define PotDto and related contract objects
- [ ] Enhance game state API to include detailed pot information
- [ ] Add pot breakdown to hand history
- [ ] SignalR events for pot state updates

#### Phase 3: UI Implementation (Priority: High)
- [ ] Main pot and side pot displays in TablePlay.razor
- [ ] Player contribution indicators
- [ ] All-in badges
- [ ] Pot eligibility tooltips
- [ ] Showdown animation for multiple pot awards

#### Phase 4: Documentation and Examples (Priority: Medium)
- [ ] Code documentation and XML comments
- [ ] Example scenarios in developer documentation
- [ ] Update ADDING_NEW_GAMES.md with side pot considerations
- [ ] Tutorial/guide for side pot mechanics

#### Phase 5: Advanced Features (Priority: Low)
- [ ] Pot cap support (if needed for specific game variants)
- [ ] Pot splitting with odd chip rules by position
- [ ] Historical pot statistics and analytics
- [ ] Pot visualization enhancements

### 12.3 Testing Strategy

**Test-Driven Approach**:
1. Write failing tests for each edge case
2. Verify existing implementation passes or fails
3. Fix implementation if needed
4. Verify all tests pass
5. Add integration tests
6. Add UI tests (if applicable)

**Test Execution Order**:
1. Unit tests (fast, run on every change)
2. Integration tests (slower, run on commit)
3. Manual tests (before release)
4. Performance tests (periodic)

### 12.4 Rollout Plan

**Stage 1: Internal Testing**
- Deploy to development environment
- Run automated test suite
- Manual testing by development team
- Fix any discovered issues

**Stage 2: Beta Testing**
- Deploy to staging environment
- Invite select users for testing
- Monitor for edge cases in real scenarios
- Collect feedback

**Stage 3: Production Release**
- Deploy to production
- Monitor closely for first 24-48 hours
- Be prepared to hotfix if critical issues found
- Collect usage metrics

### 12.5 Success Metrics

**Functional Metrics:**
- Zero chip loss/creation in pot calculations
- 100% of side pot scenarios handled correctly
- All edge cases covered by tests

**Performance Metrics:**
- < 10ms side pot calculation time
- < 100ms total pot award time
- Zero memory leaks over extended play

**User Experience Metrics:**
- Clear pot display understood by players
- Showdown animations complete without confusion
- Hand history accurately reflects pot awards

---

## 13. Implementation Guidance for LLM Code Generation

This section provides detailed, step-by-step guidance for implementing side pots in the current codebase. This guidance is specifically designed for LLM-based code generation tools.

### 13.1 Current State Assessment

**✅ Already Implemented:**

The codebase already has a fully functional side pot implementation:

- **`PotManager` class** (`src/CardGames.Poker/Betting/PotManager.cs`) contains:
  - `AddContribution(string playerName, int amount)` - Tracks player contributions
  - `CalculateSidePots(IEnumerable<PokerPlayer> players)` - Creates side pots based on all-in levels
  - `AwardPots(Func<IEnumerable<string>, IEnumerable<string>> determineWinners)` - Awards pots at showdown
  - `RemovePlayerEligibility(string playerName)` - Handles folds

- **`BettingRound` class** (`src/CardGames.Poker/Betting/BettingRound.cs`) integrates with PotManager:
  - Calls `_potManager.AddContribution()` for each bet, call, raise, and all-in action
  - Calls `_potManager.RemovePlayerEligibility()` when a player folds

- **Game implementations** (e.g., `FiveCardDrawGame.cs`, `HoldEmGame.cs`) use PotManager:
  - Call `_potManager.CalculateSidePots()` after betting rounds complete
  - Call `_potManager.AwardPots()` at showdown

### 13.2 What Needs to Be Implemented

The **core logic is complete**. Implementation work focuses on:

1. **API Enhancement**: Expose pot state to clients (main + side pots)
2. **UI Implementation**: Display side pots in the web interface
3. **SignalR Events**: Broadcast pot state changes in real-time
4. **Testing**: Comprehensive test coverage for edge cases
5. **Documentation**: Update game rules and help documentation

### 13.3 Step-by-Step Implementation Guide

#### Step 1: Add Pot DTOs to Contracts Project

**Location**: `src/CardGames.Contracts/Poker/PotDto.cs` (create new file)

**Action**: Create data transfer objects for exposing pot information to API clients.

**Code Template**:
```csharp
namespace CardGames.Contracts.Poker;

/// <summary>
/// Represents a single pot (main or side pot) in a poker hand.
/// </summary>
public class PotDto
{
    /// <summary>
    /// Pot identifier: 0 = main pot, 1+ = side pots
    /// </summary>
    public int PotNumber { get; set; }
    
    /// <summary>
    /// Total chips in this pot
    /// </summary>
    public int Amount { get; set; }
    
    /// <summary>
    /// Player names eligible to win this pot
    /// </summary>
    public List<string> EligiblePlayers { get; set; } = [];
    
    /// <summary>
    /// Human-readable name: "Main Pot", "Side Pot", "Side Pot #2", etc.
    /// </summary>
    public string DisplayName { get; set; }
}

/// <summary>
/// Current pot state for a poker hand
/// </summary>
public class PotStateDto
{
    /// <summary>
    /// Total chips across all pots
    /// </summary>
    public int TotalAmount { get; set; }
    
    /// <summary>
    /// List of all pots (main + side pots)
    /// </summary>
    public List<PotDto> Pots { get; set; } = [];
    
    /// <summary>
    /// Total contribution per player for current hand
    /// </summary>
    public Dictionary<string, int> PlayerContributions { get; set; } = [];
}
```

**Implementation Notes**:
- Place in `CardGames.Contracts` project for shared use between API and Web
- Keep DTOs simple with public setters for serialization
- Use `List<>` instead of `IReadOnlyList<>` for JSON serialization compatibility

#### Step 2: Create Pot State Mapper

**Location**: `src/CardGames.Poker/Betting/PotManagerExtensions.cs` (create new file)

**Action**: Add extension method to convert `PotManager` internal state to DTOs.

**Code Template**:
```csharp
using CardGames.Contracts.Poker;

namespace CardGames.Poker.Betting;

public static class PotManagerExtensions
{
    /// <summary>
    /// Converts PotManager internal state to a DTO for API/UI consumption.
    /// </summary>
    public static PotStateDto ToPotStateDto(this PotManager potManager)
    {
        var pots = potManager.Pots;
        var potDtos = new List<PotDto>();
        
        for (int i = 0; i < pots.Count; i++)
        {
            var pot = pots[i];
            var displayName = i == 0 
                ? "Main Pot" 
                : pots.Count == 2 
                    ? "Side Pot" 
                    : $"Side Pot #{i}";
            
            potDtos.Add(new PotDto
            {
                PotNumber = i,
                Amount = pot.Amount,
                EligiblePlayers = pot.EligiblePlayers.ToList(),
                DisplayName = displayName
            });
        }
        
        var contributions = new Dictionary<string, int>();
        // Note: PotManager doesn't currently expose contributions.
        // If needed, add a public property to PotManager:
        // public IReadOnlyDictionary<string, int> GetContributions() => _contributions;
        
        return new PotStateDto
        {
            TotalAmount = potManager.TotalPotAmount,
            Pots = potDtos,
            PlayerContributions = contributions
        };
    }
}
```

**Implementation Notes**:
- If you need to expose player contributions, add a method to `PotManager`:
  ```csharp
  public IReadOnlyDictionary<string, int> GetPlayerContributions()
  {
      return _contributions;
  }
  ```

#### Step 3: Enhance Game State Response to Include Pot State

**Location**: Existing game state endpoints in API project

**Action**: Add `PotState` property to game state responses.

**Example for Five Card Draw**:

Find the game state DTO (likely in `CardGames.Contracts` or response models) and add:

```csharp
public class GameStateResponse
{
    // ... existing properties ...
    
    /// <summary>
    /// Current pot state including main pot and side pots
    /// </summary>
    public PotStateDto PotState { get; set; }
}
```

Update the API endpoint handler to populate this:

```csharp
// In your API handler where you build game state response
var response = new GameStateResponse
{
    // ... existing mappings ...
    
    PotState = game.PotManager.ToPotStateDto()
};
```

**Implementation Notes**:
- The exact location depends on your API structure
- Look for existing endpoints that return game state (e.g., `GET /api/games/{gameId}`)
- Ensure `PotManager` instance is accessible from game object

#### Step 4: Add SignalR Event for Pot State Updates

**Location**: SignalR Hub (e.g., `CardGames.Poker.Api/Hubs/GameHub.cs`)

**Action**: Add event broadcasting for pot state changes.

**Code Template**:
```csharp
// In your SignalR Hub class
public async Task BroadcastPotStateUpdate(string gameId, PotStateDto potState)
{
    await Clients.Group(gameId).SendAsync("PotStateUpdated", potState);
}
```

**Integration Point**: Call this after `CalculateSidePots()` in game logic:

```csharp
// In game implementation (e.g., FiveCardDrawGame.cs)
// After this line:
_potManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));

// Add:
var potState = _potManager.ToPotStateDto();
await _gameHub.BroadcastPotStateUpdate(GameId, potState);
```

**Implementation Notes**:
- Requires dependency injection of the Hub context into game classes
- Alternative: Raise an event from game that API layer handles
- Consider using domain events pattern if already in use

#### Step 5: Implement UI Components (Blazor)

**Location**: `src/CardGames.Poker.Web/Components/` (or similar)

**Action**: Create Blazor component to display pots.

**Code Template**:

Create `PotDisplay.razor`:
```razor
@if (PotState != null && PotState.Pots.Any())
{
    <div class="pot-container">
        @foreach (var pot in PotState.Pots)
        {
            <div class="pot-display @(pot.PotNumber == 0 ? "main-pot" : "side-pot")">
                <div class="pot-label">@pot.DisplayName</div>
                <div class="pot-amount">$@pot.Amount</div>
                @if (ShowEligibility && pot.EligiblePlayers.Any())
                {
                    <div class="pot-eligible">
                        <small>Eligible: @string.Join(", ", pot.EligiblePlayers)</small>
                    </div>
                }
            </div>
        }
    </div>
}

@code {
    [Parameter]
    public PotStateDto? PotState { get; set; }
    
    [Parameter]
    public bool ShowEligibility { get; set; } = false;
}
```

Create `PotDisplay.razor.css`:
```css
.pot-container {
    display: flex;
    flex-direction: column;
    gap: 10px;
    align-items: center;
    margin: 20px 0;
}

.pot-display {
    border: 2px solid #4CAF50;
    border-radius: 8px;
    padding: 15px;
    min-width: 150px;
    text-align: center;
    background: rgba(76, 175, 80, 0.1);
}

.pot-display.side-pot {
    border-color: #2196F3;
    background: rgba(33, 150, 243, 0.1);
}

.pot-label {
    font-weight: bold;
    font-size: 14px;
    color: #666;
}

.pot-amount {
    font-size: 24px;
    font-weight: bold;
    color: #333;
    margin: 5px 0;
}

.pot-eligible {
    margin-top: 8px;
    font-size: 12px;
    color: #666;
}
```

**Usage in TablePlay.razor**:
```razor
<PotDisplay PotState="@currentGameState?.PotState" ShowEligibility="true" />
```

**Implementation Notes**:
- Adjust CSS to match your existing design system
- Consider adding animations for pot updates
- May need to adapt for your specific UI framework

#### Step 6: Update SignalR Client to Handle Pot Updates

**Location**: Client-side JavaScript/TypeScript or Blazor code-behind

**Action**: Subscribe to pot state update events.

**For Blazor**:
```csharp
// In your page component
protected override async Task OnInitializedAsync()
{
    await hubConnection.On<PotStateDto>("PotStateUpdated", async (potState) =>
    {
        currentGameState.PotState = potState;
        await InvokeAsync(StateHasChanged);
    });
}
```

**For JavaScript**:
```javascript
connection.on("PotStateUpdated", (potState) => {
    updatePotDisplay(potState);
});

function updatePotDisplay(potState) {
    // Update DOM elements showing pot information
    document.getElementById('total-pot').textContent = `$${potState.totalAmount}`;
    
    // Clear and rebuild pot list
    const potList = document.getElementById('pot-list');
    potList.innerHTML = '';
    
    potState.pots.forEach(pot => {
        const potElement = createPotElement(pot);
        potList.appendChild(potElement);
    });
}
```

#### Step 7: Update Hand History to Include Pot Details

**Location**: Hand history recording logic

**Action**: Store pot breakdown in hand history.

**Enhancement to Hand History Record**:
```csharp
public class HandHistoryEntry
{
    // ... existing fields ...
    
    /// <summary>
    /// Pot breakdown at showdown
    /// </summary>
    public List<PotHistoryDto> PotBreakdown { get; set; } = [];
}

public class PotHistoryDto
{
    public int PotNumber { get; set; }
    public int Amount { get; set; }
    public List<string> EligiblePlayers { get; set; } = [];
    public List<string> Winners { get; set; } = [];
    public Dictionary<string, int> Payouts { get; set; } = [];
}
```

**In Showdown Logic**:
```csharp
// After awarding pots
var potHistory = new List<PotHistoryDto>();
for (int i = 0; i < _potManager.Pots.Count; i++)
{
    var pot = _potManager.Pots[i];
    var winners = /* extract winners for this pot */;
    var payouts = /* extract payouts for this pot */;
    
    potHistory.Add(new PotHistoryDto
    {
        PotNumber = i,
        Amount = pot.Amount,
        EligiblePlayers = pot.EligiblePlayers.ToList(),
        Winners = winners,
        Payouts = payouts
    });
}

// Add to hand history
handHistory.PotBreakdown = potHistory;
```

#### Step 8: Add Comprehensive Tests

**Location**: `src/CardGames.Poker.Tests/Betting/PotManagerTests.cs` (or create)

**Action**: Add test cases for all edge cases documented in Section 10.

**Test Template**:
```csharp
using Xunit;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Tests.Betting;

public class PotManagerTests
{
    [Fact]
    public void CalculateSidePots_SimpleSidePot_OneAllIn_CreatesCorrectPots()
    {
        // Arrange
        var potManager = new PotManager();
        var players = new[]
        {
            new PokerPlayer("Pat", 0) { IsAllIn = true },  // All-in for $30
            new PokerPlayer("Rob", 70),
            new PokerPlayer("Sam", 70)
        };
        
        potManager.AddContribution("Pat", 30);
        potManager.AddContribution("Rob", 100);
        potManager.AddContribution("Sam", 100);
        
        // Act
        potManager.CalculateSidePots(players.Where(p => !p.HasFolded));
        
        // Assert
        Assert.Equal(2, potManager.Pots.Count);
        
        // Main pot: $30 x 3 = $90
        Assert.Equal(90, potManager.Pots[0].Amount);
        Assert.Equal(3, potManager.Pots[0].EligiblePlayers.Count);
        Assert.Contains("Pat", potManager.Pots[0].EligiblePlayers);
        Assert.Contains("Rob", potManager.Pots[0].EligiblePlayers);
        Assert.Contains("Sam", potManager.Pots[0].EligiblePlayers);
        
        // Side pot: $70 x 2 = $140
        Assert.Equal(140, potManager.Pots[1].Amount);
        Assert.Equal(2, potManager.Pots[1].EligiblePlayers.Count);
        Assert.Contains("Rob", potManager.Pots[1].EligiblePlayers);
        Assert.Contains("Sam", potManager.Pots[1].EligiblePlayers);
        Assert.DoesNotContain("Pat", potManager.Pots[1].EligiblePlayers);
    }
    
    [Fact]
    public void CalculateSidePots_MultipleAllIns_CreatesThreePots()
    {
        // Arrange
        var potManager = new PotManager();
        var players = new[]
        {
            new PokerPlayer("A", 0) { IsAllIn = true },  // $20
            new PokerPlayer("B", 0) { IsAllIn = true },  // $50
            new PokerPlayer("C", 0),                      // $100
            new PokerPlayer("D", 0)                       // $100
        };
        
        potManager.AddContribution("A", 20);
        potManager.AddContribution("B", 50);
        potManager.AddContribution("C", 100);
        potManager.AddContribution("D", 100);
        
        // Act
        potManager.CalculateSidePots(players.Where(p => !p.HasFolded));
        
        // Assert
        Assert.Equal(3, potManager.Pots.Count);
        
        // Pot 1: $20 x 4 = $80 (all eligible)
        Assert.Equal(80, potManager.Pots[0].Amount);
        Assert.Equal(4, potManager.Pots[0].EligiblePlayers.Count);
        
        // Pot 2: $30 x 3 = $90 (B, C, D eligible)
        Assert.Equal(90, potManager.Pots[1].Amount);
        Assert.Equal(3, potManager.Pots[1].EligiblePlayers.Count);
        Assert.DoesNotContain("A", potManager.Pots[1].EligiblePlayers);
        
        // Pot 3: $50 x 2 = $100 (C, D eligible)
        Assert.Equal(100, potManager.Pots[2].Amount);
        Assert.Equal(2, potManager.Pots[2].EligiblePlayers.Count);
        Assert.Contains("C", potManager.Pots[2].EligiblePlayers);
        Assert.Contains("D", potManager.Pots[2].EligiblePlayers);
    }
    
    // Add more tests for:
    // - Fold handling
    // - Odd chip division
    // - All players all-in
    // - Empty pots
    // - etc. (see Section 10 for complete list)
}
```

**Implementation Notes**:
- Create test cases for all edge cases in Section 10
- Test both `CalculateSidePots()` and `AwardPots()` methods
- Verify pot totals always equal sum of contributions

### 13.4 Integration Checklist

When implementing side pots, follow this checklist:

- [ ] **Step 1**: Add PotDto classes to Contracts project
- [ ] **Step 2**: Create PotManagerExtensions with ToPotStateDto() method
- [ ] **Step 3**: Enhance game state API responses with PotState property
- [ ] **Step 4**: Add SignalR event for pot state broadcasts
- [ ] **Step 5**: Implement PotDisplay.razor component
- [ ] **Step 6**: Update SignalR client to handle pot updates
- [ ] **Step 7**: Enhance hand history with pot breakdown
- [ ] **Step 8**: Add comprehensive unit tests
- [ ] **Verification**: Run existing game tests to ensure no regression
- [ ] **Verification**: Manual test with multiple all-in scenarios
- [ ] **Documentation**: Update user-facing game rules documentation

### 13.5 Code Locations Reference

Quick reference for where to find and modify code:

| Component | File Path | Action |
|-----------|-----------|--------|
| **Core Logic** | `src/CardGames.Poker/Betting/PotManager.cs` | ✅ Already complete - no changes needed |
| **Betting Integration** | `src/CardGames.Poker/Betting/BettingRound.cs` | ✅ Already integrated - no changes needed |
| **Game Implementations** | `src/CardGames.Poker/Games/*/Game.cs` | ✅ Already calling CalculateSidePots() - verify all games |
| **DTOs** | `src/CardGames.Contracts/Poker/PotDto.cs` | ➕ Create new file |
| **Extensions** | `src/CardGames.Poker/Betting/PotManagerExtensions.cs` | ➕ Create new file |
| **API Responses** | `src/CardGames.Poker.Api/` | ✏️ Enhance existing game state responses |
| **SignalR Hub** | `src/CardGames.Poker.Api/Hubs/GameHub.cs` | ✏️ Add pot state broadcast method |
| **UI Component** | `src/CardGames.Poker.Web/Components/PotDisplay.razor` | ➕ Create new component |
| **Client Handler** | `src/CardGames.Poker.Web/Pages/TablePlay.razor` | ✏️ Subscribe to pot state updates |
| **Tests** | `src/CardGames.Poker.Tests/Betting/PotManagerTests.cs` | ➕ Add comprehensive tests |

**Legend**:
- ✅ Already complete
- ➕ Create new
- ✏️ Modify existing

### 13.6 Common Pitfalls and Solutions

**Pitfall 1: Forgetting to Call CalculateSidePots()**
- **Problem**: Side pots not created even when players are all-in
- **Solution**: Ensure `CalculateSidePots()` is called after EACH betting round where all-ins occurred
- **Check**: Look for pattern: after `BettingRound.IsComplete`, call `potManager.CalculateSidePots(players)`

**Pitfall 2: Pot Total Mismatch**
- **Problem**: Sum of pots doesn't equal total contributions
- **Solution**: The PotManager already handles this in lines 164-172 of PotManager.cs
- **Check**: Verify `_potManager.TotalPotAmount` equals sum of all player contributions

**Pitfall 3: Not Removing Folded Players from Eligibility**
- **Problem**: Folded players still eligible for pots
- **Solution**: `BettingRound` already calls `RemovePlayerEligibility()` on fold (line 189)
- **Check**: Verify fold action triggers `_potManager.RemovePlayerEligibility(playerName)`

**Pitfall 4: UI Not Updating After Side Pot Creation**
- **Problem**: UI shows single pot even after side pots created
- **Solution**: Ensure SignalR event is broadcast after `CalculateSidePots()`
- **Check**: Add logging before/after pot calculation to verify events fire

**Pitfall 5: Treating All-In as Fold**
- **Problem**: All-in players excluded from showdown
- **Solution**: Check `!player.HasFolded` not `player.CanAct` when determining showdown participants
- **Check**: All-in players should be in `playersInHand` list at showdown

### 13.7 Testing Strategy

**Unit Test Priority Order**:
1. Basic side pot creation (1 all-in)
2. Multiple all-ins with different amounts
3. Fold handling (before and after all-in)
4. Pot award with different winners per pot
5. Edge cases (odd chips, all players all-in, etc.)

**Integration Test Approach**:
```csharp
[Fact]
public async Task IntegrationTest_FullHandWithSidePots()
{
    // Setup game with 3 players: short stack, medium, large
    var game = new FiveCardDrawGame(
        players: new[] 
        { 
            ("Short", 50), 
            ("Medium", 100), 
            ("Large", 200) 
        },
        ante: 10,
        minBet: 10
    );
    
    // Play through hand with all-in scenario
    game.StartNewHand();
    
    // First betting round
    game.ProcessAction("Short", BettingActionType.AllIn); // $50 total
    game.ProcessAction("Medium", BettingActionType.Raise, 100); // $100 total
    game.ProcessAction("Large", BettingActionType.Call); // $100 total
    
    // Verify side pots created
    var potState = game.PotManager.ToPotStateDto();
    Assert.Equal(2, potState.Pots.Count);
    Assert.Equal(150, potState.Pots[0].Amount); // Main: $50 x 3
    Assert.Equal(100, potState.Pots[1].Amount); // Side: $50 x 2
    
    // Continue to showdown and verify awards
    // ... (complete betting round, draw, second betting round)
    
    var result = game.PerformShowdown();
    Assert.True(result.Success);
    
    // Verify payouts make sense based on hand strengths
    // Different players can win different pots
}
```

**Manual Test Scenarios**:
1. **Scenario A**: Three players, one short-stacked goes all-in preflop
2. **Scenario B**: Four players, two different all-in amounts
3. **Scenario C**: All-in on flop, continued betting on turn/river
4. **Scenario D**: Player folds after another player is all-in

### 13.8 Debugging Tips

**Enable Detailed Logging**:
```csharp
// Add to PotManager.CalculateSidePots()
_logger.LogDebug("Calculating side pots. Contributions: {Contributions}", 
    string.Join(", ", _contributions.Select(kvp => $"{kvp.Key}=${kvp.Value}")));

_logger.LogDebug("Created {Count} pots: {Pots}", 
    _pots.Count,
    string.Join(", ", _pots.Select((p, i) => 
        $"Pot {i}: ${p.Amount} (eligible: {string.Join(",", p.EligiblePlayers)})")));
```

**Verify Pot State at Key Points**:
1. After each call to `AddContribution()`
2. After `CalculateSidePots()`
3. Before `AwardPots()`
4. After each pot is awarded

**Use Assertions**:
```csharp
// Add defensive checks
System.Diagnostics.Debug.Assert(
    _potManager.TotalPotAmount == _gamePlayers.Sum(p => p.Player.CurrentBet),
    "Pot total must equal sum of player bets");
```

---

## 14. References and Resources

### 14.1 Internal Documentation
- `ARCHITECTURE.md` - System architecture overview
- `ADDING_NEW_GAMES.md` - Game implementation guide
- `LeaveTable.md` - Example of comprehensive requirements document

### 14.2 Code References
- `src/CardGames.Poker/Betting/PotManager.cs` - Current implementation
- `src/CardGames.Poker/Betting/PokerPlayer.cs` - Player state management
- `src/CardGames.Poker/Betting/BettingRound.cs` - Betting logic

### 14.3 External Resources
- Poker game rules and side pot mechanics
- No-limit hold'em tournament director association rules
- Mathematical principles of pot distribution

---

## Appendix A: Glossary

**All-In**: When a player bets all their remaining chips and cannot bet further.

**Side Pot**: Additional pot(s) created when players bet beyond an all-in player's contribution.

**Main Pot**: The primary pot that all active players (who haven't folded) are eligible to win.

**Eligibility**: A player's qualification to win a specific pot based on their contribution level.

**Contribution**: The total amount a player has bet into the pot during a hand.

**Layer**: Conceptual "level" of betting used to construct pots (e.g., $0-$20, $20-$50).

**Showdown**: The final phase where remaining players reveal hands to determine winner(s).

**Pot-Limit**: Betting structure where maximum bet is the current pot size.

**No-Limit**: Betting structure where players can bet any amount up to their stack.

---

## Appendix B: Mathematical Examples

### Example A: Verify Pot Totals

**Given:**
- Player A: $30 contribution
- Player B: $100 contribution
- Player C: $100 contribution
- Total: $230

**Pot Calculation:**
- Main Pot: $30 × 3 = $90
- Side Pot: ($100 - $30) × 2 = $140
- Total: $90 + $140 = $230 ✓

### Example B: Three All-Ins

**Given:**
- Player A: $10 (all-in)
- Player B: $25 (all-in)
- Player C: $50 (all-in)
- Player D: $100
- Total: $185

**Pot Calculation:**
```
Pot 1 (Layer $0-$10):
  $10 × 4 players = $40
  Eligible: A, B, C, D

Pot 2 (Layer $10-$25):
  ($25 - $10) × 3 players = $15 × 3 = $45
  Eligible: B, C, D

Pot 3 (Layer $25-$50):
  ($50 - $25) × 2 players = $25 × 2 = $50
  Eligible: C, D

Pot 4 (Layer $50-$100):
  ($100 - $50) × 1 player = $50 × 1 = $50
  Eligible: D

Total: $40 + $45 + $50 + $50 = $185 ✓
```

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-01-16 | System | Initial comprehensive requirements document |

---

**End of Document**
