# Prompt 01 — Consolidate the OpenTelemetry bootstrap, fix service naming and exporters

> Addresses audit findings **F5** (generic/misleading service name + dead meter), **F6**
> (console-only exporters, no production path in the host), and **F7** (duplicated, divergent OTel
> bootstrap between the host and `ServiceDefaults`).

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET solution. Clean up and consolidate the OpenTelemetry
bootstrap so the Poker API emits consistent, queryable telemetry with a production-grade export
path. Make the smallest changes that achieve the goals below; do not change application behavior.

### Context / current state (verify before editing)

- `src/CardGames.ServiceDefaults/Extensions.cs` already configures OpenTelemetry once via
  `ConfigureOpenTelemetry`: OTel logging (`IncludeScopes`, `IncludeFormattedMessage`), metrics
  (`AddAspNetCoreInstrumentation`, `AddHttpClientInstrumentation`, `AddRuntimeInstrumentation`),
  tracing (`AddFusionCacheInstrumentation`, `AddSource(ApplicationName)`,
  `AddAspNetCoreInstrumentation` with a health-check filter, `AddHttpClientInstrumentation`), and the
  OTLP exporter via `AddOpenTelemetryExporters` (enabled only when `OTEL_EXPORTER_OTLP_ENDPOINT`
  is set). `builder.AddServiceDefaults()` is already called first in `Program.cs`.
- `src/CardGames.Poker.Api/Program.cs` then **re-configures** OpenTelemetry a second time
  (`AddOpenTelemetry().WithTracing(...).WithMetrics(...)`), with its own
  `ResourceBuilder.CreateDefault().AddService("api")` (twice), `AddConsoleExporter()` on both
  signals, a Prometheus exporter + `MapPrometheusScrapingEndpoint()`, the Service Bus source
  `AddSource("Azure.Messaging.ServiceBus")`, and meters `AddMeter("Forkful.ApiService")` and
  `AddMeter("CardGames.Poker.Api.Leagues")`. It also calls `builder.Logging.AddOpenTelemetry(...)`
  again. The `Forkful.ApiService` meter is **dead** — no meter by that name is created anywhere in
  the repo.

### Goals

1. **Single source of truth for cross-cutting OTel config.** Keep resource attributes, exporters,
   and logging configuration in `ServiceDefaults`. In `Program.cs`, keep **only** Poker-specific
   additions: the Prometheus exporter + scraping endpoint, `AddSource("Azure.Messaging.ServiceBus")`,
   and `AddMeter("CardGames.Poker.Api.Leagues")` (plus any meters/sources added by later prompts).
2. **Meaningful, consistent service name.** Set the OpenTelemetry service name to
   `cardgames-poker-api` for the API process, applied **once** so traces, metrics, and logs all
   agree. Prefer setting it through the resource in `ServiceDefaults` (or via the `OTEL_SERVICE_NAME`
   environment variable / `AddService(...)`), not by calling `AddService("api")` in two places.
   Remove both `AddService("api")` calls and both ad-hoc `ResourceBuilder.CreateDefault()` blocks in
   `Program.cs`.
3. **Remove the dead meter.** Delete `x.AddMeter("Forkful.ApiService");`.
4. **Production export path, not console-only.** Remove the two `AddConsoleExporter()` calls (one on
   tracing, one on metrics) from `Program.cs`, OR gate them behind
   `builder.Environment.IsDevelopment()` so they never run in production. Rely on the OTLP exporter
   from `ServiceDefaults` (driven by `OTEL_EXPORTER_OTLP_ENDPOINT`) for real backends, and keep the
   Prometheus scraping endpoint for metrics.
5. **Remove the duplicate logging registration.** `builder.Logging.AddOpenTelemetry(...)` in
   `Program.cs` duplicates `ServiceDefaults.ConfigureOpenTelemetry`. Remove the duplicate from
   `Program.cs` (keep the one in `ServiceDefaults`).
6. **Keep the dev-only `AlwaysOnSampler`** currently set in `Program.cs` for development.

### Constraints

- Do not remove FusionCache instrumentation, ASP.NET Core instrumentation, HttpClient
  instrumentation, the Prometheus exporter/endpoint, or the Service Bus source.
- Do not change rate limiting, auth, CORS, or any non-telemetry code.
- The app must still build and start; `OTEL_EXPORTER_OTLP_ENDPOINT` remaining unset must not crash
  the app (OTLP stays disabled, Prometheus still works).

### Acceptance criteria

- `grep -rn "Forkful" src` returns nothing.
- `grep -rn "AddService(\"api\")" src` returns nothing; the service name is `cardgames-poker-api`.
- `AddConsoleExporter()` is gone from production paths (removed or dev-gated).
- `Program.cs` no longer calls `builder.Logging.AddOpenTelemetry`.
- `dotnet build src/CardGames.Poker.Api/CardGames.Poker.Api.csproj` succeeds.
- Running the AppHost locally, the API still exposes `/metrics` (Prometheus) and, when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set, exports OTLP traces/metrics/logs tagged with
  `service.name=cardgames-poker-api`.

### Suggested diff shape for `Program.cs`

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(x =>
    {
        if (builder.Environment.IsDevelopment())
        {
            x.SetSampler<AlwaysOnSampler>();
        }

        x.AddFusionCacheInstrumentation(o => o.IncludeMemoryLevel = true);
        x.AddSource("Azure.Messaging.ServiceBus");
        // resource + ASP.NET/HttpClient instrumentation + exporters come from ServiceDefaults
    })
    .WithMetrics(x =>
    {
        x.AddPrometheusExporter();
        x.AddMeter("CardGames.Poker.Api.Leagues");
        x.AddFusionCacheInstrumentation(o =>
        {
            o.IncludeMemoryLevel = true;
            o.IncludeDistributedLevel = true;
            o.IncludeBackplane = true;
        });
    });
```

Set the service name in `ServiceDefaults.ConfigureOpenTelemetry` (or document the
`OTEL_SERVICE_NAME=cardgames-poker-api` environment variable in the AppHost), e.g.:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: builder.Environment.ApplicationName));
```
…ensuring the API's `ApplicationName`/`OTEL_SERVICE_NAME` resolves to `cardgames-poker-api`.
