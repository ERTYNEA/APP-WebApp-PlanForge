# Architecture

Status: implementation in progress, not a released application.

One .NET 10 solution separates Domain rules, Application use cases, Contracts DTOs, Infrastructure EF Core SQL, Azure Functions isolated HTTP and Blazor Interactive Server. The Web is a server-side BFF and never receives SQL credentials or references Infrastructure.

## Identity and session decision

The API issues cryptographically random opaque human sessions and distinct agent tokens. Only SHA-256 token hashes are stored in the existing SQL database. Human sessions survive restarts and are revocable; agent tokens expire after 30 days. The Web stores the opaque session in a Secure/HttpOnly/SameSite cookie, validates it through the API, and validates antiforgery for browser commands. Cookie session validation is based on SQL rather than an ephemeral process signing key. No new shared cloud secrets or Entra identities are required. Implementation and restart tests remain pending.

API ownership authorization is mandatory for every entity and relationship. The authenticated owner is never a DTO-provided UserId. Human-only workflow approval cannot be asserted by agents.

## Notifications decision

SQL project revisions and lightweight persisted actor/action events bridge Functions changes to active Blazor views. Cancellable low-frequency API polling refreshes current views and displays actor-correct notifications. There is no claimed direct push channel from Functions to Blazor.

## Persistence

One existing Azure SQL Basic database, additive EF migrations through GitHub Actions only. Resource binaries live in SQL; independent project and phase links do not cascade-delete resources. Avatars are separate. Quotas protect the 2 GB database budget. Relational SQLite tests cover portable behavior, never substitute for SQL Server compatibility/integration evidence.

## Delivery boundary

Exact-SHA tests must succeed before ordered SQL → Functions API → Blazor deployment. Only existing GitHub repository secrets are consumed by trusted deployment jobs. Public PR tests never receive Azure credentials. No Azure CLI, new identities, storage services or local SQL Server are required.