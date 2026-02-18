# Basher Decision Inbox — Issue #224 Quality Gates

Date: 2026-02-18  
Requested by: Rob Gibbens  
Agent: Basher (Tester)

## Decisions in this pass

1. **Leagues P0 quality-gate coverage centered on API integration tests**
   - Added end-to-end API coverage for: join by code with trust preview and request submit, admin approve/deny moderation, and moderation queue behaviors.
   - Rationale: these represent the highest-value regression surface for authz/idempotency/error semantics while staying deterministic and close to external behavior.

2. **Bridge hardening for event -> playable session launch focuses on conflict semantics**
   - Added a second-launch conflict test that verifies original event-to-game linkage remains stable.
   - Rationale: guards non-idempotent duplicate launch behavior that could create orphaned/mismatched sessions.

3. **Result ingestion -> standings added as non-breaking scaffold**
   - Added a skipped integration test marker for the future journey because standings/result-ingestion backend endpoints are not yet present.
   - Rationale: keeps builds green while making missing v0.5.0 gate scope explicit and discoverable.

4. **CI quality gate is enforced in `squad-ci` via .NET integration test execution**
   - Replaced placeholder Node test step with restore/build + integration test gate for `CardGames.IntegrationTests` on PRs and `dev` pushes.
   - Rationale: ensures P0 leagues journey tests are required in CI before merge progression.
