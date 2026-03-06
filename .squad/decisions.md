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

### 2026-02-26: Unified card styling across league UI
**By:** Linus (Frontend Dev), requested by Rob Gibbens
**What:** All "card" collection items (event rows, active game cards, season rows, league club cards) now share a consistent visual treatment: thin grey border, left primary-color accent (`border-left: 3px solid var(--primary)`), and border-based hover glow (`box-shadow: 0 0 0 1px var(--primary)`). Shadow-based hover on `.league-club-card` was replaced with border-based hover. CSS-only changes in `app.css` — no Razor markup modified.
**Why:** Rob requested visual consistency across all card-like list items in the league pages.

### 2026-02-26: Lobby tabs visual redesign
**By:** Linus (Frontend Dev), requested by Rob Gibbens
**What:** Replaced pill-shaped button tabs (`.lobby-tabs`) with proper tab navigation — bottom-border rail, 3px `var(--primary)` active indicator, 8px top-corner radius for folder-tab shape, no gradient/box-shadow on active. Badges stay functional with primary color on active tabs and muted on inactive.
**Why:** User feedback that tabs looked like buttons, not navigation tabs. CSS-only change, zero markup impact.

### 2026-02-27: Inline Season Events in Schedule Tab
**By:** Arwen (Frontend Dev)
**What:** Restructured `LeagueDetailScheduleTab.razor` so season events render inline beneath their parent season row within the same card. Added toggle behavior (View/Hide Events) and moved "Create Event" button to appear next to the toggle when a season is expanded. Removed separate "Season Events" section.
**Why:** Users had to mentally connect which events belonged to which season in the previous two-section layout. Inline rendering creates correct visual hierarchy.

### 2026-03-01: Community cards moved to horizontal row beside deck
**By:** Arwen (Linus) — requested by Rob Gibbens
**What:** Restructured `.table-center` in TableCanvas.razor so the deck and community cards sit in a horizontal `.table-center-row` flex container, with pot and phase indicator stacked above. This prevents community cards from overlapping the bottom player's hand and uses the available horizontal space in the table center.
**Why:** Community cards for "The Good, the Bad, and the Ugly" were hidden behind the bottom player's hand in the previous vertical-only layout.

### 2026-03-02: Dealer's Choice as a "Meta-Game" Wrapper, Not a GameType
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Dealer's Choice should NOT be implemented as another `GameType` in the `PokerGameMetadataRegistry`. Instead, it is a table mode — a property on the `Game` entity (`IsDealersChoice` flag) that changes how hands are sequenced. The `Game.GameTypeId` becomes nullable so it can change per hand. Each hand still resolves to a real game type handler via `IGameFlowHandlerFactory.GetHandler(gameTypeCode)`.
**Why:** The existing `GameType` entity describes static game rules. DC isn't a variant — it's an orchestration mode that selects from existing variants. A table-mode flag keeps the existing handler resolution clean.

### 2026-03-02: Track DC Dealer Separately from Per-Hand Dealer
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Introduce `DealersChoiceDealerPosition` on the `Game` entity — this tracks whose turn it is to choose the next game type. The existing `DealerPosition` continues to track the per-hand dealer for game mechanics.
**Why:** Kings and Lows rotates `DealerPosition` internally across its multi-hand lifecycle. The separation ensures inner-game dealer rotation never corrupts the outer DC rotation.

### 2026-03-02: New Phase — WaitingForDealerChoice
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Add `WaitingForDealerChoice` to the `Phases` enum. When continuous play enters this phase, the background service pauses and the UI presents a modal to the DC dealer to choose game type, ante, and minimum bet.
**Why:** The current `ContinuousPlayBackgroundService.StartNextHandAsync` assumes it can immediately start the next hand. DC requires human input between hands. Structurally similar to `WaitingForPlayers`.

### 2026-03-02: Kings and Lows Encapsulation — "Inner Game" Lifecycle
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** When a DC dealer selects Kings and Lows, the entire KAL lifecycle runs as a self-contained unit. DC treats the entire KAL session as a single "turn" for the choosing dealer. When KAL concludes, DC advances the DC dealer position by one.
**Why:** KAL already owns its own internal `MoveDealer` calls and pot carryover logic. Treating multi-hand games as atomic units from DC's perspective avoids complex coupling.

