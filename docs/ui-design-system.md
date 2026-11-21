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

Raised surfaces (`#272C2E`), input backgrounds (`#15191B`) and decorative borders (`#3C4143`) stay near-neutral. Muted text uses `#ADB3B5`; control boundaries use `#878C8F`. Decorative separators are not substitutes for control boundaries. Work-state badges use amber Pending, blue In progress, red Blocked and green Completed/Decided, with the exact v0.7.0 table below. Other success/warning/error semantics retain their existing tokens; agent activity, Library assignment and toast kinds are not work-progress states. Selection also uses a visible primary border, not background color alone. `--pf-primary` replaces the old color-specific `--pf-cyan` name.

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

## v0.5.0 confirmation help and inspector dates

Registration retains `Use 12–128 characters.` under Password and adds exactly `Enter the same password again.` under Confirm password, inside the same label/input/help structure and spacing. Both fields retain native required/length/new-password behavior. Confirmation's normal `aria-describedby` names its help; mismatch adds the error ID without removing help, and valid input removes only the error reference. The original password alone reaches the API; Login and authentication policy do not change.

The right inspector places the unchanged effective-state badge beside a small decorative clock and `Since {browser-local date and time}` for Project, Phase, Subphase and Step. The row flex-wraps without adding dates to tree rows. A `time` element exposes the full ISO UTC instant, with visible local text rather than a tooltip-only value. Unknown legacy history displays `Status date unavailable`; workflow captions and `Recorded` completion evidence are not substitutes for this date. Long descriptions, actions, breadcrumbs, resources and decisions remain intact. No animation or palette change is introduced.

## v0.6.0 typed notifications and destructive controls

One shared toast family uses the same bottom-right placement, geometry, typography, padding, radius and stacking. Kinds are explicit in Web state, not inferred from message text:

| Kind | Background | Border | Accent | Decorative icon |
|---|---|---|---|---|
| Success | `#183329` | `#5DAD88` | `#75EFBE` | Circle with check |
| Neutral | `#34312B` | `#D1BC9A` | `#E1CCAA` | Circle with information mark |
| Danger | `#4C222E` | `#C87584` | `#FFABB5` | Circle with X, never a trash can |

Confirmed creation/save/upload/import/export uses Success. Delete, Remove, Unlink, Revoke and cleanup that actually deletes records use Danger, as do ownerless global operation failures. Informative remote project changes and zero-deletion cleanup results use Neutral; available destructive event metadata selects Danger and import/export selects Success. These are operation semantics, not a language-dependent keyword classifier. A known committed operation keeps its result if a follow-up refresh fails, accompanied by a truthful contextual refresh warning rather than a false failed-write claim. Field/modal errors stay beside their owner without a duplicate background toast; periodic read failures do not repeatedly announce operations.

Circuit-scoped state survives ordinary page navigation and is isolated between sessions. The deduplicated stack displays at most four notifications, each lasting six seconds. Layout and topmost modal hand off a single active host and a single `role="status"` atomic announcer without replaying announcements; individual toast visuals have no second live role and icons are aria-hidden. Text and icons accompany color, and reduced-motion overrides remain in force. This is implemented/tested accessible behavior, not independent screen-reader certification.

The existing `danger` button family is the destructive standard; legitimate compact sizing remains. Within a mixed action group the DOM and visual order is destructive left, ordinary right: Remove before Upload, Revoke before Renew, Delete before Edit/Add, Delete decision before Edit decision, and final confirmation before Cancel. Unlink uses `danger compact`; orphan cleanup precedes ordinary filter/view actions in its shared toolbar. Existing standalone danger zones and destruction-only groups are not rearranged unnecessarily. Clear filters, dismissal and navigation are not data deletion. Confirmation initially focuses Cancel, uses explicit button types and requires intent; Enter must not implicitly confirm destruction. Busy/disabled guards, ownership checks and existing confirmations remain mandatory.

## v0.6.0 human and agent avatars

Settings/Profile and Edit agent share `AvatarEditor`: a native PNG/JPEG/WebP file selector with `maximum 4 MiB`, optional current image, left `danger` Remove avatar and right unchanged `quiet` Upload avatar. The right-aligned action group retains that order on narrow layouts. Remove exists only with a current avatar; it adds no extra confirmation modal. Busy state disables the child controls and parent agent Save/Delete so overlapping writes cannot race through the form. Removing an avatar does not delete its user or agent.

New agent has the optional selector only, without independent Upload/Remove buttons. Save first persists the agent, then accepts its selected image through the avatar endpoint. If image acceptance fails after the agent saves, retain that same saved agent/editor with an explicit recoverable error; never imply rollback or create another agent on retry. Confirmed image acceptance produces one `Avatar updated` Success toast, not an additional generic agent-success toast for the same combined outcome.

