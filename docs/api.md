# REST API

Status: alpha.2 business handlers are implemented and tested in-process. Authentication and the initial project schema are cloud-verified; the new business HTTP tranche awaits its exact-commit deployment and real HTTP acceptance. The resource library, ZIP export, account editing/avatars, change polling and Web UI are not yet available. This is not full product acceptance.

One .NET 10 isolated Functions app is the authorizing backend. Anonymous platform triggers still require application Bearer authorization. Clients never submit an OwnerUserId. Known IDs belonging to another owner return the same safe 404 as missing records. HTTPS is mandatory outside loopback development.

## Routes

All paths below start with `/api/v1`. Bodies are JSON except raw CSV import. Authentication, parsing and ownership checks happen server-side.

- GET `/health`, GET `/ready`: liveness/version and real SQL schema readiness.
- POST `/auth/register`: email, password, displayName; returns 201 with a human session grant.
- POST `/auth/login`: email and password; returns a new human session grant.
- GET `/auth/session`: human session profile. POST `/auth/logout`: revoke that session, 204.
- GET `/projects`: current owner's projects. POST `/projects`: name, optional description and workflow; returns 201.
- GET `/projects/{projectId}`: complete snapshot, stable IDs and revision. PUT the same route: full edited snapshot, preserving records. DELETE: `{ "revision": 1, "confirmed": true }`, permanent project/plan deletion.
- GET/POST `/projects/{projectId}/phases`, `/subphases`, `/steps`, `/decisions`: list/create typed records.
- GET/PUT/DELETE `/projects/{projectId}/phases/{id}` and equivalent subphases/steps/decisions routes: read/edit/reorder/move/delete a record. Delete requires revision and confirmed=true; descendants are intentional, and the last Subphase of a retained Phase cannot be removed.
- POST `/projects/{projectId}/completed-phases/clean`: revision and confirmed=true; removes only fully completed Phase trees.
- GET `/agents`, POST `/agents`: owner's logical agents; creation requires a human session. GET/PUT/DELETE `/agents/{id}`: inspect, edit, or deactivate. Deactivation preserves historical identity and invalidates its tokens.
- GET/POST `/agent-tokens`: human-only metadata listing or issuance with name and optional agentId. DELETE `/agent-tokens/{id}`: immediate revocation, 204. POST `/agent-tokens/{id}/renew`: new value and expiry, old value revoked.
- GET `/projects/{projectId}/work-plan/export`: UTF-8 `work-plan.csv` download.
- POST `/projects/{projectId}/work-plan/import`: raw UTF-8 CSV, at most 1 MiB. Set `X-PlanForge-Replace: true` explicitly to permit omitted-record deletion. Ownership is checked before reading/parsing content. See `work-plan-csv.md`.

A plan mutation command has revision, title, parentId, optional zero-based sortOrder, description, status, assignmentMode, assignedAgentId, resolution and evidence. New Phases receive a real editable Default subphase. Subphases belong to Phases, Steps to Subphases, Decisions to one of those three types. Moving/reordering preserves IDs and checks the complete resulting hierarchy. Omitted sortOrder appends a new record or preserves an edited sibling position.

## Workflow, decisions and concurrency

Workflow values are `PlanningOnly`, `LocalChangesManualApproval`, `BranchPullRequest`, `DirectToMain`, displayed in that order. Step completion requires respectively `WorkDone`, human `OwnerApproved`, `MergeConfirmed`, `PushConfirmed` evidence. Reporting WorkDone under another mode keeps the step InProgress; it cannot be completed prematurely. Agents cannot forge human approval. Changing workflow is human-only and cannot simultaneously close pending work under a more permissive mode. This records an authenticated confirmation, not automatic GitHub verification.

Phase/Subphase/Step statuses: Pending, InProgress, Blocked, Completed. Decisions use Pending or Decided and require a resolution when decided. Aggregate status/progress derive from actual children and closed steps; no fake progress counters. Agent assignment modes: Inherit, Override with a valid owner-local agent, or explicit Unassigned. Inactive agent references remain historical but cannot be newly assigned.

Every mutation supplies the current revision; one successful logical operation increments it once. Stale or concurrently superseded revisions return 409. Reload and reconcile; never automatically overwrite the winner. Snapshot and child changes/events share a transaction. Export events do not increment revision. Stored events distinguish Human and Agent and use the exact action strings documented in the specification; observing Web polling remains pending.

## Authentication and safety

Passwords require 12–128 characters. Email is normalized uniquely, display name is 1–80 characters. Human sessions expire after seven days; tokens contain 256 random bits and only SHA-256 hashes are persisted. Agent tokens expire exactly 30 days after issuance, are shown only in issuance/renewal responses, and cannot manage credentials. A disabled linked agent is rejected even in a concurrent issuance/deactivation race. The final Web uses a server-side BFF, secure HttpOnly/SameSite cookies and antiforgery, never browser localStorage or SQL credentials.

JSON is bounded to 64 KiB without trusting Content-Length. CSV uses a separate 1 MiB bound and strict encoding. Responses are no-store/nosniff with correlation IDs; safe problem JSON uses 400/401/403/404/409/413/429 as applicable. Unhandled exceptions do not expose SQL details, credentials or stack traces. Login/registration/API/exchange budgets are in-process per worker, not distributed anti-DDoS protection; SQL session validity is persistent across workers and restarts.

Publishing profiles, SQL credentials and cloud control-plane secrets are never part of the HTTP contract. Tokens should be injected via secure variables, never hardcoded in examples or checked into source.
