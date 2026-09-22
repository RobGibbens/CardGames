# Prompt 08 — Harden the Leagues metric tags (cardinality and dimensions)

> Addresses audit finding **F8** — `LeaguesTelemetry.RecordEndpointLatency` tags with `endpoint` and
> `status_code`. If `endpoint` is ever a raw path containing ids, cardinality explodes; and the
> latency histogram has no error/outcome dimension.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET solution. Make the existing Leagues metrics safe and useful
without breaking their current shape more than necessary.

### Context (verify before editing)

- `src/CardGames.Poker.Api/Features/Leagues/v1/Telemetry/LeaguesTelemetry.cs` creates meter
  `CardGames.Poker.Api.Leagues` and exposes:
  - `leagues_funnel_attempts_total` counter tagged `step`, `outcome` — good pattern, keep.
  - `leagues_endpoint_latency_ms` histogram tagged `endpoint`, `status_code`.
- Registered via `builder.Services.AddSingleton<LeaguesTelemetry>();` and
  `x.AddMeter("CardGames.Poker.Api.Leagues");` in `Program.cs`.
- Find all call sites of `RecordEndpointLatency` and `RecordFunnelAttempt` before changing the
  signatures.

### Goals

1. **Guarantee `endpoint` is low-cardinality.** Audit every `RecordEndpointLatency` call site and
   ensure the `endpoint` argument is a **stable route template or logical endpoint name**
   (e.g. `leagues/{id}/join`), never a concrete path with a league/user id interpolated. If any call
   site passes a raw path, change it to pass the template/name. Optionally add a short XML-doc
   comment on the method stating the cardinality contract.
2. **Add an `outcome` dimension to latency** so you can separate success vs failure latency.
   Either derive it from `status_code` (2xx/3xx = `success`, else `error`) inside the method, or add
   an explicit `outcome` tag. Prefer deriving from `status_code` to avoid touching every call site:
   ```csharp
   public void RecordEndpointLatency(string endpoint, int statusCode, double latencyMs)
   {
       var outcome = statusCode is >= 200 and < 400 ? "success" : "error";
       _endpointLatencyMs.Record(
           latencyMs,
           new("endpoint", endpoint),
           new("status_code", statusCode),
           new("outcome", outcome));
   }
   ```
   > Note: `status_code` itself is bounded/low-cardinality, so keeping it is fine.

### Constraints

- Keep metric and tag **names** stable where possible (dashboards/alerts may depend on
  `leagues_endpoint_latency_ms`, `leagues_funnel_attempts_total`, `step`, `outcome`, `endpoint`,
  `status_code`). Only **add** the `outcome` dimension and tighten `endpoint` values.
- Do not change the funnel counter shape.

### Tests

- Add/extend a `LeaguesTelemetryTests` using a `MeterListener` to assert that
  `RecordEndpointLatency` emits the `outcome` tag and that a 2xx maps to `success`, a 4xx/5xx to
  `error`.

### Acceptance criteria

- Build succeeds; tests pass.
- All `RecordEndpointLatency` call sites pass route templates / logical names, never raw id-bearing
  paths.
- `leagues_endpoint_latency_ms` now carries an `outcome` tag in addition to `endpoint` and
  `status_code`.