Accepted originals are backend-persistent and visible immediately in the profile/editor, personal sidebar and agent list/editor surfaces that display avatars. Initials remain the no-image fallback; the release does not introduce portraits into unrelated initial-only surfaces. One green `Avatar updated` follows confirmed upload; one red `Avatar removed` follows confirmed removal and hides Remove. Validation/quota errors remain contextual and red, with no success announcement. The owner-wide temporary-original budget is 40 MiB across the human and all agents, separate from Library.

Visible avatar components check owning-profile metadata every two seconds while the document is visible, stop on disposal and use generation-versioned authenticated image URLs. A new image is preloaded before replacing the displayed one; transient reads retain the last confirmed image. Silent optimization changes the original to proportional WebP within 512×512 without F5, an optimization spinner, queue terminology or a second toast. A definitive third conversion failure silently returns the current generation to initials; neither failure nor replacement restores an old photo. No photo history/undo is offered, and no deletion claim extends to Azure-managed backups.

Real local browser checks cover ordinary forms, optional-image recovery, original persistence after fresh sign-in, silent sidebar/list/editor refresh, active replacement/removal, three-attempt failure, mobile controls, keyboard confirmation and reduced motion. Focused deployed checks cover controlled human and existing-agent upload/silent conversion/removal with no late toast or image-error event. These scopes do not imply physical-phone, literal browser-toolbar zoom or independent accessibility certification; see [acceptance](acceptance.md).

## Plan explorer and distinct Home

Plan rows are compact true Phase→Subphase→Step hierarchy, with equal 24px expanders/leaf reservation, full title available despite ellipsis, light guides and existing text/color badges right-aligned. Hover and selection use the sidebar language; selection and keyboard focus remain distinct. Nested lists and buttons expose aria-expanded/controls and current selection without claiming an incomplete ARIA tree pattern. Decisions/resources/editors belong in the inspector or contextual dialogs, never nested card forms.

Desktop structure/inspector columns adapt around 35/65. One natural page scroll is mandatory: neither pane has fixed-height scrolling. Inspector sticky behavior is enabled only on wide viewports when its measured height fits; narrow/short/reflow equivalents stack in normal flow, with no Back to structure action. A single full escaped Description preserves paragraphs and expands in ordinary page flow; no Full description disclosure or duplicate header paragraph remains. Compact three-row editing textareas are intentionally separate from full read-only inspector text. Manual selection/expansion survives ordinary polling. Initial/restored/deep-link selection, successful CSV reset and selected-Step completion follow the policies in architecture/API; smooth scrolling respects reduced motion.

Home has four equal-height category cards: Projects, Phases, Subphases and Steps. Each shows its total above a neutral divider and exactly four always-visible rows in this order: Pending, In progress, Blocked, Completed. Text accompanies decorative state dots; aligned counts use tabular numerals and zeros do not disappear. A container-query layout uses one column below 480px available width, two from 480px and four from 960px. The full-width Overall progress band below the cards now presents the four compact effective-state category compositions defined below. It does not average level percentages or change the existing API/business progress calculation. At most three eligible focus rows mix Pending/InProgress by immutable age, with actual hierarchy labels and Step links. All recently updated projects are reachable in 25-row pages. Projects remains the full catalog; All projects links target `/projects`. Real Chromium verification included desktop, 390px mobile and 720/360px short reflow-equivalent layouts, keyboard focus, long Unicode titles and 108 hierarchy rows; reflow equivalents are not a claim of literal zoom gestures or independent screen-reader certification. No product screenshots are included in README.

## v0.7.0 compact semantic hierarchy

### Creation and file-selection feedback

Home has no heading-level New project action and adds no substitute creation button. `/projects` retains its heading action, creation modal and empty-state Create your first project action. Metrics and the two-endpoint dashboard/summary loading pattern are unchanged.

CSV, Library and the shared Settings/Agents avatar editor clear only the previous unconfirmed upload/import attempt's error when another file is selected. Selection is not validation or success. Existing confirmed-refresh warnings, avatar-removal errors and unrelated agent-identity save errors survive selection. CSV/Library opening and closure reset the selected browser file and replacement/title state; no invisible old file can be submitted. Selectors and ordinary cancel actions disable during submission, and callbacks also guard Busy. The request snapshots its selected file/options before awaiting reads. Retired CSV requests cannot replace a new route or publish obsolete errors through the toast host.

