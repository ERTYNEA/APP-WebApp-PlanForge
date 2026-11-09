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

## v0.3.0 shared interactions and contextual feedback

The six approved brand tokens and canonical SVG are unchanged. Primary hover uses parchment #E1CCAA without a new decorative border; secondary hover changes both raised background and parchment border. Danger actions reuse Library's dedicated semantic colors rather than parchment. Disabled controls remain inert with no misleading hover. Noninteractive multi-link project cards do not imitate whole-card buttons. Inputs use #15191B normally and #202628 on focus with a parchment border and one composed visible indicator; dimensions remain stable. Chrome autofill styles preserve readable text. Checkbox/radio/file/disclosure retain their own native semantics; reduced motion and forced-color fallbacks remain in force.

Field errors live beside their control with invalid/describedby linkage. Short submit errors precede their action; page-load errors stay page-level. Dialog errors follow the explanation/confirmation and precede actions without a duplicate background alert. Profile/avatar/password, project/agent/token forms, plan/decision editors, CSV and Library title/link/unlink operations keep separate feedback. Failed saves retain nonsensitive input; cancellation clears obsolete feedback. Conditional modal removal restores focus to an available opener, current selected row or focusable main, not a detached element. Register places required new-password Confirm password immediately after Password; exact-match feedback is beneath that field. The BFF checks the same trust boundary without forwarding a second password.

Valid tokens render no residual list border when empty. History is a native disclosure with a visible keyboard focus and 16px separation. Cleanup sits right-aligned above history with 16–20px clearance, sharing the danger variant with Revoke. Historical rows page 10 at a time; centered Previous / Page X / Y / Next is absent for a single page. Local real-browser acceptance traversed 13 historical records and confirmed cleanup preserves the valid token.

## v0.4.0 inspector, controls and navigation

The Project inspector places Project settings beside Add phase. Phase adds Subphase, Subphase adds Step, and Step remains a structural leaf. Each level has a Resources section/Attach resources and Decisions section/Add decision. Resource provenance distinguishes Direct from Inherited from Project/Phase/Subphase, deduplicates files and permits scoped Unlink only for the current direct link. Decisions use the inspector surface with subtle separators, full title/badge/description/resolution and real Edit decision/Delete decision buttons.

Complete breadcrumbs include the current nonclickable aria-current node and clickable ancestors. Automatic parent states use “Status updates automatically as work progresses.”; modal badges precede that copy. Step selectors contain only Pending/InProgress/Completed, with a separate effective-blocker explanation. Position is one-based in forms and maps to zero-based SortOrder. Description replaces Purpose; modal textareas remain compact. Technical Revision is not displayed.

The Work plan header places Expand all, Collapse all, a neutral separator and Reload in that order. Global controls retain selection and deliberate collapse wins over polling. Tree-row and sidebar keyboard focus are composed on a single surface, not two nested rectangles. Shared danger-zone separators are neutral gray; destructive buttons/headings and errors retain their semantics. Projects cards have one Open project link, plain titles, no Settings query and no whole-card activation. Registration has one confirmation field, accurate 12–128 help beneath Password and the same label/input spacing as other fields; Login is unchanged.

Home and Projects have distinct route lifetimes with a shared template, eliminating the observed direct-pair polling delay without a loading animation. Actual repeated local/deployed browser timings, populated catalog checks, native keyboard, narrow reflow and bounded pointer-emulation evidence support acceptance; physical-device/screen-reader certification is not claimed.

## Plan explorer and distinct Home

Plan rows are compact true Phase→Subphase→Step hierarchy, with equal 24px expanders/leaf reservation, full title available despite ellipsis, light guides and existing text/color badges right-aligned. Hover and selection use the sidebar language; selection and keyboard focus remain distinct. Nested lists and buttons expose aria-expanded/controls and current selection without claiming an incomplete ARIA tree pattern. Decisions/resources/editors belong in the inspector or contextual dialogs, never nested card forms.

Desktop structure/inspector columns adapt around 35/65. One natural page scroll is mandatory: neither pane has fixed-height scrolling. Inspector sticky behavior is enabled only on wide viewports when its measured height fits; narrow/short/reflow equivalents stack in normal flow, with no Back to structure action. A single full escaped Description preserves paragraphs and expands in ordinary page flow; no Full description disclosure or duplicate header paragraph remains. Compact three-row editing textareas are intentionally separate from full read-only inspector text. Manual selection/expansion survives ordinary polling. Initial/restored/deep-link selection, successful CSV reset and selected-Step completion follow the policies in architecture/API; smooth scrolling respects reduced motion.

Home has exactly three indicators: Projects, literal Pending steps and weighted Overall progress. At most three eligible focus rows mix Pending/InProgress by immutable age, with actual hierarchy labels and Step links. All recently updated projects are reachable in 25-row pages. Projects remains the full catalog; All projects links target `/projects`. Real Chromium verification included desktop, 390px mobile and 720/360px short reflow-equivalent layouts, keyboard focus, long Unicode titles and 108 hierarchy rows; reflow equivalents are not a claim of literal zoom gestures or independent screen-reader certification. No product screenshots are included in README.