You are auditing a friends’ online poker app for correctness. Players must be able to trust every hand: the deal, what each player is allowed to see, the betting, the winner, and the chips. A bug that misdeals, leaks a hole card, double-pays a pot, or lets two actions both succeed is a release blocker. Style, naming, and performance are out of scope unless they cause one of those failures.

The code is the specification. Docs, comments, and tests are claims about intended behavior. When they disagree with the code that actually runs a hand, report the disagreement and say which one a live table executes.

Do not change code. Do not propose refactors. Report defects you can demonstrate from the code, with a concrete hand.

## What “correct” means

Check every live path against these invariants.

Dealing
- Each hand uses a full French deck of 52 distinct cards unless that game’s rules explicitly say otherwise.
- A card dealt to a player, the board, a burn, or the muck is removed from the remaining deck. The same card never appears twice in one hand.
- The next card is chosen uniformly from the cards still remaining. Shuffle-or-reset plus “pick a random index” is fair only if the index is uniform, the card is removed, and a new hand cannot replay or predict the previous hand’s order.
- Randomness is not shared across threads unsafely, and the seed or sequence is not recoverable from data sent to a client.
- Burn cards are burned, stay hidden, and are never evaluated.
- Hole cards, upcards, community cards, draws, and replacement cards are dealt in the order and counts that game’s rules require, including short-handed play.

Information
- A payload built for one player contains the rank and suit of only that player’s hidden cards, plus cards the rules have already turned face up.
- Folded hands stay hidden. Showdown reveals only the cards that must be shown.
- Logs, debug strings, odds helpers, and “public” table state must not carry another player’s hole-card identity to the browser.
- The viewer’s own cards render face up to them and face down to everyone else. Upcards, board cards, and showdown cards render face up to everyone.

Betting and payouts
- A player cannot bet, call, raise, or post more than their stack. All-in is exactly the remaining stack.
- Antes, blinds, and brings are posted once, by the correct seats, at the correct amounts.
- Action order, bet closing, check, call, raise, fold, and all-in match the game’s betting structure.
- Chips are conserved. For every completed hand, the sum of stack changes plus rake plus returned uncalled chips is zero. The app never creates or destroys chips.
- Uncalled chips are returned to the bettor and are not part of the pot.
- Each pot layer is won only by players who matched that layer and did not fold. A player cannot win chips they did not put in.
- Side pots are layered by distinct all-in amounts, including all-ins on later streets.
- The best hand, under that game’s evaluator, wins. Ties split that pot. Odd chips follow one explicit rule, applied the same way everywhere.
- If every other player folds, the last player wins the pot and no hole cards are shown.
- A hand cannot be dealt, bet, shown down, or paid twice.

Concurrency
- Two betting actions, a timer auto-action and a human action, or the background hand-starter and an API call cannot both commit.
- A failed optimistic concurrency check must not drop a chip update that the client was told succeeded, and a retry must not apply the same action twice.
- Rebuy, sit-out, and grace-period pauses cannot change stacks or eligibility in the middle of a hand incorrectly.

Display
- Button, blinds, turn, pot, side pots, board, upcards, and showdown labels on screen match the server state for that viewer.
- Hand-rank text matches the evaluator that decided the pot.

## Where the live path is

Trace the path that persists a hand and updates stacks. Domain game classes and playground simulations are evidence of intended rules, not proof that the table uses them.

- Cards, deck, dealer, RNG: `src/CardGames.Core`, `src/CardGames.Core.French`, especially `Dealer<T>.DealCard` and `StandardRandomNumberGenerator`.
- Rules, evaluators, and the in-memory pot model: `src/CardGames.Poker`, especially `Betting/PotManager.cs` and `Evaluation/`.
- What a table actually does: `src/CardGames.Poker.Api/GameFlow` and `src/CardGames.Poker.Api/Features/Games`.
- Payouts: every `PerformShowdown` handler, including `Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`. Determine which handler each game type really calls. Diff copies instead of assuming they match.
- What the browser is allowed to see: `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`, then the Blazor table in `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` and `Components/Shared/`.
- Races with timers and the next-hand starter: `ActionTimerService` and `ContinuousPlayBackgroundService`.
- Persistence and row versions: `src/CardGames.Poker.Api/Data`.

Games to cover: Hold’em, Omaha, Irish Hold’em, Crazy Pineapple, Bob Barker, Phil’s Mom, Red River, Nebraska, South Dakota, Klondike, Five Card Draw, Deuces/Jacks/Man with the Axe, Seven Card Stud, Razz, Baseball, Hold the Baseball, Follow the Queen, Good Bad Ugly, Pair Pressure, Kings and Lows, Tollbooth, In Between, Screw Your Neighbor.

Known high-risk seams to verify, not assume:
- Several games have their own showdown handler and a generic one also exists.
- `TableStateBuilder` branches on game type for visibility, including stud, Klondike, Good Bad Ugly, and Bob Barker reveal.
- `Dealer.Shuffle` resets the deck; fairness depends on how the next index is chosen and how the deck removes cards.
- `docs/SidePots.md` and `docs/TexasHoldEmRules.md` describe intended pot and Hold’em behavior. Check the code that runs.

## How to audit

For this pass, trace at least one complete hand from deal through payout in the code that writes the database. Use a numeric scenario: name the seats, stacks, posted chips, cards, actions, and the resulting stacks. When behavior is wrong, show the expected stacks or cards and the ones the code produces.

Read the tests for that path as a statement of intent, then check whether production code on the live path does what the test assumes. A green test of a helper does not clear the API handler.

If you cannot prove a path safe, say what you could not trace. Do not fill gaps with “likely correct.”

## Report

Lead with findings, highest severity first. Omit empty sections.

For each finding:
- Severity: chips wrong, unfair or predictable deal, hole-card leak, double action or lost action, wrong winner, display disagrees with server, rules disagree across duplicated handlers.
- Game types affected.
- File and method.
- The invariant broken.
- A concrete hand: stacks, cards, actions, expected result, actual result.
- Confidence: certain from inspection, or probable with the branch you could not fully trace.

Then list, briefly:
- Paths traced and found consistent with the invariants.
- Paths you did not finish, and why.

No patches. No style notes.