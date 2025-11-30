# Epic: Real-Time Table Interaction & Gameplay

## Epic Overview

Implement real-time multiplayer poker table interaction and gameplay via ASP.NET Blazor and SignalR. Simulate an authentic poker table environment with live seating, player actions, animations, cards dealt, chip movement, action timers, chat/muting, dealer button indicator, blinds/antes, and card visibility/privacy. Supports all core poker variants and works with API-driven game logic.

**Labels**: epic, enhancement, frontend, backend, signalR, api, ui, testing, docs  
**Estimate**: XL  
**Milestone**: MVP

---

## Feature Breakdown

### Feature 1: Real-Time SignalR Infrastructure Enhancement

**Description**: Extend the existing GameHub and GameHubService to support full table gameplay communication with robust connection management, player state synchronization, and event broadcasting.

**Priority**: High (Foundation for all other features)

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 1.1 | Extend GameHub with Table Actions | Add hub methods for player actions (fold, call, raise, check, bet, all-in) that broadcast to table groups | M | None |
| 1.2 | Add Connection-to-Player Mapping | Create service to track which connection IDs map to which players at which tables | S | None |
| 1.3 | Implement Player Reconnection Handling | Handle disconnection/reconnection scenarios preserving player state at table | M | 1.2 |
| 1.4 | Add Table State Synchronization | Create methods to sync full table state on connect/reconnect | M | 1.2 |
| 1.5 | Create Table-Specific Event Broadcasting | Implement private/public message routing (e.g., hole cards only to specific player) | M | 1.1 |
| 1.6 | Add GameHubService Client Events | Extend client-side service with all new game event handlers | M | 1.1, 1.5 |
| 1.7 | Implement Connection Health Monitoring | Add heartbeat/ping mechanism to detect stale connections | S | 1.2 |
| 1.8 | Write Unit Tests for Hub Methods | Test all hub methods and event broadcasting logic | M | 1.1-1.6 |

---

### Feature 2: Player Seating & Entry System

**Description**: Implement smooth and reliable player seating flows including seat selection, buy-in, standing up, and seat reservation.

**Priority**: High

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 2.1 | Create Seat Selection API Endpoint | API to request a specific seat at a table with validation | S | None |
| 2.2 | Implement Buy-In Flow | Allow players to specify buy-in amount within table min/max | M | 2.1 |
| 2.3 | Add Seat Reservation Timer | Reserve seat for player with configurable timeout | S | 2.1 |
| 2.4 | Create Stand Up / Leave Table Flow | Allow players to leave mid-hand (sit out) or between hands | M | 2.1 |
| 2.5 | Implement Sit Out / Sit Back In | Toggle player between active and sitting out states | S | 2.4 |
| 2.6 | Add Seat Change Request | Allow players to request seat changes between hands | S | 2.1 |
| 2.7 | Create Seat Status Events | SignalR events for seat taken, freed, reserved, player sitting out | M | 2.1-2.6, 1.5 |
| 2.8 | Build Seat Selection UI Component | Blazor component showing available/occupied seats with selection | M | 2.7 |
| 2.9 | Implement Buy-In Dialog Component | Modal for buy-in amount selection and confirmation | S | 2.2 |
| 2.10 | Write Integration Tests for Seating | Test seat selection, buy-in, and leave flows end-to-end | M | 2.1-2.6 |

---

### Feature 3: Dealer Button, Blinds & Antes

**Description**: Display and enforce dealer position movement, blind posting, and ante collection according to variant rules.

