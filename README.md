# PlanForge

<img src="src/PlanForge.Web/wwwroot/favicon.svg" alt="PlanForge symbol" width="64" height="64" />

Version **0.5.0** · MIT licensed

## About PlanForge

PlanForge turns a project into a clear, structured work plan. It brings the work to do, the decisions still needed, the responsible logical agents and the supporting files into one focused workspace—without confusing progress with final approval.

### Keep the work understandable

- Organize projects into **Phases → Subphases → Steps**, with descriptions, statuses, ordering and stable identities.
- Record decisions where they belong and keep unresolved decisions visible alongside the work.
- Assign logical agents at the right level. Descendants can inherit an assignment, override it or remain explicitly unassigned.
- Choose the completion workflow that fits the project: **Planning Only**, **Local Changes + Manual Approval**, **Branch + Pull Request**, or **Direct to Main**. Work done, human approval, a confirmed merge and a confirmed push are different milestones.
- Follow actual completed-step progress and recent project activity. Open views refresh through lightweight polling rather than requiring a manual reload for every change.

### Keep supporting material reusable

The **Library** stores files independently of a single plan. A resource can be linked directly to a Project, Phase, Subphase or Step and reused through multiple associations. Descendants inherit availability without copied files or links. Removing a project or structural node removes its links—not the Library file itself. Assigned and orphan filters make the remaining relationships visible; permanent cleanup requires explicit confirmation.

Export a plan as UTF-8 semicolon `work-plan.csv`, or download a project ZIP containing its plan, readable summary, resource-association manifest and linked files, including Phase-only resources. Imports preserve stable IDs, validate the whole snapshot and reject stale revisions instead of silently overwriting newer work.

### Connect tools deliberately

Logical agents identify assignments and authenticated activity; they are **not hosted AI workers**. Use an unlinked **Personal token** for a personal integration, or a specifically linked **Agent token** to attribute its requests to that identity. Tokens expire after 30 days, can be renewed or revoked, and are shown only when issued. PlanForge does not execute agents, connect to providers or verify GitHub merges and pushes automatically.

Each account has its own projects, resources, agents and credentials. Account settings include a display name, raster avatar, password/session management and a persistent language preference. The interface currently supports **English only**; user-authored names and descriptions retain their original language.

## Technology & Architecture

PlanForge uses **C# and .NET 10**, **Blazor Interactive Server**, an **isolated-worker Azure Functions API**, **Entity Framework Core** and **Azure SQL**. The Web runs on Azure App Service. GitHub Actions tests and publishes through the existing ordered **Tests → SQL reconciliation → API → Web** pipeline, with path-aware skips when a component has not changed.

One solution, `APP-WebApp-PlanForge.slnx`, separates responsibilities:

| Project | Responsibility |
|---|---|
| `PlanForge.Domain` | Work-plan rules and domain entities |
| `PlanForge.Application` | Authorized use cases and application boundaries |
| `PlanForge.Contracts` | API request/response contracts |
| `PlanForge.Infrastructure` | EF Core persistence, SQL transactions and migrations |
| `PlanForge.Api` | HTTP authentication, validation and composition |
| `PlanForge.Web` | Accessible UI and server-side API client/BFF; no direct SQL access |

The API enforces authenticated ownership, human-only operations and revision concurrency. The Web keeps its opaque session in a protected HttpOnly cookie, uses antiforgery and does not put credentials in browser localStorage. Production cookies require HTTPS. The Web-to-API client admits only explicit approved destinations and does not automatically follow redirects. Passwords are robustly hashed; only token hashes are persisted. Resource binaries and raster avatars are stored separately within SQL.

### Check the project locally

Use the SDK pinned in `global.json`:

```sh
dotnet restore APP-WebApp-PlanForge.slnx
dotnet restore src/PlanForge.Api
dotnet build APP-WebApp-PlanForge.slnx --configuration Release
dotnet test APP-WebApp-PlanForge.slnx --configuration Release
python3 -m unittest discover -s tests/pipeline -v
node --test tests/browser/ui.test.cjs
```

The Node.js tests use the built-in runner with no npm dependencies. Default tests are hermetic: domain/application checks, relational SQLite fixtures, direct Functions-handler tests, bUnit and ASP.NET TestServer. They do not reset or contact shared Azure SQL and are not substitutes for separate SQL Server, real-browser and deployed-endpoint acceptance.

To run the Web against an available API, first set `PLANFORGE_API_URL` to its approved HTTPS origin. The endpoint policy also permits a separately running Development API on loopback port 7601; arbitrary origins are rejected.

```sh
export ASPNETCORE_ENVIRONMENT=Development
export PlanForgeApi__BaseUrl="$PLANFORGE_API_URL"
dotnet run --project src/PlanForge.Web --configuration Release --no-launch-profile --urls http://127.0.0.1:7600
```

Keep credentials and local settings outside source control. Preserve the existing DataProtection key ring across Web restarts. Shared SQL migrations and Azure publication run only through trusted GitHub Actions; local development never requires resetting the shared database or provisioning another cloud service.

### Technical documentation and limits

Read the [architecture](docs/architecture.md), [REST API](docs/api.md), [CSV contract](docs/work-plan-csv.md), [UI design system](docs/ui-design-system.md), [deployment and recovery](docs/deployment.md), and [acceptance and operational limits](docs/acceptance.md).

Recent activity is bounded project-change history, not a complete perpetual audit system. Rate limits are per worker rather than distributed anti-DDoS protection. Selected contrast and responsive checks do not constitute independent WCAG certification. Cloud point-in-time recovery is an owner-operated disaster-recovery procedure, not an application undo button. Password-recovery email, Spanish translation and autonomous agent execution are not included.

Licensed under [MIT](LICENSE).

## What's New — v0.5.0

- **Clear confirmation guidance (REG-02):** `Enter the same password again.` now appears beneath Confirm password with the same spacing as the existing `Use 12–128 characters.` help. Accessible mismatch feedback, native constraints and Login are preserved; no second password reaches the API.
- **A complete workspace overview (HOME-01):** four responsive Projects, Phases, Subphases and Steps cards show totals and all four effective-state counts, including zeros. Overall progress occupies its own full-width band; Next in focus, Recently updated and the Projects catalog keep their existing behavior.
- **Know when the current state began (PLAN-04):** the inspector shows a persistent, server-assigned effective-state entry date beside each Project/Phase/Subphase/Step badge, formatted in your browser's local timezone. Real state changes update it; unrelated edits and unchanged CSV imports do not. Historical dates remain honestly unavailable rather than inferred.

Published on 2026-11-14 and verified with 506 passing .NET tests, 23 Node tests, 14 pipeline tests, real isolated local Blazor QA and focused public HTTPS browser/API checks after ordered Actions publication. The tenth additive migration preserves legacy data and unknown dates. No isolated live SQL Server test environment was available; see the acceptance documentation for the precise provider, migration, accessibility and runtime verification limits. Temporary QA servers and sessions were closed selectively; no product screenshots are included.

[Full changelog](./CHANGELOG.md)
