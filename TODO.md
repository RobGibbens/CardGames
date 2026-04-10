# TODO

- [ ] Animate the chips going to/from the pot
- [ ] Tony was trying to join during DC, it stole the choice from Russ
- [ ] DC - Eric joined, it glitched
- [x] Rabbit hunt

## Still to do

- [ ] Lobby - Filter by status
- [ ] Work on isolating games code more
- [ ] Either remove "confirm login" or set up email

- [ ] Add leagues
- [ ] Add seasons
  - [ ] Change UI - Hard to find seasons
  - [ ] Change UI - Hard to find events
  - [ ] Create event modal too big
  - [ ] Event doesn't need sequence, just order by date
  - [ ] Event should take blind and ante preferences
  - [ ] When there is an error, it doesn't show the message on the modal, it shows it behind it
- [ ] Add tournaments
  - [x] Active games should not be listed in upcoming also
  - [ ] Tournaments blinds should increase as time goes on
  - [ ] When the tournament ends, it just says "Waiting for Players" . We should have a nice summary page.
  - [x] League games should be noted on the lobby page
  - [x] Texas Hold Em, not collecting blinds
  - [x] Change the UI of the Schedule page, remove segmented control
  - [x] The description at the top of the tableplay should read "Tournament"
  - [x] When joining, automatically take a seat
  - [x] Default start time to now + 30 minutes
  - [x] Can't edit future tournament
  - [x] Can't delete future tournament
  - [x] Can't add a start time for tournament
  - [x] Validation errors should look better
  - [x] On the "Create Tournament" and "Create Cash Game" modals, add descriptions
  - [x] Allows creating tournaments in the past
  - [x] The edit and delete buttons on lobby are not styled correctly
  - [x] Upcoming events should show the variant
  - [x] Can't specify buy-in
  - [x] Tournaments on the lobby page don't allow you to start/join
  - [x] When the host clicks "Launch Play" it auto buys in for 100
  - [x] When the host clicks "Launch Play", send a signalr message to update the schedule and lobby pages
  - [x] When joining a tournament, you shouldn't be able to change the buy-in amount

- [ ] Productize it - deployment and hosting (Hetzner)

- [ ] Make sure that we're not showing the hand in the showdown when we're not supposed to
- [ ] Check for security issues
- [ ] Add a "Rules" property to each game
- [ ] Refactor TablePlay
- [ ] <https://github.com/leeoades/FunctionalStateMachine>
- [ ] Add individual control to sounds
- [ ] During Phil's mom, it plays the alert every time a player takes an action

### P1 - Can't play without these

- [ ] Test side pots
- [ ] Make sure all game types handle all ins correctly
- [ ] Test what happens when a player disconnects
- [ ] Really test all-ins, sidepots, etc.
- [ ] SYN - check for ties
- [ ] Kings and Lows - Should make it obvious when someone dropped
- [ ] If buy-in protection is on for a table, that should apply to adding money later
- [ ] Make sure that if a player leaves the game doesn't just keep going and taking all of their money.

### P2 - Important but not blocking play

- [ ] Be able to see a full hand history (including rabbit hunt, and folds)
- [ ] Make sure all calls are idempotent
- [ ] Continuous play should stop if all players disconnect
- [ ] During ties, the showdown should show the kicker
- [ ] Showdown overlay should handle split pots better
- [ ] Table should be able to scroll if the screen isn't big enough

### P3 - Nice to have

- [ ] There are a lot of extra database calls going on
- [ ] Hand history should auto size columns and allow resizing columns
- [ ] Hand history should paginate
- [ ] Endpoints shouldn't use generic Error responses

## When expanding beyond my friends

- [ ] Add a chat box
- [ ] Add the ability to mute other players in chat
- [ ] Add the ability to block other players
- [ ] Add a friends list
- [ ] Add the ability to invite friends to a table
- [ ] Add the ability to create private tables

## Add Games

### Hold 'Em Variants

- [ ] Add Omaha Hi/Lo
- [ ] Add Pittsburgh
- [ ] Add Squid Game
- [ ] Add Hall of Mirrors

- [x] Add Texas Hold'em
- [x] Add Omaha
- [x] Add The Bob Barker
- [x] Add Irish Hold'em
- [x] Add Hold the Baseball
- [x] Add Nebraska
- [x] Add South Dakota
- [x] Add Red River
- [x] Add Phil's Mom
- [x] Add Crazy Pineapple

### 7 Card Stud Variants

- [x] Add Seven Card Stud
- [x] Add Baseball
- [x] Add Follow the Queen
- [x] The Good, the Bad, and the Ugly
- [x] Add Razz
- [x] Pair Pressure
- [x] Tollbooth

### 5 Card Stud Variants

- [ ] Add Five Card Stud

### 5 Card Draw Variants

- [x] Five card draw
- [x] Add Twos, Jacks, Man with the Axe
- [x] Add Kings and Lows

### Other Poker Variants

- [ ] Add In Between
- [ ] Add Bzzjt
- [ ] Add Guts
- [x] Add Screw Your Neighbor

## Completed

