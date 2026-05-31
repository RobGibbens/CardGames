# Prompt 05 — Tighten the wide-open SignalR CORS policy

> Addresses **Top-10 item 5** / Security Finding in
> [`docs/ArchitectureReview.md`](../ArchitectureReview.md). The CORS policy allows **any** origin
> together with `AllowCredentials()`, which is unsafe for a credentialed platform.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET 10 solution. Replace the permissive CORS policy with a
configuration-driven allow-list, keeping the wildcard behaviour for local Development only.

### Context (verify before editing)

- `src/CardGames.Poker.Api/Program.cs` lines ~90–99:
  ```csharp
  builder.Services.AddCors(options =>
  {
      options.AddPolicy("SignalRPolicy", policy =>
      {
          policy.SetIsOriginAllowed(_ => true) // Allow any origin for development
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
      });
  });
  ```
- The policy is applied via `app.UseCors("SignalRPolicy")` (~line 339) and is needed because SignalR
  hubs (`/hubs/game`, `/hubs/lobby`, `/hubs/notifications`, `/hubs/leagues`) require credentialed
  cross-origin requests from the Blazor Web app.

### Goal

1. Bind an **allowed-origins** list from configuration (e.g. `Cors:AllowedOrigins` as a string array)
   per environment. In production this should be the Web app origin(s) only.
2. Build the policy with `WithOrigins(allowedOrigins).AllowCredentials().AllowAnyHeader()
   .AllowAnyMethod()` when origins are configured.
3. Preserve the `SetIsOriginAllowed(_ => true)` convenience **only when
   `builder.Environment.IsDevelopment()`** (or when the allow-list is empty in Development), so local
   Aspire/dev flows keep working.
4. Fail safe: if no origins are configured in a non-Development environment, do **not** silently fall
   back to allow-all — log a warning and serve a restrictive policy.

### Constraints

- Keep `AllowCredentials()` working alongside explicit origins (it is incompatible with allow-all,
  which is exactly why the current combination is unsafe).
- Do not change hub routes or authentication.

### Tests / verification

- Add an integration test (using the existing `ApiWebApplicationFactory`) asserting that a
  disallowed `Origin` does **not** receive `Access-Control-Allow-Origin` in a non-Development
  configuration, and an allowed origin does.

### Acceptance criteria

- Production CORS is restricted to configured origins; Development retains permissive behaviour.
- `Program.cs` no longer unconditionally calls `SetIsOriginAllowed(_ => true)`.
- Build green from `src`: `dotnet build CardGames.slnx --no-restore`.
