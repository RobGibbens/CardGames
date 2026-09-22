# OpenTelemetry & Logging Audit — CardGames.Poker.Api

Audit of OpenTelemetry, structured logging, metrics, traces, and diagnostic coverage for the
poker/card-game platform, focused on `CardGames.Poker.Api` and the shared
`CardGames.ServiceDefaults`. Every conclusion below is grounded in a concrete repository location.

> Scope note: This document is the audit deliverable. One critical finding (F1) has also been
> fixed in code as part of this change; the remaining findings are recommendations.

---

## Executive Summary

The host **wires up logs, traces, and metrics** and gets a lot for free from the Aspire
`ServiceDefaults` (ASP.NET Core, HttpClient, Runtime, FusionCache instrumentation, OTLP exporter
when `OTEL_EXPORTER_OTLP_ENDPOINT` is set). For generic HTTP request health, the platform is
**partially observability-ready**.

For the *domain* — game lifecycle, hand advancement, the continuous-play background loop, SignalR
broadcasting, rebuy/grace flows, showdown/hand-history — the platform is **not** production-ready
for debugging. There is **zero custom `ActivitySource`/span usage anywhere in `CardGames.Poker.Api`**
(`grep` for `ActivitySource`/`StartActivity` in the project returns only the `AppContext.SetSwitch`
line in `Program.cs`). The only custom metric surface is `LeaguesTelemetry`. Multi-step workflows are
observable through logs only, and those logs frequently lack stable identifiers
(`GameId`, `HandNumber`, `UserId`, `ConnectionId`).

Most urgent, the MediatR `LoggingPipelineBehavior` **dumped every request property at `Information`
level** (`LogRequest` reflected over all properties and logged `{@Value}`), which leaks secrets,
tokens, emails, and full payloads, and is high-volume noise. This is fixed in this change.

Bottom line: good plumbing, dangerous blind spots in the domain. A future engineer asking
"what happened to this game/hand/player?" cannot currently answer it from traces or metrics.

---

## Findings

Severity-ordered.

### F1 — CRITICAL — MediatR pipeline logged full request payloads (secret/PII leak)
- **Why it matters:** Commands such as auth/seed/profile/league flows carry tokens, emails, and
  user data. Dumping them to logs is a security and privacy incident waiting to happen and floods
  log storage.
- **Evidence:** `CardGames.Poker.Api/Infrastructure/PipelineBehaviors/LoggingPipelineBehavior.cs`
  `LogRequest` previously did:
  ```csharp
  foreach (var property in GetProperties(request))
      _logger.LogInformation("{Property} : {@Value}", property.Name, property.GetValue(request, null));
  ```
  Every property of every command/query was serialized at `Information`.
- **Recommended fix (applied):** Stop logging request instances. Log only the low-cardinality
  `RequestType`, wrap the handler in a logging **scope** carrying `RequestType` for correlation,
  log start at `Debug`, success at `Information` with elapsed ms, and failure at `Error` with the
  exception and elapsed ms. Handlers that need a business identifier should log it explicitly.

### F2 — HIGH — No custom spans for domain workflows (`ActivitySource` absent)
- **Why it matters:** Game-flow handlers, `TableStateBuilder`, `ContinuousPlayBackgroundService`,
  broadcasters, and hubs are multi-step and stateful. Without spans, latency regressions and
  partial failures in these flows are invisible in a trace backend; you cannot see where a hand
  advancement spent its time or which step failed.
- **Evidence:** No `ActivitySource` exists in `CardGames.Poker.Api` (only `Migrations` defines one,
  in `CardGames.MigrationService/Worker.cs`). MediatR has **no** tracing behavior registered
  (`Program.cs` registers only `LoggingPipelineBehavior`, `GameStateBroadcastingBehavior`,
  `LobbyStateBroadcastingBehavior`, `LeagueGameCompletionSyncBehavior`).
- **Recommended fix:** Introduce a single `ActivitySource` (e.g. `CardGames.Poker.Api`), register it
  with `AddSource(...)`, add a `TracingPipelineBehavior` that opens a span per MediatR request, and
  open child spans/span-events at the major game-flow steps and in the background loop.

