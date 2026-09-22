# Prompt 05 — Add SignalR broadcast telemetry (metrics, spans, correlation)

> Addresses audit finding **F4** — SignalR hubs and broadcasters use `ILogger` only, with **no
> metrics and no spans**. "Player didn't see the update" is a top support class and is currently
> undiagnosable: broadcast failures and per-connection send errors are not countable or joinable to
> a game.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET solution. Add OpenTelemetry **metrics** and **spans** plus
consistent **log correlation** to the SignalR broadcast paths in `CardGames.Poker.Api`.

### Context (verify before editing)

- Hubs: `src/CardGames.Poker.Api/Hubs/GameHub.cs`, `LobbyHub.cs`, `NotificationHub.cs`,
  `LeagueHub.cs`. Mapped in `Program.cs` at `/hubs/game`, `/hubs/lobby`, `/hubs/notifications`,
  `/hubs/leagues`.
- Broadcasters (scoped services that wrap `IHubContext` sends):
  `src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs`, `LobbyBroadcaster.cs`,
  `LeagueBroadcaster.cs`, `GameJoinRequestBroadcaster.cs` (interfaces alongside them).
- These use `ILogger` but expose no metrics and no spans.
- Shared `ActivitySource`: `CardGames.Poker.Api.Infrastructure.Telemetry.PokerActivitySource`
  (prompt 02). Meter naming pattern: `CardGames.Poker.Api.<Area>`; create via `IMeterFactory`
  (see `LeaguesTelemetry`).

### Step 1 — Broadcast telemetry type

Add `src/CardGames.Poker.Api/Services/BroadcastTelemetry.cs`:

```csharp
using System.Diagnostics.Metrics;

namespace CardGames.Poker.Api.Services;

public sealed class BroadcastTelemetry
{
    public const string MeterName = "CardGames.Poker.Api.Realtime";

    private readonly Counter<long> _broadcasts;
    private readonly Histogram<double> _broadcastDurationMs;

    public BroadcastTelemetry(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        // hub: game | lobby | league | notification ; event: low-cardinality method name ; outcome: ok | failed
        _broadcasts = meter.CreateCounter<long>("realtime_broadcasts_total");
        _broadcastDurationMs = meter.CreateHistogram<double>("realtime_broadcast_duration_ms", unit: "ms");
    }

    public void RecordBroadcast(string hub, string @event, string outcome, double durationMs)
    {
        _broadcasts.Add(1, new("hub", hub), new("event", @event), new("outcome", outcome));
        _broadcastDurationMs.Record(durationMs, new("hub", hub), new("event", @event));
    }
}
```

> **Cardinality rule:** `hub`, `event` (the SignalR method/event name), and `outcome` are fixed,
> low-cardinality strings — safe. **Do not** tag with `ConnectionId`, `GameId`, or `UserId`.

### Step 2 — Register it

In `Program.cs`:
- `builder.Services.AddSingleton<BroadcastTelemetry>();`
- `x.AddMeter(BroadcastTelemetry.MeterName);`

Inject `BroadcastTelemetry` into each broadcaster.

### Step 3 — Instrument each broadcaster send

In each broadcaster method that sends to clients:
- Time the send, wrap in try/catch, and call
  `RecordBroadcast(hub, eventName, outcome, elapsedMs)` with `outcome` = `ok` or `failed`.
- Open a span around the send and tag with correlation IDs (span tags, not metric tags):
  ```csharp
  using var activity = PokerActivitySource.Source.StartActivity("realtime.broadcast");
  activity?.SetTag("hub", hub);
  activity?.SetTag("event", eventName);
  activity?.SetTag("game.id", gameId);   // when applicable
  ```
- On failure, log at `Error` (with the exception) **and** set the span status to error; today some
  send failures are swallowed — ensure every send failure is both counted and logged.

### Step 4 — Hub connection-lifecycle correlation

In each hub's `OnConnectedAsync`/`OnDisconnectedAsync` and join methods, wrap the work in a logging
scope so every log line carries the connection/game/user identifiers:

```csharp
using var scope = _logger.BeginScope(new Dictionary<string, object>
{
    ["ConnectionId"] = Context.ConnectionId,
    ["UserId"] = Context.UserIdentifier ?? "anonymous",
    // ["GameId"] / ["LeagueId"] where known
});
```

Ensure connect / join / disconnect are logged at `Information` with these identifiers, and
disconnects with an exception are logged at `Warning`/`Error` including
`exception` and the `ConnectionId`.

### Step 5 — Tests

Add broadcaster tests (mirror `src/Tests/CardGames.Poker.Tests/Api/Services/GameStateBroadcasterTests.cs`)
using NSubstitute for `IHubContext` and a `MeterListener`. Assert:
- A successful broadcast records `realtime_broadcasts_total{outcome=ok}`.
- A send that throws records `realtime_broadcasts_total{outcome=failed}`, logs at `Error`, and does
  not crash the caller (or rethrows per existing contract — match current behavior).

### Acceptance criteria

- Build succeeds; new tests pass.
- `realtime_broadcasts_total{hub,event,outcome}` and `realtime_broadcast_duration_ms` appear on
  `/metrics`.
- Every broadcaster send failure is both counted (`outcome=failed`) and logged with the relevant IDs.
- Hub connect/join/disconnect logs always include `ConnectionId` and `UserId`.
