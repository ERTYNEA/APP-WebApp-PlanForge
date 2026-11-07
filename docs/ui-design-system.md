# UI design system

Functional Blazor Interactive Server dark developer control panel, not a static mockup. Visual acceptance checks readable title geometry as well as absence of horizontal overflow; narrow plans place metadata beneath the full-width title.

## Color tokens

`wwwroot/app.css` is the token source. Approved v0.2.0 values:

| Purpose | CSS token | Value |
|---|---|---|
| Main background | `--pf-bg` | `#111416` |
| Panels | `--pf-surface` | `#1D2123` |
| Primary action / brand | `--pf-primary` | `#D1BC9A` |
| Primary hover / focus | `--pf-primary-hover` | `#E1CCAA` |
| Selection | `--pf-selection` | `#34312B` |
| Main text | `--pf-text` | `#E7E8E4` |

Raised surfaces (`#272C2E`), input backgrounds (`#15191B`) and decorative borders (`#3C4143`) stay near-neutral. Muted text uses `#ADB3B5`; control boundaries use `#878C8F`. Decorative separators are not substitutes for control boundaries. Green success/active (`#75EFBE`), amber pending/warning (`#FFD080`) and red error/destructive (`#FFABB5`) retain dedicated semantic backgrounds and borders. Selection also uses a visible primary border, not background color alone. `--pf-primary` replaces the old color-specific `--pf-cyan` name.

Node regression tests resolve the actual CSS tokens and check text at 4.5:1 or better and control/focus contrast at 3:1 or better. Measured panel contrast: main text 13.18:1, muted text 7.64:1, primary links 8.79:1, input boundary 4.77:1 and focus 10.37:1. Primary-button text is 10.01:1 (hover 11.82:1); placeholder/select-arrow contrast against the input background is 8.34:1. Across background, panel, raised, input and selected surfaces, the lowest tested normal-text ratio is 6.11:1 and the lowest control-boundary ratio is 3.81:1. Disabled controls remain visually distinct; reduced-motion overrides remain in place.

## Canonical symbol

`wwwroot/favicon.svg` is the single canonical static corporate symbol, byte-for-byte the approved 128×128 SVG. The favicon, sidebar and authentication wordmarks consume this same file as an image with `.brand-mark`, beside the text “PlanForge”. The CSS image box is 34×34px with no colored tile or reconstructed letter. Do not duplicate the path in inline SVG or another asset. This trusted static asset does not permit arbitrary user SVG uploads.

## Account navigation and contextual location

The bottom avatar/name/Personal workspace/gear row is one accessible NavLink to `/settings`, the only visible account-settings navigation. Normal state has a reserved transparent 1px border, 11px/14px inner padding and 8px gaps; this keeps the whole subtitle readable at the fixed sidebar width. Hover uses the same neutral raised surface as the main menu without underlining any child. Active state uses selection plus primary border, with the same dimensions and ample avatar/gear clearance. Focus remains visibly outlined; Sign out is a separate antiforgery-protected form.

Breadcrumbs use semantic nav/ol/current-page markup and actual routes. Workspace links Home; Projects links the real collection; project names come from the already-loaded snapshot, remain Unicode/user-authored and truncate visually with their full title available. Account / Settings has no invented Account landing route. The existing Project Settings modal adds its real contextual level and restores the project level on closure. Compact viewports wrap or ellipsize without global overflow.

English resource-backed labels and the per-account Language selector preserve native browser form/file-picker behavior. Valid credentials are distinguished as Personal token or Agent token; metadata rows are bounded to25/page, and revoked/expired history starts as a native collapsed disclosure with10/page. Secret issuance dialogs are not normal metadata, and a confirmed manual cleanup is separate from per-token revocation.

## Native selects

Single-selection HTML controls use only a decorative 16px SVG chevron: `--pf-select-inset: 16px`, `--pf-select-arrow-size: 16px` and `--pf-select-text-gap: 16px`. The right-aligned arrow and 48px reserved text padding apply globally, including filter and form selects. The real `select`, native popup, keyboard interaction and validation remain unchanged. Multiple/size-based listboxes are excluded; `size="1"` is included. Forced-colors mode restores the browser arrow and removes the decorative image.

The Node suite verifies static CSS and canonical SVG bytes, not rendered geometry or browser interaction. Recheck desktop/mobile selects, keyboard focus, reduced motion and the logo at full, 24px, 16px and favicon sizes in the real application before visual acceptance.

System sans for text; system monospace only technical identifiers/events. Spacing 4/8/12/16/24px, radius 8–12px, restrained borders/shadows, compact symbolic icons with semantic text labels. Text labels supplement color, visible keyboard focus and WCAG AA contrast. Reduced motion disables nonessential 200–400ms transitions.

Desktop fixed sidebar: Home, Projects, Library and Agents, followed by the single bottom account-settings profile surface, separate Sign out and central product version. Mobile uses collapsible navigation and a single-column plan without global horizontal scrolling. The topbar contains contextual breadcrumbs and the mobile navigation toggle; existing project Settings/actions remain in their real page. No fictional teams, contextual-search screen, agent online indicators or token metrics.

Work Plan is the core: Phase → Subphase → Step; active branch expanded, completed/future collapsed initially, manual expansion retained for the session. Pending decisions amber warning plus label; decided green check plus label. Assigned resources distinct from zero-link Unassigned resources, including accessible labels.

Review actual login, empty/populated Home, expanded/collapsed plan, Assigned/Orphan Library and narrow viewport before declaring visual acceptance.