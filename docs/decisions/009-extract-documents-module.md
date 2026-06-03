# ADR-009: Extract Documents into Its Own Sibling Module

**Status**: Accepted — Phases 0–4 landed (contracts + party-scoped vector index, module extraction, Compliance decouple, event-driven RAG ingestion, scoped agent search, legacy decommission + opt-in backfill, deletion / right-to-erasure). Remaining polish: signed read-URL (`GetReadUrlAsync`) and `/documents/*` request-validator coverage.
**Date**: 2026-06-02 (updated 2026-06-03)
**Decision Makers**: Development Team
**Related**: [ADR-005](005-adopt-module-first-modular-monolith.md), [ADR-006](006-extract-personal-finance-module.md), [Spec 035](../specifications/035.extract-documents-module.html)

## Context

Aonik has two unrelated things both called "documents", and neither lives where it should:

1. A **compliance evidence model** — `Document`, `DocumentFile`, `DocumentUsage`, `DocumentVerification`, `DocumentVersion` — inside the Compliance slice of `Aonik.Platform`. It conflates generic file storage with KYC verification, so a customer's personal document (e.g. a tax return) has no natural home: it is a document, but it has no `DocumentUsage` and no `DocumentVerification`, and the endpoints are gated behind `AdminUserPolicy` under `/compliance/documents` — unreachable for an end customer.

2. A **RAG upload endpoint** (`POST /ai/documents/upload`) in `Aonik.Api` that chunks, embeds, and upserts text into a single Qdrant `documents` collection — completely disconnected from the compliance model, `text/plain`-only, synchronous.

Documents are not a finance concept; they are a customer concept that Finance, PersonalFinance, Platform/Compliance, Ai, and Agents all need to consume. ADR-005 anticipated selective extraction once a subdomain earns its boundary, and ADR-006 / Spec 027 established the mechanics for doing so.

## Decision

Promote the **generic** document capability into a first-class sibling module `Aonik.Documents`, and refactor Compliance into a **consumer** of it.

### The two-concern split

| | Generic document capability | Compliance verification |
|---|---|---|
| Question | "A file exists, who owns it, where is it, can the AI read it?" | "Does this evidence satisfy a KYC purpose?" |
| Owner | `Aonik.Documents` (new) | `Aonik.Platform` / Compliance (existing) |
| Entities | `Document`, `DocumentFile`, `DocumentVersion`, `DocumentIngestion`, `DocumentExtraction` | `DocumentUsage`, `DocumentVerification` |
| Applies to a tax return? | Yes — stored, classified, indexed | No |

This mirrors the platform's existing Order-vs-Payment-vs-Ledger discipline: distinct concepts are never collapsed just because they co-occur. `DocumentUsage` keeps the document id as a plain `Guid` (the same pattern it already uses for `RelatedEntityId`) and resolves document detail through a `SharedKernel` read contract.

### Architectural guarantees (inherited from ADR-006)

1. **No database schema change beyond additive columns/tables.** Tables keep their `Ank` prefix; no table renames; no data migration. New fields (`Classification`, `Source`, `IndexStatus`, `IndexedAt`, `ExtractedTextStatus`) and new tables (`AnkDocumentIngestions`, `AnkDocumentExtractions`) are generated **only** by the EF CLI against `AonikDbContext`.
2. **Single migration stream stays in `AonikDbContext`.** `DocumentsDbContext` is runtime-only DI scoping with no migrations. The cost is one permanent ProjectReference: `Aonik.Infrastructure → Aonik.Documents`.
3. **No `ProjectReference` from any module to `Aonik.Documents`.** Consumers read/write/search documents exclusively through `SharedKernel.Abstractions.Documents` and integration events.
4. **HTTP contract care.** `/compliance/documents/*` usage/verification routes keep working; the legacy `/ai/documents/upload` route is removed only after the unified path is live.

### SharedKernel boundary

New contracts in `SharedKernel.Abstractions.Documents/`:

- `IDocumentReader` → `DocumentDto`, `DocumentFileDto`, `DocumentListItem` (read/list/get/signed-url)
- `IDocumentWriter` → `CreateDocumentCommand`, `UploadFileCommand` (create/upload)
- `IDocumentSearch` → `DocumentChunkHit` under a **mandatory** `DocumentSearchScope` (no unscoped overload)
- `IDocumentVectorIndex` → index / purge a document's chunks (implemented in Infrastructure over `IVectorStore`)
- `DocumentClassification`, `DocumentIndexStatus` enums

Integration events (in `SharedKernel.Events.Integration`): `DocumentUploadedEvent`, `DocumentIndexedEvent`, `DocumentDeletedEvent`.

### RAG-by-default via events

Every **indexable** document (gated by `DocumentClassification`) is auto-ingested into the vector store by an asynchronous, event-driven pipeline: upload writes blob + metadata and publishes `DocumentUploadedEvent`; a Worker job extracts text, chunks, embeds, and upserts. Upload never blocks on embedding. `Restricted` is never indexed; `Sensitive` is metadata-only until OCR + redaction are available.

## The vector-scoping correction (important)

