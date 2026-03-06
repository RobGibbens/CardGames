### 2026-03-02T20:00:00Z: Phase 1 schema changes blocked on 3 nullable GameTypeId errors
**By:** Danny (Backend Dev)
**What:** All Dealer's Choice DB schema changes are applied (Game.cs, DealersChoiceHandLog, GameConfiguration, CardsDbContext, Phases enum). However, making `GameTypeId` nullable caused 3 compile errors in existing code that assumes non-null:
1. `GetActiveGamesMapper.cs:16` — cannot convert `Guid?` to `Guid`
2. `LobbyStateBroadcastingBehavior.cs:130` — cannot convert `Guid?` to `Guid`
3. `GetGameMapper.cs:19` — cannot convert `Guid?` to `Guid`

EF migration `AddDealersChoice` cannot be generated until these are fixed. These are Phase 2 handler fixes.
**Why:** Nullable FK is required for Dealer's Choice tables where game type is chosen per-hand, not at table creation.
