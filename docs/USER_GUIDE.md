# CardGames Poker User Guide

Welcome to CardGames Poker! This guide covers all player-facing features and controls for playing poker in real-time.

## Table of Contents

- [Getting Started](#getting-started)
- [Game Variants](#game-variants)
- [Joining a Table](#joining-a-table)
- [The Game Interface](#the-game-interface)
- [Betting Actions](#betting-actions)
- [Turn Timer and Time Bank](#turn-timer-and-time-bank)
- [Showdown](#showdown)
- [Chat and Communication](#chat-and-communication)
- [Settings and Preferences](#settings-and-preferences)
- [Glossary](#glossary)

---

## Getting Started

### Account Requirements

To play at CardGames Poker, you need:
- A registered account
- A minimum chip balance (varies by table stakes)

### Navigating the Lobby

The lobby shows all available tables with the following information:
- **Table Name** - The name of the table
- **Variant** - The poker variant being played
- **Stakes** - Small Blind / Big Blind structure
- **Seats** - Occupied seats / Maximum seats
- **Average Pot** - Typical pot size at the table

---

## Game Variants

CardGames Poker supports multiple poker variants:

### Texas Hold'em

The most popular poker variant. Each player receives:
- **2 hole cards** (private)
- **5 community cards** (shared)

You can use any combination of your hole cards and community cards to make the best 5-card hand. You may use 0, 1, or 2 hole cards.

**Betting Rounds:**
1. Preflop (after hole cards are dealt)
2. Flop (after 3 community cards)
3. Turn (after 4th community card)
4. River (after 5th community card)

### Omaha

Similar to Hold'em but with key differences:
- **4 hole cards** (private)
- **5 community cards** (shared)

**Important:** You **must use exactly 2 hole cards** and **exactly 3 community cards** to make your hand.

### Seven Card Stud

A classic stud poker variant:
- **7 cards per player** (3 face-down, 4 face-up)
- No community cards
- Make the best 5-card hand from your 7 cards

**Betting Rounds:**
1. Third Street (2 down + 1 up)
2. Fourth Street (+ 1 up card)
3. Fifth Street (+ 1 up card)
4. Sixth Street (+ 1 up card)
5. Seventh Street (+ 1 down card)

### Kings and Lows (Draw Poker)

A five card draw variant with wild cards:
- **Kings are always wild**
- **Lowest card(s) in your hand are also wild**
- Players can discard up to 5 cards and draw new ones

**Special Rules:**
- Drop or Stay: Instead of betting, players simultaneously choose to stay in or drop out
- Player vs Deck: If only one player stays, they compete against a hand from the deck
- Losers Match Pot: Losing players must match the pot for the next hand

---

## Joining a Table

### Finding a Table

1. Browse available tables in the lobby
2. Filter by:
   - **Variant** - Texas Hold'em, Omaha, etc.
   - **Stakes** - Low, Medium, High
   - **Seats Available** - Tables with open seats
   - **Limit Type** - No Limit, Pot Limit, Fixed Limit

### Selecting a Seat

When you join a table:
1. The seat selection interface appears
2. Available seats are highlighted
3. Click on an available seat to select it
4. Enter your buy-in amount (within table limits)
5. Confirm to take your seat

### Quick Join

Use Quick Join to automatically find and join a suitable table:
1. Select your preferred variant
2. Choose your stake level
3. Click "Quick Join"
4. The system finds the best available table

### Waiting List

If all seats are full:
1. Join the waiting list for that table
2. You'll receive a notification when a seat becomes available
3. You have a limited time to accept the seat offer
4. If you don't respond, the next player on the list is notified

---

## The Game Interface

### Table Layout

```
        [Community Cards Area]
              [Pot: $100]
              
   [P3]                      [P4]
          [Dealer Button]
[P2]                              [P5]
   
   [P1]          YOU          [P6]
        [Your Hole Cards]
        [Action Buttons]
```

### Information Displays

| Element | Description |
|---------|-------------|
| **Hole Cards** | Your private cards (visible only to you) |
| **Community Cards** | Shared cards in the center |
| **Pot** | Total chips in the current pot |
| **Player Info** | Chip stack, current bet, status for each player |
| **Dealer Button** | Indicates the dealer position |
| **Blinds** | Small blind and big blind positions |

### Your Status Indicators

- **Green outline** - It's your turn to act
- **Gray cards** - You have folded
- **All-in indicator** - Player is all-in
- **Sitting out** - Player is temporarily away

---

## Betting Actions

### Available Actions

| Action | When Available | Description |
|--------|----------------|-------------|
| **Fold** | Always | Give up your hand and any chips in the pot |
| **Check** | When no bet to call | Pass the action without betting |
| **Call** | When there's a bet | Match the current bet |
| **Bet** | When no previous bet | Put chips into the pot |
| **Raise** | After someone bets | Increase the current bet |
| **All-In** | Always | Bet all remaining chips |

### Betting Controls

1. **Action Buttons** - Large buttons for quick actions (Fold, Check, Call, Raise)
2. **Bet Slider** - Drag to set your bet/raise amount
3. **Bet Input** - Type a specific amount
4. **Preset Buttons** - Quick amounts (1/2 Pot, 3/4 Pot, Pot, All-In)

### Betting Limits

**No Limit:**
- Minimum bet: Big blind amount
- Maximum bet: Your entire chip stack

**Pot Limit:**
- Minimum bet: Big blind amount
- Maximum bet: Current pot size (including your call)

**Fixed Limit:**
- Preflop/Flop: Small bet (equal to big blind)
- Turn/River: Big bet (2x big blind)
- Maximum 4 bets per round (bet, raise, re-raise, cap)

---

## Turn Timer and Time Bank

### Turn Timer

You have a limited time to act on each turn:
- A timer appears when it's your turn
- Default time: 30 seconds (varies by table)
- A warning sounds when time is running low

### Time Bank

Extra time for difficult decisions:
- Each player starts with a time bank (e.g., 60 seconds)
- Click "Use Time Bank" to add extra time
- Time bank replenishes slowly between hands
- Use sparingly for important decisions

### Timer Expiration

If your timer runs out:
- The system performs a default action
- If you can check: You automatically check
- Otherwise: You automatically fold

---

## Showdown

### When Showdown Occurs

Showdown happens when:
- All betting is complete
- Two or more players remain
- No one is all-in (or the river has been dealt)

### Showing Your Cards

At showdown:
1. Players reveal cards in order (last aggressor first)
2. You can choose to **Show** or **Muck**
3. If you have the winning hand, you must show
4. Losers may muck (hide) their cards

### Auto-Show Situations

Your cards are automatically shown when:
- You win the pot
- All remaining players are all-in
- You must show to claim the pot

### Winner Determination

The best 5-card hand wins. If tied:
- The pot is split equally among winners
- Odd chips go to the player closest to the dealer button

---

## Chat and Communication

### Table Chat

Communicate with other players at your table:
- Type messages in the chat input
- Messages appear in the chat window
- Keep chat friendly and respectful

### Chat Rules

- No abusive language
- No collusion discussions
- No excessive spam
- Breaking rules may result in a chat ban

### Muting Players

To mute a player:
1. Click on their avatar or name
2. Select "Mute Player"
3. You won't see their messages
4. To unmute: Access mute controls in settings

### System Announcements

The system broadcasts important events:
- Player joins/leaves
- Hand winners
- All-in situations
- Game state changes

---

## Settings and Preferences

### Table Settings

Access via the settings icon at your table:

| Setting | Options |
|---------|---------|
| **Card Display** | 4-color deck, 2-color deck |
| **Auto-Muck Losers** | On/Off - Automatically muck losing hands |
| **Sound Effects** | Volume controls, toggle on/off |
| **Animation Speed** | Slow, Normal, Fast |
| **Table Theme** | Classic, Modern, Dark |

### Account Settings

Manage in your profile:
- Display name
- Avatar
- Notification preferences
- Chat settings

---

## Glossary

| Term | Definition |
|------|------------|
| **All-In** | Betting all of your remaining chips |
| **Ante** | Forced bet from all players before the hand |
| **Big Blind** | Larger forced bet, typically 2x the small blind |
| **Board** | The community cards in Hold'em and Omaha |
| **Button** | The dealer position, rotates each hand |
| **Buy-In** | The chips you bring to the table |
| **Call** | Matching the current bet |
| **Check** | Passing when no bet is required |
| **Community Cards** | Shared cards in Hold'em/Omaha |
| **Flop** | The first three community cards |
| **Fold** | Giving up your hand |
| **Hand** | The cards you hold, or a complete round of play |
| **Heads-Up** | Playing against one other player |
| **Hole Cards** | Your private cards |
| **Muck** | Folding without showing your cards |
| **Pot** | All chips bet in the current hand |
| **Raise** | Increasing the current bet |
| **River** | The fifth and final community card |
| **Showdown** | When remaining players reveal their hands |
| **Side Pot** | A separate pot when a player goes all-in |
| **Small Blind** | Smaller forced bet to the left of the button |
| **Turn** | The fourth community card |
| **Wild Card** | A card that can represent any other card |

---

## Tips for New Players

1. **Start at low stakes** - Learn the game without risking too much
2. **Pay attention to position** - Acting later gives you more information
3. **Manage your bankroll** - Don't play with money you can't afford to lose
4. **Use the time bank wisely** - Save it for difficult decisions
5. **Watch and learn** - Observe how experienced players act
6. **Take breaks** - Long sessions can lead to poor decisions
7. **Review your hands** - Use hand history to improve your play

---

## Getting Help

If you need assistance:
- Check the FAQ section
- Contact support through your profile
- Report issues using the in-game feedback option

Enjoy playing at CardGames Poker!
