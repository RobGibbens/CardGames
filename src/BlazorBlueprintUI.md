# Blazor Blueprint UI Audit

Date: 2026-04-08

## Scope

This audit reviews the UI surface in `CardGames.Poker.Web` and identifies where the app can move from hand-built Blazor/HTML/CSS patterns to Blazor Blueprint components and blueprints.

The focus is on replacement opportunities, not a full redesign. The goal is to standardize common UI primitives while preserving the custom poker-table experience where that custom work is part of the product.

## Executive Summary

Blazor Blueprint is already installed and partially wired into the app:

- `BlazorBlueprint.Components` is referenced in `CardGames.Poker.Web.csproj`.
- `AddBlazorBlueprintComponents()` is already called in `Program.cs`.
- `BlazorBlueprint.Components` is already imported in `Components/_Imports.razor`.
- `BbPortalHost` is already present in `Components/Layout/MainLayout.razor` and `Components/Account/Shared/ManageLayout.razor`.
- Tailwind-driven theme tokens already exist in `wwwroot/css/theme.css`.

That means the project is not blocked on setup. This is an adoption problem, not an integration problem.

The current UI is still overwhelmingly hand-built:

| Metric | Value | Notes |
| --- | ---: | --- |
| Raw or standard form controls | 97 | `InputText`, `InputNumber`, `InputDate`, `InputSelect`, `InputCheckbox`, `EditForm`, raw `select` |
| Blazor Blueprint component usages | 6 | Mostly `BbButton`, `BbTypography*`, and `BbPortalHost` |
| Overlay / modal / dialog components | 18 | Strong candidates for `BbDialog`, `BbAlertDialog`, `BbSheet`, `BbDrawer` |
| Razor pages in `Components/Pages` | 18 | Main application surface |
| Razor shared components in `Components/Shared` | 33 | Reusable migration seam |

The highest-value migration targets are:

1. Navigation, menus, dropdowns, and account shell
2. Dialogs, confirm flows, approval flows, and event editors
3. Forms, filters, tabs, and list/grid data views
4. Account and auth pages using Blueprint blueprints
5. League management pages and reusable dashboard panels

The lowest-value migration target is the poker table canvas itself. That should remain bespoke.

## Readiness Assessment

### What is already ready

- Overlay infrastructure is ready because `BbPortalHost` is already mounted.
- Namespace imports are already in place for Blueprint components.
- Theming is already Tailwind-based, so Blueprint styling fits the current stack.
- The app already uses Tailwind tokens and custom variables, which is compatible with Blueprint customization.

### What is currently missing

- Consistent adoption of Blueprint primitives for common interaction patterns
- Shared form field abstractions around validation and field layout
- Shared dialog primitives replacing custom overlay markup
- Shared data presentation primitives replacing hand-built cards, tables, tabs, badges, and empty states

### Practical conclusion

You can start replacing hand-built UI immediately without any setup phase.

## Recommended Migration Principles

1. Replace repeated primitives first, not pages first.
2. Start with shared components that affect many screens.
3. Keep custom poker-table and game-state visuals bespoke.
4. Use Blueprint for accessibility-heavy interactions: dialogs, dropdowns, tabs, switches, popovers, tooltips.
5. Use Blueprint forms inside `EditForm` with `ValueExpression` where validation is needed.
6. Prefer `BbNativeSelect` for simple browser-like selects and `BbSelect` when richer interaction is worth it.
7. Use Blueprint blueprints as source material for auth, shell, sidebar, and admin surfaces rather than building those from scratch again.

## Component Mapping Cheat Sheet

| Current pattern | Blueprint replacement |
| --- | --- |
| Hand-built `button.btn...` | `BbButton` |
| Raw `select` | `BbNativeSelect` or `BbSelect` |
| `InputText`, `InputNumber`, `InputDate`, `InputSelect` | `BbInput`, `BbNumericInput`, `BbDatePicker`, `BbSelect`, or `BbFormField*` variants |
| Custom toggle switch buttons | `BbSwitch` or `BbToggleGroup` |
| Custom tab bars | `BbTabs` |
| Hand-built badges / pills | `BbBadge` |
| Hand-built cards | `BbCard` |
| Hand-built loading blocks | `BbSpinner` or `BbSkeleton` |
| Hand-built empty states | `BbEmpty` |
| Hand-built dropdown menus | `BbDropdownMenu`, `BbMenubar`, or `BbNavigationMenu` |
| Hand-built confirm modals | `BbAlertDialog` |
| Hand-built modals / editors | `BbDialog`, `BbSheet`, `BbDrawer` |
| Hand-built lists / tables | `BbDataTable`, `BbDataView`, `BbScrollArea`, `BbPagination` |
| Hand-built accordions / expanders | `BbAccordion` or `BbCollapsible` |
| Hand-built avatars | `BbAvatar` |
| Hand-built status or error banners | `BbAlert` |
| Ad hoc feedback after actions | `BbToast` |