### 2026-03-02: Dealer's Choice — API & Database Schema Design
**By:** Gimli (Backend Dev)
**Requested by:** Rob Gibbens
**What:** Comprehensive design covering schema changes (`GameTypeId` nullable, `IsDealersChoice` flag, `DealersChoiceDealerPosition`, `CurrentHandGameTypeCode`, new `DealersChoiceHandLog` entity), new API endpoints (`POST /api/v1/games/{gameId}/dealers-choice`, `GET /api/v1/games/dealers-choice/available-games`, `GET /api/v1/games/{gameId}/dealers-choice/history`), MediatR commands, SignalR state extensions, and contract DTOs.
**Why:** Enables tables where the dealer chooses the game type each hand while preserving all existing single-game-type behavior unchanged. Phase-based blocking (`WaitingForDealerChoice`) slots into existing phase machine. Backward compatible — `IsDealersChoice = false` + non-null `GameTypeId` for all existing tables.
**Reference:** `docs/DealersChoiceUIDesign.md`, inline in inbox file

### 2026-03-02: Dealer's Choice UI architecture — table mode, not game type
**By:** Linus (Frontend Dev)
**Requested by:** Rob Gibbens
**What:** Dealer's Choice is modeled as a table-level mode (`IsDealersChoice`), not a game type in the domain engine. The `DEALERS_CHOICE` code is a client-side constant used at table creation time; the actual `_gameTypeCode` updates per hand via `DealerChoiceMade` SignalR event. Two new components: `DealerChoiceModal.razor` (dealer) and `DealerChoiceWaiting.razor` (others). Ante/min bet set per-hand by dealer. 60-second timeout with server auto-fallback.
**Why:** Keeps game rules in the domain layer. The UI adapts per-hand by updating `_gameTypeCode` from SignalR, reusing all existing game-type-specific rendering.
**Reference:** `docs/DealersChoiceUIDesign.md`

### 2026-03-02: Dealer's Choice test strategy — architectural test assumptions
**By:** Basher (Tester)
**Requested by:** Rob Gibbens
**What:** Comprehensive test strategy with ~35 scenarios across 5 test files (P0/P1/P2). Key assumptions: DC mode via null/empty `GameCode`, separate `DcDealerPosition` field, new `ChooseDealerGameCommand`, `WaitingForDealerChoice` phase, ContinuousPlayBackgroundService DC-aware, and K&L encapsulation where DC dealer advances after full K&L resolution.
**Why:** These assumptions drive all test scenarios. Team should validate before implementation begins to avoid test/implementation divergence.
**Reference:** `docs/DealersChoiceTestStrategy.md`

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

### 2026-02-24: Create Dropdown on League Detail Header
**By:** Arwen (Frontend Dev)
**Requested by:** Rob Gibbens
**What:** Replaced the single "Create" button on the League Detail page with a dropdown that lets managers choose between creating a Cash Game or Tournament. Extracted the one-off event modal into `CreateOneOffEventModal.razor` under `LeagueDetailTabs/`, with self-contained form state, validation, and inline error display. Click-outside dismissal uses a transparent fixed backdrop (pure Blazor, no JS interop).
**Why:** Supports multiple event types from the league header without adding JS dependencies or coupling modal state to the parent page.
**Files:** `LeagueDetail.razor`, `LeagueDetailTabs/CreateOneOffEventModal.razor`, `wwwroot/app.css`
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

### 2026-02-19: League creator invite permissions role-projection alignment
**By:** Danny (Backend Dev)
**Requested by:** Rob Gibbens
**What:** Aligned league API role projection so current-user role fields in create/detail/my-leagues responses map persisted `Owner` to projected `Manager`, while leaving persisted membership role unchanged as `Owner`.
**Why:** Keeps invite/admin UX flows immediately usable for league creators and consistent with management-capable authorization without changing storage semantics.

### 2026-02-19: Governance-capable admins can execute member role administration for #216
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Finalized #216 authority so active admins and managers can execute promote/demote admin membership operations; ownership transfer remains manager-only; no-governance/no-manager safety invariants remain enforced.
**Why:** Closes remaining member-administration gaps while preserving P0 lockout-prevention guarantees.

