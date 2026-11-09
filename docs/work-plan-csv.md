# Work plan CSV — schema 1

The download filename is exactly `work-plan.csv`. It contains project metadata and normalized plan records, never document bytes. Encoding is strict UTF-8; import accepts one leading BOM. Delimiter is semicolon, with RFC 4180 double-quote escaping for delimiters, quotes and newlines. Output record endings are CRLF; import accepts CRLF or LF. Embedded whitespace, Unicode and newlines remain unchanged.

## Exact columns

`SchemaVersion;ProjectId;BaseRevision;Kind;Id;ParentId;SortOrder;Title;Description;Status;Workflow;UpdatedAt;AssignmentMode;AssignedAgentId;Resolution;Evidence;EvidenceIsNull;CompletedBy;CompletedAt`

- SchemaVersion is `1` on every record. ProjectId and BaseRevision must agree throughout the file. GUIDs use their full nonempty D format; revisions are nonnegative invariant integers.
- The first data record is the single Project row. Its Id equals ProjectId, Title is the project name, Description/Status/Workflow are project metadata, UpdatedAt is the round-trip timestamp including its offset. Other fields are empty.
- Following rows are Phase, Subphase, Step or Decision, exactly once per stable ID. ParentId follows the hierarchy and must belong to this snapshot. SortOrder is a nonnegative integer, unique within each sibling record type; sparse order values are accepted. Workflow and UpdatedAt are empty on item rows.
- Phase/Subphase Status remains a validated derived-state enum for compatibility. Step Status is the underlying manual Pending/InProgress/Completed only; manual Blocked is rejected 400 without a write. A blocked Step exports its preserved base Status, and the Pending Decision reconstructs effective Blocked when reimported. Decision Status uses Pending or Decided; decided records require Resolution. A Decision parent may be the real ProjectId root or a structural item; no fictitious project Phase is used.
- AssignmentMode is Inherit, Override, or Unassigned. Only Override contains AssignedAgentId; the API validates owner membership and active new assignments.
- Evidence is absent or the exact workflow enum WorkDone, OwnerApproved, MergeConfirmed, PushConfirmed. EvidenceIsNull is `1` for null or `0` for a supplied value. It is an explicit null marker, not permission to invent evidence strings. CompletedBy and CompletedAt are optional audit GUID/timestamp fields. The API, not the file, authorizes closure and stamps trusted actor/time. Existing completed audit history is preserved; new imported completions are server-validated.
- Timestamps use invariant .NET round-trip `O` format. Inapplicable fields must be empty. Headers, types and enum names are case-sensitive; malformed quoting, duplicate IDs, mixed projects/revisions, unsupported versions and invalid parentage fail safely.

## Limits and safe update procedure

The CSV is bounded to 1 MiB including a supplied BOM, and at most 5000 plan items plus one Project metadata row. Oversize returns 413; malformed content returns 400. Every Phase requires at least one Subphase, including after import.

1. GET the exact project by GUID and download its current export.
2. Edit permitted metadata, records, order, assignments, states or decisions without changing identity/version/base revision.
3. POST the raw CSV to the same project's work-plan/import endpoint. A different ProjectId is rejected, never inferred by name.
4. Missing existing records fail unless `X-PlanForge-Replace: true` explicitly authorizes a full replacement. This is an intentional deletion contract, not a patch format. Removing Phases removes only their links; physical Library resources survive and remain assigned elsewhere or become orphaned.
5. A successful atomic import increments revision once and emits the actor-correct event. A stale revision returns 409 with no partial changes; reload, compare and reconcile instead of automatically retrying an overwrite.

The pure codec preserves all supported snapshot fields and never authorizes, persists, rewrites closure evidence or generates IDs. The application layer owns authorization, safe replacement, concurrency and workflow policy. CSV fields are untrusted user text, not spreadsheet formulas to execute; the Web must escape their display.

## v0.4.0 compatible schema and navigation

Schema1 and the exact public columns are unchanged. Project/Phase/Subphase Status remains a validated enum for compatibility with v0.2.0 exports, but importing it does not impose parent state: the application recalculates effective states from descendants and scoped pending decisions. Step manual states, workflow evidence, human approval and Decision resolution validation remain authoritative. Export shows the actual calculated parent values.

CreationOrder is intentionally absent from CSV. Reimporting a retained Step ID preserves its immutable stored age, even after editing or reordering; omitted IDs require the same explicit replacement permission. New IDs receive the import transaction's new creation-event order, with actual Phase/Subphase/Step hierarchy and SortOrder breaking equal-age ties before stable identifiers, not arbitrary supplied historical dates. Historical negative ranks are reconstructed and documented in architecture/deployment, not fabricated timestamps.

After a deliberate successful Web import, the Web selects the oldest effectively InProgress Step, otherwise oldest effectively Pending, otherwise the Project, revealing necessary ancestors and using reduced-motion-aware scrolling only when the target needs revealing. Blocked/Completed are excluded. Retained IDs preserve immutable age through repeated imports. Failed 400/409 imports retain graph/selection with contextual feedback. Agent/API imports and ordinary polling update data/badges without changing valid manual selection, expansion, focus or page scroll; if selected work disappears, use its nearest surviving ancestor.
