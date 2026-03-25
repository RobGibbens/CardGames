Phase 1 is complete. Production-Ready mode is confirmed, the repo is clean, and I’ve resolved the key rule ambiguities needed for a concrete plan:

- Chosen Tollbooth display cards follow normal street visibility
- Tollbooth offers replace Fourth through Seventh street dealing as the deal mechanism
- Paid Tollbooth options are unavailable if the player cannot afford them

## Plan: Tollbooth Stud Variant

Implement Tollbooth as a Seven Card Stud family game that reuses the stud lifecycle, betting, and showdown patterns, while introducing a new per-player Tollbooth offer action and two persistent display-only table cards. The safest architecture is: reuse Seven Card Stud endpoints for start, ante, deal, betting, and showdown; add one Tollbooth-specific action endpoint for offer selection; store the two display cards as shared table cards for UI only; explicitly exclude those cards from evaluation, odds, and showdown hand construction.

**Steps**
1. Phase A: Domain registration and rules
   - Add Tollbooth as a new stud-family game under [src/CardGames.Poker/Games](src/CardGames.Poker/Games), using [src/CardGames.Poker/Games/SevenCardStud/SevenCardStudGame.cs](src/CardGames.Poker/Games/SevenCardStud/SevenCardStudGame.cs) and [src/CardGames.Poker/Games/SevenCardStud/SevenCardStudRules.cs](src/CardGames.Poker/Games/SevenCardStud/SevenCardStudRules.cs) as templates.
   - Define Tollbooth rules so the initial Third Street stays standard, then later streets are driven by Tollbooth offer phases that ultimately feed the normal Fourth, Fifth, Sixth, and Seventh betting rounds.
   - Keep the game metadata aligned with Stud conventions: Stud variant type, ante/bring-in structure, image tollbooth.png, code TOLLBOOTH.
   - Record the clarified rule that face-up Tollbooth cards still obey normal street visibility when they become player-owned cards.

2. Phase B: API flow and state model
   - Add a Tollbooth code constant to [src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs](src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs) because this repo still uses explicit game-code checks even with metadata discovery.
   - Add a dedicated Tollbooth flow handler under the existing game-flow area, modeled primarily on [src/CardGames.Poker.Api/GameFlow/SevenCardStudFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/SevenCardStudFlowHandler.cs), [src/CardGames.Poker.Api/GameFlow/PairPressureFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/PairPressureFlowHandler.cs), and [src/CardGames.Poker.Api/GameFlow/BobBarkerFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/BobBarkerFlowHandler.cs).
   - Use the existing Game.CurrentDrawPlayerIndex and GamePlayer.HasDrawnThisRound flow-control fields for the per-player Tollbooth selection cycle rather than introducing a broader persistence refactor.
   - Reuse CardLocation.Community for the two Tollbooth display cards and keep exactly two display cards on the table across rounds by moving selected cards to the acting player and replenishing from the deck.
   - Do not add a new card-location enum unless implementation proves it is required; the cleaner plan is to branch by game type and treat Tollbooth community cards as display-only.

3. Phase C: Tollbooth action endpoint and contracts
   - Add a Tollbooth-specific endpoint group parallel to the Bob Barker pattern, using [src/CardGames.Poker.Api/Features/Games/BobBarker/v1/V1.cs](src/CardGames.Poker.Api/Features/Games/BobBarker/v1/V1.cs) and [src/CardGames.Poker.Api/Features/Games/BobBarker/v1/Commands/SelectShowcase/SelectShowcaseCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/BobBarker/v1/Commands/SelectShowcase/SelectShowcaseCommandHandler.cs) as the closest reference.
   - Create a command, request, success result, and error model for selecting one of three Tollbooth choices: furthest free, nearest for 1x ante, deck for 2x ante.
   - Keep start-hand, collect-antes, initial deal, process-betting-action, and showdown on the existing Seven Card Stud family where possible.
   - Add a dedicated Tollbooth API contract surface in [src/CardGames.Contracts](src/CardGames.Contracts) only for the new offer action. Refit regeneration is only required if the team wants this new endpoint represented in generated output instead of the established partial-interface pattern.
   - Validation rules should explicitly cover: wrong phase, wrong player turn, already selected this round, invalid option, unaffordable paid option, and no eligible players left.