### 2026-02-19: Design-review scope lock for My Clubs visual refresh (pre-implementation)
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Approved a look-and-feel-only refresh for the existing `My Clubs` section in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor`, preserving current information architecture, actions, loading/empty states, role badges, pending indicators, and responsive behavior.
**Why:** Improves readability and hierarchy while preventing scope creep into behavior/API/permission changes.

**Constraints:**
- Styling/layout refinement only within existing component boundaries and design-system primitives.
- No new features, controls, data fields, navigation changes, or backend/API/contract updates.

### 2026-02-19: My Clubs refresh constrained to utility-class UI tuning
**By:** Linus (Frontend)
**Requested by:** Rob Gibbens
**What:** Constrained `My Clubs` updates in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor` to Bootstrap/existing utility-class changes for section rhythm, summary tile spacing, quick-switch cohesion, and list-row polish.
**Why:** Delivers a focused polish pass without altering behavior, bindings, API integration, copy intent, or responsive structure.

### 2026-02-19: My Clubs visual polish guardrails
**By:** Tess (Graphic Designer)
**Requested by:** Rob Gibbens
**What:** Set implementation-ready guardrails for `My Clubs` visual refinement using existing Bootstrap/utility classes only (spacing, hierarchy, grouping, metadata rhythm, badge consistency), with no UX/behavior changes and no new design tokens/components.
**Why:** Keeps current sprint scope safe while improving readability and preserving accessibility baseline.

