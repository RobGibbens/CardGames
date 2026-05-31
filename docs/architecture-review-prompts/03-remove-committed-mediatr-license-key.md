# Prompt 03 — Remove the committed MediatR license key from source control

> Addresses **Top-10 item 3** / Security Finding in
> [`docs/ArchitectureReview.md`](../ArchitectureReview.md). A licensing secret is hard-coded in
> `Program.cs` and is therefore permanently exposed in git history.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET 10 solution. Remove the hard-coded MediatR license key from
source and load it from configuration instead. Make the smallest change that achieves this; do not
alter MediatR behaviour.

### Context (verify before editing)

- `src/CardGames.Poker.Api/Program.cs` registers MediatR around lines 157–166 and sets
  `cfg.LicenseKey = "******";` on **line 160** — a long JWT-style string committed in
  plaintext.
- Configuration is already available via `builder.Configuration`. The project already binds options
  sections (e.g. `InternalApiAuthOptions`, `AvatarStorageOptions`) and uses Aspire/user-secrets.

### Goal

1. Replace the inline literal with a value read from configuration, e.g.
   `builder.Configuration["MediatR:LicenseKey"]` (or an `IOptions`-bound settings class consistent
   with the existing options pattern). Only call `cfg.LicenseKey = …` when the value is non-empty so
   local/dev builds without the key still run.
2. Add the key to **user-secrets** for local dev and document the configuration key in
   `appsettings.json` as an **empty placeholder** (no real value committed). For deployed
   environments, source it from environment variables / key vault.
3. Treat the previously committed key as **compromised**: add a short note in the PR description that
   the key should be rotated with the vendor (Lucky Penny Software / MediatR). Do **not** attempt to
   rewrite git history in this change.

### Constraints

- Do not commit the real key value anywhere in the repo (no appsettings, no tests, no comments).
- Keep startup working when the key is absent (guard the assignment).

### Acceptance criteria

- `grep -rn "eyJhbGci" src` returns no results in tracked source.
- `Program.cs` reads the key from configuration and guards against null/empty.
- Solution builds: from `src`, `dotnet build CardGames.slnx --no-restore`.
- PR description notes the rotation requirement.
