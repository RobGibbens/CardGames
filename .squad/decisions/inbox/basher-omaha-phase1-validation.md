# Basher — Omaha Phase 1 (Internal Validation) Slice

**Date:** 2026-03-06
**Requested by:** Rob Gibbens

## Decision
For Omaha Phase 1 (internal validation), use a *targeted* test slice that covers Omaha-specific behavior plus the minimum required regression surface, without running the full solution test suite.

### Commands
1) Poker unit/regression suite (fast evaluator + rules signal):
- `dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj -c Release`

2) Integration subset focused on Omaha flows + required regressions:
- `dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj -c Release --filter 'FullyQualifiedName~Omaha|FullyQualifiedName~HoldEmHandLifecycleTests|FullyQualifiedName~ChooseDealerGameCommandTests|FullyQualifiedName~CreateGameCommandHandlerTests|FullyQualifiedName~DealersChoiceContinuousPlayTests'`

## Rationale
- Covers Omaha evaluator/rules + Omaha creation + Dealer’s Choice selection + continuous play, while pairing Hold’em lifecycle to detect accidental regressions (per docs/OmahaPRD.md backward-compat strategy).
- Avoids full-suite runtime while still exercising the highest-risk Omaha routing/orchestration surfaces.

## Outcome
- ✅ All passing on 2026-03-06 (Poker.Tests: 557/557; IntegrationTests filtered: 62/62).
