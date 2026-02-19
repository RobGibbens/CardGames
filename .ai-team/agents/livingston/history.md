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
