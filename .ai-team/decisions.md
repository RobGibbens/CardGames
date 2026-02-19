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

### 2026-02-18: Aspire installation verification and idempotent success
**By:** Livingston (DevOps)
**Requested by:** Rob Gibbens
**What:** Verified that Aspire is already installed and operational in the CardGames project via NuGet SDK (v13.1.0). Current package set includes Aspire.AppHost.Sdk 13.1.0 alongside Azure SQL, Functions, Storage, DevTunnels, Redis, Service Bus, and StackExchange.Redis.DistributedCaching all at v13.1.1. Build and restore confirm all packages are accessible from nuget.org.
**Why:** Aspire was already integrated during AppHost setup; no marketplace installation or workload installation required. Build pipeline confirms operational readiness.
**Future notes:** Keep Aspire packages pinned to v13.1.1 for compatibility with CardGames.AppHost.Sdk/13.1.0. Aspire versioning should coordinate with .NET SDK updates (.NET 10.0.100 in use).

### 2026-02-18: Web Design Reviewer skill installation & marketplace readiness
**By:** Livingston (DevOps)
**Requested by:** Rob Gibbens
**What:** Installed `web-design-reviewer` skill as a local variant at `.ai-team/skills/web-design-reviewer/SKILL.md` after determining it is not available from active GitHub Copilot CLI plugin marketplaces. Defined scope for Blazor component review, CSS/styling, accessibility (WCAG 2.1), and design system validation, with activation via `/skills` command or `@web-design-reviewer` mention.
**Why:** GitHub Copilot CLI does not expose a public skill marketplace in current config; local skill variant enables team to immediately use web-design-reviewer capabilities while remaining future-ready for marketplace-based distribution.
**Applicability:** This decision applies to future `web-design-reviewer` references or activation requests. If marketplace-based skills become available, the local variant can serve as a fallback or reference template.
**Status:** ✅ Idempotent Success—Skill is ready for use and installation is repeatable.

### 2026-02-19: Refactor Skill Installation Decision
**By:** Livingston (DevOps)
**Date:** 2026-02-19
**What:** Created a local skill variant at `.ai-team/skills/refactor/SKILL.md` instead of searching marketplace sources, following the precedent established by the web-design-reviewer skill installation.
**Why:**
1. **No Marketplace Integration:** Like web-design-reviewer, no active plugin marketplace was found in the current dotnet-tools configuration.
2. **Consistency:** Applied the same pattern as the existing web-design-reviewer skill—craft a durable local skill definition aligned with the project domain.
3. **Idempotent by Design:** The skill file is standalone and requires no external dependencies; re-running this installation will find the file valid and leave it unchanged.
4. **Domain Alignment:** The skill scope is tailored to CardGames' C#/.NET stack and game domain refactoring needs (SOLID, design patterns, test-driven workflow).

**Scope & Capabilities:**
The refactor skill addresses:
- C# modernization (.NET 10)
- SOLID principle application and pattern recognition
- Complexity reduction in game orchestration
- Test-aware refactoring with xUnit/FluentAssertions
- Integration with Rusty (architecture), Danny (backend), Linus (frontend), and Basher (testing)

**Future Marketplace Migration Path:**
If GitHub Copilot CLI marketplace supports skill plugins in future releases, prioritize:
1. `github.com/github/copilot-skills/refactor` (if available)
2. Other official GitHub marketplace sources

Current local variant will remain stable regardless of marketplace availability.

**Status:** ✅ Idempotent Success—Skill file present and valid, history and decision files updated, no unrelated files touched, re-running installation will detect existing file and skip creation.

### 2026-02-19: PRD Skill Installation — Repeat Approval
**By:** Livingston (DevOps)
**Requested by:** Rob Gibbens
**What:** Installed and activated the `prd` skill from team-local marketplace to support product requirements documentation and specification guidance across squad decision-making and feature planning. Skill file exists at `.ai-team/skills/prd/SKILL.md` with stable content.
**Why:** CardGames repository has increasingly complex product features (Leagues, Cashier, future expansions) requiring clear specification, scope definition, and cross-functional alignment. A dedicated PRD skill provides feature specification support, scope management, design alignment, decision documentation, and stakeholder review templates.
**Scope & Integration:**
- Feature Specification Support: Clarify acceptance criteria, edge cases, and success metrics before implementation
- Scope Management: Help define MVP boundaries and prioritization (defer vs. include decisions)
- Design Alignment: Validate technical decisions against stated product requirements
- Decision Documentation: Structure decision rationale linking product goals to implementation choices
- Stakeholder Review: Template and checklist support for product sign-off and cross-functional validation
- Works with Rusty (Lead/product), Danny (backend), Linus (frontend), Basher (testing)
- Activation: Via `/skills` command or `@prd` mention in Copilot CLI prompts
**Idempotence:** Skill file exists at `.ai-team/skills/prd/SKILL.md` with stable content. Subsequent runs verify file presence and validate content integrity.
**Status:** ✅ Idempotent Success—PRD skill is ready for activation and supports product-focused decision making in `.ai-team/decisions.md` workflows and feature design documentation in `docs/` directory.

