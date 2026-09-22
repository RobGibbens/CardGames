# Prompt 04 — Add metrics and spans to the continuous-play background loop

> Addresses audit finding **F3** — `ContinuousPlayBackgroundService` is the game heartbeat
> (next-hand, draw transitions, rebuy grace, abandoned games, league sync) but has **no metrics and
> no spans**; failures and backlogs are only visible as hard-to-aggregate log lines.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET solution. Add OpenTelemetry **metrics** and **spans** to the
continuous-play background loop in `CardGames.Poker.Api` so we can alert on backlogs and per-game
failures and trace individual hand advancements.

### Context (verify before editing)

- `ContinuousPlayBackgroundService` is a `partial class` split by concern under
  `src/CardGames.Poker.Api/Services/`:
  - `ContinuousPlayBackgroundService.cs` — orchestration: `ExecuteAsync` +
    `ProcessGamesReadyForNextHandAsync`.
  - `.AbandonedGames.cs`, `.DrawTransitions.cs`, `.NextHand.cs`, `.RebuyGrace.cs`, `.LeagueSync.cs`.
- It is registered with `builder.Services.AddHostedService<ContinuousPlayBackgroundService>();`.
- It currently uses `ILogger` only — no `Meter`, no `ActivitySource`.
- The shared `ActivitySource` is `CardGames.Poker.Api.Infrastructure.Telemetry.PokerActivitySource`
  (create it via prompt 02 first if it does not exist).
- Custom meters follow the name pattern `CardGames.Poker.Api.<Area>` and are registered in
  `Program.cs` with `x.AddMeter(...)`. Meter instances are created with `IMeterFactory` (see
  `LeaguesTelemetry` for the canonical example).

### Step 1 — Create a telemetry type for the loop

Add `src/CardGames.Poker.Api/Services/ContinuousPlayTelemetry.cs`:

```csharp
using System.Diagnostics.Metrics;

namespace CardGames.Poker.Api.Services;

public sealed class ContinuousPlayTelemetry
{
    public const string MeterName = "CardGames.Poker.Api.ContinuousPlay";

    private readonly Counter<long> _gamesProcessed;
    private readonly Histogram<double> _iterationDurationMs;

    public ContinuousPlayTelemetry(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        // phase: next_hand | draw_transition | rebuy_grace | abandoned | league_sync
        // outcome: advanced | skipped | failed
        _gamesProcessed = meter.CreateCounter<long>("continuous_play_games_processed_total");
        _iterationDurationMs = meter.CreateHistogram<double>("continuous_play_iteration_duration_ms", unit: "ms");
    }

    public void RecordGameProcessed(string phase, string outcome)
        => _gamesProcessed.Add(1, new("phase", phase), new("outcome", outcome));

    public void RecordIteration(double durationMs)
        => _iterationDurationMs.Record(durationMs);
}
```

> **Cardinality rule:** `phase` and `outcome` are fixed enums — safe. **Never** add `GameId` /
> `UserId` as metric tags; those go on spans/logs only.

### Step 2 — Register it

In `Program.cs`:
- `builder.Services.AddSingleton<ContinuousPlayTelemetry>();`
- Add the meter to metrics: `x.AddMeter(ContinuousPlayTelemetry.MeterName);` (or
  `x.AddMeter("CardGames.Poker.Api.ContinuousPlay")`).

Inject `ContinuousPlayTelemetry` into `ContinuousPlayBackgroundService` (it is a singleton hosted
service, so constructor injection of a singleton is fine).

### Step 3 — Instrument the loop

In `ContinuousPlayBackgroundService.cs` `ExecuteAsync`/`ProcessGamesReadyForNextHandAsync`:
- Time each loop iteration with a `Stopwatch` and call `RecordIteration(elapsedMs)`.
- For each game processed in each phase, call `RecordGameProcessed(phase, outcome)` with the correct
  `phase` constant and an `outcome` of `advanced`, `skipped`, or `failed`.
- Wrap each **per-game advancement** in a span and tag it with the correlation identifiers (these
  are span tags, not metric tags):

  ```csharp
  using var activity = PokerActivitySource.Source.StartActivity("continuous_play.advance");
  activity?.SetTag("game.id", gameId);
  activity?.SetTag("hand.number", handNumber);
  activity?.SetTag("phase", phase);
  ```
- In the per-game `catch` blocks that already log failures, also call
  `RecordGameProcessed(phase, "failed")` and set `activity?.SetStatus(ActivityStatusCode.Error, ex.Message)`.

Apply the same pattern in the partial files for their respective phases (`.NextHand.cs`,
`.DrawTransitions.cs`, `.RebuyGrace.cs`, `.AbandonedGames.cs`, `.LeagueSync.cs`).

### Step 4 — Tests

Extend the existing background-service tests
(`src/Tests/CardGames.IntegrationTests/Services/ContinuousPlayBackgroundServiceTests.cs`, which calls
the internal `ProcessGamesReadyForNextHandAsync` directly with in-memory fakes). Use a
`MeterListener` to record measurements and assert:
- A successful advancement increments `continuous_play_games_processed_total` with
  `phase=next_hand, outcome=advanced`.
- A forced per-game failure increments the counter with `outcome=failed` and still continues the
  loop (does not throw out of the iteration).

### Acceptance criteria

- Build succeeds; new/updated tests pass.
- `continuous_play_games_processed_total{phase,outcome}` and
  `continuous_play_iteration_duration_ms` are visible on the Prometheus `/metrics` endpoint after the
  loop runs.
- Each advanced hand produces a `continuous_play.advance` span carrying `game.id` and `hand.number`.