4. Phase D: Betting progression and all-in runout
   - Update the Seven Card Stud family betting progression so Tollbooth transitions from Third Street betting into the first Tollbooth offer round, and from each subsequent betting round into the next Tollbooth offer round until the final betting round completes.
   - Handle all-in runout in the betting action path, not in UI, matching your recommendation. The modified stud betting handler should detect when all remaining players are all-in and auto-run the remaining Tollbooth rounds by always taking the free furthest card.
   - Ensure the rapid runout path still respects the clarified visibility rules for player-owned cards and still leaves exactly two display cards on the table for public state.

5. Phase E: Public state, evaluation, odds, and showdown
   - Update [src/CardGames.Poker.Api/Services/TableStateBuilder.cs](src/CardGames.Poker.Api/Services/TableStateBuilder.cs) so Tollbooth display cards are exposed through CommunityCards for rendering, but never included in evaluation card sets.
   - Add Tollbooth-specific projection rules near the existing Bob Barker and stud-family branches so the UI can see the two display cards and the acting Tollbooth player without corrupting hand descriptions.
   - Update the generic showdown path in [src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs) so Tollbooth evaluates like Seven Card Stud and ignores shared display-only cards entirely.
   - Update [src/CardGames.Poker.Web/Services/DashboardHandOddsCalculator.cs](src/CardGames.Poker.Web/Services/DashboardHandOddsCalculator.cs) so Tollbooth uses stud odds and ignores CommunityCards for odds purposes.
   - Update [src/CardGames.Poker.Web/Components/Shared/ShowdownOverlay.razor](src/CardGames.Poker.Web/Components/Shared/ShowdownOverlay.razor) only if the existing display composition would accidentally render the two Tollbooth display cards as evaluation cards.

6. Phase F: Web integration
   - Reuse the Seven Card Stud wrapper approach for standard stud actions and add Tollbooth-specific routing only for the new offer action in [src/CardGames.Poker.Web/Services/IGameApiRouter.cs](src/CardGames.Poker.Web/Services/IGameApiRouter.cs).
   - Update [src/CardGames.Poker.Web/Program.cs](src/CardGames.Poker.Web/Program.cs) to register any new Refit client or router dependency needed for the Tollbooth action endpoint.
   - Update [src/CardGames.Poker.Web/Components/Pages/TablePlay.razor](src/CardGames.Poker.Web/Components/Pages/TablePlay.razor) to:
     - recognize Tollbooth as Seven Card Stud style for existing table behavior,
     - show a Tollbooth offer overlay when it is the acting player’s offer turn,
     - keep the two table display cards visible via CommunityCards,
     - prevent CommunityCards from leaking into odds or showdown assumptions for Tollbooth.
   - Update [src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor](src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor) for icon/label polish if needed, but avoid unnecessary layout changes because CommunityCards rendering already exists.
   - Confirm whether [src/CardGames.Poker.Web/Components/Pages/CreateTable.razor](src/CardGames.Poker.Web/Components/Pages/CreateTable.razor), [src/CardGames.Poker.Web/Components/Pages/EditTable.razor](src/CardGames.Poker.Web/Components/Pages/EditTable.razor), and [src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor](src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor) need changes; current discovery suggests basic inclusion is mostly metadata-driven for this non-blind stud game, so only adjust them if there are explicit stud-family filters.

7. Phase G: Tests
   - Add domain tests beside [src/Tests/CardGames.Poker.Tests/Games/SevenCardStudGameTests.cs](src/Tests/CardGames.Poker.Tests/Games/SevenCardStudGameTests.cs) and [src/Tests/CardGames.Poker.Tests/Games/SevenCardStudRulesTests.cs](src/Tests/CardGames.Poker.Tests/Games/SevenCardStudRulesTests.cs) to verify Tollbooth metadata, phases, and special rules.
   - Add flow-handler tests beside [src/Tests/CardGames.IntegrationTests/GameFlow/SevenCardStudFlowHandlerTests.cs](src/Tests/CardGames.IntegrationTests/GameFlow/SevenCardStudFlowHandlerTests.cs) to verify phase sequencing, special phases, and the two-display-card invariant.
   - Add targeted Tollbooth action tests modeled on [src/Tests/CardGames.IntegrationTests/Games/BobBarker/BobBarkerShowcaseSelectionTests.cs](src/Tests/CardGames.IntegrationTests/Games/BobBarker/BobBarkerShowcaseSelectionTests.cs) for:
     - valid free, paid, and deck selections,
     - timeout auto-selecting the free option,
     - unaffordable paid option rejection,
     - wrong phase rejection,
     - already-acted rejection.
   - Add end-to-end lifecycle coverage beside [src/Tests/CardGames.IntegrationTests/EndToEnd/SevenCardStudGameFlowTests.cs](src/Tests/CardGames.IntegrationTests/EndToEnd/SevenCardStudGameFlowTests.cs) for:
     - start hand,
     - Third Street deal and betting,
     - one or more Tollbooth rounds,
     - all-in auto-runout through remaining Tollbooth rounds,
     - showdown ignoring display-only table cards.
   - Add regression tests for [src/Tests/CardGames.Poker.Tests/Evaluation/DashboardHandOddsCalculatorTests.cs](src/Tests/CardGames.Poker.Tests/Evaluation/DashboardHandOddsCalculatorTests.cs) to confirm Tollbooth ignores display CommunityCards.

