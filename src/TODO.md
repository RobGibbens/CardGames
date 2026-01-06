# TODO

## Still to do
- [ ] Kings and lows - can not join a table already in progress
- [ ] FiveCardDraw - can't draw four with an Ace
- [ ] Kings and Lows - don't deal to players that don't have enough chips to match the pot

- [ ] Make sure all calls are idempotent
- [ ] Add chip management to the dashboard (buy in, cash out, add chips)

- [ ] Search for code == "KINGSANDLOWS" and refactor
- [ ] Creating a deck and dealing is done in the StartHandCommandHandler, but it should be using CardGames.Core.Dealer;
- [ ] Make sure signalr isn't broadcasting to all players in all games
- [ ] Table settings should be dynamic when creating a table. Not all games have an ante or a minimum bet.
- [ ] Make the chips look better
- [ ] Table - make the table seat look like PokerStars
- [ ] During ties, the showdown should show the kicker 
- [ ] Showdown overlay should handle split pots better
- [ ] Showdown overlay is not showing payouts correctly
- [ ] Hand history should auto size columns and allow resizing columns
- [ ] Hand history should paginate
- [ ] When the Draw Overlay is showing, the cards are not opaque
- [ ] Leaderboard should show first names
- [ ] The bottom of the table goes off the bottom of the screen
- [ ] Add a chip stack graph in the dashboard
- [ ] Add sound effects
- [ ] Add a chat box
- [ ] Add the ability to mute other players in chat
- [ ] Add the ability to block other players
- [ ] Add a friends list
- [ ] Add the ability to invite friends to a table
- [ ] Add the ability to create private tables
- [ ] The betting action overlay. Raise to X doesn't work
- [ ] Add an action timer
- [ ] Have the ability for someone to take a seat and be dealt into the next hand
- [ ] When already logged in, take them to the Lobby
- [ ] Test side pots
- [ ] The fields on the register page should be wider
- [ ] Showdown should use better description of the cards (Pair of Aces vs One Pair)
- [ ] Add animation of dealing the cards
- [ ] Table should be able to scroll if the screen isn't big enough
- [ ] Rearrange the register and login screens to make OAuth more obvious
- [ ] Allow them to upload an image when registering
- [ ] Add leagues
- [ ] Add seasons
- [ ] Add tournaments


## Add Games

### Hold 'Em Variants
- [ ] Add Texas Hold'em
- [ ] Add Omaha
- [ ] Add Pittsburgh
- [ ] Add Squid Game

### 7 Card Stud Variants
- [ ] Add Seven Card Stud
- [ ] Add Baseball
- [ ] Add Follow the Queen
- [ ] The Good, the Bad, and the Ugly
- [ ] Add Razz
- [ ] 

### 5 Card Stud Variants
- [ ] Add Five Card Stud

### 5 Card Draw Variants
- [x] Five card draw
- [x] Add Twos, Jacks, Man with the Axe
- [ ] Add Kings and Lows

### Other Poker Variants

- [ ] Add Screw Your Neighbor
- [ ] In Between
- [ ] Add Guts
- [ ] 




## Completed
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
