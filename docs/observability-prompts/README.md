# Observability Implementation Prompts

These are ready-to-use **GitHub Copilot** prompts for implementing the recommendations in
[`docs/ObservabilityAudit.md`](../ObservabilityAudit.md). Each file is a self-contained prompt:
paste it into Copilot Chat (agent mode) in this repository, or open it as context, and Copilot
will have enough detail to implement the change against the real project structure.

Each prompt maps to a finding (F#) from the audit and includes: goal, why it matters, exact files
to touch, the concrete implementation, registration steps, naming/cardinality rules, tests to add,
and acceptance criteria.

## Recommended order

Implement in this order — it builds the telemetry primitives first, then layers domain coverage on
top, so later prompts can reuse the shared `ActivitySource`/meter from earlier ones.

| # | Prompt | Finding | Severity | Type |
|---|--------|---------|----------|------|
| 1 | [01-consolidate-otel-bootstrap.md](01-consolidate-otel-bootstrap.md) | F5, F6, F7 | medium | config / exporters / naming |
| 2 | [02-shared-activitysource-and-mediatr-tracing.md](02-shared-activitysource-and-mediatr-tracing.md) | F2 | high | new ActivitySource + tracing behavior |
| 3 | [03-efcore-instrumentation.md](03-efcore-instrumentation.md) | Coverage Map | high | instrumentation registration |
| 4 | [04-continuous-play-metrics-and-spans.md](04-continuous-play-metrics-and-spans.md) | F3 | high | new metrics + spans |
| 5 | [05-signalr-broadcast-telemetry.md](05-signalr-broadcast-telemetry.md) | F4 | high | new metrics + spans + correlation |
| 6 | [06-logging-scope-correlation.md](06-logging-scope-correlation.md) | F9 | medium | structured-log enrichment |
| 7 | [07-hand-history-and-showdown-telemetry.md](07-hand-history-and-showdown-telemetry.md) | Coverage Map | medium | span + counters |
| 8 | [08-leagues-metric-hardening.md](08-leagues-metric-hardening.md) | F8 | medium | metric tag hygiene |

## Shared conventions all prompts must follow

- **Service/source naming:** the canonical OpenTelemetry service name is `cardgames-poker-api`.
  The canonical custom `ActivitySource` name is `CardGames.Poker.Api`. Custom meters are named
  `CardGames.Poker.Api.<Area>` (matching the existing `CardGames.Poker.Api.Leagues`).
- **Metric naming:** snake_case, with `_total` suffix for counters and an explicit `unit` for
  histograms (matches `LeaguesTelemetry`, e.g. `leagues_endpoint_latency_ms`).
- **Cardinality:** metric tag values must be low-cardinality (enums, route templates, phase names,
  outcome strings). **Never** tag a metric with `GameId`, `UserId`, `ConnectionId`, or raw paths.
  Those high-cardinality identifiers belong on **spans** and in **log scopes**, not metric tags.
- **Correlation IDs** that belong on spans/logs: `GameId`, `HandNumber`, `UserId`, `LeagueId`,
  `ConnectionId`, `RequestType`.
- **Secrets/PII:** never log request payloads, tokens, emails, or private cards (see F1, already
  fixed in `LoggingPipelineBehavior`).
- **Tests:** mirror existing xUnit + FluentAssertions + NSubstitute conventions in
  `src/Tests/CardGames.Poker.Tests`. Run with
  `dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj` from `src`.
