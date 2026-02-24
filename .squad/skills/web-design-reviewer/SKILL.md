# Web Design Reviewer Skill

## Overview
The **web-design-reviewer** skill extends Copilot CLI to provide specialized code review and analysis for web UI components and styling, with a focus on accessibility, responsiveness, and design system consistency.

## Scope
This skill is particularly suited for:
- Blazor component review (`.razor` files in `src/CardGames.Poker.Web/Components`)
- CSS/styling consistency checks
- Accessibility (a11y) validation
- Responsive design patterns
- Design system alignment (colors, spacing, typography)
- Semantic HTML structure

## Capabilities
1. **Component Analysis:** Review Blazor components for structure, props, and composition patterns
2. **CSS Review:** Inspect styling for responsiveness, accessibility, and design consistency
3. **Accessibility Checks:** Validate WCAG 2.1 compliance, semantic markup, and assistive technology support
4. **Design System Validation:** Ensure components align with CardGames brand/design system
5. **Cross-browser Concerns:** Identify potential compatibility issues

## Activation
Once installed, activate the skill via:
```bash
/skills
# Select "web-design-reviewer" from the list
```

Or invoke directly in prompts:
```
@web-design-reviewer: Review the Login.razor component for accessibility compliance
```

## Configuration
- Default scope: Web UI components, styling, and design patterns
- Language: Blazor/C#, CSS/SCSS, HTML
- Integration: Works with Copilot CLI code review tools (`/review`, `/diff`)

## Relevant Project Paths
- Component library: `src/CardGames.Poker.Web/Components/`
- Account components: `src/CardGames.Poker.Web/Components/Account/`
- Shared layouts: `src/CardGames.Poker.Web/Components/Layout/`
- CSS/styling: `src/CardGames.Poker.Web/Components/**/*.css`

## Known Integration Points
- Works with Linus (Frontend) for UI component decisions
- Complements Rusty (Lead) design reviews
- Provides signal to Basher (Tester) for UI/a11y test coverage recommendations

---
**Status:** Ready for installation
**Last Updated:** 2026-02-18
