**Plan: Private Rabbit Hunt**

Add a single authenticated, read-only Rabbit Hunt query under the common game endpoints and keep the reveal entirely private to the requesting client. The server should compute the completed community board from persisted GameCards using the same variant-specific runout rules the game already uses, so the revealed cards are the exact cards that would have appeared if the hand had continued.

**Steps**
1. Phase 1, server query and projection. Add a new common Rabbit Hunt query/endpoint beside the existing game-scoped queries in src/CardGames.Poker.Api/Features/Games/Common/v1/V1.cs. The handler should validate that the caller is authenticated, seated in the game, the game type is in the selected community-card family, and the current hand has an incomplete public board.
2. Centralize completed-board calculation instead of hard-coding a five-card assumption. Reuse or extract the existing future-street logic from src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs and align it with the supported shared-board variant list in src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs. That projector needs to cover Klondike reveal behavior, Red River bonus board logic, and South Dakota’s shorter runout.
3. Return a private DTO that carries the completed board in display order, the already-public cards, the newly revealed Rabbit Hunt cards, the hand number, and the game type. Keep this ephemeral: no DB writes, no public showdown changes, no SignalR rebroadcast.
4. Add a temporary IGamesApi partial extension in Contracts, following the same manual-extension pattern as src/CardGames.Contracts/GenericShowdownExtensions.cs, so the web app can call the new endpoint without editing generated src/CardGames.Contracts/RefitInterface.v1.cs. Regenerating via src/CardGames.Poker.Refitter/.refitter can stay as a cleanup step.
5. Phase 2, client state in src/CardGames.Poker.Web/Components/Pages/TablePlay.razor. Add local Rabbit Hunt state, loading/error handling, and a request method that calls the common endpoint through GamesApiClient. Clear that state when the overlay closes, when the hand number changes, or when a new results phase starts.
6. Extend src/CardGames.Poker.Web/Components/Shared/ShowdownOverlay.razor with CanRabbitHunt, RabbitHuntState, IsRabbitHuntLoading, and an OnRabbitHunt callback. Show the button only when the current variant is in scope and the full board is not already public. Render a dedicated Rabbit Hunt board section after success, with clear visual separation between cards that were already public and cards revealed only by Rabbit Hunt.
7. Update src/CardGames.Poker.Web/Components/Shared/ShowdownOverlay.razor.css so the new control and reveal section fit the existing overlay layout on desktop and mobile without crowding the winner panel or Bob Barker showcase area.
8. Phase 3, tests. Add server-side coverage for the completed-board projector and query, and web-side coverage for the overlay gating and local-state lifecycle, using the existing test patterns in src/Tests/CardGames.IntegrationTests/Services/TableStateBuilderTests.cs and src/Tests/CardGames.Poker.Tests/Web/BobBarkerShowdownOverlayTests.cs.

**Relevant files**
- src/CardGames.Poker.Api/Features/Games/Common/v1/V1.cs — add the new common endpoint mapping.
- src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetHandHistory/GetHandHistoryEndpoint.cs — reference pattern for a common game-scoped query.
- src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs — existing runout logic to reuse.
- src/CardGames.Poker.Api/Features/Games/HoldTheBaseball/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs — variant-specific remaining-board logic to align with the shared projector.
- src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs — current shared-board variant scope.
- src/CardGames.Poker.Web/Components/Pages/TablePlay.razor — own per-client Rabbit Hunt state and pass it into the overlay.
- src/CardGames.Poker.Web/Components/Shared/ShowdownOverlay.razor — add the button and private reveal UI.
- src/CardGames.Poker.Web/Components/Shared/ShowdownOverlay.razor.css — style the new section.
- src/CardGames.Contracts/GenericShowdownExtensions.cs — pattern for temporary IGamesApi additions.
- src/CardGames.Contracts/RefitInterface.v1.cs — generated output to avoid editing directly.
- src/CardGames.Poker.Refitter/.refitter — regeneration config if you want to fold the endpoint into generated contracts later.

**Verification**
1. Add integration tests for preflop, flop, and turn endings to confirm Rabbit Hunt returns the exact next cards from persisted deck order.
2. Add variant-specific tests for Klondike hidden-card reveal, Red River bonus-board qualification, South Dakota short board, and unsupported-game rejection.
3. Add web tests to verify the button only appears for in-scope community-card variants with incomplete public boards, and that local Rabbit Hunt state resets on overlay close and new-hand transitions.
4. Run dotnet build src/CardGames.sln.
5. Run dotnet test src/CardGames.sln.
6. Manually verify with two browser sessions that only the player who clicks Rabbit Hunt sees the completed board.

**Decisions**
- Included variants, per your selection: HOLDEM, REDRIVER, KLONDIKE, IRISHHOLDEM, PHILSMOM, CRAZYPINEAPPLE, HOLDTHEBASEBALL, OMAHA, NEBRASKA, SOUTHDAKOTA, and BOBBARKER.
- Recommended transport: a common authenticated query plus client-local state, not a public showdown payload change and not a server-persisted private-state flag.
- Scope boundary: Rabbit Hunt is informational only. It does not change settlements, showdown results, hand history, or shared SignalR state.

1. Approve this plan as-is and I’ll hand off to implementation.
2. If you want a smaller first slice, I’d trim the first implementation to the Hold’em-family variants and add Omaha/Nebraska/Bob Barker after the shared projector is proven.