### 2026-02-19: NuGet Manager Skill Installation
**By:** Livingston (DevOps)
**Requested by:** Rob Gibbens
**What:** Installed `nuget-manager` skill as a local variant at `.ai-team/skills/nuget-manager/SKILL.md` following the established pattern from web-design-reviewer, refactor, and prd skills, when no active GitHub Copilot CLI marketplace integration was found.
**Why:** Completes team skill portfolio for NuGet/package-management operations across CardGames solution. Local variant provides immediate functionality while remaining future-ready for marketplace-based skill distribution if GitHub Copilot CLI marketplace supports skill plugins.
**Scope & Integration:**
- NuGet package add/update/remove guidance for CardGames.sln and multi-target projects
- Dependency version alignment (Aspire v13.1.1 pinning, .NET 10.0.100 coordination)
- Directory.Packages.props centralized management and override resolution
- Integration with Livingston (DevOps), Danny (backend), Linus (frontend), Basher (testing), Rusty (architecture)
- Activation: Via `/skills` command or `@nuget-manager` mention
**Idempotency:** Skill file created at `.ai-team/skills/nuget-manager/SKILL.md`; future runs will verify presence and skip if exists.
**Status:** ✅ Complete—nuget-manager skill ready for team activation and package-management support.

### 2026-02-19: Microsoft Docs Skill Installation (Repeat/Verification)
**By:** Livingston (DevOps)
**Requested by:** Rob Gibbens
**What:** Installed `microsoft-docs` skill as a local variant at `.ai-team/skills/microsoft-docs/SKILL.md` following established pattern. Verified and fixed idempotent state: previous log indicated completion but filesystem check found skill file missing; recreated skill file with full documentation.
**Why:** Microsoft Docs skill provides squad access to official .NET, ASP.NET Core, Azure SDK, Blazor, and Entity Framework Core documentation—all core technologies in CardGames stack. Idempotent verification is essential to ensure skill file persists across session boundaries.
**Scope & Integration:**
- Official .NET and ASP.NET Core API reference and patterns
- Azure SDK, SQL, Functions, Storage, Service Bus documentation
- Blazor component and WebAssembly documentation
- Entity Framework Core patterns and migration guides
- Integration with Livingston (DevOps/infrastructure), Danny (backend/API), Linus (frontend/Blazor), Basher (testing), Rusty (architecture/roadmap)
- Activation: Via `/skills` command or `@microsoft-docs` mention
**Idempotency:** Skill file verified/created at `.ai-team/skills/microsoft-docs/SKILL.md`; re-running installation will detect existing file and skip creation.
**Status:** ✅ Idempotent Success—Skill file present, history updated, no related files lost, re-installation will be safe and complete without duplication.

### 2026-02-19: Add anthropics/skills to Marketplace Configuration
**By:** Livingston (DevOps Specialist)
**Requested by:** Rob Gibbens
**Status:** ✅ Implemented
**What:** Created `.ai-team/plugins/marketplaces.json` with anthropics/skills marketplace source enabled by default, centralizing skill marketplace discovery configuration.
**Why:** Enables idempotent skill marketplace integration while aligning with existing local skill pattern (web-design-reviewer, refactor, prd, nuget-manager, microsoft-docs). Centralizes marketplace configuration in dedicated plugin registry without duplication.
**Impact:** Skills from anthropics/skills marketplace are now discoverable; team members can reference `@anthropics-skills` or activate skills via marketplace discovery. No impact on existing skills or team configurations.

### 2026-02-19: Microsoft Code Reference Skill Installation Idempotence
**By:** Livingston (DevOps Lead)
**Requested by:** Rob Gibbens
**Status:** ✅ Idempotent Success
**What:** Installed `microsoft-code-reference` skill as a local variant at `.ai-team/skills/microsoft-code-reference/SKILL.md` with full idempotent behavior to provide C# code analysis, static analysis, and code intelligence capabilities to the CardGames team.
**Why:** Complements existing skills (microsoft-docs, refactor, nuget-manager, prd, web-design-reviewer) by providing deeper code-level analysis including code navigation, pattern recognition, static analysis via Roslyn, architecture validation against `docs/ARCHITECTURE.md`, and team enablement with role-specific code analysis insights.
**Scope & Capabilities:**
- Code Navigation: understanding codebase structure and dependencies across solution
- Pattern Recognition: identifies architectural patterns and anti-patterns (MediatR, DI, factory patterns)
- Static Analysis: leverages Roslyn analyzers for C# code quality and compliance checks
- Architecture Validation: ensures code adheres to layered architecture (Domain, API, Web, Contracts)
- Team Enablement: supports all team members (Danny, Linus, Basher, Rusty, Scribe) with role-specific code analysis
**Integration:** Mapped to all solution layers (domain, API, Web, Contracts, tests, AppHost, service defaults); activation via `/skills` command or `@microsoft-code-reference` mentions.
**Idempotency:** Skill file verified at `.ai-team/skills/microsoft-code-reference/SKILL.md`; re-running installation will detect existing file and skip creation.

### 2026-02-19: Install frontend-design Skill
**By:** Livingston (DevOps Specialist)
**Requested by:** Rob Gibbens
**Status:** ✅ Implemented
**What:** Installed `frontend-design` skill as a local variant at `.ai-team/skills/frontend-design/SKILL.md` providing specialized frontend design analysis, UI/UX pattern review, component design guidance, and design system validation capabilities.
**Why:** Complements Linus (Frontend Dev) with dedicated design analysis support; fills capability gap for UI/UX-focused work. Follows established local skill installation pattern (web-design-reviewer, refactor, prd, nuget-manager, microsoft-docs, microsoft-code-reference).
**Scope & Capabilities:**
- Pattern analysis and design system validation
- Layout review and interaction design
- Accessibility (WCAG 2.1) and responsive design
- CSS architecture and component design guidance
**Integration:** Works with Linus (Frontend), web-design-reviewer skill, Rusty (Lead), and Basher (Tester). Activation via `/skills` command or `@frontend-design` mentions.
**Validation:** SKILL.md created with valid structure at `.ai-team/skills/frontend-design/SKILL.md`; team integration documented; activation paths verified.
**Idempotency:** ✅ Idempotent—Re-running will detect existing SKILL.md and skip creation. Local variant remains stable until marketplace version becomes available.
