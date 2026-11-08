# Changelog

All published release notes are retained here, newest first. The root README introduces only the latest stable release. Historical entries describe their original verification scope, not the current test counts.

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
