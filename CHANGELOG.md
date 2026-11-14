# Changelog

All published release notes are retained here, newest first. The root README introduces only the latest stable release. Historical entries describe their original verification scope, not the current test counts.

## [0.5.0] — 2026-11-14

### Added

- REG-02: registration now shows exactly `Enter the same password again.` beneath Confirm password, matching the existing help spacing. Normal help and mismatch errors remain simultaneously accessible; native constraints, exact confirmation, the 12–128-character policy and Login are unchanged. No second password is persisted or forwarded.
- PLAN-04: persistent server-assigned UTC entry dates for the current effective Project/Phase/Subphase/Step state, displayed beside the inspector badge in browser-local time. Real transitions—including blocking, restoration and reversal—receive a new instant; irrelevant edits, hidden base changes under Blocked and unchanged retained-ID snapshots/CSV preserve exact dates. Historical unknowns remain null and display `Status date unavailable`; UpdatedAt, CompletedAt and immutable creation age retain their original meanings.

### Changed

- HOME-01: four equal-height Projects/Phases/Subphases/Steps cards show totals and fixed Pending/In progress/Blocked/Completed breakdowns, including zeros. Three owner-filtered Serializable SQL queries supply coherent effective-state aggregates. Available-container 4/2/1 layouts and a separate full-width weighted Overall progress band preserve Next in focus, Recently updated and Projects behavior.

### Publication and verification

Functional source `fbd213e28ffbbe7cddc1c01b2b6e93f86d949e4f` is served by API and Web as 0.5.0, published through unchanged run 43561511298. Tests → tenth additive SQL migration → API succeeded; one Web preflight read timeout before publication was resolved by a single Web-only failed-job rerun, without changing workflows, secrets or Azure configuration. The migration adds two nullable datetimeoffset columns without fabricated legacy dates or destructive changes.

Verification includes 506 passing .NET tests (10 Domain / 259 Infrastructure / 48 API / 189 Web), 23 Node tests and 14 pipeline tests, real isolated local Blazor/handler-adapter browser QA, 2000-Step relational query-count checks, revision/rollback/CSV/security regressions and focused postpublication HTTPS browser/API checks. No isolated live SQL Server test environment was accessible: provider SQL/model checks, actual SQLite legacy expansion, real Actions production migration and public SQL-backed HTTP behavior are distinct evidence, not a fabricated prepublication SQL Server run. Existing QA sessions/cookies and exact resource bytes survived observed worker/Web replacement. Only operation-owned fixtures and new temporary sessions were cleaned up; local QA servers/process groups and ports were verified closed, with Hermes/Codex intact. No product screenshots or independent physical-device/accessibility/PITR certification are claimed.

## [0.4.0] — 2026-11-09

### Added

- Pending Decisions at the real Project root, Phase, Subphase or Step now derive effective Blocked upward without blocking innocent descendants or siblings. Steps retain their underlying work state and valid closure evidence; resolving or deleting the last cause restores that state. Manual Step Blocked is rejected consistently by Web, REST and CSV.
- Reusable resources can be attached directly at all four plan levels and inherited downward without copying files or links. Effective lists deduplicate by resource ID, show direct/inherited origins and permit scoped unlinking only of the current direct association. ZIP manifest schema 2 retains original typed associations and includes each binary once.
- Expand all and Collapse all beside Reload, complete current-node inspector breadcrumbs, full single descriptions and integrated Decisions/Resources actions at every level. Compact editors use clear one-based Position, specific action labels and consistent calculated-state badges and explanations.

### Changed

- Equal-age Steps now follow actual hierarchy positions before identifiers. Intentional Web CSV imports reselect eligible work with reduced-motion-aware reveal, while passive API updates preserve selection, deliberate collapse, focus and scroll; deletion falls back to a surviving ancestor.
- Distinct Home/Projects route lifetimes remove the measured direct-pair polling delay without adding a spinner or skeleton. Projects cards have one query-free Open project link and plain titles. Sidebar/tree focus uses one composed contour, destructive-zone separators are neutral, and registration places accurate password-length help under Password without adding another confirmation field or changing Login.

### Publication and verification

Functional source `5c4cab6fc2bee5acd3111991391d05050d9823e8` was published through successful ordered run 42570455512: Tests → SQL → API → Web. Both deployed components report 0.4.0 plus that exact source. The ninth migration preserves Step base state and reconciles effective blocking with a transactional version04 revision-writer fence; protected accounts, agents and original credentials remain intact. Verification includes 486 passing .NET tests, 19 Node tests, 14 pipeline tests, generated SQL Server cases, real local/published Chromium, 64 deployed API behavior assertions and additional resource-concurrency/orphan checks. Prepublication human sessions, the protected Web cookie and exact resource bytes survive observed process replacement. Only exact controlled fixtures and newly authorized temporary sessions were cleaned up. Native-touch protocol attempts are not physical-device certification; see the documented accessibility and runtime limits. No README screenshots are included.

## [0.3.0] — 2026-11-08

### Added

- A compact hierarchical plan explorer with a contextual inspector, preserved work/decision/resource/assignment/evidence actions, stable session selection, authorized Step deep links and one natural page scroll. Successful CSV imports and selected-Step completion reveal the next eligible work without disrupting ordinary exploration or polling.
- A distinct operational Home with project and literal Pending-Step counts, weighted overall progress, up to three eligible Steps mixed strictly by creation age, and paginated access to every recently updated project. Projects retains its complete searchable/filterable/sortable catalog.
- Required exact password confirmation during registration, checked in the browser and authoritative Web BFF without persisting or forwarding a second secret.

