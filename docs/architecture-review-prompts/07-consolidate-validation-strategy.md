# Prompt 07 — Consolidate the validation strategy (remove unused FluentValidation)

> Addresses **Top-10 item 7** / Code Quality Finding in
> [`docs/ArchitectureReview.md`](../ArchitectureReview.md). FluentValidation is registered but no
> `AbstractValidator` exists anywhere; validation is actually done with DataAnnotations.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET 10 solution. Pick a single input-validation strategy and
remove the dead wiring for the other. Make the smallest change that achieves a coherent, documented
approach.

### Context (verify before editing)

- `src/CardGames.Poker.Api/Program.cs` (~lines 102–104, 170) registers FluentValidation:
  ```csharp
  builder.Services.AddValidation();
  builder.Services.AddValidatorsFromAssemblyContaining<IValidationMarker>();
  builder.Services.AddFluentValidationAutoValidation();
  ...
  builder.Services.AddValidatorsFromAssembly(typeof(MapFeatureEndpoints).Assembly);
  ```
- A solution-wide search shows **zero** `: AbstractValidator<…>` implementations.
- Actual validation lives as **DataAnnotations** on the `src/CardGames.Contracts/**` request DTOs
  (e.g. `FavoriteVariants/UpdateFavoriteVariantsRequest.cs`, `AddChips/AddChipsRequest.cs`,
  `TableSettings/UpdateTableSettingsRequest.cs`) and the new minimal-API `AddValidation()`.

### Decision

Default recommendation: **keep DataAnnotations + `AddValidation()`** (it is the de-facto active
strategy and already covers the DTOs) and **remove the unused FluentValidation registrations**. If
the team prefers FluentValidation, do the inverse: add `AbstractValidator<T>` classes for each write
request and drop the DataAnnotations.

### Goal (DataAnnotations path — default)

1. Remove `AddValidatorsFromAssemblyContaining<IValidationMarker>()`,
   `AddFluentValidationAutoValidation()`, and `AddValidatorsFromAssembly(typeof(MapFeatureEndpoints)
   .Assembly)` from `Program.cs`.
2. Remove the now-unused FluentValidation package references if nothing else uses them (check `.csproj`
   and `using FluentValidation*;`). Keep `IValidationMarker` only if it is still used to locate the
   contracts assembly for `AddValidation()`; otherwise delete it.
3. Audit write endpoints (`MapPost`/`MapPut`) to confirm each request DTO has appropriate
   DataAnnotations and that `AddValidation()` enforces them; add missing annotations where a write
   endpoint currently has none.
4. Add a short note to `docs/ARCHITECTURE.md` stating DataAnnotations + `AddValidation()` is the
   validation convention.

### Constraints

- Do not weaken existing validation — verify behaviour is unchanged for already-annotated DTOs.
- Keep `ProblemDetails` error responses consistent (the project already calls
  `AddProblemDetails().AddErrorObjects()`).

### Tests / verification

- Add/extend integration tests asserting an invalid payload on a representative write endpoint
  returns `400` with `ProblemDetails`.

### Acceptance criteria

- Exactly one validation strategy remains wired up; no unused validator registrations.
- `grep -rn "FluentValidation" src` (if removed) returns nothing, or only intentional references.
- Build + targeted tests green from `src`.
