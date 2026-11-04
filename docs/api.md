# REST API

Status: alpha.3 business and Library/ZIP APIs with four additive SQL migrations are cloud-verified, including ownership, workflow, CSV, ZIP bytes/CRC/manifest and real attachment/cleanup races. Alpha.4 adds tested account/password/avatar management, lightweight summaries, change cursors and settings; its fifth additive migration and new handlers await ordered publication/acceptance. Blazor and complete integrated acceptance remain pending.

One .NET 10 isolated Functions app is the authorizing backend. Anonymous platform triggers still require application Bearer authorization. Clients never submit an OwnerUserId. Known IDs belonging to another owner return the same safe 404 as missing records. HTTPS is mandatory outside loopback development.

## Routes

All paths below start with `/api/v1`. Bodies are JSON except raw CSV import. Authentication, parsing and ownership checks happen server-side.

- GET `/health`, GET `/ready`: liveness/version and real SQL schema readiness.
- POST `/auth/register`: email, password, displayName; returns 201 with a human session grant.
- POST `/auth/login`: email and password; returns a new human session grant.
- GET `/auth/session`: human session profile. POST `/auth/logout`: revoke that session, 204.
- GET `/users/me`, PUT `/users/me`: human-only profile and display-name update; identity/email stay unchanged.
- POST `/users/me/password`: currentPassword/newPassword; checks the current hash and atomically revokes every human session and agent token owned by that account. Returns 204; sign in again. Another owner remains unaffected. Account/credential security versions also invalidate sessions and agent tokens issued by an already-in-flight pre-change login/request.
- GET/PUT/DELETE `/users/me/avatar`: human-only raster avatar; PUT raw bytes with image/png, image/jpeg or image/webp, at most 256 KiB. MIME/signature validation, safe inline response, independent SQL storage outside Library cleanup.
- GET `/settings`: authenticated safe product version, limits and the four ordered workflow identifiers, never credentials/configuration secrets.
- GET `/projects`: current owner's complete snapshots, retained for API compatibility. POST `/projects`: name, optional description and workflow; returns 201.
- GET `/projects/summary?skip=0&take=50`: lightweight SQL projections, description preview up to 240 characters, actual counts/progress and last event cursor, no trees or binaries. Returns items/total/skip/take/hasMore; take 1–100, skip 0–100000.
- GET `/projects/{projectId}/changes?after=0`: coherent current revision, latestChangeId, resetRequired and latest 100 retained actor/action events newer than the cursor. Actor names resolve owner-local current identities; IDs/kinds remain authoritative. A stale/pruned cursor requires refreshing the project before advancing the baseline.
- GET `/projects/{projectId}`: complete snapshot, stable IDs and revision. PUT the same route: full edited snapshot, preserving records. DELETE: `{ "revision": 1, "confirmed": true }`, permanent project/plan deletion.
- GET/POST `/projects/{projectId}/phases`, `/subphases`, `/steps`, `/decisions`: list/create typed records.
- GET/PUT/DELETE `/projects/{projectId}/phases/{id}` and equivalent subphases/steps/decisions routes: read/edit/reorder/move/delete a record. Delete requires revision and confirmed=true; descendants are intentional, and the last Subphase of a retained Phase cannot be removed.
- POST `/projects/{projectId}/completed-phases/clean`: revision and confirmed=true; removes only fully completed Phase trees.
- GET `/agents`, POST `/agents`: owner's logical agents; creation requires a human session. GET/PUT/DELETE `/agents/{id}`: inspect, edit, or deactivate. Deactivation preserves historical identity and invalidates its tokens.
- GET/PUT/DELETE `/agents/{id}/avatar`: owner-isolated safe raster read; human-only upload/removal, same 256 KiB and MIME/signature policy as account avatars.
- GET/POST `/agent-tokens`: human-only metadata listing or issuance with name and optional agentId. DELETE `/agent-tokens/{id}`: immediate revocation, 204. POST `/agent-tokens/{id}/renew`: new value and expiry, old value revoked.
- GET `/projects/{projectId}/work-plan/export`: UTF-8 `work-plan.csv` download.
- POST `/projects/{projectId}/work-plan/import`: raw UTF-8 CSV, at most 1 MiB. Set `X-PlanForge-Replace: true` explicitly to permit omitted-record deletion. Ownership is checked before reading/parsing content. See `work-plan-csv.md`.

A plan mutation command has revision, title, parentId, optional zero-based sortOrder, description, status, assignmentMode, assignedAgentId, resolution and evidence. New Phases receive a real editable Default subphase. Subphases belong to Phases, Steps to Subphases, Decisions to one of those three types. Moving/reordering preserves IDs and checks the complete resulting hierarchy. Omitted sortOrder appends a new record or preserves an edited sibling position.

## Resource library and project archives