A saved new agent whose optional avatar failed keeps its actual saved ID. Reselection clears only that avatar failure and retains a separate saved-identity notice; retry never creates another agent. Starting another identity save transfers error ownership away from the previous avatar attempt. Avatar MIME/size policy, the asynchronous backend queue, current-photo lifecycle and Library capabilities are unchanged.

### Exact state palette

`PlanLabels.StatusClass` keeps its existing mapping: Pending→warning, InProgress→active, Blocked→error, Completed/Decided→success. Only the work-state active badge changes from green to blue. Metric-card numbers and text remain neutral; only their decorative dots take these text colors.

| Effective state | Text / metric dot | Badge background | Badge border / bar segment |
|---|---|---|---|
| Pending | `#FFD080` | `#372B16` | `#BD944E` |
| In progress | `#9BCBFF` | `#1A2F42` | `#6590B8` |
| Blocked | `#FFABB5` | `#3C1F2A` | `#C87584` |
| Completed / Decided | `#75EFBE` | `#183329` | `#5DAD88` |

The general brand, danger controls, Success/Neutral/Danger notifications, agent Active/Inactive and Library Assigned/Orphan badges retain their previous semantics and styling. Text labels accompany every color.

### Shared dynamic `StatusProgress`

Home's Overall progress has four equal columns: Projects, Phases, Subphases, Steps. Project detail first shows its real project badge and workflow, then a neutral divider and exactly three columns: Phases, Subphases, Steps. Each column has a left title, right `completed/total · N%`, an 8px continuous rounded composition bar and two legend rows: Pending | In progress, then Blocked | Completed. Counts stay neutral white; legend labels use the state text colors. There is no nested decorative panel or additional Projects column in project detail. Catalog project cards keep their original step-progress bar.

Home uses the existing four `WorkspaceDashboard.StatusCounts` values, without extra API calls or tree reads. Detail counts exact structural Kind and EffectiveStatus across the complete snapshot, independently of expansion; Decisions never enter the denominators. Integer percentages round to the nearest whole number with midpoint away from zero: 10/28→36%, 12/32→38%. Zero totals show `0/0 · 0%`, four zeros and a neutral empty bar. Ordered integer flex weights make positive segments sum to the full available width without locale-dependent decimal CSS or visible gaps. Accessible category labels, the explicit ratio and named/count legends supplement the composition's `role="img"` summary.

These are completed proportions within each level, not a new claim of project completion. The existing decision/blocking and 99%-cap business rules are unchanged: a root Decision can leave the project Blocked while all Steps, and even all three level proportions, read 100%.

The progress surfaces are inline-size containers: Home changes 1→2→4 columns at 440/880px available content width; detail changes 1→2→3 at 440/680px. Columns use `min-width:0`; labels wrap rather than silently disappearing. The metric cards retain their separate 480/960px 1→2→4 breakpoints. At 960px measured panel width, actual Home/detail heights are approximately 151/155px versus the approved 150/157px reference compositions. Desktop, 390px and 320px viewports retain all information without horizontal overflow. Reflow and reduced-motion checks do not imply physical-phone, literal browser-toolbar zoom or independent accessibility certification.

### Frozen inline icon geometry

`AppIcon` emits the approved SVG nodes and attributes literally, with `currentColor`, decorative `aria-hidden="true"` and `focusable="false"`. Eighteen variants cover:

| Location | Geometry / represented size |
|---|---|
| Sidebar Home / Projects / Library / Agents | house / folder-kanban / library / bot, each 14px |
| Project card / tree root / empty Projects | folder-kanban, 20 / 16 / 36px |
| Library resource card | file-text, 20px |
| Phase / Subphase / Step | stacked pages / one page / task list, 16px |
| Known status date | clock-3, 14px |
| Expand all / Collapse all | chevrons-up-down outward / chevrons-down-up inward, 16px |
| Refresh or Reload / Download | refresh-cw / download, 16px |
| Collapsed / expanded tree branch | chevron-right / chevron-down, 18px |

The card icon boxes remain 35×35px, expanders 24×30px and node-kind reservation 16px. Inline icons inherit active/hover text color; no external-image currentColor assumption, icon dependency or runtime fetching is introduced. The Unicode profile `⚙` remains the same glyph at 20px, line-height 1, with a true 16px reserved flex width and no larger account-link/avatar geometry; `min-width:0` prevents intrinsic glyph expansion. Long-name checks cover 236/204/240px sidebars. The official favicon/logo, toast/alert artwork, native input controls, mobile menu, modal close and conventional navigation arrows are unchanged. See [third-party notices](../THIRD-PARTY-NOTICES.md).
