# Prompt 06 — Add structured log-scope correlation across game-flow code

> Addresses audit finding **F9** (and the "missing identifiers" logging risk) — logs in game-flow
> handlers, broadcasters, and background work frequently omit stable identifiers
> (`GameId`, `HandNumber`, `UserId`, `LeagueId`, `ConnectionId`), so they cannot be filtered per
> game/hand/player or joined to traces.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET solution. Add **structured logging scopes** so that every log
line emitted while handling a game action, advancing a hand, or broadcasting carries the relevant
business identifiers. Do not add new log statements for their own sake — enrich the existing ones
with scope context. OpenTelemetry logging already has `IncludeScopes = true`, so scope values flow
into log records automatically.

### Context (verify before editing)

- OTel logging is configured with `IncludeScopes = true` and `IncludeFormattedMessage = true` in
  `src/CardGames.ServiceDefaults/Extensions.cs` (and was duplicated in `Program.cs` — see prompt 01).
- Game-flow command handlers live under
  `src/CardGames.Poker.Api/Features/Games/<Variant>/v1/Commands/...` and use `ILogger`.
- The MediatR `LoggingPipelineBehavior` already opens a scope with `RequestType`
  (`src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/LoggingPipelineBehavior.cs`) — follow
  the **same** `BeginScope(IReadOnlyDictionary<string, object>)` pattern.

### Standard scope keys (use these exact names everywhere)

`GameId`, `HandNumber`, `UserId`, `LeagueId`, `ConnectionId`, `RequestType`.

### Steps

1. **Game-flow handlers:** at the start of each `Handle(...)` that operates on a game, open a scope
   with the identifiers known from the command (at minimum `GameId`, and `UserId`/`HandNumber` when
   available):
   ```csharp
   using var scope = _logger.BeginScope(new Dictionary<string, object>
   {
       ["GameId"] = request.GameId,
       ["UserId"] = request.UserId,   // when present
   });
   ```
   Keep the scope wrapping the whole handler so every downstream log inherits it.

2. **Background loop:** in `ContinuousPlayBackgroundService` per-game processing, open a scope with
   `GameId` and `HandNumber` around each game's work (complements the span from prompt 04).

3. **Broadcasters / hubs:** ensure scopes include `ConnectionId` (hubs) and `GameId`/`LeagueId`
   (broadcasters) — this overlaps with prompt 05; if prompt 05 is done, just confirm the keys match
   these standard names.

4. **Do not** put secrets, tokens, emails, full request payloads, or private cards into scopes
   (same rule as F1). Only stable identifiers.

5. **Consistency check:** standardize any pre-existing log calls that pass these identifiers as
   message arguments (e.g. `LogInformation("... {GameId}", gameId)`) so the property name matches the
   standard keys above (`GameId`, not `gameId`/`game_id`).

### Tests

- Where handlers already have unit tests, add a `CapturingLogger`/`ILogger` fake (see
  `src/Tests/CardGames.Poker.Tests/Api/Infrastructure/PipelineBehaviors/LoggingPipelineBehaviorTests.cs`
  for a reusable capturing-logger pattern) and assert the expected scope keys are present for at
  least one representative game-flow handler.

### Acceptance criteria

- Build succeeds; tests pass.
- Logs emitted during a game action, a background hand advancement, and a broadcast all carry
  `GameId` (and `HandNumber`/`UserId`/`ConnectionId` where applicable) as structured properties.
- Property names are consistent (`GameId`, `HandNumber`, `UserId`, `LeagueId`, `ConnectionId`,
  `RequestType`) across the codebase.
- No payloads/secrets are added to scopes.
