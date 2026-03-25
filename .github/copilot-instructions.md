# Project Guidelines

## Code Style

- Primary stack is C#/.NET (`net10.0`) across API, Web, Contracts, and tests.
- Prefer readable, domain-first code and preserve existing naming/patterns from [README.md](../README.md).
- Keep game-specific orchestration out of generic UI/API flow; follow the metadata-driven approach in [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md).
- In tests, follow existing xUnit + FluentAssertions style under [src/Tests](../src/Tests).

## Architecture

- This repo uses a rule-driven poker architecture: game rules are defined in domain and consumed by API/UI.
- Start with [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) before changing game flow.
- Core boundaries:
  - Domain/game logic: [src/CardGames.Poker](../src/CardGames.Poker), [src/CardGames.Core](../src/CardGames.Core), [src/CardGames.Core.French](../src/CardGames.Core.French)
  - API: [src/CardGames.Poker.Api](../src/CardGames.Poker.Api)
  - Web UI (Blazor): [src/CardGames.Poker.Web](../src/CardGames.Poker.Web)
  - Contracts/DTO + generated Refit clients: [src/CardGames.Contracts](../src/CardGames.Contracts)
  - App composition/infrastructure wiring (Aspire): [src/CardGames.AppHost](../src/CardGames.AppHost)

## Build and Test

- Build solution: `dotnet build src/CardGames.sln`
- Run all tests: `dotnet test src/CardGames.sln`
- Run API: `dotnet run --project src/CardGames.Poker.Api`
- Run Web: `dotnet run --project src/CardGames.Poker.Web`
- Regenerate Refit clients (from OpenAPI): `dotnet build src/CardGames.Poker.Refitter`

## Project Conventions

- Treat [src/CardGames.Contracts/RefitInterface.v1.cs](../src/CardGames.Contracts/RefitInterface.v1.cs) as generated output; regenerate via Refitter instead of manual edits.
- API feature work follows MediatR + validation pipeline patterns configured in [src/CardGames.Poker.Api/Program.cs](../src/CardGames.Poker.Api/Program.cs).
- Web uses service-discovery style API base address (`https+http://api`) in [src/CardGames.Poker.Web/Program.cs](../src/CardGames.Poker.Web/Program.cs).
- Keep game type codes and rules registration consistent with existing registry patterns described in [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md).

## Integration Points

- Aspire AppHost orchestrates dependencies and startup ordering in [src/CardGames.AppHost/AppHost.cs](../src/CardGames.AppHost/AppHost.cs).
- Cross-service dependencies include SQL, Redis, Blob storage, and Service Bus wiring via AppHost + API startup.
- Shared service defaults/resilience/telemetry live in [src/CardGames.ServiceDefaults](../src/CardGames.ServiceDefaults).

## Security

- API authentication includes JWT bearer/Azure AD wiring and SignalR-specific auth configuration in [src/CardGames.Poker.Api/Program.cs](../src/CardGames.Poker.Api/Program.cs).
- Web authentication uses ASP.NET Core Identity and external providers in [src/CardGames.Poker.Web/Program.cs](../src/CardGames.Poker.Web/Program.cs).
- Keep secrets in configuration/User Secrets; do not hardcode credentials in source.