8. Phase H: Documentation and rollout readiness
   - Update [docs/AddingNewGames20.md](docs/AddingNewGames20.md) with a new Tollbooth mapping section because this introduces a reusable stud-family pattern: display-only shared cards visible through CommunityCards but excluded from evaluation.
   - Update [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) to document that shared display cards can be projected for UI independently from evaluation inputs.
   - Reuse the existing prompt source [docs/Tollbooth%20prompt.md](docs/Tollbooth%20prompt.md) as implementation intent, but convert the durable lessons into the playbook instead of leaving them only in a prompt document.
   - Confirm the asset already present at [src/CardGames.Poker.Web/wwwroot/images/games/tollbooth.png](src/CardGames.Poker.Web/wwwroot/images/games/tollbooth.png) is picked up by metadata and table discovery.

**Relevant files**
- [docs/AddingNewGames20.md](docs/AddingNewGames20.md) — primary playbook to update with the Tollbooth reusable pattern
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — document display-only shared cards versus evaluation cards
- [docs/Tollbooth%20prompt.md](docs/Tollbooth%20prompt.md) — source behavior spec already present in the repo
- [src/CardGames.Poker/Games/SevenCardStud/SevenCardStudGame.cs](src/CardGames.Poker/Games/SevenCardStud/SevenCardStudGame.cs) — metadata template for the new stud-family game
- [src/CardGames.Poker/Games/SevenCardStud/SevenCardStudRules.cs](src/CardGames.Poker/Games/SevenCardStud/SevenCardStudRules.cs) — rules template and phase vocabulary
- [src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs](src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs) — add TOLLBOOTH constant for explicit orchestration branches
- [src/CardGames.Poker.Api/GameFlow/SevenCardStudFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/SevenCardStudFlowHandler.cs) — baseline stud lifecycle and bring-in handling
- [src/CardGames.Poker.Api/GameFlow/PairPressureFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/PairPressureFlowHandler.cs) — modern stud-family flow-handler template
- [src/CardGames.Poker.Api/GameFlow/BobBarkerFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/BobBarkerFlowHandler.cs) — special-phase timeout and CurrentDrawPlayerIndex pattern
- [src/CardGames.Poker.Api/Features/Games/BobBarker/v1/V1.cs](src/CardGames.Poker.Api/Features/Games/BobBarker/v1/V1.cs) — dedicated variant endpoint-group pattern
- [src/CardGames.Poker.Api/Features/Games/BobBarker/v1/Commands/SelectShowcase/SelectShowcaseCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/BobBarker/v1/Commands/SelectShowcase/SelectShowcaseCommandHandler.cs) — bespoke per-player decision action pattern
- [src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1](src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1) — reusable stud endpoint family for start, ante, deal, betting, current-turn, showdown
- [src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs) — generic showdown branch that must ignore display-only Tollbooth cards
- [src/CardGames.Poker.Api/Services/TableStateBuilder.cs](src/CardGames.Poker.Api/Services/TableStateBuilder.cs) — the highest-risk projection file for display-versus-evaluation separation
- [src/CardGames.Contracts/SignalR/TableStatePublicDto.cs](src/CardGames.Contracts/SignalR/TableStatePublicDto.cs) — confirms CommunityCards already exists for Tollbooth table display
- [src/CardGames.Contracts/PairPressureApiExtensions.cs](src/CardGames.Contracts/PairPressureApiExtensions.cs) — reference for a manually maintained variant API contract surface
- [src/CardGames.Poker.Web/Services/IGameApiRouter.cs](src/CardGames.Poker.Web/Services/IGameApiRouter.cs) — likely web routing touchpoint for the Tollbooth action
- [src/CardGames.Poker.Web/Program.cs](src/CardGames.Poker.Web/Program.cs) — client registration and wrapper/router DI
- [src/CardGames.Poker.Web/Components/Pages/TablePlay.razor](src/CardGames.Poker.Web/Components/Pages/TablePlay.razor) — special overlay, stud-family predicates, and odds plumbing
- [src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor](src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor) — existing CommunityCards rendering and game icon mapping
- [src/CardGames.Poker.Web/Components/Shared/ShowdownOverlay.razor](src/CardGames.Poker.Web/Components/Shared/ShowdownOverlay.razor) — showdown rendering that may need Tollbooth-specific filtering
- [src/CardGames.Poker.Web/Services/DashboardHandOddsCalculator.cs](src/CardGames.Poker.Web/Services/DashboardHandOddsCalculator.cs) — must treat Tollbooth as stud and ignore CommunityCards
- [src/Tests/CardGames.Poker.Tests/Games/SevenCardStudGameTests.cs](src/Tests/CardGames.Poker.Tests/Games/SevenCardStudGameTests.cs) — unit test template for metadata/game registration
- [src/Tests/CardGames.Poker.Tests/Games/SevenCardStudRulesTests.cs](src/Tests/CardGames.Poker.Tests/Games/SevenCardStudRulesTests.cs) — unit test template for phase/rules validation
- [src/Tests/CardGames.IntegrationTests/GameFlow/SevenCardStudFlowHandlerTests.cs](src/Tests/CardGames.IntegrationTests/GameFlow/SevenCardStudFlowHandlerTests.cs) — flow and dealing regression template
- [src/Tests/CardGames.IntegrationTests/EndToEnd/SevenCardStudGameFlowTests.cs](src/Tests/CardGames.IntegrationTests/EndToEnd/SevenCardStudGameFlowTests.cs) — lifecycle test template
- [src/Tests/CardGames.IntegrationTests/Games/BobBarker/BobBarkerShowcaseSelectionTests.cs](src/Tests/CardGames.IntegrationTests/Games/BobBarker/BobBarkerShowcaseSelectionTests.cs) — bespoke action negative-path template
- [src/Tests/CardGames.Poker.Tests/Evaluation/DashboardHandOddsCalculatorTests.cs](src/Tests/CardGames.Poker.Tests/Evaluation/DashboardHandOddsCalculatorTests.cs) — odds regression coverage
- [src/CardGames.Poker.Web/wwwroot/images/games/tollbooth.png](src/CardGames.Poker.Web/wwwroot/images/games/tollbooth.png) — asset already present

