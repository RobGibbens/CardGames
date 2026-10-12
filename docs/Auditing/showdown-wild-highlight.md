# Fix: showdown highlights the natural best five while the pot uses wilds

This plan is the implementation spec for one defect. An LLM should follow it exactly. Do not fix other audit findings in the same change (side pots, Follow the Queen wild-card order, fixed-limit bet size, Tollbooth all-in runout, bring-in action order, ace-low bring-in, odd chips).

The pot is already awarded from the wild strength. `HandType` and `HandStrength` already come from that evaluation. The highlight does not. It must mark the five physical cards that produced that strength.

## Outcome

`BestCardIndexes` are positions in the `Cards` list returned for that player. The cards at those positions are `BestHandSourceCards` from the hand that decided the pot.

`BestHandSourceCards` are the player's own cards, including a wild card that was used. They are not the substituted ranks the evaluator invented, and they are not the best five cards computed with wilds ignored.

Hand history `ShowdownCards` for that player is those same five cards. A folded player still has no showdown cards. A win by fold still records no showdown cards.

When the hand has no wild cards, `BestHandSourceCards` is already the natural best five. Those highlights stay the same.

## Leave these alone

- Who wins, `HandType`, and `HandStrength`. Do not change `WildCardHandEvaluator`, `FollowTheQueenWildCardRules`, `GoodBadUglyWildCardRules`, `PairPressureWildCardRules`, or the Baseball 3s-and-9s rule.
- `StudHand.GetBestHand`. Seven Card Stud and Razz have no wild rank. Razz history and Razz indexes keep using `GetBestLowHand`.
- Baseball's response indexes. `Baseball` `PerformShowdownCommandHandler` already walks `BestHandSourceCards` into the response `Cards` list. Do not point that walk at `EvaluatedBestCards` or `GetBestHand`.
- Hold the Baseball. Its response matches `EvaluatedBestCards`, which are the substituted cards, not this natural-five bug. Do not edit that handler.
- `WildCardIndexes` on Follow the Queen. They stay on the list that handler already builds. This change does not retarget them.
- The order of `Cards` in the response. Index into that list. Do not sort it.

## Why the highlight disagrees with the pot

`StudHand.GetBestHand` ranks every five-card subset with `HandTypeDetermination.DetermineHandType`. A queen is a queen. A deuce is a deuce. The best natural five is what gets highlighted.

`FollowTheQueenHand`, `GoodBadUglyHand`, `BaseballHand`, and `PairPressureHand` override strength. When any card is wild, `EvaluateIfNeeded` stores `WildCardHandEvaluator.EvaluateBestHand(...).SourceCards` in `BestHandSourceCards`. That source set is the five physical cards whose wild substitution won. `EvaluatedBestCards` is the substituted hand and can contain a rank the player was not dealt.

The response and the history read `GetBestHand` anyway:

- Follow the Queen response, in `PerformShowdownCommandHandler`, the block commented "Get the best 5-card hand", calls `hand.GetBestHand()` and matches those cards into `kvp.Value.cards`. `HandType` on the same object is `hand.Type`. History `RecordHandHistoryAsync` calls `GetBestHand` again for `ShowdownCards`.
- Good Bad Ugly response and history do the same. The stored hand is a `GoodBadUglyHand` held as `StudHand`. The response `Cards` list is the player's cards plus the community cards, which is the list `BestCardIndexes` must index.
- Baseball history casts the hand to `StudHand` and calls `GetBestHand`. The response path above it already uses `BestHandSourceCards`.
- Pair Pressure is evaluated by generic showdown as a `PairPressureHand`. `FindBestCardIndexes` does not recognize that type. After the community-card branch it falls through to `FindBestFiveCardHand`, which is the natural best five. `Hand.Strength` still uses the wilds.

## What to read

In each place that currently calls `GetBestHand` or `FindBestFiveCardHand` for one of these four hands, read `BestHandSourceCards` instead.

Follow the Queen, `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`:

- Response: replace `kvp.Value.hand.GetBestHand()` with `kvp.Value.hand.BestHandSourceCards`. Keep the suit-and-symbol match and the `usedIndexes` set, so two cards of the same rank do not take the same slot. The `Cards` list stays `kvp.Value.cards` in its current order.
- History: replace `handEvaluation.hand.GetBestHand()` with `handEvaluation.hand.BestHandSourceCards`. Format those cards with the existing `FormatCard`.

Good Bad Ugly, `src/CardGames.Poker.Api/Features/Games/GoodBadUgly/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`:

- The tuple stores the hand as `StudHand`. Cast to `GoodBadUglyHand` and read `BestHandSourceCards`. A hand that is not a `GoodBadUglyHand` does not occur on this path.
- Use that set for the response indexes and for history `ShowdownCards`.
- Keep indexing the combined player-plus-community list. The source cards were chosen from that same combined hand.