### F3 — HIGH — Background loop steps have no spans/metrics; failures only logged
- **Why it matters:** `ContinuousPlayBackgroundService` is the heartbeat of the game (next-hand,
  draw transitions, rebuy grace, abandoned games, league sync). If it backs up, silently skips
  games, or throws per-game, there is no counter/histogram/queue-depth signal to alert on — only
  log lines that are hard to aggregate.
- **Evidence:** `CardGames.Poker.Api/Services/ContinuousPlayBackgroundService*.cs` (partial class
  split by concern) uses `ILogger` only; there is no `Meter`/`Counter`/`Histogram` and no
  `ActivitySource`.
- **Recommended fix:** Add per-iteration metrics: a counter of games processed per phase with an
  `outcome` tag, a histogram of loop iteration duration, and an up-down counter / gauge of games
  pending advancement. Wrap each per-game advancement in a span with `GameId`/`HandNumber`.

### F4 — HIGH — SignalR broadcast/send paths lack failure metrics and correlation
- **Why it matters:** "Player didn't see the update" is a top support class. Broadcast failures,
  per-connection send errors, and join/disconnect churn need to be countable and joinable to a game.
- **Evidence:** Hubs under `CardGames.Poker.Api/Hubs/*.cs` and the broadcasters
  (`GameStateBroadcaster`, `LobbyBroadcaster`, `LeagueBroadcaster`, `GameJoinRequestBroadcaster`)
  use `ILogger` but expose no metrics; SignalR has no custom `ActivitySource`. Only one `catch`
  exists across the hub files.
- **Recommended fix:** Add a `broadcasts_total{hub,event,outcome}` counter and a
  `broadcast_latency_ms` histogram; ensure connect/join/disconnect logs always carry
  `ConnectionId`, `GameId`/`LeagueId`, and `UserId`; wrap sends in spans.

### F5 — MEDIUM — Resource/service naming is generic and template-derived
- **Why it matters:** Spans and metrics from the Poker API report `service.name = "api"`, which is
  ambiguous in a multi-service deployment and hard to query. A leftover meter name suggests
  copy-paste from a template.
- **Evidence:** `Program.cs` calls `ResourceBuilder.CreateDefault().AddService("api")` (twice), and
  `x.AddMeter("Forkful.ApiService")` — "Forkful" is unrelated to this product and no meter by that
  name is created in this codebase, so the registration is dead.
- **Recommended fix:** Use a meaningful, consistent service name (e.g. `cardgames-poker-api`) and
  remove the dead `Forkful.ApiService` meter registration. Prefer setting the resource once /
  via `OTEL_SERVICE_NAME` so the host and `ServiceDefaults` agree.

### F6 — MEDIUM — Console exporters only; no production export path in the host
- **Why it matters:** `AddConsoleExporter()` is a local-dev sink. Real debugging needs OTLP/Azure
  Monitor. The host duplicates exporter setup that `ServiceDefaults` already conditionally provides.
- **Evidence:** `Program.cs` adds `AddConsoleExporter()` to both tracing and metrics; the only
  production-grade exporter (`UseOtlpExporter`, gated on `OTEL_EXPORTER_OTLP_ENDPOINT`) lives in
  `CardGames.ServiceDefaults/Extensions.cs`. The Prometheus exporter + scraping endpoint is wired
  for metrics, which is good.
- **Recommended fix:** Drop the console exporters (or gate on Development) and rely on the
  OTLP path; confirm `OTEL_EXPORTER_OTLP_ENDPOINT` is set in non-dev environments.

### F7 — MEDIUM — Duplicated, divergent OTel bootstrap (host vs ServiceDefaults)
- **Why it matters:** Two places configure OpenTelemetry with different resource builders, samplers,
  and exporters. Divergence causes confusing, inconsistent telemetry and makes it easy to silently
  drop a source/meter.
