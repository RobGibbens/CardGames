# Microsoft Code Reference Skill

## Overview
The **microsoft-code-reference** skill extends Copilot CLI to provide specialized access to Microsoft's code reference databases, static analysis tools, and code intelligence for .NET projects. It enables squad agents to understand codebase structure, identify code patterns, navigate cross-cutting concerns, and perform advanced code analysis across the CardGames project.

## Scope
This skill is particularly suited for:
- C# language reference and code semantics analysis
- .NET type system and method resolution
- ASP.NET Core request/response pipeline analysis
- Entity Framework Core query analysis and LINQ patterns
- Blazor component lifecycle and code patterns
- Dependency injection and service resolution
- MediatR command/query handler patterns
- Async/await patterns and Task-based concurrency
- Roslyn analyzer capabilities and code fixes
- Architecture and dependency graph analysis
- Code quality metrics and technical debt identification
- Naming conventions and code style consistency validation

## Capabilities
1. **Code Navigation:** Understand codebase structure, type relationships, and dependency graphs across solution
2. **Pattern Recognition:** Identify architectural patterns (MediatR, DI, factory patterns) and anti-patterns
3. **Type System Analysis:** Navigate .NET type hierarchy, interface implementations, and inheritance structures
4. **Cross-File References:** Track method calls, property usage, and type dependencies across projects
5. **Async Code Analysis:** Identify async/await patterns, deadlock risks, and Task-based design issues
6. **LINQ & Query Analysis:** Validate Entity Framework Core queries and LINQ expressions for performance
7. **Code Smell Detection:** Identify complexity hotspots, duplicated logic, and maintainability concerns
8. **Roslyn Integration:** Leverage C# static analysis for code fixes, refactoring opportunities, and compliance checks
9. **Architectural Compliance:** Verify code adheres to layered architecture (Domain, API, Web, Contracts) boundaries
10. **Test Coverage Mapping:** Identify untested code paths and coverage gaps

## Activation
Once installed, activate the skill via:
```bash
/skills
# Select "microsoft-code-reference" from the list
```

Or invoke directly in prompts:
```
@microsoft-code-reference: Show me all implementations of IGameRule in the codebase
@microsoft-code-reference: Find code duplication in the domain layer
@microsoft-code-reference: Analyze the dependency graph for CardGames.Poker.Api
@microsoft-code-reference: What are the async/await patterns used in this file?
@microsoft-code-reference: Identify potential null reference exceptions in this service
@microsoft-code-reference: Map Entity Framework Core query patterns in repository classes
@microsoft-code-reference: Find all MediatR command handlers and their validators
```

## Configuration
- Default scope: C# code, .NET projects, solution-wide analysis
- Language: C#, LINQ, T-SQL (through EF Core)
- Integration: Works with Copilot CLI type navigation, Roslyn analyzers, and solution structure analysis
- Source Priority: Roslyn-based analysis, language server integration, .NET/C# static analysis tools
- Analysis Depth: File-level, project-level, and solution-wide code intelligence

## Relevant Project Paths
- Domain projects: `src/CardGames.Poker`, `src/CardGames.Core`, `src/CardGames.Core.French`
- API project: `src/CardGames.Poker.Api`
- Web project (Blazor): `src/CardGames.Poker.Web`
- Contracts/DTO: `src/CardGames.Contracts`
- Aspire AppHost: `src/CardGames.AppHost`
- Service defaults: `src/CardGames.ServiceDefaults`
- Test projects: `src/Tests/**`
- Solution file: `src/CardGames.sln`

## Known Integration Points
- Works with Livingston (DevOps Lead) for architecture boundaries and deployment impact analysis
- Supports Danny (Backend Dev) for API code patterns, domain logic, and EF Core query analysis
- Helps Linus (Frontend Dev) with Blazor component code and client-side service calls
- Informs Basher (Tester) on test coverage gaps and testability of code paths
- Provides architectural insights to Rusty (Lead) for design decisions and refactoring priorities
- Supplements Scribe (Documentation) with actual implementation details and pattern identification

---
**Status:** Ready for installation
**Last Updated:** 2026-02-19