## Priority 1: High-Value, Low-to-Medium Risk

### 1. Shared dialogs and overlays

Representative files:

- `CardGames.Poker.Web/Components/Shared/ConfirmDialog.razor`
- `CardGames.Poker.Web/Components/Shared/NoChipsModal.razor`
- `CardGames.Poker.Web/Components/Shared/JoinApprovalModal.razor`
- `CardGames.Poker.Web/Components/Pages/LeagueDetailTabs/CreateLeagueEventModal.razor`
- `CardGames.Poker.Web/Components/Pages/Leagues.razor`

Current state:

- Repeated manual overlay markup using `confirm-dialog-overlay`
- Manual focus and dismissal behavior
- Repeated button/footer/header structures
- Repeated form markup inside modal bodies

Recommended Blueprint replacements:

- `BbAlertDialog` for destructive confirms
- `BbDialog` for general modal workflows
- `BbSheet` for larger forms that feel cramped in a centered modal
- `BbScrollArea` for long modal content
- `BbInput`, `BbNumericInput`, `BbDatePicker`, `BbSelect` inside modal forms
- `BbToast` for success/error follow-up feedback after submit

Why this should be first:

- The pattern repeats across leagues, join approval, deletion, and join blockers.
- Accessibility and keyboard behavior improve immediately.
- The migration can happen behind existing component APIs, minimizing page churn.

Concrete recommendations:

- Replace `ConfirmDialog` with a shared Blueprint-backed wrapper around `BbAlertDialog`.
- Replace `NoChipsModal` with either `BbDialog` or `BbAlertDialog` depending on whether the CTA remains mandatory.
- Replace `JoinApprovalModal` with a `BbDialog` containing `BbScrollArea`, `BbNumericInput`, and `BbButton` actions.
- Replace the event editor modals in league detail with `BbDialog` or `BbSheet`; the current form is already a textbook Blueprint form candidate.
- Replace the inline create/join league modals in `Leagues.razor` with the same shared dialog abstraction rather than keeping two more custom modal implementations.

### 2. Navigation and shell

Representative files:

- `CardGames.Poker.Web/Components/Shared/AuthenticatedNavBar.razor`
- `CardGames.Poker.Web/Components/Shared/SiteNavBar.razor`
- `CardGames.Poker.Web/Components/Account/Shared/ManageLayout.razor`
- `CardGames.Poker.Web/Components/Account/AccountShell.razor`

Current state:

- Hand-built top nav with manual mobile menu state
- Hand-built user dropdown
- Repeated avatar and account action patterns
- Custom manage layout that is structurally similar to a sidebar shell

Recommended Blueprint replacements:

- `BbResponsiveNav`
- `BbNavigationMenu`
- `BbDropdownMenu`
- `BbAvatar`
- `BbSidebar` for account management and potentially authenticated app shell
- `BbSeparator`
- `BbBadge` for counts or role labels

Why this should be first:

- Nav and shell patterns are global and visually repetitive.
- Mobile and keyboard behavior are easy places for regressions in custom UI.
- Blueprint already has strong coverage here.

Concrete recommendations:

- Move both nav bars to Blueprint nav primitives and keep only brand-specific styling custom.
- Replace the hand-built user-menu dropdown with `BbDropdownMenu`.
- Convert the account manage layout to a sidebar-based shell, likely starting from `sidebar-05` or `sidebar-08`.
- Keep the theme toggle logic, but render it through Blueprint button or dropdown primitives.

### 3. Lobby filters, tabs, table list, and cards

Representative files:

- `CardGames.Poker.Web/Components/Pages/Lobby.razor`
- `CardGames.Poker.Web/Components/Shared/TableCard.razor`

