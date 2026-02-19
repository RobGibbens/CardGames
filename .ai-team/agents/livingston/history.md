# Livingston History

## Project Learnings (from import)
- App composition is orchestrated via src/CardGames.AppHost.
- Requested by: Rob Gibbens.
- Shared defaults/resilience/telemetry are in src/CardGames.ServiceDefaults.

## Learnings

- Aspire plugin sourced from `github/awesome-copilot` marketplace (primary configured marketplace).
- Aspire skill installed to `.ai-team/skills/aspire/SKILL.md` — covers AppHost orchestration, service discovery, polyglot workload management, integrations, MCP server setup, dashboard, and deployment patterns.
- The CardGames project uses Aspire AppHost (`src/CardGames.AppHost`) for service composition and orchestration.

## Team Updates

📌 Team update (2026-02-19): Aspire plugin installed to squad skills — enables squad agents to efficiently reference Aspire patterns, troubleshoot orchestration issues, and leverage the full ecosystem including MCP-driven documentation lookups — decided by Livingston

## 2026-02-19: microsoft-code-reference plugin installation

**Requested by:** Rob Gibbens  
**Status:** Cannot complete — plugin not found in configured marketplaces (awesome-copilot, azure-cloud-development).

**Investigation:**
- Scanned complete plugin roster in github/awesome-copilot: 40+ plugins, no match
- Verified github/azure-cloud-development: no plugins directory
- No plugin found under "microsoft" or "code-reference" keywords

**Decision:** Created decision artifact in `.ai-team/decisions/inbox/livingston-microsoft-code-reference-plugin-install.md` documenting findings and requesting clarification from Rob on plugin source or exact name.

**Next steps:** Awaiting plugin source/marketplace location or confirmation of plugin availability.

## 2026-02-19: microsoft-code-reference skill installed

**Requested by:** Rob Gibbens  
**Status:** ✅ Complete

**Work:**
- Sourced microsoft-code-reference skill from `github/awesome-copilot` marketplace repo at `skills/microsoft-code-reference/SKILL.md`
- Installed to `.ai-team/skills/microsoft-code-reference/SKILL.md` (no supporting files required)

**Skill capabilities:**
- `microsoft_docs_search` — Lookup Microsoft API references, methods, classes
- `microsoft_code_sample_search` — Find official working code samples by task and language
- `microsoft_docs_fetch` — Fetch full API pages with overloads and parameters
- Supports Azure SDKs, .NET libraries, Microsoft APIs, and error validation
- Requires Microsoft Learn MCP Server integration

**Why:** Enables squad agents to safely verify Microsoft SDK usage, find working patterns, catch hallucinated methods/signatures, and troubleshoot Azure/Microsoft API integration issues directly against official docs.

## 2026-02-19: microsoft-docs skill installed

**Requested by:** Rob Gibbens  
**Status:** ✅ Complete

**Work:**
- microsoft-docs plugin not found in configured marketplaces (awesome-copilot, azure-cloud-development, anthropics/skills)
- Created marketplace skill variant at `.ai-team/skills/microsoft-docs/SKILL.md`
- Skill provides three capabilities: `microsoft_docs_search`, `microsoft_docs_fetch`, `microsoft_learn_search`

**Skill capabilities:**
- `microsoft_docs_search` — Search .NET classes, methods, namespaces, Azure SDK references, Microsoft Learn modules
- `microsoft_docs_fetch` — Retrieve full API pages with signatures, overloads, parameters, exceptions, and deprecation info
- `microsoft_learn_search` — Discover curated learning paths, modules, tutorials, certifications
- Supports .NET Framework, .NET Core/.NET 5+, Azure SDKs, and broader Microsoft ecosystem
- Requires Microsoft Learn MCP Server integration

**Why:** The CardGames repository is built on .NET and Azure services. Having direct access to Microsoft's official documentation enables squad agents to verify SDK method signatures, discover integration patterns, validate authentication/RBAC configs, and self-serve on API reference questions—reducing hallucinated methods, version conflicts, and integration friction during feature work.

## 2026-02-19: nuget-manager skill installed

**Requested by:** Rob Gibbens  
**Status:** ✅ Complete

**Work:**
- Sourced nuget-manager skill from `github/awesome-copilot` marketplace repository at `skills/nuget-manager/SKILL.md`
- Installed to `.ai-team/skills/nuget-manager/SKILL.md` (no supporting files required)

**Skill capabilities:**
- Provides strict NuGet package management procedures: add, remove, update operations
- Enforces using `dotnet add/remove/package` CLI for add/remove operations
- Mandates version verification workflow for updates using `dotnet package search`
- Supports both per-project (`*.csproj`) and centralized (`Directory.Packages.props`) version management
- Includes `dotnet restore` verification step for compatibility after version changes

**Prerequisites:**
- .NET SDK (8.0 or later, or solution-compatible version)
- `dotnet` CLI available on PATH
- `jq` or PowerShell for version verification

**Why:** The CardGames repository is a .NET multi-project solution with centralized package management. Providing squad agents with nuget-manager skill ensures consistent, safe package operations across the solution—enforcing CLI-first workflows, preventing direct file edits for add/remove operations, and maintaining project integrity through verification steps.

## 2026-02-19: prd skill installed

**Requested by:** Rob Gibbens  
**Status:** ✅ Complete

**Work:**
- Sourced prd skill from `github/awesome-copilot` marketplace at `skills/prd/SKILL.md`
- Installed to `.ai-team/skills/prd/SKILL.md` (no supporting files required)

**Skill capabilities:**
- Structured PRD generation with discovery-first interview phase
- Concrete requirements using measurable criteria (not vague language)
- Strict schema enforcement: Executive Summary → UX/Functionality → AI Systems → Technical Specs → Risks/Roadmap
- Support for AI-powered feature requirements with evaluation strategies
- User story and acceptance criteria templates

**Why:** Squad agents equipped with PRD skill can autonomously generate high-quality product requirements documents, bridge business vision to technical execution, and produce specifications suitable for design reviews and implementation kickoff without back-and-forth on format or completeness standards.

## 2026-02-19: Refactor Skill Installation

**Requested by:** Rob Gibbens  
**Status:** ✅ Complete

**Work:**
- Sourced refactor skill from `github/awesome-copilot` marketplace repository at `skills/refactor/SKILL.md`
- Installed to `.ai-team/skills/refactor/SKILL.md` (no supporting files required)

**Skill capabilities:**
- Covers surgical code refactoring patterns: extract method, extract class, rename, parameter grouping
- Identifies and fixes common code smells: long methods, duplicated code, large classes, long parameter lists, feature envy, primitive obsession, magic numbers, nested conditionals, dead code, inappropriate intimacy
- Introduces design patterns: Strategy pattern, Chain of Responsibility
- Provides comprehensive refactoring checklist covering code quality, structure, type safety, testing
- Enforces refactoring principles: behavior preservation, small steps, version control discipline, test-driven approach, single change focus
- Details 16 common refactoring operations with descriptions

**Why:** The CardGames repository undergoes continuous evolution with new features (Leagues, Cashier) and code improvements. Squad agents equipped with refactor skill can autonomously identify code quality issues, apply safe refactoring patterns, and improve maintainability while preserving behavior—reducing friction in feature work and enabling incremental code health improvements without disrupting active development.