- **Evidence:** `builder.AddServiceDefaults()` → `ConfigureOpenTelemetry()` sets logging, metrics,
  tracing and the OTLP exporter, while `Program.cs` calls `AddOpenTelemetry().WithTracing/WithMetrics`
  **again** with its own `ResourceBuilder`/console exporters, and `builder.Logging.AddOpenTelemetry`
  is configured in both files.
- **Recommended fix:** Keep cross-cutting concerns (resource, exporters, logging) in
  `ServiceDefaults` and add only Poker-specific sources/meters in `Program.cs`.

### F8 — MEDIUM — Custom metric tags risk cardinality and miss key dimensions
- **Why it matters:** `LeaguesTelemetry.RecordEndpointLatency` tags with `endpoint` and
  `status_code`. If `endpoint` is ever a raw path with ids, cardinality explodes; if it is a route
  template, it is fine. There is no `outcome`/error dimension on latency.
- **Evidence:** `CardGames.Poker.Api/Features/Leagues/v1/Telemetry/LeaguesTelemetry.cs`.
- **Recommended fix:** Document/guarantee `endpoint` is a route template (low cardinality); the
  existing `funnel{step,outcome}` counter is a good pattern to extend to other workflows.

### F9 — LOW — Logs/traces correlation relies entirely on ambient ASP.NET context
- **Why it matters:** Logs include scopes (`IncludeScopes = true`) and trace ids inside HTTP
  requests, but background-loop and hub work runs outside an HTTP `Activity`, so those logs have no
  trace to join to.
- **Evidence:** `Program.cs` / `ServiceDefaults` enable scopes and formatted messages, but F2/F3
  show no spans are created for non-HTTP work.
- **Recommended fix:** Once F2/F3 spans exist, logs emitted within them automatically carry
  `TraceId`/`SpanId`, closing the gap.

---

## Coverage Map

| Area | Current logs | Current traces | Current metrics | Gaps | Verdict |
|---|---|---|---|---|---|
| API request pipeline | Yes (ASP.NET) | Yes (`AddAspNetCoreInstrumentation`) | Yes (ASP.NET + Prometheus) | Service name `api`; console-only host exporter | Partial |
| MediatR handlers | Yes (`LoggingPipelineBehavior`) | **No** (no tracing behavior) | No | No span/metric per request | Partial (logs fixed) |
| SignalR hubs | Partial (`ILogger`) | **No** | **No** | Broadcast failure/latency metrics, IDs | Blind spot |
| Background services | Yes (`ILogger`) | **No** | **No** | Iteration/queue/outcome metrics, spans | Blind spot |
| Game-flow handlers | Partial (`ILogger`) | **No** | **No** | State-transition spans/events, metrics | Blind spot |
| Table state building | Minimal | **No** | **No** | Span around build; private-state safety | Blind spot |
| Hand history | `ILogger` (`HandHistoryRecorder`) | **No** | **No** | Span + recorded/failed counter | Blind spot |
| League workflows | Yes (`ILogger`) | **No** | Yes (`LeaguesTelemetry`) | Spans; tag hygiene | Partial |
| Cache | n/a | Yes (FusionCache instrumentation) | Yes (FusionCache instrumentation) | Domain-level hit/miss/fallback intent | Partial |
| Outbound HTTP | n/a | Yes (`AddHttpClientInstrumentation`) | Yes | Resilience retry visibility | Good |
| Azure Service Bus | n/a | Yes (`AddSource("Azure.Messaging.ServiceBus")`) | No | DLQ/retry/processing metrics | Partial |
| Auth/authz | Partial | Via ASP.NET | No | Auth-failure counter, decision context | Partial |
| Persistence / EF Core | Via ASP.NET request | **No EF Core instrumentation** | No | `AddEntityFrameworkCoreInstrumentation` not registered | Blind spot |

---

## Missing Telemetry to Add First (top 10 by operational value)

1. **Stop dumping request payloads** in `LoggingPipelineBehavior` (done — F1).
2. **MediatR `TracingPipelineBehavior`** + a shared `ActivitySource` registered via `AddSource` (F2).
3. **Background-loop metrics**: games-processed counter (tagged by phase + outcome), iteration
   duration histogram, pending-advancement gauge (F3).
