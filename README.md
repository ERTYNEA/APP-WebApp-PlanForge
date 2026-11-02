# PlanForge

PlanForge is an open-source web application in its initial planning stage, intended to help individuals and small groups organize, manage, and maintain hierarchical work plans within projects. It aims to keep structured work, progress, AI agent assignments, and related resources together rather than scattered across separate tools and documents.

No application functionality has been implemented yet. The full functional specification will be provided in a later phase; the capabilities and technology choices below are planned, not currently available.

## Planned capabilities

- Organize projects with hierarchical work plans made up of phases, tasks, and steps.
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

This repository currently contains only the initial project overview, MIT license, and Git ignore rules. Application development, automated workflows, and deployment configuration have not started.

## License

PlanForge is licensed under the MIT License. See `LICENSE` for the full text.
