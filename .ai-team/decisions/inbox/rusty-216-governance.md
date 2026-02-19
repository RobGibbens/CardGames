### 2026-02-19: Governance-capable admins can execute member role administration for #216
**By:** Rusty (Lead)
**Requested by:** Rob Gibbens
**What:** Finalized #216 governance authority so active admins and managers can both execute promote/demote admin membership operations, while ownership transfer remains manager-only and all no-manager/no-governance safety invariants remain enforced.
**Why:** This closes the remaining member-administration gap from prior manager-only slices, aligns governance operations with active admin moderation responsibilities, and preserves lockout prevention guarantees required for P0 release safety.
