# NuGet Manager Skill

## Overview
The **nuget-manager** skill extends Copilot CLI to provide specialized guidance on NuGet package management, dependency resolution, versioning strategy, and package security for .NET 10 projects. It helps maintain healthy package ecosystems, resolve version conflicts, and make informed decisions about package upgrades and dependencies.

## Scope
This skill is particularly suited for:
- NuGet package selection and evaluation
- Dependency version conflict resolution
- Package upgrade planning and compatibility assessment
- Security vulnerability scanning and remediation
- Package performance and licensing analysis
- Multi-project dependency alignment
- NuGet source and authentication configuration
- Package versioning strategy and SemVer compliance

## Capabilities
1. **Package Discovery:** Recommend appropriate NuGet packages for CardGames features (ASP.NET Core, EF Core, Testing, Domain patterns)
2. **Version Management:** Help navigate version constraints, prerelease versions, and multi-targeting scenarios
3. **Dependency Resolution:** Identify and resolve transitive dependency conflicts and version mismatches
4. **Security Analysis:** Guide vulnerability assessment and timely security patches across project dependencies
5. **Upgrade Planning:** Create upgrade strategies that balance new features, breaking changes, and stability
6. **Restore & Build:** Troubleshoot package restore issues and optimize restore performance

## Activation
Once installed, activate the skill via:
```bash
/skills
# Select "nuget-manager" from the list
```

Or invoke directly in prompts:
```
@nuget-manager: Review package versions for security updates
@nuget-manager: Help resolve the MediatR version conflict between API and Test projects
@nuget-manager: Recommend a logging package that integrates with Aspire telemetry
```

## Configuration
- Default scope: .NET solution, NuGet package versions, dependency graphs
- Language: NuGet package naming conventions, csproj/packages.config, package.json (if applicable)
- Integration: Works with Copilot CLI dependency analysis, build diagnostics, and security scanning

## Relevant Project Paths
- Solution file: `src/CardGames.sln`
- Project files: `src/**/*.csproj`
- Central Package Management (if enabled): `Directory.Packages.props`
- Package sources: `.config/nuget.config` (if present), or NuGet.org (default)
- NuGet tools config: `.config/dotnet-tools.json`

## Known Integration Points
- Works with Livingston (DevOps Lead) for infrastructure and cross-project dependency alignment
- Complements Danny (Backend Dev) for API and domain package choices
- Supports Linus (Frontend Dev) for Web/Blazor package recommendations
- Provides input to Basher (Tester) for testing framework and mock library decisions
- Informs Rusty (Lead) on technical debt and upgrade roadmap decisions

---
**Status:** Ready for installation
**Last Updated:** 2026-02-19