### 2026-02-19: My Clubs second-pass visual direction (compact density pass)
**By:** Tess (Graphic Designer)
**Requested by:** Rob Gibbens
**What:** Defined a denser second-pass visual direction for `My Clubs` in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor` using Bootstrap/utility classes only: one bordered summary wrapper for KPI + quick switch, tighter spacing rhythm (`g-2`, reduced padding), stronger KPI value emphasis, cleaner list-row density, and balanced action emphasis for mobile wrap.
**Why:** Improve hierarchy, grouping, and scanability while preserving behavior, copy, component boundaries, and design-token constraints.

### 2026-02-19: My Clubs second-pass implementation decisions (class-only)
**By:** Linus (Frontend)
**Requested by:** Rob Gibbens
**What:** Applied second-pass class-level polish to `My Clubs` in `Leagues.razor`, including grouped summary/quick-switch container, tightened KPI and list spacing, stronger KPI value weight, quick-switch alignment and outline CTA, and mobile-friendly action width helpers.
**Why:** Deliver a compact, more readable visual rhythm without changing handlers, bindings, logic, text, permissions, API, or responsive intent.

### 2026-02-19: My Clubs second-pass design review approval
**By:** Tess (Graphic Designer)
**Requested by:** Rob Gibbens
**What:** Approved the second-pass `My Clubs` update, confirming improved coherence across KPI summary, quick switch, and club rows with better hierarchy and action balance.
**Why:** Confirms quality and scope adherence for the follow-up visual pass while keeping Bootstrap-only, no-behavior-change constraints intact.

### 2026-02-19: User directive
**By:** Rob Gibbens (via Copilot)
**What:** Completely rethink how the `My Clubs` section looks and works; do not treat prior small CSS-tweak constraints as limiting for this section.
**Why:** User feedback indicates prior iterations were too close to the original UX and did not meet redesign expectations.

### 2026-02-19: My Clubs rethink baseline (focused redesign)
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**Context:** User explicitly rejected prior iterations and requested a full rethink of the `My Clubs` section look and workflow. This supersedes earlier “small polish only” constraints for this section.

#### Redesign concept
- Shift `My Clubs` from a mixed “stats + raw list” block into a task-first workspace: orient the section around “where should I go next” and “what needs my attention now.”
- Promote actionable context to the top (pending join actions, quick-switch/open path), and demote passive metadata to secondary scan lines.
- Keep all existing data/actions intact, but reorganize into clearer role-based and attention-based groupings to reduce row-by-row hunting.

#### New section structure
1. **Section header rail:** Keep title and refresh, add short status subtitle (derived from existing counts) that summarizes total clubs and pending actions.
2. **Attention strip (top priority):** Surface pending join actions first, with clear “review now” affordance via existing `Open` navigation path.
3. **Quick switch panel:** Keep existing selector and open action, but position directly under attention strip as the primary jump control.
4. **Club group buckets:** Render clubs in this order using existing `_leagues` data only: Manager/Owner clubs, Admin clubs, Member clubs.
5. **Club card rows per bucket:** For each club, keep current data points but standardize row anatomy: name+role, optional description, concise meta line, action cluster (`Open`, `Leave`).
6. **State handling at section bottom:** Preserve existing loading and empty states with updated phrasing/layout only; no logic changes.

#### Must keep
- Data sources and bindings: `_leagues`, `_adminClubCount`, `_managerClubCount`, `_totalPendingJoinActions`, `_isLoadingPendingActions`, `_quickSwitchLeagueId`, per-league pending count via `GetPendingJoinActionsCount(...)`.
- Existing actions/handlers: `LoadLeaguesAsync`, `OpenQuickSwitchLeagueAsync`, `OpenLeagueDetail(...)`, `LeaveLeagueAsync(...)`.
- Current role semantics and display values from `LeagueRole` (`Owner`/`Manager`/`Admin`/member role text as currently projected).
- Existing loading/empty behavior branches (`_isLoading`, no leagues state) and current navigation destinations (`/leagues/{leagueId}`).
- No backend/API/contract/domain changes; no new endpoints; no altered request/response flows.

#### Can change
- Layout and grouping inside `My Clubs`: reorder blocks, bucket clubs by role, and restructure row composition for clearer scanability.
- Label wording and visual hierarchy: headings, helper text, badge emphasis, density/spacing rhythm, and button prominence.
- Style implementation details using existing design system primitives/utilities only (no new components, tokens, or backend-driven fields).
- **Arwen boundary:** implement structural IA changes in `My Clubs` markup (section order, grouping containers, bucket rendering, and conditional block placement) while preserving all existing bindings and handlers.
- **Galadriel boundary:** implement presentation hierarchy pass (spacing, typography weight, badge/button emphasis, responsive rhythm) on top of Arwen structure without changing behavior, handlers, or data contracts.

### 2026-02-19: My Clubs full redesign spec (task-first IA)
**By:** Tess (Graphic Designer)
**Requested by:** Rob Gibbens

**Intent:** Replace the current `My Clubs` composition with a significantly different, action-first structure that improves scanability and reduces the awkward open flow, while keeping existing data, handlers, and contracts unchanged.

## Scope lock (must not change)
- Keep existing data/state sources: `_leagues`, `_adminClubCount`, `_managerClubCount`, `_totalPendingJoinActions`, `_isLoadingPendingActions`, `_quickSwitchLeagueId`, `GetPendingJoinActionsCount(...)`.
- Keep existing handlers/actions: `LoadLeaguesAsync`, `OpenQuickSwitchLeagueAsync`, `OpenLeagueDetail(...)`, `LeaveLeagueAsync(...)`.
- Keep existing loading + empty state behavior branches.
- No backend/API/data contract/domain changes.
- No new external dependencies.

## 1) Replacement markup blueprint (section-by-section)

### A. My Clubs command header (single action rail)
**Goal:** Immediate orientation + one obvious maintenance action.

**Structure:**
- Left: `h2` title `My Clubs`.
- Under title: one compact dynamic summary sentence using existing counts (example: "8 clubs · 2 manager/admin clubs · 3 pending joins").
- Right: small `Refresh` button.

**Bootstrap guidance:**
- Container: `d-flex flex-wrap justify-content-between align-items-start gap-2`
- Title block: `d-flex flex-column gap-1`
- Summary line: `small text-muted`
- Refresh button: `btn btn-sm btn-outline-secondary`

### B. Priority action strip (new top surface)
**Goal:** Put urgent workload and “where to go next” first.

**Structure:**
- Left tile: `Pending join actions` with loading or count.
- Middle tile: `Manage-capable clubs` (manager + admin total using existing computed counts).
- Right tile: `Quick Open` control group (selector + `Open club`).

**Important change:** Quick switch moves into this top strip as a primary jump control; it is no longer below KPI tiles.

**Bootstrap guidance:**
- Strip wrapper: `border rounded p-3 d-flex flex-column gap-3`
- Desktop layout: inner `row g-2 align-items-end`
- Tiles: `col-12 col-md-3` + card `border rounded p-2 h-100 d-flex flex-column`
- Tile labels: `small text-muted`
- Tile values: `fw-semibold fs-5` (or `fw-bold` if `fs-5` feels too strong)
- Quick open column: `col-12 col-md-6 d-flex flex-column gap-2`
- Quick open input row: `d-flex flex-wrap gap-2 align-items-end`
- Selector wrapper: `form-floating flex-grow-1`
- Open action: `btn btn-primary btn-sm px-3` (primary in this strip)

### C. Role buckets (replace single flat list)
**Goal:** Remove row-hunting by grouping clubs by responsibility.

**Render order (fixed):**
1. `Manager / Owner clubs`
2. `Admin clubs`
3. `Member clubs`

**Structure per bucket:**
- Bucket header row: title + item count badge.
- Bucket body: list-group of only that role slice.
- Skip empty buckets entirely (no placeholder noise), except when all buckets empty (use existing empty state branch).

**Bootstrap guidance:**
- Buckets wrapper: `d-flex flex-column gap-3`
- Bucket container: `border rounded p-2 p-md-3`
- Bucket header: `d-flex justify-content-between align-items-center mb-2`
- Bucket title: `h6 mb-0 fw-semibold`
- Count pill: `badge text-bg-light text-dark`
- Bucket list: `ul.list-group`

### D. Club row anatomy (standardized, card-like list item)
**Goal:** Make every row scan the same way and place actions predictably.

**Row layout:**
- **Line 1 (identity):** Club name (strong), role badge, optional pending-joins badge.
- **Line 2 (context):** Description if present.
- **Line 3 (meta):** Created date (and role text only if needed for clarity).
- **Right action cluster:** `Open` (primary) then `Leave` (outline-danger).

**Bootstrap guidance:**
- List item: `list-group-item py-2 px-3 d-flex flex-wrap gap-2 align-items-start`
- Content stack: `d-flex flex-column gap-1 flex-grow-1`
- Identity line: `d-flex flex-wrap align-items-center gap-2`
- Club name: `fw-semibold`
- Role badge: `badge text-bg-light text-dark`
- Pending badge loading: `badge text-bg-light text-muted`
- Pending badge active: `badge text-bg-warning`
- Description/meta: `small text-muted`
- Action cluster: `d-flex flex-wrap gap-2 ms-md-auto mt-2 mt-md-0`
- Open button: `btn btn-sm btn-primary league-action-btn w-100 w-sm-auto`
- Leave button: `btn btn-sm btn-outline-danger league-action-btn w-100 w-sm-auto`

### E. State branch placement
**Goal:** Keep state comprehension simple.

**Order:**
1. If loading: show loading text below command header.
2. Else if no clubs: show empty text below command header.
3. Else: show priority strip + role buckets.

This keeps loading/empty from competing with containers that only make sense with content.

## 2) Priority hierarchy (what appears first and why)
1. **Command header + refresh** — user immediately knows location and can recover stale state.
2. **Priority action strip** — urgent/decision-driving signals and quick open path are first actionable surface.
3. **Manager/Owner bucket** — highest responsibility clubs first to support admin-heavy workflows.
4. **Admin bucket** — secondary governance responsibilities.
5. **Member bucket** — lowest urgency browsing/entry points.

Rationale: ordering follows action urgency and responsibility, not alphabetic/flat list parity.

## 3) Interaction flow rewrite (open/act without awkward sequence)

### New default path (fast open)
1. User lands in `My Clubs` and sees quick open in the top strip immediately.
2. User selects club from quick switch.
3. User presses `Open club` (or keeps current auto-selected default and presses open).
4. App navigates via existing `OpenQuickSwitchLeagueAsync` → `OpenLeagueDetail(...)`.

### New responsibility-first path
1. User scans role buckets top-down.
2. If pending actions badge appears in a row, user opens that club directly from row `Open`.
3. If leaving, user triggers `Leave` directly from same row without changing selection context.

### Why this removes awkwardness
- Current layout forces users through mixed KPI/list rhythm and delayed quick-switch prominence.
- New layout places jump control and urgency signal before list exploration.
- Buckets reduce cognitive load from "find my admin clubs in one long list" to "go to first relevant section".

## 4) Class-level guidance Arwen can implement immediately (Razor/Bootstrap)

### Keep
- Existing iconography and button labels (`Refresh`, `Open club`, `Open`, `Leave`).
- Existing bindings and conditional logic sources.

### Refactor markup shape in `My Clubs` block
- Replace the current KPI grid + quick-switch wrapper with the new **Priority action strip**.
- Add precomputed role slices in Razor code block (or inline LINQ) for three buckets:
	- manager/owner: `Role == Manager || Role == Owner`
	- admin: `Role == Admin`
	- member: everything else
- Render bucket sections conditionally only when each slice has items.

### Accessibility/responsive notes
- Preserve semantic headings (`h2` for section, `h6` for bucket headers).
- Keep buttons as native `<button>` with existing disabled states.
- Ensure action cluster wraps on small screens (`flex-wrap`, `w-100 w-sm-auto`).
- Keep muted metadata contrast patterns already in use (`small text-muted`) and avoid color-only meaning by retaining text labels (`Pending joins:`).

## Implementation checklist for Arwen
- [ ] Rebuild `My Clubs` section into: command header → state branch → priority action strip → role buckets.
- [ ] Move quick switch into priority strip and keep `OpenQuickSwitchLeagueAsync` wiring unchanged.
- [ ] Keep refresh button and handler unchanged; place in command header rail.
- [ ] Partition `_leagues` into manager/owner, admin, member buckets and render in that strict order.
- [ ] Standardize club row anatomy (identity line, optional description, meta line, action cluster).
- [ ] Preserve per-row `Open` and `Leave` handlers exactly.
- [ ] Preserve pending-joins badge behavior and loading variant exactly.
- [ ] Preserve loading and empty text branches with same behavior.
- [ ] Validate responsive wrap at mobile widths and keyboard reachability of all controls.
- [ ] Confirm no backend/API/contracts changed and no new dependencies added.

### 2026-02-19: My Clubs full redesign implementation (task-first IA)
**By:** Linus (Frontend)
**Requested by:** Rob Gibbens

**What:** Implemented a substantial structural redesign of the `My Clubs` section in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor` only, replacing the previous flat KPI/list rhythm with a task-first flow:
1. command header rail with dynamic summary + refresh,
2. state branch placement (loading / empty before content surfaces),
3. top priority action strip (pending joins, manage-capable clubs, quick open),
4. role-bucketed club rendering (Manager/Owner, Admin, Member),
5. standardized per-row club anatomy and action cluster.

