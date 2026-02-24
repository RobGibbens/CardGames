# Livingston History

## Project Learnings (from import)
- App composition is orchestrated via src/CardGames.AppHost.
- Requested by: Rob Gibbens.
- Shared defaults/resilience/telemetry are in src/CardGames.ServiceDefaults.

## Aspire Installation Verification (2026-02-18)
- Aspire is already installed and operational via `Aspire.AppHost.Sdk/13.1.0` in src/CardGames.AppHost.csproj.
- AppHost project successfully references 7 Aspire hosting packages (v13.1.1): Azure.Sql, Azure.Functions, Azure.Storage, DevTunnels, Redis, Azure.ServiceBus, StackExchange.Redis.DistributedCaching.
- Build and restore commands confirm all Aspire packages are available from nuget.org.
- No additional workload installation required; Aspire is managed via NuGet SDK.
- Idempotent success: no changes needed.

## Web Design Reviewer Skill Installation (2026-02-18)
- web-design-reviewer was not available in configured marketplaces (no active plugin marketplace integration found in current .config/dotnet-tools.json).
- Created skill variant at `.ai-team/skills/web-design-reviewer/SKILL.md` with full documentation of capabilities, scope, and integration points.
- Skill provides Blazor component review, CSS/styling analysis, accessibility (WCAG 2.1) validation, design system alignment, and cross-browser concern identification.
- Idempotent success: skill file structure in place and ready for activation via `/skills` command or `@web-design-reviewer` mention.
- Future marketplace integration: If GitHub Copilot CLI marketplace supports skill plugins, prioritize `github.com/github/copilot-skills/web-design-reviewer` (if available) over local variant.

## Refactor Skill Installation (2026-02-19)
- refactor skill was not available in configured marketplaces.
- Created skill variant at `.ai-team/skills/refactor/SKILL.md` with C#/.NET-specific refactoring guidance.
- Skill provides code analysis, SOLID principle application, design pattern recognition, complexity reduction, and test-aware refactoring.
- Integrated with team routing: complements Rusty (architecture), Danny (backend), Linus (frontend), and Basher (testing).
- Idempotent success: skill file structure in place and ready for activation via `/skills` command or `@refactor` mention.

## PRD Skill Installation (2026-02-19)
- prd skill was not available in configured marketplaces.
- Created skill variant at `.ai-team/skills/prd/SKILL.md` with product requirements documentation and specification guidance.
- Skill provides PRD authoring, feature specification clarity, design alignment validation, scope management, stakeholder review support, and decision documentation.
- Integrated with team routing: supports Rusty (Lead/product direction), complements Danny (backend feasibility), Linus (frontend scope), and Basher (testing scope).
- Idempotent success: skill file structure in place and ready for activation via `/skills` command or `@prd` mention.

## NuGet Manager Skill Installation (2026-02-19)
- nuget-manager skill was not available in configured marketplaces.
- Created skill variant at `.ai-team/skills/nuget-manager/SKILL.md` with NuGet package management and dependency resolution guidance.
- Skill provides package discovery, version management, dependency conflict resolution, security analysis, upgrade planning, and restore troubleshooting.
- Integrated with team routing: works with Livingston (DevOps Lead), complements Danny (backend package choices), Linus (frontend packages), and Basher (testing frameworks).
- Idempotent success: skill file structure in place and ready for activation via `/skills` command or `@nuget-manager` mention.

## Microsoft Docs Skill Installation (2026-02-19)
- microsoft-docs skill was not available in configured marketplaces.
- Created skill variant at `.ai-team/skills/microsoft-docs/SKILL.md` with official Microsoft documentation and learning resource guidance.
- Skill provides API reference navigation, framework patterns, Azure integration guidance, authentication documentation, performance best practices, code samples, migration paths, and troubleshooting resources.
- Covers .NET 10, ASP.NET Core, Blazor, Entity Framework Core, Azure SDK, authentication/authorization, testing frameworks, Dependency Injection, Redis caching, and Cloud design patterns.
- Integrated with team routing: works with Livingston (infrastructure/Azure), Danny (API/.NET patterns), Linus (Blazor/Web), Basher (test frameworks), Rusty (architecture), and Scribe (API references).
- Idempotent success: skill file structure in place and ready for activation via `/skills` command or `@microsoft-docs` mention.

## Anthropics Skills Marketplace Configuration (2026-02-19)
- anthropics/skills marketplace added to `.ai-team/plugins/marketplaces.json` with idempotent behavior.
- Configuration entry: name = anthropics-skills, source = anthropics/skills, enabled = true.
- Centralizes marketplace plugin registry; prevents duplication via single authoritative entry.
- Enables skill discovery from anthropics marketplace for team members.
- Idempotent success: configuration in place; no duplicates on re-run.

## Microsoft Code Reference Skill Installation (2026-02-19)
- microsoft-code-reference skill was not available in configured marketplaces.
- Created skill variant at `.ai-team/skills/microsoft-code-reference/SKILL.md` with C# code analysis and reference guidance.
- Skill provides code navigation, pattern recognition, type system analysis, cross-file reference tracking, async code analysis, LINQ validation, code smell detection, Roslyn integration, architectural compliance verification, and test coverage mapping.
- Covers C#, .NET type system, ASP.NET Core, Entity Framework Core, Blazor components, dependency injection, MediatR patterns, async/await, and static analysis.
- Integrated with team routing: works with Livingston (architecture/deployment), Danny (code patterns/API), Linus (Blazor components), Basher (test coverage), Rusty (design decisions), and Scribe (implementation details).
- Idempotent success: skill file structure in place and ready for activation via `/skills` command or `@microsoft-code-reference` mention.

## Frontend Design Skill Installation (2026-02-19)
- frontend-design skill was not available in configured marketplaces.
- Created skill variant at `.ai-team/skills/frontend-design/SKILL.md` with UI/UX design and component design guidance.
- Skill provides design pattern analysis, design system validation, layout and composition review, interaction design assessment, accessibility-first validation, responsive design guidance, and CSS architecture recommendations.
- Integrated with team routing: works with Linus (Frontend Dev) for UI component design, complements web-design-reviewer skill for technical review, provides signal to Rusty (Lead) for design consistency, and informs Basher (Tester) for UI/accessibility test planning.
- Idempotent success: skill file structure in place and ready for activation via `/skills` command or `@frontend-design` mention.

## Learnings