### Changed

- Project, Phase and Subphase statuses are calculated recursively from descendants and scoped pending decisions; only Steps retain manual status. Empty parents remain Pending, real Blocked work has priority, and existing workflow evidence/human-approval rules remain enforced. Compatible legacy parent-status input is validated but recalculated.
- Server-owned immutable creation order survives edit/move/reorder and retained-ID CSV import. Historical negative ranks are deterministic reconstruction, not invented creation dates; new items receive their real creation-event order. The public CSV schema and stable-ID/revision contract are unchanged.
- Shared hover/focus/disabled and destructive actions, contextual accessible form/dialog errors and focus restoration are consistent across the application. Token history has no empty-list border, clear disclosure/cleanup spacing and centered conditional pagination. The canonical SVG and six approved brand colors are unchanged.

### Publication and verification

An additive eighth migration reconciles existing parent states and creation order with a transactional revision-writer fence. Ordered Tests → SQL → API → Web publication and actual runner payload hashes identify functional source `8100469cf9b75cba12714729256ec6b5be56e2c4`. Verification includes 431 passing .NET tests, 14 pipeline tests, 16 Node tests, real local/published Chromium and SQL Server checks, all four workflows, protected non-QA fingerprints and continuity of prepublication cookies/sessions/agent credentials/resource bytes. Only ledgered controlled fixtures and their temporary sessions were removed. See the technical documentation for reconstructed-age, cutover and accessibility/runtime verification limits. No product screenshots are included.

## [0.2.0]

The following release information is preserved from the published v0.2.0 README; no publication date is inferred from its later documentation commit.

- **Refined visual identity:** a single canonical geometric P-hammer symbol, dark near-neutral surfaces and restrained parchment accents, with distinct success, warning and danger states.
- **Clearer navigation:** one account-settings entry through the bottom profile row, contextual clickable breadcrumbs with real project names, and consistent native-select arrow spacing. Project-specific Settings remain available.
- **Localization foundation:** application-owned UI and safe error messages use English `.resx` resources. The account's English language preference persists; native browser validation and user content are unchanged.
- **Safer agent lifecycle:** optional raster avatar upload during creation, honest recovery if that upload is not confirmed, explicit linked-token consequences for deactivation/reactivation, and confirmed physical deletion. Deletion removes the profile, avatar and every linked credential while preserving work, resources and the names in actual retained activity; affected assignments and revisions are reconciled transactionally.
- **Clearer token management:** visible Personal/Agent types, bounded valid-token pages, collapsed paginated revoked/expired history, working renewal/revocation controls, and confirmed manual cleanup that preserves valid credentials and other accounts.
- **Security hardening:** explicitly Secure production antiforgery cookies, exact Web-to-API destination checks, disabled automatic redirects, and generation/transaction gates covering concurrent credential issuance and revocation. These changes do not claim to determine or remove an external Safe Browsing classification.

Release verification includes automated regressions, controlled SQL Server rollback/lifecycle checks, local and published browser flows, migration history, protected non-test data fingerprints, and continuity of prepublication sessions, credentials and resource bytes. No README screenshots are included in this version.

## [0.1.0] — 2026-11-04

The original GitHub v0.1.0 release notes are retained below.

Initial MIT-licensed PlanForge release: an owner-isolated project workbench built with .NET 10, Azure Functions, Azure SQL and Blazor Interactive Server.

- Hierarchical Work Plans (Phase → Subphase → Step), decisions, inherited/scoped agent assignments and four evidence-gated completion workflows.
- Persisted human sessions, protected Web BFF cookies/antiforgery, hashed 30-day agent tokens, renewal/revocation, password security-version invalidation and independent avatars.
- Owner-global SQL binary Library with independent Project/Phase links, explicit transactional orphan cleanup, revision-safe UTF-8 semicolon CSV import/export and full project ZIP export.
- Responsive dark UI, current-work focus, reduced-motion-aware scrolling and actual SQL-backed event polling with human/agent notifications.
- Four ordered, exact-SHA, path-aware GitHub Actions workflows. Six additive migrations are applied; the runtime SQL account cannot perform DDL.

Verification: 299 passing .NET tests, 11 pipeline tests and 4 JavaScript tests; controlled deployed API and real-browser acceptance, known-GUID owner isolation, binary/ZIP integrity and an intentional failing CI ref that executed no deployment jobs. Observed worker/process replacement preserved pre-existing controlled credentials and data; no full Azure Portal stop/start or PITR restoration was performed. See docs/acceptance.md and docs/deployment.md for scope and limitations.

Release source and Web: a7cd3a39bdc26e722956e6173381418c924cf978. API: b0df489d51961d703023fba3312ba6d2f8ef395e. Backend sources are unchanged by the final Web/documentation-only adjustment, so the distinct deployed API SHA is intentional. Both components report product version 0.1.0.

No automatic GitHub-provider integration or live-agent-online service is claimed. An owner creates their own operational agent token through the deployed Agents UI; temporary acceptance credentials were revoked and test resources removed. Private operational/audit files are not release assets.
