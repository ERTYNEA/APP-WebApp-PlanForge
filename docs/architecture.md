# Architecture

Status: implementation in progress, not a released application.

One .NET 10 solution separates Domain rules, Application use cases, Contracts DTOs, Infrastructure EF Core SQL, Azure Functions isolated HTTP and Blazor Interactive Server. The Web is a server-side BFF and never receives SQL credentials or references Infrastructure.

## Identity and session decision

The API issues cryptographically random opaque human sessions and distinct agent tokens. Only SHA-256 token hashes are stored in the existing SQL database. Human sessions survive restarts and are revocable; agent tokens expire after 30 days. The Web BFF protects the opaque session inside an HttpOnly/Secure/SameSite=Lax authentication cookie, validates it through the API on HTTP requests, and validates antiforgery before normal browser form commands. Interactive commands travel through the authenticated Blazor circuit and remain API-authorized. The key ring is persisted under the existing App Service HOME/ASP.NET/DataProtection-Keys directory, or ignored local .secrets/web-keys, so a process restart does not generate incompatible ephemeral keys. Existing SQL remains the authority for session expiry/revocation/security version. No new shared cloud secrets or Entra identities are required. Real cloud Web restart acceptance remains pending.

API ownership authorization is mandatory for every entity and relationship. The authenticated owner is never a DTO-provided UserId. Human-only workflow approval cannot be asserted by agents.

## Notifications decision

SQL project revisions and lightweight persisted actor/action events bridge Functions changes to active Blazor views. Cancellable low-frequency API polling refreshes current views and displays actor-correct notifications. There is no claimed direct push channel from Functions to Blazor.

## Persistence

One existing Azure SQL Basic database, additive EF migrations through GitHub Actions only. Resource binaries live in SQL; independent project and phase links do not cascade-delete resources. Avatars are separate. Quotas protect the 2 GB database budget. Relational SQLite tests cover portable behavior, never substitute for SQL Server compatibility/integration evidence.

## Work-plan persistence

Projects use immutable GUIDs, owner-scoped queries and integer revision compare-and-swap inside an explicit SQL transaction. The new additive schema represents Phase, Subphase, Step and Decision as typed WorkItems with a composite project/parent foreign key. This is a normalized hierarchy, not a flat task list. Application validation requires Phase → Subphase → Step, at least one real Subphase per Phase, correct decision parents, stable record types and unique sibling order. DTOs are separate from EF entities.

A full snapshot save increments revision exactly once and retains stable IDs. Omitted records require explicit replacement. Invalid hierarchy and unauthorized ownership are rejected before writes; database collisions roll back metadata, records and events together. Short serializable read transactions keep revisions and trees consistent. Aggregate completion cannot bypass child completion or unresolved decisions; Step closure validates the project's workflow and human-only approval and stamps the authenticated actor/time. Only completed-step closure evidence is authoritative.

ProjectChanges retain the latest 100 events per project. The event and new revision are committed atomically. This is groundwork for the observing Web view, not a claim that Web polling already exists. Business HTTP adapters, agent lifecycle, CSV exchange and the Library/ZIP tranche consume this model. Library associations are independent and restrictive, orphan cleanup rechecks under the attachment row-lock protocol, and project archives verify the revision after gathering their authorized union. Bounded owner-filtered summary/change endpoints now expose actual progress and the latest 100 actor events without plan-tree/binary polling. User/agent avatars use separate raster byte storage, excluded from Library quotas/cleanup. Password changes use a concurrency-protected hash update, increment a persisted account security version and atomically revoke only the owner’s human/agent credentials. Every authentication compares the credential’s captured security version with the account, so a login or token issuance already in flight cannot escape password-change invalidation; transport/database failures are not mislabeled as stale account conflicts. The observing Web now polls bounded revision/event pages every 10 seconds while active, reloads changed snapshots and detects export events even when revision is unchanged. Polling pauses during open editors/confirmations to preserve their base revision and cancels on navigation. Exact actor messages feed deduplicated, six-second accessible toasts; no Functions-to-Blazor push channel is claimed. Blazor and streaming file BFF routes use Contracts/API only, never DbContext. First cloud Web publication and full browser acceptance remain pending.

## Delivery boundary

Exact-SHA tests must succeed before ordered SQL → Functions API → Blazor deployment. Only existing GitHub repository secrets are consumed by trusted deployment jobs. Public PR tests never receive Azure credentials. No Azure CLI, new identities, storage services or local SQL Server are required.