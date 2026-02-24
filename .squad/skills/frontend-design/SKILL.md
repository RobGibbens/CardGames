# Frontend Design Skill

## Overview
The **frontend-design** skill extends Copilot CLI to provide specialized frontend design analysis and guidance, with a focus on UI/UX patterns, component design, styling systems, and user-centered design principles within the CardGames context.

## Scope
This skill is particularly suited for:
- UI/UX design review and feedback
- Component design patterns and composition
- Design system consistency and enforcement
- Color, typography, spacing, and layout standardization
- Responsive design and mobile-first considerations
- Interaction patterns and user flows
- Accessibility-first design (WCAG 2.1)
- Brand alignment and visual consistency

## Capabilities
1. **Design Pattern Analysis:** Review component designs against established patterns and best practices
2. **Design System Validation:** Ensure color palettes, typography, spacing, and iconography align with brand guidelines
3. **Layout & Composition:** Evaluate grid systems, spacing, alignment, and responsive breakpoints
4. **Interaction Design:** Assess UX flows, form patterns, navigation, feedback mechanisms, and microinteractions
5. **Accessibility-First Review:** Validate inclusive design practices, color contrast, focus states, and assistive technology support
6. **Mobile & Responsive Design:** Ensure designs work across devices and viewports
7. **CSS Architecture Guidance:** Recommend scalable and maintainable styling approaches

## Activation
Once installed, activate the skill via:
```bash
/skills
# Select "frontend-design" from the list
```

Or invoke directly in prompts:
```
@frontend-design: Review the Login dialog component design for consistency
@frontend-design: Suggest responsive layout improvements for the Game Board
@frontend-design: Validate the color palette against brand guidelines
```

## Configuration
- Default scope: UI/UX design, component layouts, styling systems, design patterns
- Language: HTML, CSS, Blazor components, design tokens
- Integration: Works with Copilot CLI code review and design tools (`/review`, `/suggest`)
- Team focus: Blazor Web UI with .NET component architecture

## Relevant Project Paths
- Component library: `src/CardGames.Poker.Web/Components/`
- Account/Auth components: `src/CardGames.Poker.Web/Components/Account/`
- Shared layouts: `src/CardGames.Poker.Web/Components/Layout/`
- CSS/styling: `src/CardGames.Poker.Web/Components/**/*.css`
- App layouts: `src/CardGames.Poker.Web/App.razor`
- Root page: `src/CardGames.Poker.Web/Components/Pages/`

## Known Integration Points
- Works with Linus (Frontend) for UI component design and implementation feedback
- Complements web-design-reviewer skill for deep technical review
- Provides signal to Rusty (Lead) for design consistency decisions
- Informs Basher (Tester) for UI/accessibility test coverage planning

---
**Status:** Ready for installation
**Last Updated:** 2026-02-19
