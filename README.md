# PlanForge

PlanForge is an open-source project work-plan application for individuals and a few colleagues. Its hierarchy is Phase → Subphase → Step, with decisions, agent assignments and a reusable resource library.

Version: `0.1.0-alpha.3`, implementation in progress, not a finished product. Implemented API services and handlers cover authentication, owner-isolated projects and hierarchical work plans, four workflow closure policies, logical agents and hashed 30-day tokens, transactional CSV exchange, SQL-backed resource libraries and complete project ZIP exports. SQL and API publication use exact-commit gated GitHub Actions. The alpha.2 business API has passed controlled real HTTP acceptance. The new Library/ZIP tranche awaits its ordered cloud verification; Blazor/account-management/change-polling and complete integrated acceptance remain pending. See the API and deployment documents for the boundary between implemented code and cloud-verified behavior.

## Planned capabilities

- Organize projects with hierarchical work plans made up of phases, subphases, and steps.
- Track work status and progress.
- Assign AI agents to different parts of a plan.
- Import and export plans using the `work-plan.csv` exchange file.
- Maintain a reusable resource library, including documents and images associated with projects or specific phases.
- Provide a web interface for managing projects, viewing plans, and administering resources.
- Expose an authenticated REST API so external agents can query and update information within their projects.

## Planned technology

- .NET 10 LTS.
- Blazor for the web interface, hosted on Azure App Service.
- Azure Functions with the .NET 10 Isolated Worker model for the REST API.
- Azure SQL Database with Entity Framework Core.
- GitHub Actions for automated testing and deployments.
- Test-Driven Development (TDD).

## Project status

The current slices establish .NET 10 domain/application/relational/handler tests, persistent authentication, owner-scoped hierarchy and concurrency, agent lifecycle and strict CSV exchange. See `docs/architecture.md`, `docs/deployment.md` and `docs/ui-design-system.md` for decisions and current delivery boundaries. The complete product remains under implementation.

## Local checks

Requires the .NET 10 SDK pinned by `global.json`. No local SQL Server is required for the default tests.

```sh
dotnet restore APP-WebApp-PlanForge.slnx
dotnet build APP-WebApp-PlanForge.slnx --configuration Release
dotnet test APP-WebApp-PlanForge.slnx --configuration Release
```

Tests use independent in-memory SQLite relational databases where appropriate; these are not SQL Server integration evidence and never touch the shared Azure database. Runtime credentials, publishing profiles and local settings must remain ignored. Only trusted-main GitHub Actions may migrate the shared database.

## License

PlanForge is licensed under the MIT License. See `LICENSE` for the full text.
