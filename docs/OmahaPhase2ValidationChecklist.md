# Omaha Phase 2 Validation Checklist (Non-Prod)

**Purpose:** Validate Omaha staged deploy behavior in non-prod before Phase 3 production enablement.  
**Audience:** QA and internal validation users.  
**Precondition:** Omaha is enabled in non-prod environment.

## Scope
- Create Table flow with Omaha blinds.
- Dealer's Choice flow with Omaha blinds.
- Start hand, betting progression, and showdown.
- Omaha exact-two-hole-card rule visible via final outcomes.

## Setup
- Use a non-prod environment and two test users (`Player A`, `Player B`).
- Ensure both users have sufficient chips to play multiple hands.
- Clear browser cache/session if stale game-state UI is suspected.

## A) Create Table (Omaha + blinds)
- [ ] Navigate to `Create Table`.
- [ ] Select `Omaha` variant.
- [ ] Confirm blind inputs are shown (`Small Blind`, `Big Blind`).
- [ ] Enter `Small Blind = 5`, `Big Blind = 10`, valid table name, and at least 2 players.
- [ ] Create table.
- [ ] On table screen, confirm blinds display as `5 / 10` and game variant is Omaha.

## B) Dealer's Choice (Omaha + blinds)
- [ ] Create or open a `Dealer's Choice` table.
- [ ] Wait for dealer prompt (`WaitingForDealerChoice`).
- [ ] Select `Omaha` in Dealer's Choice modal.
- [ ] Confirm modal requests blind values (not ante/min-bet-only path).
- [ ] Submit `Small Blind = 10`, `Big Blind = 20`.
- [ ] Confirm selected hand shows Omaha context and blind values.

## C) Start hand + betting progression
- [ ] Start hand.
- [ ] Verify each active player receives **4 hole cards**.
- [ ] Verify blinds are posted to pot at hand start.
- [ ] Verify betting actions proceed through Omaha community-card flow (`PreFlop` -> `Flop` -> `Turn` -> `River` -> `Showdown`).
- [ ] Verify no action routing/fallback errors occur.

## D) Showdown exact-two-hole validation (observable outcome)
- [ ] Run or set up a hand where board texture could mislead winner selection if Omaha rule is broken.
- [ ] At showdown, verify outcome matches Omaha rule: winner must be based on **exactly 2 hole cards + 3 board cards**.
- [ ] Confirm hand closes cleanly and payouts/outcome summary are consistent with expected winner.

## Pass/Fail
- **PASS** if all checklist items succeed with no routing/fallback issues and showdown outcome reflects exact-two-hole Omaha behavior.
- **FAIL** if any item fails, including:
  - Omaha not selectable where expected.
  - Blinds not configurable/persisted for Omaha.
  - Hand start/betting/showdown flow breaks.
  - Outcome indicates non-Omaha hand construction behavior.

## Evidence to capture
- Table creation screenshot (Omaha + blinds).
- Dealer's Choice modal screenshot (Omaha selected + blinds inputs).
- In-hand screenshot showing 4 hole cards/player and blind context.
- Showdown screenshot with final winner/outcome panel.
- Short note with table ID, environment, and timestamp.
