You are helping me add a brand-new multiplayer card game called “In-Between” to my existing online card game application. This is NOT a poker variant. It is a separate game type that must integrate cleanly into the existing architecture, game lobby flow, table creation flow, turn engine, deck handling, pot handling, multiplayer synchronization, persistence, and UI.

First, inspect the existing solution and identify the patterns already used for:

- game registration / game type selection
- table creation and game configuration
- game state models
- turn/action handling
- deck creation, shuffling, and dealing
- pot / chip accounting
- dealer rotation and action order
- multiplayer event broadcasting / real-time updates
- client UI rendering for a game table
- toasts / notifications
- end-of-game handling
- the existing “Screw Your Neighbor” implementation, especially how continuous deck usage and deck refresh are handled

Then implement In-Between by following the same architecture and conventions already used in the codebase. Reuse existing abstractions where appropriate instead of inventing a parallel system.

Game rules to implement
Game name:

- In-Between

Pre-game:

- Every player antes an amount chosen by the table creator
- The ante amount should be part of the game configuration for this table
- At game start, every player pays the ante into the pot

Turn flow:

- The player to the dealer’s left acts first
- There is a single shared deck face down in the center
- For the acting player, two cards are flipped face up, one on either side of the deck
- The player then chooses a bet amount from 0 up to the current pot, subject to the restriction below
- The bet means they are betting that the next card from the deck will rank strictly between the two face-up cards
- If they are correct, they collect that bet amount from the pot
- If they are incorrect, they pay that same amount into the pot
- If they pass by betting 0, then no third card is flipped
- After the player is done, action moves to the next player
- Each player gets their own fresh pair of face-up cards on their turn
- Players never hold cards in a hand in this game

Ace rules:

- If the first face-up card is an Ace, do NOT immediately flip the second card
- The acting player must choose whether that first Ace is treated as low or high
- Only after that choice is made should the second card be flipped
- If the second face-up card is an Ace, it is always high

Bet restriction:

- A player may NOT bet the entire current pot until every player has had one betting turn
- Implement this as a first-orbit restriction
- Passing with bet 0 still counts as that player having had their first betting turn
- After all seated active players have completed one turn, betting the full pot is allowed

POST rule:

- If the player makes a non-zero bet and the third card is exactly equal in rank to either of the two face-up cards, the player “POSTS”
- In that case, the player pays the pot double their bet
- This is not treated as a normal loss; it is its own outcome/state
- Make sure the pot and player balance updates reflect this correctly

Deck rules:

- Cards are NOT reshuffled between players
- Use one continuous deck for successive turns
- When there are three or fewer cards left in the deck, create a brand-new freshly shuffled deck and continue dealing to the next player
- Show a toast/notification in the UI when a new deck is being dealt
- Follow the same deck refresh pattern used in “Screw Your Neighbor”

Game end:

- The game continues until a player takes all the money from the pot
- End the game immediately when the pot reaches zero because a player won the remaining pot
- Reuse any existing winner/game-over flow where appropriate

Important implementation details
Please inspect the existing code and implement this feature end-to-end, including:

1. Game registration
   - Add In-Between as a selectable game
   - Make sure it appears anywhere game types are listed or described

2. Table configuration
   - Add ante amount to table/game setup for In-Between
   - Validate allowed ante values using existing conventions

3. Domain / engine logic
   - Add the full rules engine for In-Between
   - Model turn phases explicitly if the architecture supports it, such as:
     - waiting for turn
     - reveal first card
     - ace high/low decision when needed
     - reveal second card
     - waiting for bet/pass
     - resolve third card if bet > 0
     - apply payout / loss / POST
     - advance turn
   - Use existing rank/value utilities if present
   - Make sure “strictly between” is enforced correctly

4. Card comparison behavior
   - The third card must be strictly between the two boundary cards to win
   - If equal to either boundary card, that triggers POST
   - Otherwise it is a normal loss
   - Sort / normalize the two boundary values for comparison after Ace treatment is resolved

5. Player state
   - Players have no hand in this game
   - Any UI or state that assumes a player hand should be bypassed or hidden for this game type

6. Turn order
   - Start with the player to the dealer’s left
   - Continue normally around the table
   - Reuse existing dealer/action rotation patterns

7. Pot and chip accounting
   - Ante contributions go into the pot at game start
   - Winning a bet subtracts from the pot and adds to the player
   - Losing a bet adds to the pot and subtracts from the player
   - POST adds double the bet to the pot and subtracts it from the player
   - Enforce that players cannot bet more than they are allowed to bet under both pot and balance constraints
   - Reuse any existing chip/balance validation rules already present in the system

8. Real-time multiplayer synchronization
   - Broadcast the revealed cards, Ace choice, bet, outcome, pot updates, deck refresh, next turn, and game-over state to all connected players/spectators using existing patterns
   - Make sure hidden information rules are respected, although this game appears to be fully public per turn

9. UI
   - Build the game table UI for In-Between using existing game screen patterns
   - Show:
     - current pot
     - acting player
     - dealer
     - two face-up boundary cards for the current turn
     - Ace low/high choice prompt when required
     - bet controls
     - pass option
     - turn result (win / lose / POST)
     - deck refresh toast
     - winner / game-over state
   - Since players have no hand, do not render player hand areas in the normal way for this game, unless a shared reusable layout requires placeholders.
   - We will need to alter the DrawPanel so that, when playing In-Between, the player can choose to pass or bet an amount up to the pot (but no more)

10. Validation and safeguards

- Prevent actions when it is not the player’s turn
- Prevent betting before the second card is available
- Prevent second-card reveal before Ace high/low decision when the first card is an Ace
- Prevent illegal full-pot bets during the first orbit
- Prevent negative bets or bets over the pot or over the player’s allowed funds
- Ensure deck refresh happens before a turn begins whenever the remaining deck has three or fewer cards

11. Persistence / reconnection

- Ensure game state can survive reconnects/reloads using the same persistence pattern as existing games
- Rehydrating a live In-Between game should restore the exact current phase, visible cards, pot, active player, deck state, first-orbit restriction state, and any pending Ace choice

