# Decisions

### 2026-02-17: Initialize squad roster and routing
**By:** Squad (Coordinator)
**What:** Created initial team roster, routing map, and casting state for the CardGames repository.
**Why:** Team workflows require authoritative squad metadata before multi-agent work can run.

### 2026-02-17: OAuth-first auth page prioritization
**By:** Linus (Frontend)
**What:** Prioritized OAuth providers on `Login.razor` and `Register.razor` by placing `ExternalLoginPicker` first and keeping local auth forms as a secondary fallback.
**Why:** Most users are expected to use third-party sign-in; UI should guide that default without changing auth behavior or available fields.

### 2026-02-17: OAuth-first wording convention for auth pages
**By:** Linus (Frontend)
**What:** Standardized auth-page copy to present Google/Microsoft OAuth as the preferred path while keeping local email/password as a clearly labeled fallback, with no layout or logic changes.
**Why:** Reinforces OAuth-first UX intent through wording only, preserving existing auth behavior.

**Applied in:**
- `src/CardGames.Poker.Web/Components/Account/Pages/Login.razor`
- `src/CardGames.Poker.Web/Components/Account/Pages/Register.razor`

### 2026-02-17: Cashier feature architecture and API direction
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Defined Cashier as account-scoped functionality with Profile-based endpoints (`/api/v1/profile/cashier/*`), immediate account wallet add-chips behavior (without game-phase queueing), an MVP ledger using `take/skip` newest-first pagination, and an append-only audited ledger entry model.
**Why:** Preserves clear boundary between account wallet operations and table/game-phase operations while minimizing implementation risk for MVP and ensuring auditability.

**Reference:** `docs/CashierFeatureDesign.md`

### 2026-02-17: Leagues release gating is P0 invariants-first with telemetry-backed rollout
**By:** Basher (Tester)
**Requested by:** Rob Gibbens

**What:** Established a risk-based testing strategy for Leagues that gates release on P0 coverage for (1) creator bootstrap/admin assignment, (2) invite-link lifecycle validation, (3) league-scoped RBAC for admin actions, and (4) temporal membership integrity/idempotency. Defined P1/P2 follow-up coverage for concurrency depth, UI affordances, accessibility, and performance hardening.

**Why:** Leagues introduces high-risk authorization and temporal-state transitions; preventing privilege escalation and data integrity drift is more critical than broad feature breadth for MVP.

**Guardrails:**
- CI must pass all P0 tests before release.
- Progressive rollout behind feature flag with cohort expansion after stable metrics windows.
- Production alerts for unauthorized mutation success, invite-join failure anomalies, and any member with overlapping active league intervals.

**Reference:** `docs/LeaguesTestStrategy.md`

### 2026-02-17: Backend design direction for Leagues MVP
**By:** Danny (Backend Dev)
**Requested by:** Rob Gibbens
**What:** Proposed a Leagues backend design with a dedicated league aggregate boundary (social/membership/admin/invite/schedule only), append-only temporal membership/admin events with current-state projections, revocable+expiring invite-link tokens, and MediatR-based command/query/API surface for create/join/manage/seasons/events.
**Why:** Delivers required Leagues capabilities while preserving existing poker game-rule/domain boundaries and aligning with repository architecture conventions.

**MVP included:**
- league create with creator auto-admin bootstrap
- temporal membership join/leave and admin promotion
- invite-link lifecycle (active/revoked/expired-by-time)
- season containers + one-off events (metadata primitives)

**Deferred post-MVP:**
- standings/leaderboards/scoring aggregation
- richer invite abuse controls (max uses/single-use/allowlist)
- public league discovery and expanded moderation flows

**Reference:** `docs/LeaguesBackendDesign.md`

### 2026-02-17: Leagues MVP UX is list/detail with server-driven role gating
**By:** Linus (Frontend)
**Requested by:** Rob Gibbens
**What:** Defined a minimal Blazor UX for Leagues centered on four routes (`/leagues`, `/leagues/{leagueId}`, `/leagues/join/{token}`, create flow from list) with sectioned league detail (Overview, Members, Invites, Schedule). Admin actions (invite create/revoke, promote member, create season, create one-off event) are visible and enabled strictly from server-provided role and membership state.
**Why:** Keeps Leagues as a social coordination layer, aligns with architecture boundaries, and delivers required create/join/admin/schedule scenarios without adding leaderboard or game-rule complexity in MVP.

