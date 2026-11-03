# Deployment

Status: initial database delivery under development. API and Web deployments are not available yet.

Only GitHub Actions applies migrations or publishes Azure apps. `tests.yml` runs hermetic Release restore/build/tests on main and external PRs without Azure secrets. Its trusted-main database job calls `deploy-database.yml` only after the same SHA passes tests, and only when migration/database mapping paths change. The pipeline serializes main runs, and the database workflow serializes migrations without cancellation. Readme-only pushes do not migrate. Manual Tests dispatch requests migration reconciliation after tests.

The database job builds a self-contained EF migration bundle in Production, reads migration history before/after, and executes only pending migrations with `AZURE_SQL_MIGRATION_CONNECTION_STRING`. The connection stays in the runner environment, not a repository file or artifact. API runtime uses a different least-privilege SQL identity. No Azure login/OIDC or IP firewall updater is used.

Future API and Web jobs must be explicitly gated after database/API success or legitimate no-change skips; simultaneous push triggers are not a deployment gate. Existing secret names for publication are `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` and `AZURE_WEBAPP_PUBLISH_PROFILE`. Flex publication requires `sku: flexconsumption`; never create new identities to work around authentication failure.

## Data protection and recovery

Never reset or drop the shared SQL database, run local database-update against it, or use real user accounts for test fixtures. Additive schema changes preserve previous readers/writers. Inspect migration SQL, current history/schema and usage before deployment. A failure must stop dependent deployments until actual schema/history is reconciled. Prefer a forward corrective migration; do not blindly execute a generated destructive Down method.

Azure SQL Basic has managed point-in-time backups with seven-day retention as provisioned by the owner. For an accident, the human owner uses Azure portal database Restore, chooses a point before the incident, restores into a separate target, validates data/schema and plans a controlled switch. NEA has no Azure portal/CLI credentials and cannot promise this action. Do not delete the original database. Backups do not make destructive tests or cleanup buttons reversible.

Current official sources consulted: Microsoft Learn isolated worker guide, Functions GitHub Actions guide, EF applying migrations/bundles, and Azure/functions-action authentication documentation. Recheck compatibility when changing SDK/action versions.