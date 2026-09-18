# Engine operations over HTTP

The Arke engine records every mutation as an operation — inserted once by key before the work,
completed once with its result after — and replays a retried request from that record rather than
doing the work twice. Its file-backed store is one process's memory; this is the same contract held
by the platform (aonik#327), so any host serving a workspace finds the same truth, and a host that
died mid-operation leaves a row that says so.

All routes require the `UserPolicy` and act as the caller's **party**. Access is the workspace's
(Spec 089 §8.1; Spec 095 §12 for a child's world): beginning or completing needs `Write`, reading
needs `Read`, and a key the caller may not read answers `404` whether or not it exists. Refusals
share the workspace problem shape, `{ "status": 409, "code": "operation-conflict", "message": "…" }`.

| Status | Code | When |
|---|---|---|
| 404 | `workspace-not-found` | Beginning against a workspace the caller cannot write, or that does not exist. |
| 404 | `operation-not-found` | No such key, or one about a workspace the caller cannot read. |
| 409 | `operation-conflict` | The key was begun for a different request, or completed earlier with a different result. |
| 422 | `invalid-request` | A key or fingerprint that is not a lowercase hex SHA-256, or an empty action. |

## What is kept

The engine's record, verbatim: its key (a hash over the trusted scope, actor, world and the
client's operation id), its fingerprint (action, resource, input, subject), the action, the
context and resource as JSON, and the result as JSON — carried from the start for a settlement,
written once at completion otherwise. The platform never interprets these; what the engine wrote
is what the engine reads. Reservation ids and job ids live inside the result, which is the stable
link between an operation and the usage and generation records it touched. Rows are never deleted.

## Calls

### `POST /operations` — begin

```json
{ "workspaceId": "…", "key": "8f39…", "fingerprint": "a1c2…", "action": "chapter-draft",
  "context": "{\"actorId\":\"…\",\"scopeId\":\"…\",\"executorId\":\"…\",\"subjectId\":\"…\"}",
  "resource": "{\"worldId\":\"…\",\"productionId\":\"…\",\"chapterId\":\"…\"}", "result": null }
```

`200` `{ "inserted": true, "operation": { …, "status": "started" } }`. Insert-if-absent: two hosts
beginning the same key at once get one row between them, and the one that lost the insert is
handed the row that won. Begun earlier, `inserted` is false and the row comes back **as it stands**
— `started` (an operation whose outcome is not yet known: the engine answers "uncertain" and never
re-runs it blind) or `completed` with its result (what a retried request replays). The same key
with a different fingerprint is `409 operation-conflict`.

### `POST /operations/{key}/complete`

```json
{ "fingerprint": "a1c2…", "result": "{\"proposal\":{…}}" }
```

`200` with the row. Completed once: the same result again is a replay and answers the row; a
different result, or a different fingerprint, is `409`.

### `GET /operations/{key}`

The row as recorded, or `404`.

## What a host does with these

Compose the engine with an `EngineOperationStore` whose `begin`, `complete` and `read` are these
three calls, keyed by the engine's own key and carrying the engine's own fingerprint. Nothing else
changes: replay, refusal of a reused id, and the `409 uncertain` answer for a started-but-never-
completed operation are the engine's behaviour over any store, and are now the platform's memory
rather than one process's file. Reconciling an uncertain operation — against the workspace's
revisions, the generation jobs and the usage reservations its result names — is the host's work,
and this record is what it reconciles against.

## Not in this slice

The atomic hosted-writer claim — the ownership decision made together with the authoritative write
so that a replaced worker cannot finalize under stale ownership — is the shared design of
ArkeStudio#468 and aonik#322, not this record. A started row is a durable "uncertain", not a lease.