Current state:

- Hand-built tabs
- Raw `select` filters
- Hand-built badge counts
- Mixed grid/list presentation
- Hand-built card composition for tables
- Hand-built empty and loading states
- Partial Blueprint usage only in list-mode buttons and typography

Recommended Blueprint replacements:

- `BbTabs`
- `BbNativeSelect` or `BbSelect`
- `BbToggleGroup` for grid/list switching
- `BbBadge`
- `BbCard`
- `BbDataView` for grid/list switching if you want a more unified abstraction
- `BbDataTable` for list mode
- `BbEmpty`
- `BbSpinner` or `BbSkeleton`
- `BbAlertDialog` for delete confirmation flow

Why this should be first:

- `Lobby.razor` is one of the most visible pages.
- It already has partial Blueprint adoption, so the path is obvious.
- The page has repeated admin-style UI patterns that Blueprint handles well.

Concrete recommendations:

- Replace `lobby-tabs` with `BbTabs`.
- Replace the three filter `select` controls with `BbNativeSelect` first; upgrade to `BbSelect` only if richer search or richer rendering is needed.
- Replace the grid/list toggle buttons with `BbToggleGroup`.
- Rebuild `TableCard.razor` as a `BbCard` with `BbBadge`, Blueprint buttons, and consistent action layout.
- Replace the hand-built empty/loading sections with `BbEmpty` and `BbSpinner`.
- Replace the raw HTML table used in list view with `BbDataTable` if you want sorting, consistent column structure, and stronger accessibility.

## Priority 2: Medium Effort, High Payoff

### 4. Create and edit table flows

Representative files:

- `CardGames.Poker.Web/Components/Pages/CreateTable.razor`
- `CardGames.Poker.Web/Components/Pages/EditTable.razor`

Current state:

- Large amounts of hand-built form structure
- Repeated labels, hints, validation text, and inline loading indicators
- Hand-built variant search and grid/list selection
- Hand-built toggle switches for host approval and odds visibility
- Hand-built step layout that behaves like a wizard

Recommended Blueprint replacements:

- `BbCard`
- `BbFormSection`
- `BbFormWizard` if you want to formalize the step experience
- `BbInput`
- `BbNumericInput`
- `BbSwitch`
- `BbSelect` or `BbCombobox`
- `BbCommand` for variant search if you want command-palette-style search
- `BbBadge` for variant metadata
- `BbTooltip` for rule hints and generated name behavior
- `BbAlert` for load/create/save errors
- `BbSpinner` or `BbSkeleton` for load state
- `BbEmpty` for unavailable variants or empty favorites

Why this is worth doing:

- These pages contain some of the highest concentration of hand-built form UI in the app.
- This is where Blueprint form consistency will save the most maintenance cost.
- The current form patterns are reusable for league events and future game configuration flows.

Concrete recommendations:

- Introduce a shared Blueprint form-field style for labels, hints, validation, and actions.
- Replace hand-built ON/OFF switches with `BbSwitch`.
- Consider a `BbCombobox` or `BbCommand` for variant search instead of a plain text filter.
- Use `BbFormWizard` only if you want explicit step semantics; otherwise `BbCard` plus `BbFormSection` is probably enough.
- Keep the actual variant imagery and game-specific copy custom, but render the chrome with Blueprint cards and badges.

### 5. League admin and league detail pages

Representative files:

- `CardGames.Poker.Web/Components/Pages/Leagues.razor`
- `CardGames.Poker.Web/Components/Pages/LeagueDetail.razor`
- `CardGames.Poker.Web/Components/Pages/JoinLeague.razor`
- `CardGames.Poker.Web/Components/Pages/LeagueDetailTabs/*.razor`

Current state:

- Hand-built cards, stat pills, modals, tabs, dropdowns, lists, alerts, and metadata blocks
- Manual tab implementation in league detail
- Manual create dropdown in league detail header
- Mixed Bootstrap-style and custom CSS patterns

Recommended Blueprint replacements:

- `BbTabs`
- `BbCard`
- `BbBadge`
- `BbAvatar`
- `BbDropdownMenu`
- `BbDialog`
- `BbDatePicker`
- `BbSelect`
- `BbDataTable`
- `BbPagination`
- `BbAlert`
- `BbEmpty`
- `BbButtonGroup` where grouped actions make sense

