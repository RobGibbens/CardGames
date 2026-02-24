# Microsoft Docs Skill

## Overview
The **microsoft-docs** skill extends Copilot CLI to provide specialized access to Microsoft's official documentation, API references, samples, and learning resources for .NET, Azure, ASP.NET Core, and related technologies. It enables squad agents to quickly find authoritative guidance on SDKs, frameworks, best practices, and architectural patterns used in CardGames.

## Scope
This skill is particularly suited for:
- .NET 10 and ASP.NET Core API documentation and migration guidance
- Azure SDK and service integration patterns (SQL, Functions, Storage, Service Bus, Redis)
- Entity Framework Core ORM patterns and performance optimization
- Blazor Web framework components and architecture
- Authentication and authorization patterns (Azure AD, OAuth 2.0, JWT)
- SignalR real-time communication configuration
- .NET testing frameworks (xUnit, FluentAssertions, Moq)
- Dependency Injection and ASP.NET Core configuration
- Distributed caching and session management with Redis
- Cloud design patterns and serverless Azure Functions

## Capabilities
1. **API Reference Lookup:** Navigate .NET, ASP.NET Core, and Azure SDK API documentation with type, method, and property signatures
2. **Framework Guidance:** Provide official patterns for Blazor, MediatR, Entity Framework Core, and ASP.NET Core middleware
3. **Azure Integration:** Guide Azure service integration (SQL Database, Storage Blobs, Service Bus, Redis, DevTunnels)
4. **Authentication & Security:** Link to official Azure AD, OAuth 2.0, JWT, and ASP.NET Core Identity documentation
5. **Performance Best Practices:** Reference official guidance on caching, query optimization, connection pooling, and distributed tracing
6. **Code Samples:** Direct to Microsoft Learn and GitHub official samples for hands-on patterns
7. **Migration & Upgrade Paths:** Navigate .NET version upgrades and framework deprecation guidance
8. **Troubleshooting:** Access official diagnostic and error documentation for common issues

## Activation
Once installed, activate the skill via:
```bash
/skills
# Select "microsoft-docs" from the list
```

Or invoke directly in prompts:
```
@microsoft-docs: How do I configure Azure Service Bus in ASP.NET Core?
@microsoft-docs: Show me the Entity Framework Core query best practices
@microsoft-docs: What is the official pattern for Blazor component lifecycle?
@microsoft-docs: Link to Azure AD authentication setup for ASP.NET Core
@microsoft-docs: How do I optimize Redis caching in an ASP.NET Core app?
```

## Configuration
- Default scope: Official Microsoft documentation, Learn modules, GitHub samples
- Language: .NET, C#, TypeScript (Blazor), Azure SDK documentation
- Integration: Works with Copilot CLI code navigation, dependency analysis, and quick-reference lookups
- Source Priority: docs.microsoft.com, learn.microsoft.com, github.com/dotnet, github.com/Azure

## Relevant Project Paths
- API project: `src/CardGames.Poker.Api`
- Web project (Blazor): `src/CardGames.Poker.Web`
- Domain projects: `src/CardGames.Poker`, `src/CardGames.Core`, `src/CardGames.Core.French`
- Aspire AppHost: `src/CardGames.AppHost`
- Service defaults: `src/CardGames.ServiceDefaults`
- Test projects: `src/Tests/**`
- Solution file: `src/CardGames.sln`

## Known Integration Points
- Works with Livingston (DevOps Lead) for infrastructure, Azure, and Aspire configuration
- Supports Danny (Backend Dev) for .NET API, EF Core, and domain pattern guidance
- Helps Linus (Frontend Dev) with Blazor components, authentication, and Web integration
- Provides reference material to Basher (Tester) for xUnit, test patterns, and mocking
- Informs Rusty (Lead) on architectural decisions and technology upgrades
- Supplements Scribe (Documentation) with official API and pattern references

---
**Status:** Ready for installation
**Last Updated:** 2026-02-19
