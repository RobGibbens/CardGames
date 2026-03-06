# Tess History

## Project Learnings (from import)
- Project stack: C#/.NET (`net10.0`) with ASP.NET API and Blazor web app.
- Requested by: Rob Gibbens.
- Key visual surfaces: `src/CardGames.Poker.Web` components and pages.

## Learnings
- For list-heavy Blazor surfaces, tighten hierarchy by pairing `fw-semibold` title rows with consistently muted `small text-muted` metadata lines.
- Keep action groups visually secondary until needed: use `btn-sm` with one primary action and remaining destructive/secondary actions as outlines.
- KPI tiles read better when each tile uses matching structure (`small text-muted` label + `fw-semibold` value) and equal padding/height.
- Mobile-first readability improves when list rows stack naturally (`flex-wrap`) and action clusters get explicit top margin (`mt-2 mt-md-0`) rather than relying on implicit wrapping.
- Badge rhythm matters: reserve strong badge colors for attention states (e.g., pending joins) and keep role/status badges on neutral variants for scanability.
- 2026-02-19 (team update): My Clubs visual polish guardrails are canonical in `.ai-team/decisions.md`; design direction remains Bootstrap/utility tuning only with no new tokens/components and no UX behavior change.
- 2026-02-19 (second pass): For denser My Clubs UI, unify KPI + quick switch inside one bordered utility wrapper, tighten spacing (`g-2`, `p-2`/`py-2`), and keep hierarchy explicit with muted labels + stronger numeric/title emphasis.
- 2026-02-19 (second pass): Preserve action balance by demoting section-level CTA styling (outline where possible) while keeping per-row primary action dominant, especially in wrapped mobile layouts.
- 2026-02-19 (review pass): The updated My Clubs section is visually approved; summary metrics, quick switch, and club list now read as one coherent information rhythm without adding new UI surface area.
- 2026-02-19 (review pass): Critical hierarchy signal is now consistent—muted metadata, semibold titles/values, and restrained badges—improving scan speed while keeping Bootstrap-only constraints intact.
- 2026-02-19 (team update): My Clubs second-pass direction and review approval were merged from inbox into canonical `.ai-team/decisions.md`; subsequent design nudges should align to that consolidated decision record.
- 2026-02-19 (full rethink): Flat club lists underperform for governance-heavy users; role-bucket ordering (Manager/Owner → Admin → Member) materially improves first-pass findability without requiring new data.
- 2026-02-19 (full rethink): Quick-switch controls work best as part of a top priority action strip (with pending and manage-capable signals) rather than as a secondary control below KPI tiles.
- 2026-02-19 (full rethink): Standardizing each club row into identity/context/meta/action lines improves scan consistency and reduces decision latency while preserving existing handlers and accessibility semantics.
- 2026-02-19 (full redesign review): The implemented `My Clubs` block satisfies the full rethink spec end-to-end (state ordering, priority strip, role buckets, and standardized row anatomy) while preserving all existing behavior and contracts.
- 2026-02-19 (full redesign review): Material UX change can be achieved through IA reordering and component reshaping alone; no new tokens/components are required to produce a clearly different operational flow.
- 2026-02-19 (team update): Full-rethink spec + review artifacts for My Clubs were merged from inbox into canonical `.ai-team/decisions.md`; subsequent design guidance should reference the consolidated canonical record.
- 2026-02-19 (command center review): Command Center card-grid pattern is approved when the section keeps one compact header rail (title + icon refresh) followed immediately by three scan-friendly stats pills (Total/Admin/Pending) with explicit text labels.
- 2026-02-19 (command center review): Removing Quick Open and per-card Leave simplifies cognitive load without workflow loss when each card retains a full-width `Open Club` CTA and responsive 1/2/3-column grid behavior.
- 2026-02-19 (team update): Command Center baseline/implementation/review artifacts are now merged into canonical `.ai-team/decisions.md`; follow-up design guidance should reference canonical entries only.
- 2026-02-19 (pill/card final review): My Clubs implementation is fully approved against the 8-point acceptance list (pill labels/count circles, role badge behavior, card anatomy, spacing, and full-width CTA).
- 2026-02-19 (pill/card final review): Keeping KPI pills and row role chips on consistent rounded-pill patterns while preserving `H2` identity hierarchy produced a clearer scan path without introducing any new components.
- 2026-02-19 (team update): Final-pass My Clubs implementation/review artifacts (`linus-my-clubs-pill-card-final-pass.md`, `tess-my-clubs-pill-card-final-review.md`) are merged into canonical `.ai-team/decisions.md`; follow-up guidance should reference canonical entries only.
- 2026-03-06 (table controls review): `TablePlay` top-strip clutter is primarily caused by mixing persistent metadata (table/game/blinds) with frequent actions; separating these into a draggable metadata overlay improves first-scan action clarity.
- 2026-03-06 (table controls review): For poker play surfaces, the top strip should prioritize “always-needed” controls (leave/sit-out/sound + host run-state actions) while descriptive context is secondary and can live in a collapsible panel.
- 2026-03-06 (table controls review): Existing draggable panel conventions (`drag-handle`, fixed glass panel styling, localStorage position persistence) are sufficient for an MVP metadata overlay without introducing new visual tokens or interaction patterns.
- 2026-03-06 (table controls review): Connection UX should avoid duplicate emphasis—retain global reconnect/disconnect banner for blocking state and keep strip-level status compact and low-noise.
- 2026-03-06 (team update): Tess/Linus table-controls-strip alternatives were merged into canonical `.squad/decisions.md` as one deduped decision; decision inbox entries were cleared.
