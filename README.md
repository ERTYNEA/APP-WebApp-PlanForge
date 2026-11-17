# PlanForge

<img src="src/PlanForge.Web/wwwroot/favicon.svg" alt="PlanForge symbol" width="64" height="64" />

Version **0.6.0** · MIT licensed

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

## What's New — v0.6.0

- **Consistent feedback everywhere (NOTIF-01):** one navigation-safe toast family distinguishes green Success, brand-yellow Neutral and red Danger with check, information and circle-X icons. Destructive outcomes, remote actors, partial cleanup and contextual errors now keep their real meaning, without duplicate announcements or lost navigation feedback.
- **Predictable destructive controls (DANGER-01):** the existing danger style and destructive-left/normal-right order are consistent across avatar, token, resource, inspector and confirmation actions. Cancel receives safe initial confirmation focus; ordinary Upload and Save styles remain unchanged.
- **Original now, optimized silently (AVATAR-01):** human and agent avatars accept validated PNG/JPEG/WebP up to 4 MiB. Accepted originals appear persistently at once, then refresh silently to proportional WebP within 512×512. Upload confirms once; optimization adds no toast, spinner or manual-refresh requirement. Optional-image failure preserves an already saved new agent for recovery without duplicates.
- **Reliable replacement and cleanup:** the existing Functions storage queue awakens a SQL-backed global FIFO worker with fenced generations, bounded cancellation and three processing attempts. Pending replacement keeps its turn; active replacement moves to the tail. Old photos are not restored, and a shared 40 MiB temporary-original budget covers each human and all their agents independently of Library quotas.

Release dated 2026-11-17; functional API/Web publication was verified on 2026-11-16 through ordered Tests → eleventh additive SQL migration → API → Web. Verification includes 764 passing .NET tests, 33 Node tests, 14 pipeline tests, real isolated local Blazor QA, a genuine local Functions-host/Azurite recovery probe and focused public human/agent browser/API acceptance. Temporary fixtures, sessions and QA processes were retired selectively. No isolated live SQL Server, Azure-scale or independent accessibility certification is claimed; see the acceptance documentation for precise cleanup, provider and runtime evidence limits. No product screenshots are included.

[Full changelog](./CHANGELOG.md)
