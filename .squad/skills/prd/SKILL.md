# PRD Skill

## Overview
The **prd** skill extends Copilot CLI to provide specialized guidance on product requirements documentation, feature specifications, and product management workflows for the CardGames platform. It helps clarify feature intent, validate design decisions against product goals, and maintain alignment across technical and product teams.

## Scope
This skill is particularly suited for:
- Product Requirements Document (PRD) authoring and review
- Feature specification clarity and completeness
- User story and acceptance criteria validation
- Product goals and KPI definition
- Cross-functional design review support
- Feature scope and MVP boundary definition
- Stakeholder communication and prioritization
- Roadmap and release planning

## Capabilities
1. **PRD Authoring:** Guide creation of clear, structured product requirement documents
2. **Feature Specification:** Clarify acceptance criteria, edge cases, and success metrics for features
3. **Design Alignment:** Validate design decisions (architecture, UX, backend) against stated product requirements
4. **Scope Management:** Help identify MVP boundaries, defer vs. include decisions, and prioritization rationale
5. **Stakeholder Review:** Provide templates and checklists for product sign-off and cross-functional validation
6. **Decision Documentation:** Structure decision rationale linking product goals to implementation choices

## Activation
Once installed, activate the skill via:
```bash
/skills
# Select "prd" from the list
```

Or invoke directly in prompts:
```
@prd: Review this feature scope and help clarify acceptance criteria
```

## Configuration
- Default scope: Feature specifications, product goals, design requirements
- Language: Product documentation, specifications, decision rationale
- Integration: Works with Copilot CLI documentation and design tools

## Relevant Project Context
- Feature designs: `docs/` directory (e.g., `docs/CashierFeatureDesign.md`, `docs/LeaguesBackendDesign.md`)
- Architecture guidance: `docs/ARCHITECTURE.md`
- Decisions registry: `.ai-team/decisions.md`
- README/scope: `README.md`

## Known Integration Points
- Works with Rusty (Lead) for product direction and design review
- Complements Danny (Backend), Linus (Frontend), and Basher (Tester) for technical feasibility and testing scope
- Provides input to feature prioritization and MVP definition
- Supports decision documentation workflows in `.ai-team/decisions.md`

---
**Status:** Ready for installation
**Last Updated:** 2026-02-19