**Reference:** `docs/LeaguesUXDesign.md`

### 2026-02-17: League admin RBAC bootstrap and promotion rules
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Set admin policy that league creator is auto-admin, leagues can have multiple admins, and any current admin may promote another active member to admin.
**Why:** Matches product requirements while enabling shared administration without introducing a separate owner-only bottleneck.

**Review context:** Design Review ceremony (Danny, Linus, Basher)

### 2026-02-17: League join uses revocable shareable invite-link tokens
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Standardized on league-scoped invite URLs backed by tokens that support status (active/revoked) and expiration for MVP, with join validation before membership insertion.
**Why:** Delivers required shareable join UX while preserving basic abuse controls and admin governance over access.

**Review context:** Design Review ceremony (Danny, Linus, Basher)

### 2026-02-17: Support both season schedules and one-off league events in MVP model
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Decided MVP will model both season containers (e.g., ten tournaments) and one-off events/nights, while deferring advanced standings/leaderboard scoring logic.
**Why:** Satisfies required league usage modes immediately and controls scope by sequencing event primitives before analytics-heavy ranking features.

**Review context:** Design Review ceremony (Danny, Linus, Basher)

### 2026-02-17: Leagues are social coordination containers, not game-rule containers
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Defined Leagues as account-scoped entities that own membership, admin roles, invite links, and event/season metadata while leaving game rules and hand orchestration in existing poker domain flows.
**Why:** Preserves architecture boundaries from `docs/ARCHITECTURE.md` and avoids coupling league concerns to game-engine mechanics.

**Review context:** Design Review ceremony (Danny, Linus, Basher)

### 2026-02-17: League membership is temporal and event-backed
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Chose an append-only membership history model with join/leave timestamps and role-change events, with current membership derived from active intervals.
**Why:** Supports membership changes over time, auditability, and safer conflict handling for concurrent membership/admin updates.

**Review context:** Design Review ceremony (Danny, Linus, Basher)

### 2026-02-17: Consolidated Leagues design artifact is MVP approval baseline
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Adopted `docs/LeaguesFeatureDesign.md` as the consolidated approval baseline for Leagues MVP, including account-scoped social boundary, event-based membership projection, invite security controls, contract-regeneration workflow, and scoped MVP delivery phases.
**Why:** Aligns backend, UX, and testing directions in a single source of truth while minimizing coupling risk and controlling MVP scope.

**Execution mapping:**
- Phase 1: domain + persistence
- Phase 2: API + contracts + authorization
- Phase 3: Blazor UX
- Phase 4: tests + rollout hardening

**Reference:** `docs/LeaguesFeatureDesign.md`

### 2026-02-18: Leagues governance safety operations landed for P0 (#216)
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Added Leagues v1 governance operations for ownership transfer, admin demotion, and member removal using minimal MediatR + endpoint flows; enforced invariants that leagues always retain at least one manager and at least one governance-capable member (manager/admin); extended membership history values for ownership transfer/admin demotion and reused member-leave events for removals.
**Why:** Provides required governance controls for league administration while preventing lockout or governance loss states in MVP.

### 2026-02-18: Leagues P0 quality gates standardized in integration coverage and CI (#224)
**By:** Basher (Tester)
**Requested by:** Rob Gibbens
**What:** Added API integration coverage for invite-code preview/join-request and moderation approve/deny queue behavior, added event-launch conflict coverage to protect linkage stability, introduced a skipped standings-ingestion scaffold test for future scope visibility, and enforced Leagues P0 checks in `squad-ci` with .NET integration test execution.
**Why:** Centers release confidence on the highest-risk external behaviors (authz, moderation semantics, idempotency/conflicts) and makes required gates explicit in CI.

