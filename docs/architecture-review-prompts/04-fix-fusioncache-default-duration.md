# Prompt 04 — Fix the FusionCache default duration (`2ms` `TODO:ROB`)

> Addresses **Top-10 item 4** / Performance Finding in
> [`docs/ArchitectureReview.md`](../ArchitectureReview.md). The Redis + FusionCache stack is wired up
> but the default entry duration is `2ms`, which effectively disables caching.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET 10 solution. Restore a sensible default cache duration and
set explicit per-entry options for hot read paths. Make the smallest change that achieves this.

### Context (verify before editing)

- `src/CardGames.Poker.Api/Program.cs` lines ~146–155 configure FusionCache:
  ```csharp
  builder.Services.AddFusionCache()
      .WithSerializer(...)
      //TODO:ROB = .WithDefaultEntryOptions(options => options.Duration = TimeSpan.FromMinutes(5))
      .WithDefaultEntryOptions(options => options.Duration = TimeSpan.FromMilliseconds(2))
      .WithRegisteredDistributedCache()
      .AsHybridCache();
  ```
- A distributed cache (`builder.AddRedisDistributedCache("cache")`) and FusionCache backplane/
  instrumentation are already registered, so the infrastructure exists but is bypassed by the 2ms
  TTL.
- Cache intent/design notes: `docs/InMemoryCacheImplementationPlan.md`.

### Goal

1. Replace the `2ms` default with a real default duration. Use the intended **5 minutes** unless
   `docs/InMemoryCacheImplementationPlan.md` specifies otherwise; remove the commented-out
   `//TODO:ROB` line.
2. Where appropriate, prefer **configuration-driven** durations (bind from `appsettings.json`, e.g.
   `Cache:DefaultDurationMinutes`) so environments can tune TTLs without a rebuild.
3. Set explicit per-call `FusionCacheEntryOptions` on the **hottest read paths** that already use the
   cache (search for `IFusionCache`/`HybridCache` usages), so volatile data isn't pinned for the full
   default. Add fail-safe options (`IsFailSafeEnabled`, soft/hard timeouts) consistent with
   FusionCache best practices if not already set.

### Constraints

- Do not introduce stale-data correctness bugs: pick conservative TTLs for anything that changes per
  game action; longer TTLs only for genuinely static reference data (e.g. available game metadata).
- Keep the distributed/backplane wiring intact.

### Tests / verification

- Add or update a unit/integration test asserting the configured default duration is **not**
  sub-second (guard against regressing to a near-zero TTL).
- Manually verify cache hits via the existing FusionCache OpenTelemetry instrumentation (already
  registered in `Program.cs`).

### Acceptance criteria

- No `TimeSpan.FromMilliseconds(2)` default remains; the `TODO:ROB` line is gone.
- Default duration is meaningful (configurable, default ≈ 5 min).
- Build green from `src`: `dotnet build CardGames.slnx --no-restore`.
