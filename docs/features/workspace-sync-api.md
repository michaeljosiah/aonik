# Workspace sync over HTTP

The platform's own endpoints for opening a workspace, moving bytes in and out of it, and committing
revisions (Spec 089 §6–§8, Spec 091 §5–§6). They are the calls a product host — first, the private
Arke Kidz Node backend (ArkeKidz#16) — uses to treat a workspace as the permanent record of a folder
it materialises and commits back. Nothing here knows what the files mean.

All routes sit under `/workspaces`, require the `UserPolicy` (a signed-in user of the tenant), and act
as the caller's **party**, resolved server-side by `ICurrentPartyResolver`. The request never says who
the caller is; a user with no party gets `403 no-party` before any workspace is looked at.

## Access, and what a caller can learn

Every workspace-scoped call resolves the caller's effective access first (`Owner` for the owning
party, else the level on an active Spec 086 grant, else `None`) and enforces it in the module, not in
a product (Spec 089 §8.1):

| Call | Requires |
|---|---|
| get, manifest, revisions, negotiate, download | `Read` |
| upload, commit, resolve | `Write` |

`None` — whether the workspace does not exist or belongs to someone else — always answers
`404 workspace-not-found` with an identical body, so a family cannot enumerate a tenant. A caller with
`Read` calling a `Write` route gets `403 insufficient-access`. A content hash that the caller has not
uploaded, or that only another workspace names, is reported as **missing**, never as forbidden
(Spec 089 §12): the hash is a name, not a capability.

## Refusals

Every refusal these endpoints make on purpose has one shape:

```json
{ "status": 409, "code": "missing-content", "message": "…", "missingHashes": ["…"] }
```

| Status | Code | When |
|---|---|---|
| 403 | `no-party` | The signed-in user is linked to no party. |
| 403 | `forbidden` | The caller may not act for the named billing subscriber. |
| 403 | `insufficient-access` | A `Write` route with `Read` access. |
| 404 | `workspace-not-found` | No access, or no such workspace. |
| 404 | `content-not-found` | No revision of this workspace names the hash. |
| 402 | `allowance-exceeded` | The billing subscriber's `workspaces` or `workspace-bytes` ceiling is full. |
| 409 | `commit-id-reused` | The `commitId` was used for a different tree. |
| 409 | `missing-content` | The manifest names content the caller does not possess; `missingHashes` lists it. |
| 409 | `contention` | The head moved under every re-classification attempt; re-read and try again. |
| 411 | `length-required` | An upload without `Content-Length`. |
| 413 | `too-large` | A single-shot upload above `Workspaces:MultipartThresholdBytes` (default 32 MiB). |
| 422 | `content-hash-mismatch` / `declared-length-mismatch` | The uploaded bytes are not what was declared. |
| 422 | `invalid-request` | A malformed manifest (path traversal, collisions, reserved prefix). |

Validation failures raised before the module is reached (a missing name, a non-hex hash) use
FastEndpoints' standard `422` error response.

## Calls

### Create — `POST /workspaces`

```json
{ "name": "Pip's Harbour", "kind": "world", "billingSubscriber": { "kind": "group", "id": "…" } }
```

`kind` defaults to `world`. `billingSubscriber` defaults to the caller's own party; a consumer product
names the family `Group` (Spec 087) so a member's workspace draws on the family plan. The meter refuses
a subscriber the caller may not act for, so this cannot bill a stranger. The `workspaces` slot is
claimed **before** the row exists, so a refused claim leaves nothing behind.

`201 Created`, `Location: /workspaces/{id}`:

```json
{
  "id": "5f0c…", "kind": "world", "name": "Pip's Harbour", "slug": "pips-harbour",
  "ownerPartyId": "9a1e…", "headRevisionId": null, "fileCount": 0, "totalBytes": 0, "status": "active"
}
```

### Open — `GET /workspaces/{id}` and `GET /workspaces`

The summary above, including `headRevisionId` — what a client materialises from and names as the
parent of its next commit. `GET /workspaces` lists the workspaces the caller's party **owns**; grants
are a separate question answered by Spec 086's routes.

### Manifest — `GET /workspaces/{id}/manifest[?revisionId=…]`

The complete manifest of the named revision, or of the head:

```json
{
  "revisionId": "c2a0…",
  "entries": [
    { "path": "characters/pip.md", "contentHash": "3b7e…", "sizeBytes": 412, "contentType": "text/markdown" },
    { "path": "world.json", "contentHash": "9d0f…", "sizeBytes": 118, "contentType": "application/json" }
  ]
}
```

A workspace with no commit yet answers `"revisionId": null` and no entries.

### Revisions — `GET /workspaces/{id}/revisions[?take=50]`

Newest first, at most 500. `state` is one of `fast-forward`, `diverged`, `accepted`, `rejected`,
`superseded`.

```json
{ "revisions": [ { "id": "…", "sequence": 3, "parentRevisionId": "…", "authorPartyId": "…",
                   "message": "Pip is born", "committedAt": "2026-09-17T18:25:05Z",
                   "fileCount": 2, "totalBytes": 530, "state": "fast-forward" } ] }
```