4. **Per-game advancement spans** with `GameId`/`HandNumber` in `ContinuousPlayBackgroundService` (F3).
5. **EF Core instrumentation** (`AddEntityFrameworkCoreInstrumentation`) so DB spans appear (Coverage Map).
6. **SignalR broadcast metrics**: `broadcasts_total{hub,event,outcome}` + latency histogram (F4).
7. **Consistent log scope** carrying `GameId`/`HandNumber`/`UserId`/`ConnectionId` in hubs,
   broadcasters, and game-flow handlers (F4, F9).
8. **Meaningful service name** + remove dead `Forkful.ApiService` meter (F5).
9. **Production exporter path**: drop/gate console exporters, ensure OTLP endpoint configured (F6).
10. **Hand-history + showdown counters** (`hand_history_recorded_total{outcome}`) and a span around
    `HandHistoryRecorder` (Coverage Map).

---

## Logging Risks

- **Full request property dump (CRITICAL, fixed):** old `LoggingPipelineBehavior.LogRequest`
  serialized every property at `Information` — secrets/PII/payload leak + noise.
- **Redundant `DateTime.UtcNow` properties (fixed):** the old behavior logged `{Timestamp}` on every
  line; OTel log records already carry a timestamp. Removed.
- **Per-property `Information` spam (fixed):** one log line *per property per request* drowned out
  signal. Replaced with a single start (`Debug`) / completion (`Information`) pair.
- **Error log without request context (fixed):** old `LogError` logged only a timestamp, so failures
  could not be tied to the failing request type. Now includes `RequestType` + elapsed.
- **Missing identifiers (open):** hub/broadcaster/game-flow logs frequently omit
  `GameId`/`HandNumber`/`ConnectionId`; add via logging scopes.

---

## Concrete Fix Sketches

### 1. Hardened MediatR logging behavior (applied)
See `CardGames.Poker.Api/Infrastructure/PipelineBehaviors/LoggingPipelineBehavior.cs`. Key points:
no request instance logged; logging scope with `RequestType`; `Debug` start, `Information` success
with `ElapsedMilliseconds`, `Error` failure with exception + elapsed.

### 2. Shared ActivitySource + tracing behavior (recommended)
```csharp
// Infrastructure/Telemetry/PokerTelemetry.cs
public static class PokerTelemetry
{
    public const string ActivitySourceName = "CardGames.Poker.Api";
    public static readonly ActivitySource Source = new(ActivitySourceName);
}

// Infrastructure/PipelineBehaviors/TracingPipelineBehavior.cs
public sealed class TracingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        using var activity = PokerTelemetry.Source.StartActivity($"mediatr {typeof(TRequest).Name}");
        try { return await next(ct); }
        catch (Exception ex) { activity?.SetStatus(ActivityStatusCode.Error, ex.Message); throw; }
    }
}
```
Register in `Program.cs`:
```csharp
x.AddSource(PokerTelemetry.ActivitySourceName);
// ...
.AddScoped(typeof(IPipelineBehavior<,>), typeof(TracingPipelineBehavior<,>))
```

### 3. Background-loop metrics (recommended)
```csharp
var meter = meterFactory.Create("CardGames.Poker.Api.ContinuousPlay");
_gamesAdvanced = meter.CreateCounter<long>("continuous_play_games_advanced_total"); // tags: phase, outcome
_iteration     = meter.CreateHistogram<double>("continuous_play_iteration_ms", unit: "ms");
// per game:
using var span = PokerTelemetry.Source.StartActivity("advance-hand");
span?.SetTag("GameId", gameId);
span?.SetTag("HandNumber", handNumber);
```

### 4. Service-name / exporter cleanup (recommended)
```csharp
// remove: x.AddMeter("Forkful.ApiService");
// remove (or gate on Development): x.AddConsoleExporter();
ResourceBuilder.CreateDefault().AddService("cardgames-poker-api");
```