**Verification**
1. Build the full solution with dotnet build src/CardGames.sln after the API, web, and contracts changes land.
2. Run all tests with dotnet test src/CardGames.sln.
3. Run targeted Tollbooth coverage first during development with dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter FullyQualifiedName~Tollbooth.
4. Start the API with dotnet run --project src/CardGames.Poker.Api and verify:
   - Tollbooth game type is discoverable,
   - start/ante/deal succeeds,
   - Tollbooth offer action endpoint responds,
   - all-in runout bypasses manual offer selection.
5. Start the web app with dotnet run --project src/CardGames.Poker.Web and verify:
   - Tollbooth appears in game discovery,
   - the table shows two Tollbooth display cards through CommunityCards,
   - the acting player sees the offer overlay,
   - paid choices disable when unaffordable,
   - showdown and odds do not count the display cards.
6. Only run dotnet build src/CardGames.Poker.Refitter if the implementation chooses generated contract updates instead of the existing partial-interface pattern.

**Decisions**
- Included scope: new Tollbooth game, API orchestration, one bespoke player action, web offer UI, test coverage, and docs updates.
- Excluded scope: broad refactors of the generic game flow, card-location enum redesign, or cleanup of unrelated routing/client duplication.
- Recommended architecture decision: keep the two display cards in CommunityCards for public rendering, but branch Tollbooth out of every evaluation path so they remain display-only.
- Recommended endpoint decision: keep the Seven Card Stud family for shared lifecycle endpoints and add one dedicated Tollbooth offer action surface instead of cloning a whole separate API family.
- Recommended state decision: reuse CurrentDrawPlayerIndex, HasDrawnThisRound, and visible Community cards before introducing new persistence primitives.

**Further Considerations**
1. If implementation reveals the offer-round state is too awkward to infer from existing entities, the next-best fallback is a small JSON variant-state helper for Tollbooth, modeled on the Bob Barker player variant-state pattern rather than a schema migration.
2. If the web table becomes visually ambiguous, add a light label treatment for the two Tollbooth display cards in TableCanvas, but keep the data contract unchanged unless the UI truly needs a dedicated flag.