**Why:** User explicitly rejected prior iterations and requested a full rethink. The redesign improves scanability and action-first navigation without changing behavior, data contracts, handlers, or backend flows.

**Functional parity preserved:**
- `Refresh` button still invokes `LoadLeaguesAsync`.
- Quick switch dropdown + `Open club` still use `_quickSwitchLeagueId` + `OpenQuickSwitchLeagueAsync`.
- Per-club `Open` and `Leave` buttons still call `OpenLeagueDetail(...)` and `LeaveLeagueAsync(...)` unchanged.
- Loading state, empty state, and pending-join count/loading behavior are preserved.
- Existing data/state sources remain intact (`_leagues`, `_adminClubCount`, `_managerClubCount`, `_totalPendingJoinActions`, `_isLoadingPendingActions`, `GetPendingJoinActionsCount(...)`).

**Out of scope honored:**
- No backend/API/domain/contract changes.
- No new dependencies.
- Existing information remains visible (role, created date, description, pending join indicators).

**Validation:**
- Error check run on `Leagues.razor` after edit; no errors reported.

### 2026-02-19: My Clubs full redesign review
**By:** Tess (Graphic Designer)
**Requested by:** Rob Gibbens

VERDICT: APPROVED

- The information architecture is materially changed from a flatter list presentation to a task-first flow: command header, then state branch, then priority action strip, then role buckets in fixed responsibility order.
- Quick navigation is materially elevated by moving `Quick Open` into the top priority strip with pending and manage-capable signals, creating a direct “decide then open” path before list scanning.
- Club entries are materially standardized into consistent identity/context/meta/action anatomy inside role buckets, which improves scanability and responsibility-first findability without changing handlers, data, or contracts.