The original Spec 035 draft claimed RAG vectors carried "no `tenant_id`". **That is wrong**, and this ADR records the correction. `QdrantVectorStore` already enforces **tenant** isolation fail-closed:

- `EnhancePayloadWithTenant` injects `tenant_id` on every upsert and throws if there is no tenant context.
- `BuildMergedFilter` always adds a `tenant_id` `must` clause on every search and scroll.

The genuine, narrower gap is:

1. **No `owner_party_id` (sub-tenant) isolation.** In a B2C product like Payabo, many individual customers live under one tenant. With tenant-only filtering, customer A's agent could retrieve customer B's documents. **This is the real P0** once personal documents (tax returns, statements) are indexed by default.
2. **No classification/purpose scoping** on vectors.
3. **`DeleteAsync` ignores its `filter` argument** (it only deletes by explicit point id), so a document-level vector purge needs a scroll-then-delete pass.

Phase 0 closes (1)–(3) additively via a `ScopedDocumentVectorIndex` in `Aonik.Infrastructure.VectorStore` that stamps `owner_party_id` / `classification` / `purpose` / `document_id` on every chunk, applies an `owner_party_id` filter on search on top of the existing tenant clause, and purges by scrolling `document_id` then deleting each point. Search scope is derived from authenticated context, never from model input.

## Phased rollout

| Phase | Status | Description |
|-------|--------|-------------|
| 0 | ✅ Landed | `SharedKernel.Abstractions.Documents` contracts + DTOs + events; `ScopedDocumentVectorIndex` (party/classification scoping + purge) registered in Infrastructure DI. Additive, no migration, no entity move. |
| 1 | ✅ Landed | `Aonik.Documents.csproj` + `DocumentsModule` + solution entry + `DocumentsDbContext`; `DocumentIngestion` / `DocumentExtraction` + new columns; **tool-generated** migration against `AonikDbContext`. |
| 2 | ✅ Landed | Moved `Document` / `DocumentFile` / `DocumentVersion` + EF configs with **namespaces preserved** (`Aonik.Platform.Entities.Compliance`) per ADR-006 Phase 2, so the migration was a single FK drop, not a destructive rebuild. |
| 3 | ✅ Landed | Split `DocumentService` (generic ops → Documents; verification ops → Compliance `DocumentVerificationService`); re-homed generic endpoints under `/documents/*` with customer-accessible policy; dropped the EF navigation on `DocumentUsage`. |
| 4 | ✅ Landed | Event-driven ingestion via an outbox `IEventHandler<DocumentUploadedEvent>` in the Worker (durable retry/back-off/dead-letter) + native/DOCX text extraction with a deferred `IDocumentOcrExtractor` hook; `IDocumentSearch` exposed as the read-only `documents_search` agent tool (scope from auth, never model input); legacy `DocumentUploadEndpoint` removed; opt-in `DocumentIngestionBackfillJob` (disabled by default); **deletion / right-to-erasure** (`DeleteDocumentAsync` + `DELETE /documents/{id}` purge vectors → remove blobs → soft-delete → `DocumentDeletedEvent`, which Compliance handles by marking dependent usages `Expired`, never deleting them); `Document-Model.md` updated. |

> **Toolchain note.** Phase 0 landed first as an entirely additive change (new SharedKernel files + one Infrastructure adapter + DI wiring) so it neither moved entities nor touched the migration stream — safe without a local build. Phases 1–4 then landed once the .NET toolchain was available to build and run `dotnet ef`, with the entity-move migration **tool-generated** against `AonikDbContext` per CLAUDE.md (never hand-authored). All five phases are now in; the remaining work is polish (signed read-URL, `/documents/*` request-validator coverage), not core capability. Agent-initiated deletion stays out of band — there is no in-band delete agent tool, so a future one must be High-tier per Spec 032.

## Consequences

### Positive

- Documents become a tenant-and-party-scoped substrate any module can consume through `SharedKernel`, so personal documents (tax returns, statements) get a real home independent of compliance.
- The party-level vector scoping fix closes a concrete B2C leakage risk before personal documents are indexed at scale.
- RAG-by-default makes a customer's evidence searchable by their agent without a second manual step — the AI-native payoff.

### Trade-offs

- **One permanent reverse reference**: `Aonik.Infrastructure → Aonik.Documents` (Infrastructure already references every module to host the canonical migration stream).
- **Embedding cost** scales with "index all"; mitigated by the classification gate, async batching, and per-tenant cost recorded on `DocumentIngestion`.
- **PII in a vector store** demands the erasure path; `Sensitive` defaults to metadata-only and deletion purges vectors.
- Namespaces on moved entities will be **deliberately preserved** (`Aonik.Platform.Entities.Compliance`) during Phase 2 to keep migration snapshot FQN strings intact, exactly as ADR-006 did — intentional, not an oversight.

## See Also

- [Spec 035](../specifications/035.extract-documents-module.html) — full specification (current-state inventory, classification policy, risk register).
- [ADR-006](006-extract-personal-finance-module.md) — the module-extraction precedent this follows.
- [ADR-005](005-adopt-module-first-modular-monolith.md) — module-first modular monolith.