### Negotiate — `POST /workspaces/{id}/negotiate`

```json
{ "contentHashes": ["3b7e…", "9d0f…"] }
```

```json
{ "missing": ["9d0f…"] }
```

Which of the hashes the **caller** does not possess and cannot reach through this workspace. Answered
without transferring anything, so an unchanged tree syncs in one round trip. Negotiation and commit
answer the same question, which is what makes the protocol terminate.

### Upload — `PUT /workspaces/{id}/blobs/{contentHash}`

The body is the raw bytes (`Content-Type: application/octet-stream`); `Content-Length` is the
declared length and is mandatory, because quota is claimed against the declaration before the stream
is read. The route's hash is the declared hash. The staged bytes are compared against both before
promotion and discarded on any difference.

```json
{ "contentHash": "3b7e…", "sizeBytes": 412, "alreadyPresent": false }
```

`alreadyPresent: true` means the tenant already held these bytes; the caller's possession is recorded
all the same, and the upload cost nothing. Blobs above the single-shot limit use Spec 091 §7's
resumable upload, which is not yet exposed over HTTP.

### Download — `GET /workspaces/{id}/blobs/{contentHash}`

The bytes, with the manifest's `contentType` where one was recorded. `Read` access is necessary but
not sufficient: a revision of **this** workspace must name the hash. Divergent and rejected revisions
count — their bytes are this workspace's history — but identical bytes in another workspace do not.

### Commit — `POST /workspaces/{id}/commits`

```json
{
  "commitId": "0b2f7c1e-…",
  "parentRevisionId": "c2a0…",
  "message": "Chapter one saved",
  "manifest": [
    { "path": "characters/pip.md", "contentHash": "3b7e…", "sizeBytes": 412, "contentType": "text/markdown" },
    { "path": "productions/pip-won-t-swim/chapters/the-harbour.md", "contentHash": "77a1…", "sizeBytes": 58 },
    { "path": "world.json", "contentHash": "9d0f…", "sizeBytes": 118, "contentType": "application/json" }
  ]
}
```

- `commitId` is chosen by the client **once, before the first attempt**, and reused unchanged on every
  retry (Spec 089 §6.1). A product host derives it from its durable operation identity.
- `parentRevisionId` is the head the client built on; `null` only for a first commit.
- `manifest` is complete, never a delta. Paths are relative, forward-slash, NFC; a manifest is rejected
  as malformed for traversal, reserved `.aonik/` names, case or prefix collisions.

```json
{ "outcome": "fastForward", "revisionId": "e41d…", "sequence": 4, "headRevisionId": "e41d…" }
```

| `outcome` | Meaning |
|---|---|
| `fastForward` | The parent was the head; the head advanced to `revisionId`. |
| `diverged` | The parent was **not** the head. Stored and sequenced, head unchanged; `headRevisionId` is the head it did not descend from, so the client can fetch that manifest and put the decision in front of a person (Spec 089 §7). Never merged by the platform. |
| `replayed` | The same `commitId` with the same tree: the stored outcome, no second revision. A lost response retried lands here. |

The same `commitId` with a **different** tree is `409 commit-id-reused` — loudly, never a silent replay
(§6.1.1). Content the caller has not uploaded refuses the whole commit as `409 missing-content` and
creates no revision: upload first, commit second.

### Resolve — `POST /workspaces/{id}/revisions/{revisionId}/resolve`

```json
{ "resolution": "accept" }
```

`accept` advances the head through a **new** revision parented on the current head — never by
rewriting history; `reject` releases the revision's bytes after the retention window; `supersede`
records that a third tree replaced it. `{ "resolved": false }` means the revision was not divergent or
was already resolved; resolving twice does nothing the second time.

## What a client does with these

A save-and-reopen cycle, as the Arke Kidz host performs it:

1. `GET /workspaces/{id}` → `headRevisionId`; `GET …/manifest` → entries; `GET …/blobs/{hash}` for each
   entry → the working copy, rebuilt from nothing.
2. Work happens in the working copy under the engine's own gate.
3. On the engine's authoritative-save hook: scan the folder → `POST …/negotiate` → `PUT …/blobs/{hash}`
   for each missing hash → `POST …/commits` with `parentRevisionId` = the materialised head and
   `commitId` derived from the operation key.
4. `fastForward` or `replayed` acknowledges the save; `diverged` does not — the operation stays
   uncertain and a person resolves it.

## Not in this slice

Child-owned workspaces (the owner cannot authenticate; the guardian acts), guardian-consent reads,
allowance reservation for generation, safety-decision reads and operation reconciliation are the
next calls in aonik#326, alongside aonik#327/#328/#323. The resumable multipart upload (Spec 091 §7)
exists as a service and is not yet exposed. The atomic hosted-writer fence remains aonik#322 /
ArkeStudio#468: the parent check here stores a stale writer's commit as divergent, which is the
specified behaviour, but it is not a lease.