- [x] FTQ second third street doesn't delay
- [x] Spin cards
- [x] Search for code == "KINGSANDLOWS" and refactor
- [x] Add a chip stack graph in the dashboard
- [x] Show current standings
- [x] Show hand history
- [x] Show the user's avatar on TableSeat and their name
- [x] Always make the logged in player the bottom seat
- [x] When discarding cards, leave the overlay up with the new cards for a few seconds
- [x] Add Dev Tunnels to Aspire
- [x] When winning, the overlay should show the loser's hand, and the payouts. It's showing the wrong winner.
- [x] Showdown overlay is not ordering cards
- [x] Check that setting the ante works correctly
- [x] Showdown does not show the description of the cards for the loser on the loser screen
- [x] Make the results overlay look better
- [x] Make sure the table says what game type it is
- [x] Add the ability to edit table settings if the table isn't started yet
- [x] Draw Phase overlay should show the current hand, and the new hand
- [x] Should be able to discard four cards IF you have an ace
- [x] Add Yahoo as Oauth provider
- [x] Add Microsoft as Oauth provider
- [x] Hand history is not displaying correctly
- [x] Dashboard needs to be bigger
- [x] I don't like how small the cards are and what they look like
- [x] Showdown overlay should show new card images
- [x] Kings and lows - setting an ante when creating the table doesn't come over
- [x] Draw Panel should show new card images
- [?] Drawing new cards isn't always dealing from the same deck
- [x] Kings and Lows - all drops revealed at the same time
- [x] Kings and Lows - odds aren't working right
- [x] Have option on lobby for a list, not graphics
- [x] Be able to soft delete a table if you're the host
- [x] Lobby should filter by game type
- [x] Make sure signalr isn't broadcasting to all players in all games
- [x] Table - make the table seat look like PokerStars
- [x] Add an action timer
- [x] Showdown should use better description of the cards (Pair of Aces vs One Pair)
- [x] FiveCardDraw - can't draw four with an Ace
- [x] Start a game, p1 goes all 1, p2 calls...game freezes
- [x] The betting action overlay. Raise to X doesn't work
- [x] The fields on the register page should be wider
- [x] Showdown overlay is not showing payouts correctly
- [x] Hamburger menu doesn't work
- [x] KAL - Waiting for showdown shows for new players
- [x] Kings and lows - can not join a table already in progress
- [x] Payouts not showing on Showdown for Kings and Lows
- [x] Make sure people can leave a table
- [x] Make sure people can sit out
- [x] Should always be able to see people's names
- [x] Seven card stud is ordering cards wrong
- [x] Five card draw is not ordering the cards
- [x] Five Card Draw - 3 players: 1 folds, 1 loses all chips, showdown says "wins by fold"
- [x] Five Card Draw - 3 players: 1 folds, 1 goes all in, still shows action panel even though one player has no remaining chips.
- [x] Sit out button is missing
- [x] Leaderboard should show first names
- [x] Make hand history more detailed
- [x] Add chip management to the dashboard (buy in, cash out, add chips)
- [x] Kings and Lows - hand history is showing the loser's hand incorrectly
- [x] Kings and Lows - Play for a while and the pot disappears
- [x] Kings and Lows - When going against the dealer, weird shit happens
- [x] Kings and Lows - Showdown disappeared immediately
- [x] Kings and Lows - Hand history is not working
- [x] Kings and Lows - don't deal to players that don't have enough chips to match the pot
- [x] SCS - when all players go all in - still need to draw cards
- [x] SCS - Showing the wrong cards (2 down, 1 up)
- [x] Kings and Lows - is always treating an Ace as high. (6,7,8,9,A should be a straight)
- [x] SCS - when a player has no chips, they're still getting dealt and can take action
- [x] SCS - odds should calculate based on future cards
- [x] Raise face up cards
- [x] FTQ should show what cards are wild
- [x] Wild cards is not showing the second wild card in FTQ
- [x] FTQ is misremembering other wild card after action
- [x] Make sure it's shuffling correctly
- [x] Have the ability for someone to take a seat and be dealt into the next hand
- [x] When you can't start the game because you're waiting for other players to sit, show a message
- [x] SCS - Choosing wrong first to act
- [x] Creating a deck and dealing is done in the StartHandCommandHandler, but it should be using CardGames.Core.Dealer
- [x] Add animation of dealing the cards
- [x] Allow them to upload an image when registering
- [x] Rearrange the register and login screens to make OAuth more obvious
- [x] Prevent joining a game if not enough chips
- [x] Tabs suck
- [x] Card UI isn't consistent
- [x] Pages load too slowly
- [x] Be able to move the action panel, draw panel, drop or stay panel
- [x] Dealer's choice round robin
- [x] The Draw Panel, Drop or Stay should show green wild cards
- [x] Baseball "Buy card" shouldn't display until the 4 actually is face up, then it should stop
- [x] Baseball (any wild seven card stud game) is not choosing the right "first to act"
- [x] Cashier ledger doesn't page
- [x] Don't auto start a new hand. Add a timer, allow pause, allow people to get out of the game or end the game
- [x] Dashboard should be scrollable
- [x] Kings and Lows - Things happen too fast
- [x] Add sound effects
- [x] Games with community cards (Hold Em, GBU) odds are wrong
- [x] Put the GameInfoOverlay under the ShowdownOverlay
- [x] Better contrast on the GameInfoOverlay close button
- [x] When joining any game, choose how many chips to bring in. Also be able to bring in more chips from your bank account.
- [x] Always show the hand description hover, just move it above the hand
- [x] Change font to MonoLisa
- [x] When taking a seat, just automatically take a seat and show the bring in panel
- [x] Add subtle icons to table felt
- [x] Make sure that when they leave a table, or close the browser, that their chips stay in their account.
- [x] When creating a table, just create it and make them press the Join button to join.
- [x] Reorganize the Create Table to group variants by type
- [x] Red River not showing extra card
- [x] When picking a game, support search
- [x] Allow the creator of the game the ability to turn off the odds calculator
- [x] Make sure players can keep a long running chip balance / only bring in a certain amount to a game
- [x] Allow table creator to turn off odds during game
- [x] Table settings should be dynamic when creating a table. Not all games have an ante or a minimum bet.
- [x] When playing Dealer's choice, filter out games that don't support the number of players
- [x] Allow each person to set their preferences for blinds when creating tables
- [x] SYN - When Pot is 0 still show it
- [x] SYN - show your remaining stacks
- [x] SYN - show everyone if you traded or stayed
- [x] SYN - no bring in, or lock it the set amount
- [x] SYN - doesn't need min bet when creating
- [x] SYN - When dealing you can briefly see all player's cards
- [x] SYN - After a few hands, table goes nuts, everybody animates a chip, cards redeal
- [x] SYN - Highlight winner(s)
- [x] SYN - only use one deck until it runs out
- [x] SYN - When running out of cards, deal a new deck
- [x] SYN - Speed up deal
- [x] SYN - Timer time out
- [x] SYN - Showdown too fast
- [x] SYN - Make sure you can't join in the middle of a game
- [x] SYN - Have end of game go to lobby
- [x] SYN - Animate trading
- [x] SYN - Animate trading with the deck
- [x] SYN - Animate chips to pot
- [x] SYN - Get a king and it still prompts you to trade
- [x] SYN - Not showing showdown
- [x] SYN - check for King block
- [x] SYN - Showdown showing email, not name
- [x] SYN in DC - Shows six stacks each - not removing them
- [x] Add rounders sounds
- [x] DC/SYN - Only setting 2 chips per player instead of 3
- [x] Create Table - Once a game is selected, hide the search box
- [x] Create table page, enable button as soon as you start typing
- [x] SYN - Enable playing in Dealer's Choice
- [x] Create Table page, make the Create Table button look like a poker table
- [x] When in dark mode, the Join Seat Amount text is hidden (white on white)
- [x] Dealer's Choice - pick which games are included
- [x] Auto generate table name
- [x] Lobby - Group by game type
- [x] DC - You're the dealer! Pick a game variant for this hand. - SYN shows Min Bet
- [x] BB - The blinds chips icons overlay the hand eval
- [x] BB - Test in DC
- [x] Add 50,000 chips to demo players
- [x] Join game modal - should not be able to click out of it
- [x] Make the create table list smaller
- [x] Add the images to the DC game list
- [x] Add the images to the game info overlay
- [x] Add Klondike
- [x] Game info should have game rules
- [x] Customize blazor rejoin modal
- [x] Add a private chime for the player when it's their turn
- [x] Lobby should wrap descriptions
- [x] Allow users to "favorite" game types and sort them at the top
- [x] Make the dark/light theme support system settings
- [x] Bringing in cash to a game should require host approval?
- [x] Create table page - Keep Dealer's Choice at the top, not with "Other"
- [x] Phil's mom loser showdown not correct
- [x] Redesign ShowdownOverlay
- [x] Remove Odds Visibility from CreateTable
- [x] Can't adjust buy-in on Join Seat modal
- [x] Show the timer
- [x] Pressing Start Game doesn't do anything
- [x] Layout cards on either side of the deck
- [x] Not showing the in-between card
- [x] Pause to see the card
- [x] Join game modal background color
- [x] SYN - odds

- [x] Crazy Pineapple description is incorrect. Every game is like this.
- [x] Delete Table modal, make the delete button red
- [x] DC - Make it more obvious what game you're playing
- [x] BB should always be twice SB
- [x] In Hold Em, the BB should be able to raise
- [x] In DC the SB and BB when creating should use preferences
- [x] Join table is limiting chips
- [x] It's not showing my avatar on tableseat
- [x] K&L - had an ace, but it colored the 5 as low
- [x] G,B,U - Good should be green
- [x] Make cards transparent when folded
- [x] Joining table adds 5001 when setting 5000
- [x] Adding chips during DC doesn't work
- [x] SYN - It took the chips from the wrong player
- [x] SYN - Double burn doesn't work
- [x] Game felt
- [x] Click Leave Table : {"message":"You are not seated at this table or have already left"}
- [x] "Add Chips:" on the dashboard is the wrong color
- [x] Allow private tables - need an invite / password
