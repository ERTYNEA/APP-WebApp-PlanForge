# PlanForge

PlanForge is an MIT-licensed work-plan application for individuals and a few colleagues. Projects contain Phase → Subphase → Step hierarchies, decisions, inherited agent assignments and a reusable resource library.

Version: `0.1.0` — functional initial release. SQL, Functions API and Blazor Web are delivered through the existing ordered GitHub Actions pipeline. Controlled real HTTP/browser acceptance covers workflows, ownership, CSV, binary resources, notifications and responsive UI; every release also requires exact-SHA cloud readiness and persisted-session/data continuity, not merely a passing build. See [acceptance and limitations](docs/acceptance.md).

## Implemented capabilities

- Public registration, persisted human sessions, account names/raster avatars and verified password changes that invalidate only that owner's sessions and agent tokens.
- Owner-isolated projects with stable IDs/revisions, nested work plans, decisions, ordering/moves and inherit/override/explicitly-unassigned agent semantics.
- Four closure workflows, in order: Planning Only; Local Changes + Manual Approval; Branch + Pull Request; Direct to Main. Work done is not human approval, merge or confirmed push.
- Logical agents and revocable, renewable, hashed API tokens with a 30-day expiry. Newly issued values are shown once, never retrieved from SQL.
- UTF-8 semicolon `work-plan.csv` snapshot exchange, stable IDs, atomic validation, explicit replacement for omissions and stale-revision 409 protection.
- SQL resource binaries, independent project/phase-only links, Assigned/Orphan metadata, bounded uploads, safe downloads and confirmed manual cleanup. Plan deletion preserves Library files.
- Complete project ZIPs with CSV, readable README, exact resource bytes and association manifest, including phase-only attachments without duplicates.
- Blazor Interactive Server Home, Projects/Work Plan, Library, Agents and Account screens, cancellable persisted-event polling and actor-correct notifications. No fake agent-online indicators or external AI execution.

## Architecture

One .NET 10 solution separates Domain, Application, Contracts, Infrastructure, isolated Functions API and Blazor Web. Web consumes the API server-side and never references SQL infrastructure. Existing Azure SQL Basic, Functions Flex and App Service resources are reused; no additional cloud identity or paid service is required.

See [architecture](docs/architecture.md), [API](docs/api.md), [CSV contract](docs/work-plan-csv.md), [design system](docs/ui-design-system.md) and [deployment/recovery](docs/deployment.md).

## Local checks

Use the .NET 10 SDK pinned by `global.json`:

```sh
dotnet restore APP-WebApp-PlanForge.slnx
dotnet restore src/PlanForge.Api
dotnet build APP-WebApp-PlanForge.slnx --configuration Release
dotnet test APP-WebApp-PlanForge.slnx --configuration Release
python3 -m unittest discover -s tests/pipeline -v
node --test tests/browser/ui.test.cjs
```

The small JavaScript regression suite uses the built-in Node.js test runner (Node 22+), with no npm dependencies. Default tests are hermetic and use SQLite relational fixtures, handler transport, bUnit and ASP.NET TestServer where appropriate. They do not touch shared Azure SQL and are not substitutes for SQL Server/cloud/browser evidence. No local SQL Server, Docker, Azurite or global Functions tools are needed.

## Run the Web locally

Point Web at a configured API root, normally the already deployed HTTPS API, and use a free loopback port:

```sh
export ASPNETCORE_ENVIRONMENT=Development
export PlanForgeApi__BaseUrl=https://your-existing-api.azurewebsites.net
dotnet run --project src/PlanForge.Web --configuration Release --urls http://127.0.0.1:7600
```

The placeholder API hostname must be replaced with the actual existing API URL. HTTP is allowed only for loopback development. Production cookies are Secure, HttpOnly and SameSite=Lax; browser forms use antiforgery. The opaque API session is protected inside the cookie, never placed in localStorage. Local key files are ignored under `.secrets/web-keys`; App Service uses its existing persistent HOME key directory. Never delete the key ring to deploy or restart.

Only trusted-main GitHub Actions may migrate shared SQL or publish Azure apps. Four workflows enforce exact-SHA tests → SQL reconciliation → API → Web, with path-aware no-change skips and superseded-publication guards. Publishing profiles and migration credentials remain existing repository secrets, not files/artifacts. Runtime credentials, private settings, test passwords and API tokens must never be committed.

## License

MIT. See [LICENSE](LICENSE).
