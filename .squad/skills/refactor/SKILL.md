# Refactor Skill

## Overview
The **refactor** skill extends Copilot CLI to provide specialized code refactoring analysis and suggestions, with a focus on improving code maintainability, reducing complexity, and applying SOLID principles within the CardGames domain.

## Scope
This skill is particularly suited for:
- C# code refactoring and modernization (.NET 10)
- SOLID principle application (Single Responsibility, Open/Closed, Liskov, Interface Segregation, Dependency Inversion)
- Design pattern recognition and implementation
- Code smell detection and remediation
- Performance optimization opportunities
- Test-driven refactoring workflows
- Domain-driven design pattern alignment

## Capabilities
1. **Code Analysis:** Identify refactoring opportunities in C# source across API, Web, Domain, and Test projects
2. **Complexity Reduction:** Suggest decomposition of large methods, classes, and modules
3. **SOLID Application:** Recommend interface extraction, dependency injection corrections, and responsibility redistribution
4. **Pattern Recognition:** Detect and improve inconsistent or missing design patterns
5. **Modernization:** Apply C# 13+ language features and .NET 10 best practices
6. **Test Impact Assessment:** Ensure refactorings maintain or improve test coverage and clarity

## Activation
Once installed, activate the skill via:
```bash
/skills
# Select "refactor" from the list
```

Or invoke directly in prompts:
```
@refactor: Improve the Game aggregate root design for better testability
@refactor: Extract service layer from the Poker.Api orchestration
```

## Configuration
- Default scope: C# codebase, game domain logic, API/Web services
- Language: C#, MediatR handlers, xUnit tests, FluentAssertions assertions
- Integration: Works with Copilot CLI code review and refactoring tools (`/review`, `/diff`, `/suggest`)

## Relevant Project Paths
- Core domain: `src/CardGames.Poker/` (game rules, aggregates)
- API handlers: `src/CardGames.Poker.Api/Features/`
- Web services: `src/CardGames.Poker.Web/Services/`
- Contracts/DTOs: `src/CardGames.Contracts/`
- Tests: `src/Tests/`
- Shared infrastructure: `src/CardGames.Core/`, `src/CardGames.ServiceDefaults/`

## Known Integration Points
- Works with Rusty (Lead) for architectural refactoring decisions
- Complements Danny (Backend Dev) for API and orchestration improvements
- Supports Linus (Frontend Dev) for client-side refactoring
- Provides signal to Basher (Tester) for test structure improvements

---
**Status:** Ready for installation
**Last Updated:** 2026-02-19