**Priority**: High

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 3.1 | Implement Dealer Button Rotation Logic | Move button clockwise after each hand per variant rules | M | None |
| 3.2 | Create Blind Posting Service | Auto-post small/big blinds based on button position | M | 3.1 |
| 3.3 | Implement Ante Collection | Collect antes from all players before hand (stud variants) | M | 3.1 |
| 3.4 | Add Missed Blind Tracking | Track and enforce posting missed blinds when player returns | M | 3.2 |
| 3.5 | Create Dead Button Rules | Handle scenarios when button would be on empty seat | S | 3.1, 3.2 |
| 3.6 | Add Dealer Button Event | SignalR event for button position changes | S | 3.1, 1.5 |
| 3.7 | Create Blind Posted Events | Events for blind/ante posting with amounts | S | 3.2, 3.3, 1.5 |
| 3.8 | Build Dealer Button UI Component | Visual dealer button with position indicator | S | 3.6 |
| 3.9 | Build Blinds Display Component | Show current blind levels and who posted | S | 3.7 |
| 3.10 | Write Tests for Button/Blind Logic | Test rotation, posting, missed blinds, edge cases | M | 3.1-3.5 |

---

### Feature 4: Player Actions & Betting System

**Description**: Implement all player betting actions with proper validation, pot calculation, and state management.

**Priority**: High

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 4.1 | Create Betting Action Processor | Process fold, check, call, bet, raise, all-in actions | L | 3.2 |
| 4.2 | Implement Action Validation | Validate actions against current game state and rules | M | 4.1 |
| 4.3 | Add Available Actions Calculator | Determine valid actions for current player with min/max amounts | M | 4.2 |
| 4.4 | Implement Pot Calculation | Calculate main pot and side pots for all-in scenarios | L | 4.1 |
| 4.5 | Create Action History Tracking | Record all actions for hand history and replay | M | 4.1 |
| 4.6 | Add Betting Round Manager | Control betting round flow (who acts, when round ends) | M | 4.1, 4.3 |
| 4.7 | Create Betting Action Events | SignalR events for each action type with pot updates | M | 4.1, 1.5 |
| 4.8 | Build Action Buttons Component | Fold, Check, Call, Bet, Raise buttons with amounts | M | 4.3, 4.7 |
| 4.9 | Build Bet Slider Component | Slider/input for selecting bet/raise amounts | M | 4.8 |
| 4.10 | Build Pot Display Component | Show main pot and side pots with amounts | M | 4.4, 4.7 |
| 4.11 | Implement Keyboard Shortcuts | Hotkeys for common actions (F=fold, C=check/call, etc.) | S | 4.8 |
| 4.12 | Write Tests for Betting Logic | Test all action types, validation, pot calculations | L | 4.1-4.6 |

---

### Feature 5: Card Dealing & Visibility

**Description**: Implement card dealing animations and enforce proper card visibility rules (hole cards private, community cards public, mucked cards hidden).

**Priority**: High

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 5.1 | Create Card Dealing Service | Deal cards according to variant rules (hole, community, stud) | M | None |
| 5.2 | Implement Card Visibility Rules | Enforce who can see which cards based on variant | M | 5.1 |
| 5.3 | Add Burn Card Handling | Track and optionally show burn cards | S | 5.1 |
| 5.4 | Create Private Card Events | Send hole cards only to owning player's connection | M | 5.2, 1.5 |
| 5.5 | Create Community Card Events | Broadcast community cards to all table players | S | 5.1, 1.5 |
| 5.6 | Handle Stud Face-Up/Down Cards | Support mixed visibility for stud variants | M | 5.2 |
| 5.7 | Build Card Component | Single card display with front/back states | M | None |
| 5.8 | Build Card Animation System | Animate cards dealing from deck to positions | L | 5.7 |
| 5.9 | Build Hole Cards Display | Show player's hole cards in private area | M | 5.4, 5.7 |
| 5.10 | Build Community Cards Display | Show board cards (flop, turn, river) | M | 5.5, 5.7 |
| 5.11 | Build Opponent Cards Display | Show face-down cards for opponents, face-up for stud | M | 5.6, 5.7 |
| 5.12 | Write Tests for Card Visibility | Test all visibility scenarios per variant | M | 5.1-5.6 |

---

### Feature 6: Action Timer System

**Description**: Implement configurable action timers that trigger default actions (fold/check) when expired.

