# Prompt 03 — Add EF Core instrumentation so database work appears in traces

> Addresses the **persistence / EF Core** row of the audit Coverage Map: EF Core is **not**
> instrumented, so database queries do not appear as spans and N+1s / slow queries are invisible in
> a trace backend.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET solution. Add OpenTelemetry EF Core instrumentation to the
Poker API so `CardsDbContext` queries appear as child spans under the request/MediatR span.

### Context (verify before editing)

- The API uses `CardsDbContext` registered via `builder.AddSqlServerDbContext<CardsDbContext>("cardsdb")`
  in `src/CardGames.Poker.Api/Program.cs`.
- Tracing is configured in `Program.cs` inside `builder.Services.AddOpenTelemetry().WithTracing(x => { ... })`,
  which already calls `AddAspNetCoreInstrumentation`, `AddHttpClientInstrumentation`,
  `AddFusionCacheInstrumentation`, and `AddSource("Azure.Messaging.ServiceBus")`.
- EF Core instrumentation is provided by the NuGet package
  `OpenTelemetry.Instrumentation.EntityFrameworkCore`.

### Steps

1. Add the package reference to `src/CardGames.Poker.Api/CardGames.Poker.Api.csproj`:
   `OpenTelemetry.Instrumentation.EntityFrameworkCore` (match the OpenTelemetry version line already
   used by the other OTel packages in the project; prefer the latest stable compatible with the
   project's `net10.0` target). Use `dotnet add package` rather than hand-editing if possible.
2. In the `WithTracing` block in `Program.cs`, add:
   ```csharp
   x.AddEntityFrameworkCoreInstrumentation(o =>
   {
       o.SetDbStatementForText = builder.Environment.IsDevelopment();
   });
   ```
   - **Important (PII/security):** keep `SetDbStatementForText` **off in production**. SQL text can
     contain parameter values (PII) and increases span size. Only capture statement text in
     Development.
3. Do not change the `DbContext` registration or any queries.

### Acceptance criteria

- `dotnet build src/CardGames.Poker.Api/CardGames.Poker.Api.csproj` succeeds.
- With an OTLP backend in a local run, executing any API endpoint that hits the database produces
  EF Core spans (operation = database, e.g. `cardsdb`) nested under the ASP.NET Core request span
  (and the MediatR span from prompt 02 if implemented).
- In production configuration, captured spans do **not** include raw SQL statement text.

### Notes

- This pairs naturally with prompt 02: once both are in place, a single trace shows
  `HTTP request → mediatr.<Request> → EF Core query`, which is the main thing a future engineer
  needs to diagnose latency and N+1 issues.