- GET `/resources`: owner metadata only, never binary Content. Search with `search`, filter `assignment=all|assigned|orphan`, and page using `skip` and `take` (default 50, maximum 100). Returns items, total, skip, take, hasMore from the actual filtered set.
- POST `/resources`: raw bytes and an allowed Content-Type. Supply `X-File-Name` as a percent-encoded UTF-8 filename and optional percent-encoded `X-Resource-Title`. Optional `X-Project-Id` and/or `X-Phase-Id` attach atomically; phase-only does not imply a direct project link. Targets are authorized before body reading.
- GET/PUT/DELETE `/resources/{resourceId}`: metadata, title update with current resource revision, or permanent deletion with revision and confirmed=true. Deletion removes all links deliberately; normal project/phase deletion never deletes the binary.
- GET `/resources/{resourceId}/download`: exact bytes, safe attachment filename and nosniff.
- POST `/resources/{resourceId}/links`: resource revision, attach boolean and exactly one projectId or phaseId. Owner membership, record type and revision are checked transactionally.
- GET `/resources/usage`: owner bytes, owner budget, maximum file bytes and near-limit warning; never another user's metadata.
- POST `/resources/orphans/clean`: `{ "confirmed": true, "resourceIds": [...] }` from the preview, at most 1000 unique IDs. Returns deletedCount/skippedCount. Only owner-local zero-link resources are physically removed after a concurrent-attachment-safe recheck; no scheduled cleanup.
- GET `/projects/{projectId}/resources`: effective direct-plus-phase union without duplicates, with each association identified. GET `/projects/{projectId}/phases/{phaseId}/resources`: only that Phase's links.
- GET `/projects/{projectId}/export`: ZIP with work-plan.csv, README.md, manifest.json and resources/{resourceId}/{safe-filename}. Includes phase-only files, excludes unrelated Library orphans and verifies a coherent project revision. Another archive in progress can return 429; a changed revision returns 409 rather than a mixed snapshot.

Defaults: 8 MiB per file, 100 MiB per owner, 1 GiB global Library reserve and 1000 files per owner. Scoped configuration keys are `ResourceLimits__MaximumFileBytes`, `ResourceLimits__OwnerBudgetBytes`, `ResourceLimits__GlobalBudgetBytes`; the global hard ceiling is 1.5 GiB to preserve headroom in SQL Basic 2 GiB. These keys require no new identity or secret and are optional. HTML, scripts and SVG MIME types are not accepted; text/Markdown/CSV, PDF, common raster images, ZIP, JSON, Office OpenXML and generic binary attachments are supported. Upload bytes are immutable; title and independent links are editable. SHA-256 and size are checked on download. Original base filenames up to 180 characters are preserved after removing path/control components; longer names are rejected, never silently truncated.

Project/phase deletion removes associations only. Resource cleanup never sees avatar storage. Linked-library mutations touch affected project revisions/events without overwriting plan data. Metadata listing explicitly excludes SQL binary columns; downloads and archives fetch only authorized selected resources. ZIP manifests give exact project/phase associations, MIME, hash and byte size; archives do not execute or automatically import resources.

## Workflow, decisions and concurrency

Workflow values are `PlanningOnly`, `LocalChangesManualApproval`, `BranchPullRequest`, `DirectToMain`, displayed in that order. Step completion requires respectively `WorkDone`, human `OwnerApproved`, `MergeConfirmed`, `PushConfirmed` evidence. Reporting WorkDone under another mode keeps the step InProgress; it cannot be completed prematurely. Agents cannot forge human approval. Changing workflow is human-only and cannot simultaneously close pending work under a more permissive mode. This records an authenticated confirmation, not automatic GitHub verification.

Phase/Subphase/Step statuses: Pending, InProgress, Blocked, Completed. Decisions use Pending or Decided and require a resolution when decided. Aggregate status/progress derive from actual children and closed steps; no fake progress counters. Agent assignment modes: Inherit, Override with a valid owner-local agent, or explicit Unassigned. Inactive agent references remain historical but cannot be newly assigned.

Every mutation supplies the current revision; one successful logical operation increments it once. Stale or concurrently superseded revisions return 409. Reload and reconcile; never automatically overwrite the winner. Snapshot and child changes/events share a transaction. Export events do not increment revision. Stored events distinguish Human and Agent and use the exact action strings documented in the specification; the bounded change endpoint exposes these events; observing Web polling remains pending.

## Authentication and safety

Passwords require 12–128 characters. Email is normalized uniquely, display name is 1–80 characters. Human sessions expire after seven days; tokens contain 256 random bits and only SHA-256 hashes are persisted. Agent tokens expire exactly 30 days after issuance, are shown only in issuance/renewal responses, and cannot manage credentials. A disabled linked agent is rejected even in a concurrent issuance/deactivation race. The final Web uses a server-side BFF, secure HttpOnly/SameSite cookies and antiforgery, never browser localStorage or SQL credentials.

JSON is bounded to 64 KiB without trusting Content-Length. CSV uses a separate 1 MiB bound and strict encoding. Responses are no-store/nosniff with correlation IDs; safe problem JSON uses 400/401/403/404/409/413/429 as applicable. Unhandled exceptions do not expose SQL details, credentials or stack traces. Login/registration/API/exchange budgets are in-process per worker, not distributed anti-DDoS protection; SQL session validity is persistent across workers and restarts.

Publishing profiles, SQL credentials and cloud control-plane secrets are never part of the HTTP contract. Tokens should be injected via secure variables, never hardcoded in examples or checked into source.
