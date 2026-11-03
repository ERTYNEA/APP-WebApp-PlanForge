# Deployment

Status: additive SQL accounts/project schema and the initial Functions authentication API are published and verified. The alpha.2 business API is real-HTTP verified; alpha.3 Library/ZIP and its additive migration use the same gates. Web implementation/deployment is still pending. Do not equate a successful pipeline with full product acceptance.

Only GitHub Actions applies migrations or publishes Azure apps. `tests.yml` runs hermetic Release restore/build/tests on main and external PRs without Azure secrets. Its trusted-main database job calls `deploy-database.yml` only after the same SHA passes tests, when schema/database paths change or an API deployment requires reconciliation. The pipeline serializes main runs, and the database workflow serializes migrations without cancellation. Readme-only pushes do not migrate. Manual Tests dispatch requests migration reconciliation after tests.

The database job builds a self-contained EF migration bundle in Production, reads migration history before/after, and executes only pending migrations with `AZURE_SQL_MIGRATION_CONNECTION_STRING`. Every API deployment requires successful same-SHA database reconciliation, even for an API-only push: this recovers migrations left unapplied by earlier failed pushes. An up-to-date database is a no-op, not a repeated schema migration. Documentation-only pushes still do not deploy. Database and API publication reject superseded commits by checking the live main ref immediately before their state-changing operation. To roll back code, use a reviewed forward revert commit; do not rerun an obsolete pipeline over a newer deployment.

The connection stays in the runner environment, not a repository file or artifact. API runtime uses a different least-privilege SQL identity. No Azure login/OIDC or IP firewall updater is used.

The API reusable job requires successful same-SHA tests and SQL reconciliation. The forthcoming Web job must be explicitly gated after API success or a proven legitimate no-change skip; simultaneous push triggers are not a deployment gate. Existing secret names for publication are `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` and `AZURE_WEBAPP_PUBLISH_PROFILE`. Flex publication requires `sku: flexconsumption`; never create new identities to work around authentication failure.

## Data protection and recovery

Never reset or drop the shared SQL database, run local database-update against it, or use real user accounts for test fixtures. Additive schema changes preserve previous readers/writers. Inspect migration SQL, current history/schema and usage before deployment. A failure must stop dependent deployments until actual schema/history is reconciled. Prefer a forward corrective migration; do not blindly execute a generated destructive Down method.

Azure SQL Basic has managed point-in-time backups with seven-day retention as provisioned by the owner. For an accident, the human owner uses Azure portal database Restore, chooses a point before the incident, restores into a separate target, validates data/schema and plans a controlled switch. NEA has no Azure portal/CLI credentials and cannot promise this action. Do not delete the original database. Backups do not make destructive tests or cleanup buttons reversible.

Current official sources consulted: Microsoft Learn isolated worker guide, Functions GitHub Actions guide, EF applying migrations/bundles, and Azure/functions-action authentication documentation. Recheck compatibility when changing SDK/action versions.