### 2026-02-19: My Clubs Command Center baseline (card-grid dashboard)
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens

**What:** Set a Command Center baseline for `My Clubs` in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor`: consolidated header rail (title + icon refresh), stats pills (`Total`, `Admin`, `Pending`), responsive card-grid body, and uniform card anatomy with full-width `Open Club` action.
**Why:** Shift from list-heavy scanning to action-first navigation while keeping behavior and data flows stable.

**Guardrails:**
- No backend/API/domain/contract changes.
- Preserve existing handlers and bindings (`LoadLeaguesAsync`, `OpenLeagueDetail(...)`, league state sources).
- Preserve loading and empty state behavior.
- Remove `Quick Open` and per-card `Leave` from `My Clubs` cards.

### 2026-02-19: My Clubs command-center implementation
**By:** Linus (Frontend)
**Requested by:** Rob Gibbens

**What:** Implemented the Command Center baseline in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor` for `My Clubs` only: icon-refresh header, `Total/Admin/Pending` pills, responsive card grid, role badges, truncated descriptions, and open-first card CTA.
**Why:** Deliver a clearer command surface for many-club scenarios with minimal interaction friction.

**Scope confirmations:**
- Kept existing loading and empty branches.
- Kept existing data sources and navigation wiring.
- No backend/API/contract/domain changes.

