# Klondike Hold'em Research Summary

## All Touchpoints for Adding a New Hold'em Variant

### 1. Domain Layer (src/CardGames.Poker/Games/)
- Create `Klondike/` directory with `KlondikeGame.cs` and `KlondikeRules.cs`
- Implement `IPokerGame`, decorate with `[PokerGameMetadata(...)]`
- Auto-discovered by PokerGameMetadataRegistry via assembly scanning

### 2. Hand Evaluator (src/CardGames.Poker/Evaluation/Evaluators/)
- Create `KlondikeHandEvaluator.cs` implementing `IHandEvaluator`
- Decorate with `[HandEvaluator("KLONDIKE")]`
- Auto-discovered by HandEvaluatorFactory via assembly scanning
- MUST handle wild card (the Klondike Card) - see WildCardHandEvaluator for pattern

### 3. Flow Handler (src/CardGames.Poker.Api/GameFlow/)
- Create `KlondikeFlowHandler.cs` extending `BaseGameFlowHandler`
- Auto-discovered by GameFlowHandlerFactory via assembly scanning
- Override `GameTypeCode => "KLONDIKE"`

### 4. API Wiring
- PokerGameMetadataRegistry: Add `KlondikeCode = "KLONDIKE"` constant
- ProcessBettingActionCommandHandler: Add `IsKlondikeGame()` check + Klondike card dealing logic
- PerformShowdownCommandHandler: Add to `UsesSharedCommunityCards()` 
- TableStateBuilder: Add to `IsHoldEmGame()` check

### 5. Web Routing (src/CardGames.Poker.Web/)
- IGameApiRouter.cs: Add `Klondike` constant + routing entry in `_bettingActionRoutes`
- TablePlay.razor: Add `IsKlondike` property + include in all Hold'em-style checks
- EditTable.razor: Add to blind-based game check
- TableCanvas.razor: Add to `IsBlindBasedGame` + add felt icon

### 6. Integration Tests
- Create under src/Tests/CardGames.IntegrationTests/Games/HoldEm/

### 7. Wild Card Handling
- WildCardRule enum: May need new value (e.g., `SingleHiddenCard`)
- Hands/WildCards/: Contains existing wild card evaluators for reference

### Key Discovery Patterns
- PokerGameMetadataRegistry: Assembly scans IPokerGame + [PokerGameMetadata]
- HandEvaluatorFactory: Assembly scans IHandEvaluator + [HandEvaluator]
- GameFlowHandlerFactory: Assembly scans IGameFlowHandler
- All use FrozenDictionary for perf