### 2026-02-18: Issue #216 Governance roles for Leagues
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Added three minimal MediatR/endpoint flows under `Leagues/v1` for ownership transfer (manager-only transfer to another active member), admin demotion (governance-capable actor can demote active admin to member), and member removal (governance-capable actor can remove active member). Enforced governance invariants: leagues must retain at least one manager and at least one governance-capable member (manager/admin). Extended membership event model with new event history values for admin demotion and ownership transfer; reused `MemberLeft` for member removal.
**Why:** Provides required governance controls while preventing lockout or governance loss states in MVP.
**Note:** Changes scoped to API Leagues v1 feature, Contracts, and integration tests with no unrelated refactors.

### 2026-02-19: Aspire plugin installed to squad skills
**By:** Livingston (DevOps)
**What:** Sourced the Aspire skill from the `github/awesome-copilot` marketplace and installed it to `.ai-team/skills/aspire/SKILL.md`. The skill covers AppHost orchestration, service discovery, polyglot workload support (C#/.NET, Python, Go, Node.js, Java, etc.), integrations catalog, MCP server setup, dashboard, and deployment patterns for Docker, Kubernetes, and Azure targets.
**Why:** The CardGames repository uses Aspire AppHost for infrastructure composition and service orchestration. Having the plugin available to squad agents ensures DevOps, backend, and infrastructure work can efficiently reference Aspire patterns, troubleshoot orchestration issues, and leverage the full Aspire ecosystem including MCP-driven documentation lookups.

### 2026-02-19: Microsoft Code Reference skill installed to squad skills
**By:** Livingston (DevOps)
**Requested by:** Rob Gibbens
**What:** Installed the `microsoft-code-reference` skill from the `github/awesome-copilot` marketplace to `.ai-team/skills/microsoft-code-reference/SKILL.md`. The skill provides direct access to Microsoft API references, code samples (in C#, Python, JavaScript, etc.), and verification tools for validating SDK method signatures and identifying deprecated patterns.
**Why:** The CardGames repository uses Azure services (AppHost, Storage, Service Bus, Identity) and Microsoft SDKs (.NET, Azure SDK libraries). Providing squad agents with microsoft-code-reference capabilities enables: (1) catching hallucinated SDK methods, (2) accessing working patterns from Microsoft Learn, (3) solving version conflicts and deprecation issues, and (4) self-service verification of auth flows, NuGet packages, and RBAC permissions without blocking on human review.
**Tools Available:**
- `microsoft_docs_search` — Look up classes, methods, namespaces
- `microsoft_code_sample_search` — Find working examples by task and language
- `microsoft_docs_fetch` — Get full API pages with overloads and parameters
**Scope:** Installation only; no runtime infrastructure or build system changes.

### 2026-02-19: Microsoft Docs Skill Installation
**Date:** 2026-02-19  
**By:** Livingston (DevOps)  
**Requested by:** Rob Gibbens  
**Status:** ✅ Complete

**Summary**

Installed `microsoft-docs` as a marketplace skill variant (not available as a plugin) to `.ai-team/skills/microsoft-docs/SKILL.md`.

**Context**

The request was to install the `microsoft-docs` plugin from configured marketplaces. Investigation determined that:

1. **Plugin not found** — Scanned all three configured marketplaces:
   - `awesome-copilot` (github/awesome-copilot) — does not exist
   - `azure-cloud-development` (github/azure-cloud-development) — no plugins available
   - `anthropics/skills` (anthropics/skills) — no microsoft-docs skill

2. **Fallback approach** — Per task instructions, created the marketplace skill variant since the plugin package was unavailable.

**Solution**

Created `.ai-team/skills/microsoft-docs/SKILL.md` with three core capabilities:

- **microsoft_docs_search** — Keyword search for .NET APIs, Azure SDKs, Microsoft Learn resources
- **microsoft_docs_fetch** — Full API page retrieval with signatures, overloads, parameters, exceptions
- **microsoft_learn_search** — Curated learning paths, modules, tutorials, certifications

Scope: Documentation lookup and reference verification only; does not generate code, modify infrastructure, or replace architectural review.

**Why This Matters**

CardGames is a .NET/Azure project using Aspire, Azure services, and Microsoft SDKs. Squad agents equipped with direct Microsoft documentation access can:

- Verify SDK method signatures and catch hallucinations before implementation
- Self-serve on API reference questions without context-switching
- Validate authentication flows and RBAC configurations
- Identify deprecations and version-specific API changes
- Discover official integration patterns and working examples

This reduces friction in feature work and improves confidence in Microsoft SDK usage.

**Integration**

- Skill available to all squad agents immediately after squad routing reload
- Requires Microsoft Learn MCP Server for live documentation fetching
- No changes to build, runtime infrastructure, or local dev workflow

**Files Created**

- `.ai-team/skills/microsoft-docs/SKILL.md` — Skill definition and capabilities

**Next Steps**

- Squad agents can reference `microsoft-docs` skill in prompts
- Monitor skill usage patterns to identify documentation gaps or refinements
- Consider extending capability set if additional reference patterns emerge from usage

### 2026-02-19: NuGet Manager Skill Installation
**By:** Livingston (DevOps)
**Requested by:** Rob Gibbens
**What:** Installed `nuget-manager` marketplace skill from `github/awesome-copilot` to `.ai-team/skills/nuget-manager/SKILL.md`. Skill provides CLI-first package management: add, remove, update versions with verification and compatibility validation via `dotnet restore`.
**Why:** CardGames is a .NET multi-project solution with centralized package management (`Directory.Packages.props`). Equipping squad agents with nuget-manager ensures consistent workflows, prevents direct file edits for add/remove, validates version existence and compatibility, and enables autonomous dependency management without friction.
**Capabilities:**
- Add/remove packages: `dotnet add [<PROJECT>] package <PACKAGE_NAME> [--version <VERSION>]`
- Update versions with centralized or per-project verification
- Compatibility validation via `dotnet restore` after changes

### 2026-02-19: PRD Skill Installation
**By:** Livingston (DevOps)
**Requested by:** Rob Gibbens
**What:** Installed `prd` skill from the `github/awesome-copilot` marketplace to `.ai-team/skills/prd/SKILL.md`. Skill provides structured Product Requirements Document generation with discovery-first workflow, concrete requirements enforcement, strict schema (Executive Summary → User Experience & Functionality → AI System Requirements → Technical Specifications → Risks & Roadmap), AI-powered feature support, and standardized user story templates.
**Why:** The CardGames team frequently defines new features (Leagues, Cashier, future enhancements) and needs structured requirements for design reviews and technical execution. Squad agents equipped with PRD skill can produce complete, well-formed specifications autonomously while enforcing quality through concrete requirements and strict schema.
**Capabilities:**
- Phase 1: Discovery — Interrogate user on problem, success metrics, constraints
- Phase 2: Analysis & Scoping — Map user flow, identify dependencies, define non-goals
- Phase 3: Technical Drafting — Generate PRD using strict schema

### 2026-02-19: Refactor Skill Installation
**Date:** 2026-02-19  
**By:** Livingston (DevOps)  
**Requested by:** Rob Gibbens  
**Status:** ✅ Complete

**Summary**

Installed the `refactor` skill from the `github/awesome-copilot` marketplace to `.ai-team/skills/refactor/SKILL.md`. The skill enables squad agents to perform surgical code refactoring with confidence—identifying code smells, extracting methods, improving type safety, and applying design patterns while preserving behavior.

**Solution**

**Source:** `github/awesome-copilot` marketplace  
**File:** `.ai-team/skills/refactor/SKILL.md`  
**Installation:** Minimal—single SKILL.md file, no build/runtime changes

**Skill Capabilities**

### Refactoring Patterns
- Extract method/class/interface  
- Inline method/class  
- Rename method/variable  
- Introduce parameter object  
- Replace conditional with polymorphism  
- Replace magic number with constant  
- Guard clause refactoring  

### Code Smells Identification & Fixes
1. **Long Method/Function** — Break into focused functions
2. **Duplicated Code** — Extract common logic
3. **Large Class/Module** — Single responsibility principle
4. **Long Parameter List** — Group into parameter object
5. **Feature Envy** — Move logic to object that owns data
6. **Primitive Obsession** — Use domain types
7. **Magic Numbers/Strings** — Named constants
8. **Nested Conditionals** — Guard clauses, early returns
9. **Dead Code** — Remove unused functions/imports
10. **Inappropriate Intimacy** — Ask don't tell principle

### Design Patterns
- **Strategy Pattern** — Encapsulate algorithms
- **Chain of Responsibility** — Decouple validators

### Process & Checklist
- Safe refactoring workflow: Prepare → Identify → Refactor → Verify → Clean Up
- Code quality checklist: function size, single responsibility, duplication, naming, magic values, dead code
- Structure checklist: module boundaries, dependency flow, circular dependencies
- Type safety checklist: public API types, no unwarranted `any`, explicit nullability
- Testing checklist: coverage, edge cases, test pass status

### Common Operations Reference
Table of 16 refactoring operations with descriptions (Extract Method, Extract Class, Pull Up Method, Push Down Method, etc.)

**Why This Matters**

**CardGames Evolution:**
- Active feature development (Leagues, Cashier) with continuous codebase changes
- Aspire AppHost orchestration + multi-project .NET solution complexity
- Quality gates for P0 test coverage and domain integrity (Leagues MVP)

**Squad Benefits:**
1. **Code Smell Detection** — Agents can identify problematic patterns autonomously
2. **Gradual Improvement** — Safe refactoring during normal feature work without disruption
3. **Behavioral Guarantee** — Strict adherence to "preserve behavior" principle reduces risk
4. **Pattern Library** — Reference for design patterns (Strategy, Chain of Responsibility) applicable to domain
5. **Unified Standard** — All squad members follow same refactoring principles and checklist

**Impact**

- **Scope:** Squad skill roster only; no build, runtime, or deployment changes
- **Enablement:** Squad agents can now reference refactor skill in work prompts
- **Availability:** Immediate once squad routing reloads
- **No Prerequisites:** Works with existing .NET toolchain and test infrastructure

**Next Steps**

- Squad agents reference `refactor` skill when improving code structure
- Monitor refactoring patterns in squad work to identify high-impact improvements
- Consider extending skill if specialized refactoring needs emerge (e.g., async/await patterns in .NET)

### 2026-02-19: Web Design Reviewer Skill Installation
**Date:** 2026-02-19  
**By:** Livingston (DevOps)  
**Requested by:** Rob Gibbens  
**What:** Installed the `web-design-reviewer` skill from the `github/awesome-copilot` marketplace to `.ai-team/skills/web-design-reviewer/SKILL.md`.
**Why:** The CardGames Poker Web UI (`src/CardGames.Poker.Web/`) is built with Blazor. Squad agents equipped with web-design-reviewer skill can autonomously inspect the running application, detect design issues visually (layout, responsive, accessibility, visual consistency), locate problematic components in source code, and apply surgical fixes—reducing friction during UI quality gates and enabling structured design review processes.

**Skill Capabilities:**
- Multi-framework support: Static HTML/CSS/JS, React, Vue, Angular, Svelte, Next.js, Nuxt, SvelteKit, CMS platforms
- Issue detection: Layout (overflow/overlap/alignment/spacing/clipping), responsive (mobile-friendliness/breakpoints/touch targets), accessibility (contrast/focus/alt text), visual consistency (fonts/colors/spacing)
- Automated workflow: Information gathering → Visual inspection → Issue fixing → Re-verification → Completion report
- Responsive testing at 4 viewports: Mobile (375px), Tablet (768px), Desktop (1280px), Wide (1920px)
- Browser automation via Playwright MCP (reference) or compatible tools (Selenium, Puppeteer, Cypress)
- Severity prioritization: P1 (fix immediately), P2 (fix next), P3 (fix if possible)

**Prerequisites:**
- Target website running (local dev, staging, or production)
- Browser automation capability available
- Source code access for fix application

**Scope:** Squad skill only; no build, runtime, or deployment changes