### 2026-02-19: My Clubs Command Center review
**By:** Tess (Graphic Designer)
**Requested by:** Rob Gibbens

VERDICT: APPROVED

**Review outcome:**
- Header + icon refresh + stats pills meet command-center structure.
- `Quick Open` is removed from `My Clubs`; per-card `Leave` is removed.
- Card grid and card anatomy meet spec (name + role badge, truncated description, full-width `Open Club`).
- Pending pill state treatment passes loading/zero/positive checks.
- Behavior parity constraints remain intact and scope is isolated to `Leagues.razor`.

### 2026-02-19: My Clubs pills + card final pass
**By:** Linus (Frontend Dev)
**Requested by:** Rob Gibbens

**What:** Implemented a scoped visual refinement of the `My Clubs` section in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor` only:
1. Replaced text badges under `My Clubs` with capsule pills that include a darker inner count circle:
	- Clubs: count + `Clubs` label
	- Manager/Admin: aggregate count from `_adminClubCount + _managerClubCount` + `Manager/Admin` label
	- Pending: count from `_totalPendingJoinActions` + `Pending` label
2. Pending pill renders muted when pending is `0` and highlighted when pending is `> 0` (loading state remains represented).
3. Updated each My Clubs league card:
	- League name rendered as `H2`
	- Right-side role pill label shows `Manager/Admin` for management roles; otherwise `Member`
	- Description shown directly beneath title row
	- Clear vertical spacing before full-width `Open Club` button

**Why:** Matches requested UX for final pass while keeping implementation minimal and behavior-safe.

**Constraints honored:**
- No API/backend/contract/logic flow changes.
- Loading/empty states and open handler behavior unchanged.
- Quick Open and Leave remain removed from this section.
- Edits scoped to one file (`Leagues.razor`) for UI plus local helper methods.

### 2026-02-19: My Clubs pills + card final review
**By:** Tess (Graphic Designer)
**Requested by:** Rob Gibbens

VERDICT: APPROVED

- Pills include count inside a darker inner circle with labels `Clubs`, `Manager/Admin`, and `Pending`.
- `Manager/Admin` and `Pending` use the same rounded-pill badge style.
- Each league entry is wrapped in a card container.
- League name is rendered as an `H2` element.
- Right-side role pill shows `Manager/Admin` for management roles and `Member` otherwise.
- Description appears beneath the league name.
- Vertical spacing exists before the `Open Club` button.
- `Open Club` button is full width.

### 2026-03-02T20:00:00Z: Phase 1 schema changes blocked on 3 nullable GameTypeId errors
**By:** Danny (Backend Dev)
**What:** All Dealer's Choice DB schema changes are applied (Game.cs, DealersChoiceHandLog, GameConfiguration, CardsDbContext, Phases enum). However, making `GameTypeId` nullable caused 3 compile errors in existing code that assumes non-null:
1. `GetActiveGamesMapper.cs:16` — cannot convert `Guid?` to `Guid`
2. `LobbyStateBroadcastingBehavior.cs:130` — cannot convert `Guid?` to `Guid`
3. `GetGameMapper.cs:19` — cannot convert `Guid?` to `Guid`

EF migration `AddDealersChoice` cannot be generated until these are fixed. These are Phase 2 handler fixes.
**Why:** Nullable FK is required for Dealer's Choice tables where game type is chosen per-hand, not at table creation.

### 2025-03-05: Texas Hold 'Em PRD Created
**By:** Squad Coordinator (Rob Gibbens requested)
**What:** Created comprehensive PRD at docs/TexasHoldEmPRD.md covering all changes needed to add Texas Hold 'Em to the platform. Key decisions: (1) Hold 'Em-specific feature folder with dedicated command handlers, (2) Community card dealing atomic within betting action transaction, (3) Blind fields via CreateGameCommand partial record extension, (4) SB/BB positions computed client-side. 17 work items across 3 priority tiers. No database migrations needed.
**Why:** User requested detailed PRD before implementation begins.