Why this is worth doing:

- League pages are admin dashboards in disguise, and Blueprint is strongest in exactly that UI category.
- The current code mixes several design languages, which increases maintenance cost.
- The page cluster is large enough that a shared admin pattern will compound quickly.

Concrete recommendations:

- Replace the league detail tab bar with `BbTabs`.
- Replace the create dropdown in `LeagueDetail.razor` with `BbDropdownMenu`.
- Replace stat pills and role pills with `BbBadge` variants.
- Convert member lists and invite lists to `BbDataTable` or at least Blueprint cards plus `BbScrollArea`.
- Convert the join league trust preview page to a centered `BbCard` with `BbAlert` and Blueprint actions.

### 6. Account and auth flows

Representative files:

- `CardGames.Poker.Web/Components/Account/Pages/Login.razor`
- `CardGames.Poker.Web/Components/Account/Pages/Register.razor`
- `CardGames.Poker.Web/Components/Account/Pages/ForgotPassword.razor`
- `CardGames.Poker.Web/Components/Account/Pages/LoginWith2fa.razor`
- `CardGames.Poker.Web/Components/Account/Pages/LoginWithRecoveryCode.razor`
- `CardGames.Poker.Web/Components/Account/Pages/Manage/*.razor`
- `CardGames.Poker.Web/Components/Account/AccountShell.razor`
- `CardGames.Poker.Web/Components/Account/Shared/ManageLayout.razor`

Current state:

- Custom auth shell with standard Blazor input controls
- A strong candidate for copy-paste Blueprint blueprints
- Manage area behaves like a dashboard/sidebar shell

Recommended Blueprint replacements:

- `login-01`, `login-06`, `login-07`, `signup-01`, `signup-05`, `login-08` blueprints
- `BbCard`
- `BbInput`
- `BbCheckbox`
- `BbButton`
- `BbSeparator`
- `BbInputOtp`
- `BbSidebar`
- `BbNavigationMenu`
- `BbAlert`

Why this is worth doing:

- Auth pages are highly standardized and low-risk to move onto Blueprint.
- Blueprint already provides blueprints for nearly every auth page pattern you have.
- The account manage area would benefit from a sidebar or structured shell immediately.

Concrete recommendations:

- Rework `AccountShell.razor` around a Blueprint login/signup blueprint rather than keeping a custom shell.
- Replace `Login.razor` inputs and buttons with Blueprint components first, then consider a blueprint-based visual rewrite.
- Use the OTP blueprint pattern for 2FA and recovery code pages.
- Convert the manage area to a sidebar shell using a Sidebar blueprint.

## Priority 3: Good Candidates, But More Optional

### 7. Home / marketing page

Representative file:

- `CardGames.Poker.Web/Components/Pages/Home.razor`

Current state:

- Entirely hand-built landing page sections
- Custom feature cards, stats, steps, CTA, and variant grid
- Strong visual identity already present

Recommended Blueprint replacements:

- `BbResponsiveNav`
- `BbCard`
- `BbBadge`
- `BbButton`
- `BbSeparator`
- `BbTypography`
- Marketing or dashboard blueprints as inspiration rather than direct copy

Recommendation:

Treat this as optional. Keep the current visual identity, but move the repeated primitives to Blueprint where it reduces maintenance. This page is not the first place I would spend migration effort.

### 8. Secondary in-game panels and histories

Representative files:

- `CardGames.Poker.Web/Components/Shared/HandHistorySection.razor`
- `CardGames.Poker.Web/Components/Shared/DashboardPanel.razor`
- `CardGames.Poker.Web/Components/Shared/LeaderboardSection.razor`
- `CardGames.Poker.Web/Components/Shared/OddsSection.razor`
- `CardGames.Poker.Web/Components/Shared/HandOddsPanel.razor`

Current state:

- Custom panels, cards, expandable lists, status sections, and history views
- Likely repeated card and section patterns

Recommended Blueprint replacements:

- `BbCard`
- `BbAccordion` or `BbCollapsible`
- `BbScrollArea`
- `BbBadge`
- `BbProgress`
- `BbTooltip`
- `BbSeparator`
- `BbEmpty`

Recommendation:

