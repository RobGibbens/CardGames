# Rusty History

## Project Learnings (from import)
- Project: CardGames (.NET 10 card games platform with API, Blazor Web, and tests).
- Requested by: Rob Gibbens.
- Primary architecture references: docs/ARCHITECTURE.md and README.md.

## Learnings

- Recorded team-level decision in `.ai-team/decisions.md` to prioritize OAuth providers first on auth pages, with local auth preserved as secondary fallback and no behavior changes.
- Designed `docs/CashierFeatureDesign.md` for an account-level Cashier feature: add `Cashier` nav item, new `/cashier` page (balance hero + add chips + ledger), and Profile-scoped MediatR APIs/contracts for summary, add-chips, and paged ledger while keeping existing table/game chip flows unchanged.
- Session outcome captured by Scribe: cashier design decisions were merged from inbox into `.ai-team/decisions.md` and orchestration/session logs were recorded under `.ai-team/orchestration-log` and `.ai-team/log`.