**Priority**: Medium

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 6.1 | Create Timer Service | Server-side timer management per player turn | M | 4.6 |
| 6.2 | Implement Timeout Actions | Auto-fold or auto-check when timer expires | M | 6.1, 4.1 |
| 6.3 | Add Time Bank Feature | Extra time bank that players can use | M | 6.1 |
| 6.4 | Create Timer Configuration | Table settings for turn time and time bank | S | 6.1 |
| 6.5 | Add Timer Events | SignalR events for timer start, tick, warning, expired | M | 6.1, 1.5 |
| 6.6 | Build Timer Display Component | Visual countdown timer with warnings | M | 6.5 |
| 6.7 | Build Time Bank UI | Show remaining time bank with activation button | S | 6.3, 6.6 |
| 6.8 | Write Tests for Timer Logic | Test timeouts, time bank, edge cases | M | 6.1-6.4 |

---

### Feature 7: Chip Animations & Pot Management

**Description**: Animate chip movements for bets, calls, pot collection, and winnings distribution.

**Priority**: Medium

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 7.1 | Create Chip Animation Events | Events describing chip movements with source/destination | M | 4.1, 1.5 |
| 7.2 | Build Chip Stack Component | Visual chip stack representation | M | None |
| 7.3 | Build Chip Animation System | Animate chips moving between positions | L | 7.1, 7.2 |
| 7.4 | Implement Bet Animation | Chips move from stack to betting area | M | 7.3 |
| 7.5 | Implement Pot Collection Animation | Chips gather to pot at round end | M | 7.3 |
| 7.6 | Implement Win Animation | Chips distribute from pot to winner(s) | M | 7.3 |
| 7.7 | Add Chip Denomination Display | Show chip colors/values for different amounts | S | 7.2 |
| 7.8 | Write Animation Integration Tests | Test animation sequences and timing | M | 7.3-7.6 |

---

### Feature 8: Player Chat & Communication

**Description**: Implement table chat with message history, mute functionality, and appropriate moderation options.

**Priority**: Medium

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 8.1 | Create Chat Message Service | Handle sending and receiving chat messages at table | M | 1.1 |
| 8.2 | Add Chat Message Validation | Filter inappropriate content, rate limiting | M | 8.1 |
| 8.3 | Implement Player Mute Feature | Allow players to mute specific other players | S | 8.1 |
| 8.4 | Add Table-Wide Mute Option | Allow disabling chat for entire table | S | 8.1 |
| 8.5 | Create Chat Events | SignalR events for new messages, system announcements | S | 8.1, 1.5 |
| 8.6 | Build Chat Window Component | Scrollable chat history with input field | M | 8.5 |
| 8.7 | Build Mute Controls UI | UI to mute/unmute players or disable chat | S | 8.3, 8.4 |
| 8.8 | Add System Announcements | Auto-messages for game events (player joins, wins pot) | M | 8.5 |
| 8.9 | Write Tests for Chat System | Test messaging, muting, rate limiting | M | 8.1-8.4 |

---

### Feature 9: Showdown & Card Reveal

**Description**: Implement proper showdown mechanics including reveal order, muck options, and winner determination display.

**Priority**: High

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 9.1 | Extend Showdown Service | Complete showdown flow with reveal order per variant rules | M | Existing ShowdownService |
| 9.2 | Implement Muck Option | Allow losing players to muck instead of showing | M | 9.1 |
| 9.3 | Add Auto-Reveal for Winners | Automatically show winning hand | S | 9.1 |
| 9.4 | Handle All-In Showdown | Run out board and show all hands when all-in | M | 9.1 |
| 9.5 | Integrate with Hand Evaluation | Display winning hand type and cards used | M | 9.1 |
| 9.6 | Create Showdown Events | Events for reveal sequence, muck, winner announcement | M | 9.1, 1.5 |
| 9.7 | Build Showdown Animation | Sequential reveal of hands with winner highlight | L | 9.6, 5.8 |
| 9.8 | Build Winner Announcement UI | Display winner, hand type, pot amount won | M | 9.6 |
| 9.9 | Write Tests for Showdown Logic | Test reveal order, muck, split pots | M | 9.1-9.5 |

