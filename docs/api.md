# REST API

Status: first authentication/readiness tranche implemented and handler-tested; cloud verification is pending. Project, work-plan, library and agent-token endpoints are not available yet.

The single Azure Functions isolated .NET10 app owns all authorization. `AuthorizationLevel.Anonymous` allows the platform trigger to reach application authentication; it does not authorize user data access. All deployed URLs are HTTPS.

## Implemented routes

| Method | Route | Behavior |
|---|---|---|
| GET | `/api/v1/health` | Liveness and central semantic/informational version |
| GET | `/api/v1/ready` | Real SQL schema query and version; no user data returned |
| POST | `/api/v1/auth/register` | Email/password/displayName JSON; 201 with one-time human session token and safe user profile |
| POST | `/api/v1/auth/login` | Email/password JSON; 200 with a new human session token and safe profile |
| GET | `/api/v1/auth/session` | Human Bearer session required; returns safe current-user profile |
| POST | `/api/v1/auth/logout` | Human Bearer session required; immediate revocation, 204 without body |

Passwords require 12–128 characters; display names 1–80 and email up to256. Emails normalize for uniqueness. Tokens contain256 random bits, have explicit human/agent prefix separation, and only SHA256 hashes are stored. Human sessions currently expire seven days after issue. Agent-token issuance/management and its30-day lifecycle are still pending, not exposed by these routes. Session credentials must never be logged or stored in browser localStorage. The upcoming Web BFF must use an HttpOnly/Secure/SameSite cookie and antiforgery for browser commands.

Authentication JSON is limited to64KB by bounded streaming, not by trusting Content-Length. Consistent application/problem+json errors expose safe detail/status/correlationId only; malformed JSON400, missing/expired/revoked session401, nonhuman session403, duplicates409, oversize413, exhausted budget429. Unexpected exceptions500 do not expose SQL/stack/credentials. Replies are no-store/nosniff and include X-Correlation-ID. Authentication budgets are lightweight in-process per worker, not a distributed anti-DDoS guarantee; multiple Flex workers/restarts have independent windows. Keep that limitation distinct from persisted SQL session validity.

Public deployment/profile credentials never form part of the application HTTP contract. SQL access is server-side only.