Baseball history, `src/CardGames.Poker.Api/Features/Games/Baseball/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` `RecordHandHistoryAsync`:

- The dictionary values are `BaseballHand` instances stored as `StudHand`. Cast to `BaseballHand` and format `BestHandSourceCards`. Do not change the response loop.

Generic `FindBestCardIndexes` in `src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`:

- Before the natural `FindBestFiveCardHand` fallback, if `hand` is a `PairPressureHand`, return `GetCardIndexes(allCoreCards, pairPressureHand.BestHandSourceCards)`.
- Leave the Razz, Klondike, and community-card branches as they are.
- Generic history stores every dealt card, not the best five, except Razz. Do not change that history list for Pair Pressure.

## Worked hand

Follow the Queen. The only wild rank is queens: the face-up list given to `FollowTheQueenHand` ends with the queen, so no following rank is wild. The player's seven cards, in response order, are:

`Qh, 2c, 3d, 4h, 5s, 9c, Kd`

`HandType` is `Straight`. The queen stands in for the missing 6. `BestHandSourceCards` are `Qh, 2c, 3d, 4h, 5s`.

`GetBestHand` ignores the wild and returns king high: `Kd, Qh, 9c, 5s, 4h`. That is what the response highlights today. `2c` and `3d` are missing, and `Kd` and `9c` are marked.

After the fix, `BestCardIndexes` has five entries. The cards at those indexes are `Qh, 2c, 3d, 4h, and 5s`. `Kd` and `9c` are not in the set. History `ShowdownCards` are those same five cards and do not contain `Kd`. `HandType` stays `Straight`. The winner does not change.

Good Bad Ugly. The Good card is a deuce, so deuces are wild. The combined card list is:

`Kh, Kd, Ah, Qh, Jh, 2c, 3d`

`HandType` is `Trips`. The deuce plus the two kings make three kings. `BestHandSourceCards` are `2c, Kh, Kd, Ah, Qh`.

`GetBestHand` returns the pair of kings with the three highest kickers: `Kh, Kd, Ah, Qh, Jh`. The deuce is not in that five.

After the fix, the highlight includes `2c` and does not include `Jh` or `3d`.

Baseball history. 3s are wild. The player's cards are `Ah, Ad, Kh, Qh, Jh, 3c, 2d`. The response indexes already include `3c` because they use `BestHandSourceCards`. History today records the natural pair of aces, `Ah, Ad, Kh, Qh, Jh`, and omits `3c`. After the fix, history includes `3c` and omits `2d`. The response indexes do not change.

Pair Pressure. The face-up order is `2c, 2d`, so deuces are the paired wild rank. The player's cards are `Ah, Ad, Kh, Qh, Jh, 2h, 3c`. The natural fallback highlights `Ah, Ad, Kh, Qh, Jh`. The wild strength is trips aces, and `BestHandSourceCards` includes `2h`. After the fix, the generic response indexes include `2h` and do not include `3c`.

## Tests

Add a Follow the Queen showdown test that seeds the seven cards and a face-up order ending in `Qh`. Assert `HandType` is `Straight`, the highlighted cards are exactly `Qh, 2c, 3d, 4h, 5s`, and the recorded `ShowdownCards` are that same set. Assert the payout winner is unchanged from a run of this hand against a worse hand, such as king high with no queen and no straight.

Add a Good Bad Ugly showdown test for the deuce-wild hand. The highlighted cards include `2c, Kh, Kd, Ah, Qh` and exclude `Jh` and `3d`. History `ShowdownCards` is that same set.

Add a Baseball assertion on the existing showdown history path: `ShowdownCards` contains `3c` and does not contain `2d`. The response `BestCardIndexes` assertion, if one already matches `BestHandSourceCards`, stays as written.

Add a Pair Pressure generic-showdown assertion that the indexes include `2h` and exclude `3c`, and that `HandType` is `Trips`.

Run a Seven Card Stud showdown test and a Razz showdown test. Their highlighted cards stay the natural best five and the best low five. Do not edit those assertions.

## Done when

- Follow the Queen and Good Bad Ugly response indexes and history cards come from `BestHandSourceCards`.
- Baseball history comes from `BestHandSourceCards`. Baseball response indexing is unchanged.
- Pair Pressure indexes come from `PairPressureHand.BestHandSourceCards`.
- The queen hand highlights `Qh, 2c, 3d, 4h, 5s` and not `Kd`.
- `GetBestHand` is still what Seven Card Stud uses.
- No winner, hand type, or stack changes on these hands.