---

### Feature 10: Table UI Layout & Responsive Design

**Description**: Create the main poker table UI with responsive layout supporting different screen sizes and player counts.

**Priority**: High

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 10.1 | Design Table Layout System | Layout engine for 2-10 player positions | L | None |
| 10.2 | Build Main Table Component | Central table surface with positions around it | L | 10.1 |
| 10.3 | Build Player Position Component | Individual player area with cards, chips, info | M | 10.2 |
| 10.4 | Implement Responsive Scaling | Scale table for mobile/tablet/desktop | M | 10.2 |
| 10.5 | Add Current Player Highlighting | Highlight whose turn it is to act | S | 10.3, 4.6 |
| 10.6 | Build Player Info Overlay | Show player name, stack, status, avatar | M | 10.3 |
| 10.7 | Create Table Settings Panel | UI for adjusting table preferences | M | None |
| 10.8 | Add Sound Effects System | Audio for actions, wins, notifications | M | None |
| 10.9 | Implement Visual Themes | Light/dark mode, custom table colors | M | 10.2 |
| 10.10 | Write UI Component Tests | Test rendering and responsiveness | M | 10.1-10.6 |

---

### Feature 11: Game State Management & Sync

**Description**: Ensure robust state management that keeps all clients synchronized with server game state.

**Priority**: High

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 11.1 | Create Client State Manager | Blazor service to manage local game state | M | None |
| 11.2 | Implement State Reconciliation | Handle conflicts between client and server state | M | 11.1 |
| 11.3 | Add Optimistic Updates | Show immediate feedback before server confirmation | M | 11.1 |
| 11.4 | Create State Snapshot System | Capture and restore game state at any point | M | 11.1 |
| 11.5 | Implement Event Replay | Replay events to rebuild state after reconnect | M | 11.4 |
| 11.6 | Add State Validation | Validate client state matches server periodically | M | 11.2 |
| 11.7 | Handle Concurrent Actions | Resolve race conditions in action processing | M | 11.1 |
| 11.8 | Write State Management Tests | Test sync, reconciliation, replay scenarios | L | 11.1-11.7 |

---

### Feature 12: Variant-Specific Adaptations

