# Prompt 07 — Add hand-history and showdown telemetry

> Addresses the **hand history** row of the audit Coverage Map — `HandHistoryRecorder` logs only,
> with no span and no success/failure counter. When hand history is missing or wrong, there is no
> aggregate signal and no trace of where recording failed.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET solution. Add a span and a success/failure counter around
hand-history recording in `CardGames.Poker.Api`.

### Context (verify before editing)

- `src/CardGames.Poker.Api/Services/HandHistoryRecorder.cs` implements `IHandHistoryRecorder`,
  depends on `CardsDbContext` and `ILogger<HandHistoryRecorder>`, and is registered as scoped:
  `builder.Services.AddScoped<IHandHistoryRecorder, HandHistoryRecorder>();`.
- Shared `ActivitySource`: `CardGames.Poker.Api.Infrastructure.Telemetry.PokerActivitySource`
  (prompt 02). Meter naming: `CardGames.Poker.Api.<Area>`; create via `IMeterFactory`
  (see `LeaguesTelemetry`).

### Step 1 — Telemetry type

Add `src/CardGames.Poker.Api/Services/HandHistoryTelemetry.cs`:

```csharp
using System.Diagnostics.Metrics;

namespace CardGames.Poker.Api.Services;

public sealed class HandHistoryTelemetry
{
    public const string MeterName = "CardGames.Poker.Api.HandHistory";

    private readonly Counter<long> _recorded;

    public HandHistoryTelemetry(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        // game_type: variant code (low cardinality) ; outcome: recorded | failed
        _recorded = meter.CreateCounter<long>("hand_history_recorded_total");
    }

    public void RecordOutcome(string gameType, string outcome)
        => _recorded.Add(1, new("game_type", gameType), new("outcome", outcome));
}
```

> **Cardinality rule:** `game_type` must be the variant **code** (e.g. `HOLDEM`), not a per-hand id.
> `outcome` is `recorded` or `failed`. Never tag with `GameId`/`HandNumber` (put those on the span).

### Step 2 — Register it

In `Program.cs`:
- `builder.Services.AddScoped<HandHistoryTelemetry>();` (or singleton — it only holds a counter; a
  singleton is fine and matches `LeaguesTelemetry`).
- `x.AddMeter(HandHistoryTelemetry.MeterName);`

Inject `HandHistoryTelemetry` into `HandHistoryRecorder`.

### Step 3 — Instrument recording

In the record method:
```csharp
using var activity = PokerActivitySource.Source.StartActivity("hand_history.record");
activity?.SetTag("game.id", gameId);
activity?.SetTag("hand.number", handNumber);

try
{
    // existing recording work
    _telemetry.RecordOutcome(gameType, "recorded");
}
catch (Exception ex)
{
    _telemetry.RecordOutcome(gameType, "failed");
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    _logger.LogError(ex, "Failed to record hand history for {GameId} hand {HandNumber}", gameId, handNumber);
    throw; // match existing error-propagation behavior
}
```
Do **not** log private cards or full hand payloads — only identifiers and counts.

### Step 4 — Tests

Add `HandHistoryRecorderTelemetryTests` (mirror existing `Api/Services/*Tests.cs`) using an in-memory
`CardsDbContext` and a `MeterListener`. Assert:
- A successful record increments `hand_history_recorded_total{outcome=recorded}`.
- A forced failure increments `hand_history_recorded_total{outcome=failed}` and rethrows.

### Acceptance criteria

- Build succeeds; new tests pass.
- `hand_history_recorded_total{game_type,outcome}` appears on `/metrics`.
- Each recording produces a `hand_history.record` span with `game.id` and `hand.number`, and
  failures are counted, logged, and marked error on the span.