These are worth migrating once the shared dialog/nav/form primitives exist. `HandHistorySection.razor` is a particularly clean fit for `BbAccordion` plus `BbScrollArea`.

## Keep Custom: Do Not Force Blueprint Here

### 1. Poker table canvas and seat layout

Representative files:

- `CardGames.Poker.Web/Components/Pages/TablePlay.razor`
- `CardGames.Poker.Web/Components/Shared/TableCanvas.razor`
- `CardGames.Poker.Web/Components/Shared/TableSeat.razor`

Why to keep custom:

- These files are spatial, animated, game-specific interfaces.
- Blueprint does not add much value to the felt layout, card placement, deck visualization, seat positioning, or game-state choreography.
- Converting these would risk flattening the gameplay experience into generic admin UI.

What to adopt anyway around them:

- Use Blueprint for surrounding drawers, dialogs, side panels, toasts, badges, tabs, and secondary data presentation.
- Do not migrate the felt/table rendering itself.

### 2. Highly branded decorative sections

Representative file:

- `CardGames.Poker.Web/Components/Pages/Home.razor`

Why to keep partly custom:

- The landing page brand treatment is part of the product personality.
- Replace primitives, not the visual concept.

## Suggested Blueprint Sources to Reuse

These Blueprint blueprints are the best matches for the current app:

| App area | Best Blueprint starting points |
| --- | --- |
| Login and registration | `login-01`, `login-08`, `signup-01`, `signup-05`, `login-06`, `login-07` |
| Account management shell | `sidebar-05`, `sidebar-08` |
| Lobby filters and results | `app-flights`, `app-dashboard` |
| League admin area | `app-tasks`, `app-dashboard` |
| Create/edit flows | `form-02`, `form-03`, `form-05` |

## Proposed Migration Order

### Phase 1: Shared primitives

Build or replace the shared pieces first:

- Blueprint-backed confirm dialog
- Blueprint-backed standard dialog / sheet
- Blueprint-backed nav/dropdown/avatar primitives
- Shared Blueprint form field wrappers
- Shared Blueprint empty/loading/error blocks

Expected result:

- All later page migrations get cheaper.

### Phase 2: Most visible page improvements

Apply the shared primitives to:

- Lobby
- Table cards
- Leagues page
- League detail tab bar and dropdown actions
- Join league page

Expected result:

- Immediate visible consistency across the authenticated app.

### Phase 3: Form modernization

Migrate:

- Create table
- Edit table
- League event editors
- Create/join league forms
- Account auth forms

Expected result:

- Lower maintenance cost and more consistent validation and field layout.

### Phase 4: Shell and account experience

Migrate:

- Authenticated nav
- Public nav
- Account shell
- Manage layout

Expected result:

- Strong structural consistency and better mobile behavior.

### Phase 5: Opportunistic cleanup

Migrate:

- Home page primitives
- Hand history and dashboard subsections
- Secondary overlays and panels around gameplay

## Implementation Notes

### Form integration

When migrating Blueprint input components inside `EditForm`, use `ValueExpression` where validation is required. Blueprint supports standard Blazor validation patterns, but you need to wire them intentionally.

### Overlay behavior

Because `BbPortalHost` is already mounted, you should prefer Blueprint overlays over custom fixed-position modal markup. That reduces z-index and clipping issues.

### Controlled state

Blueprint tabs, dialogs, selects, and similar components support standard `Value` / `ValueChanged` or `Open` / `OpenChanged` patterns. Use controlled state when you need routing, persistence, or side effects.

### Select strategy

Use this rule:

- `BbNativeSelect` when you want simple browser select behavior and minimal migration risk
- `BbSelect` when you want richer presentation or custom item content
- `BbCombobox` or `BbCommand` when search is part of the experience

## Final Recommendation

You should not attempt a page-by-page redesign first.

The best path is:

1. Introduce Blueprint-backed shared wrappers for dialogs, nav, form fields, empty/loading states, and badges.
2. Apply those wrappers to `Lobby`, `Leagues`, `LeagueDetail`, `CreateTable`, and `EditTable`.
3. Move auth and account pages onto existing Blueprint blueprints.
4. Leave the poker table canvas and seat layout custom.

If you follow that order, you will get the largest consistency gain with the lowest risk of disrupting the parts of the app that are intentionally unique.
