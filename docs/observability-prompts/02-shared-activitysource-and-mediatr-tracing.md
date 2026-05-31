# Prompt 02 — Add a shared ActivitySource and a MediatR tracing pipeline behavior

> Addresses audit finding **F2** — there is **zero** custom `ActivitySource`/span usage anywhere in
> `CardGames.Poker.Api`, so MediatR requests and domain workflows produce no spans. This prompt
> creates the shared tracing primitive that prompts 04, 05, and 07 will reuse.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET solution. Add distributed-tracing coverage to the MediatR
request pipeline in `CardGames.Poker.Api` by introducing one shared `ActivitySource` and a tracing
`IPipelineBehavior`. Follow the existing pipeline-behavior and registration conventions exactly.

### Context (verify before editing)

- MediatR is registered in `src/CardGames.Poker.Api/Program.cs` with these behaviors, in order:
  `LoggingPipelineBehavior`, `GameStateBroadcastingBehavior`, `LobbyStateBroadcastingBehavior`,
  `LeagueGameCompletionSyncBehavior` (all via `.AddScoped(typeof(IPipelineBehavior<,>), typeof(...))`).
- The hardened logging behavior lives at
  `src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/LoggingPipelineBehavior.cs` and already
  logs `RequestType` (short type name) in a logging scope. Match its style (tabs, `sealed` where
  appropriate, file-scoped namespace).
- No custom `ActivitySource` exists in the API today. Tracing sources are registered in
  `Program.cs` via `x.AddSource(...)` inside `WithTracing`.

### Step 1 — Create the shared ActivitySource

Add `src/CardGames.Poker.Api/Infrastructure/Telemetry/PokerActivitySource.cs`:

```csharp
using System.Diagnostics;

namespace CardGames.Poker.Api.Infrastructure.Telemetry;

public static class PokerActivitySource
{
    public const string Name = "CardGames.Poker.Api";

    public static readonly ActivitySource Source = new(Name);
}
```

### Step 2 — Create the tracing behavior

Add `src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/TracingPipelineBehavior.cs`:

```csharp
using System.Diagnostics;
using CardGames.Poker.Api.Infrastructure.Telemetry;
using MediatR;

namespace CardGames.Poker.Api.Infrastructure.PipelineBehaviors;

public sealed class TracingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).Name;
        using var activity = PokerActivitySource.Source.StartActivity(
            $"mediatr.{requestType}",
            ActivityKind.Internal);

        activity?.SetTag("mediatr.request_type", requestType);

        try
        {
            var response = await next(cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
```

> Notes:
> - Use the short type name (`typeof(TRequest).Name`) as the span name — low cardinality, no PII.
> - Do **not** put request property values on the span (same secret/PII rule as F1).
> - If `AddException` is unavailable in the installed OpenTelemetry/.NET version, fall back to
>   `activity?.RecordException(ex)` (from `OpenTelemetry.Trace`) or an event with the exception type.

### Step 3 — Register the behavior and the source

In `src/CardGames.Poker.Api/Program.cs`:

1. Register the behavior **outermost so it wraps the others** (add it as the first behavior in the
   MediatR registration chain, before `LoggingPipelineBehavior`):

   ```csharp
   .AddScoped(typeof(IPipelineBehavior<,>), typeof(TracingPipelineBehavior<,>))
   .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>))
   // ...existing behaviors
   ```

2. Register the source inside `WithTracing`:

   ```csharp
   x.AddSource(PokerActivitySource.Name);
   ```

### Step 4 — Tests

Add `src/Tests/CardGames.Poker.Tests/Api/Infrastructure/PipelineBehaviors/TracingPipelineBehaviorTests.cs`
using xUnit + FluentAssertions. Use an in-test `ActivityListener` that subscribes to source
`CardGames.Poker.Api` with `Sample = (ref _) => ActivitySamplingResult.AllData` so activities are
created. Assert:

- On success: an activity named `mediatr.<RequestType>` is started and its status is `Ok`.
- On a handler that throws: the activity status is `Error` and the original exception is rethrown
  (`BeSameAs`).
- The span name/tags contain only the request **type name**, never property values.

### Acceptance criteria

- `dotnet build src/CardGames.Poker.Api/CardGames.Poker.Api.csproj` succeeds.
- New tests pass:
  `dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~TracingPipelineBehaviorTests"`.
- With an OTLP backend, every MediatR command/query now produces a span, and downstream EF Core /
  HttpClient / FusionCache spans nest under it.