**Description**: Ensure all UI and game logic adapts correctly to different poker variants (Hold'em, Omaha, Stud, etc.).

**Priority**: Medium

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 12.1 | Create Variant Config Loader | Load variant rules and configure game accordingly | M | Existing RuleSetDto |
| 12.2 | Adapt Card Display for Variants | Show correct number of hole/board cards per variant | M | 5.9-5.11, 12.1 |
| 12.3 | Adapt Betting for Variants | Apply correct limits (NL, PL, FL) and rules | M | 4.1, 12.1 |
| 12.4 | Support Stud Card Visibility | Show face-up cards for opponents in stud games | M | 5.6, 12.1 |
| 12.5 | Support Draw Game Mechanics | Add discard/draw UI for draw variants | L | 12.1 |
| 12.6 | Add Hi/Lo Split Display | Show split pot scenarios for hi/lo games | M | 4.4, 12.1 |
| 12.7 | Test All Supported Variants | Integration tests for each variant end-to-end | L | 12.1-12.6 |

---

### Feature 13: Documentation & Testing

**Description**: Comprehensive documentation and test coverage for all real-time features.

**Priority**: Medium

#### Tasks

| Task ID | Task Title | Description | Estimate | Dependencies |
|---------|------------|-------------|----------|--------------|
| 13.1 | Document SignalR API | Document all hub methods and events | M | Feature 1 |
| 13.2 | Create Integration Test Suite | End-to-end tests for complete gameplay flows | L | All Features |
| 13.3 | Write Performance Tests | Load testing for concurrent connections | M | Feature 1 |
| 13.4 | Create User Guide | Document player-facing features and controls | M | Features 2-10 |
| 13.5 | Document Architecture Decisions | ADRs for key design choices | M | All Features |
| 13.6 | Create Developer Setup Guide | Instructions for local development and testing | S | All Features |

---

## Dependency Graph

```
Feature 1 (SignalR Infrastructure)
    └── Feature 2 (Seating)
    └── Feature 3 (Blinds)
        └── Feature 4 (Betting)
            └── Feature 6 (Timer)
            └── Feature 7 (Chips)
    └── Feature 5 (Cards)
        └── Feature 9 (Showdown)
    └── Feature 8 (Chat)
    └── Feature 10 (Table UI)
    └── Feature 11 (State Management)
    └── Feature 12 (Variants)
        └── Feature 13 (Documentation)
```

---

## Suggested Implementation Order

### Phase 1: Foundation (Weeks 1-2)
1. **Feature 1**: SignalR Infrastructure Enhancement
2. **Feature 11**: Game State Management (core parts)
3. **Feature 10**: Table UI Layout (basic structure)

### Phase 2: Core Gameplay (Weeks 3-5)
4. **Feature 2**: Player Seating & Entry
5. **Feature 3**: Dealer Button, Blinds & Antes
6. **Feature 4**: Player Actions & Betting
7. **Feature 5**: Card Dealing & Visibility

### Phase 3: Polish & Features (Weeks 6-8)
8. **Feature 9**: Showdown & Card Reveal
9. **Feature 6**: Action Timer System
10. **Feature 7**: Chip Animations
11. **Feature 8**: Player Chat

### Phase 4: Variants & Testing (Weeks 9-10)
12. **Feature 12**: Variant-Specific Adaptations
13. **Feature 13**: Documentation & Testing

---

## Acceptance Criteria Summary

- [ ] Real-time table UI responds to game and player actions
- [ ] Player seating and entry flows are smooth and reliable
- [ ] Dealer position and blinds/antes correctly displayed and enforced
- [ ] Animations for cards/chips/pots trigger from server events
- [ ] Card visibility (hole, board, folded/mucked) rules always enforced
- [ ] Player chat works; mute/disable options available
- [ ] Action timer cleanly triggers default actions if expired
- [ ] All major poker variants (Hold'em, Omaha, Stud) supported
- [ ] State synchronization handles disconnects/reconnects gracefully
- [ ] Responsive design works on desktop and mobile devices

---

## Risk Assessment

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| SignalR connection instability | High | Medium | Implement robust reconnection, state replay |
| Race conditions in betting | High | Medium | Server-side action ordering, optimistic locks |
| Animation performance on mobile | Medium | Medium | Progressive enhancement, disable on low-end |
| Variant rule complexity | Medium | High | Thorough ruleset schema, extensive testing |
| State synchronization drift | High | Low | Periodic validation, full state snapshots |

---

## Technical Notes

### Existing Infrastructure to Leverage

1. **GameHub.cs**: Already has basic connection, join/leave game, showdown events
2. **GameEvents.cs**: Comprehensive event records for most game actions
3. **GameHubService.cs**: Client-side SignalR service with reconnection
4. **Tables Feature**: Existing table repository and seat management contracts
5. **Shared DTOs**: CardDto, PlayerDto, HandDto, BettingActionDto, etc.
6. **RuleSetDto**: Variant configuration schema already defined

### New Components Needed

1. **TableGameState**: Server-side state for active table game
2. **ActionProcessor**: Validates and processes player actions
3. **AnimationEventQueue**: Sequences animations for smooth playback
4. **ClientStateManager**: Blazor state container for game state
5. **TableComponent**: Main Blazor component for table rendering

---

## Estimated Total Effort

| Category | Tasks | Estimated Total |
|----------|-------|-----------------|
| Backend (API/SignalR) | 45 | 3-4 weeks |
| Frontend (Blazor) | 35 | 3-4 weeks |
| Testing | 15 | 2 weeks |
| Documentation | 6 | 1 week |
| **Total** | **101 tasks** | **9-11 weeks** |

*Note: These estimates assume one full-time developer. Parallel work by multiple developers could reduce calendar time significantly.*
