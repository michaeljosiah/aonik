# Document & File Model (Flexible Evidence)

> **Status — implemented in `Aonik.Documents`.** Per [Spec 035](../specifications/035.extract-documents-module.html) / [ADR-009](../decisions/009-extract-documents-module.md), the **generic** document capability (`Document`, `DocumentFile`, `DocumentVersion`, plus `DocumentIngestion`/`DocumentExtraction`) now lives in the first-class sibling module **`Aonik.Documents`**, consumed everywhere through `SharedKernel.Abstractions.Documents` (`IDocumentReader`/`IDocumentWriter`/`IDocumentSearch`) — no module references `Aonik.Documents` directly. Compliance keeps **`DocumentUsage`/`DocumentVerification`** and resolves documents by id through `IDocumentReader`. Indexable documents are auto-ingested into the party-scoped vector store by an event-driven pipeline (`DocumentUploadedEvent` → Worker), and are searchable by agents under a mandatory tenant + owner-party scope. The original "suggested placement in `Aonik.Platform`" below is **superseded**; this file is retained as the conceptual model. See the [Module placement](#module-placement-implemented) section for the current home.

This document proposes a flexible, purpose-agnostic structure for representing documents composed of one or more files (e.g., ID cards, proof of address, bank statements, contracts, invoices, or any other evidence). The model is designed for multi-tenant, multi-purpose use with blob/object storage and strong auditability.

## Goals

- Support **documents made up of multiple files** (front/back ID, multi-page statements, PDFs + images, etc.).
- Keep **purpose separate** from the document itself (ID verification, address verification, underwriting evidence, etc.).
- Represent files stored in **blob/object storage** with flexible metadata and versioning.
- Preserve **auditability** and avoid direct mutation of financial state.
- Allow **multiple usages** of the same document across workflows (KYC, onboarding, dispute, compliance review, etc.).

## Plan

1. **Define the core entities** that separate evidence (Document) from physical artifacts (DocumentFile) and purpose (DocumentUsage).
2. **Add optional versioning** to support re-submission without mutating prior evidence.
3. **Capture verification outcomes** separately to preserve auditability and AI governance.
4. **Map relationships and example scenarios** to validate multi-file and multi-purpose use cases.
5. **Note storage/security considerations** and clean-architecture placement for implementation guidance.

## Core Concepts

### 1) Document (Logical Evidence Container)
A Document is a logical container representing the “evidence” concept, independent of specific verification workflows.

**Key fields (illustrative):**
- `DocumentId` (Guid)
- `TenantId` (Guid)
- `OwnerPartyId` (Guid) — Person/Business that owns the evidence.
- `DocumentType` (string) — e.g., `NationalId`, `DriverLicense`, `UtilityBill`, `BankStatement`, `Contract`, `Other`.
- `Status` (string) — `Draft`, `Submitted`, `Verified`, `Rejected`, `Expired`, `Revoked`.
- `IssuedOn` (DateTime?)
- `ExpiresOn` (DateTime?)
- `IssuerName` (string?)
- `CountryCode` (string?)
- `ReferenceNumber` (string?) — e.g., ID number (store encrypted/hashed or masked, per compliance policy).
- `Tags` (List<string>) — flexible categorization (`kyc`, `address`, `income`, `fraud-review`, `dispute`, etc.).
- `AttributesJson` (string?) — extensible JSON for extra fields (keep schema versioned if needed).
- `CreatedByUserId` (Guid?)
- `CreatedAt`, `UpdatedAt` (DateTime)

**RAG / classification fields (Spec 035):**
- `Classification` (enum) — `Public`, `Internal`, `Personal`, `Sensitive`, `Restricted`. Drives the index decision and the retrieval scope (Personal → tenant + owner-party; Sensitive → + purpose; Restricted → never indexed).
- `Source` (string) — `CustomerUpload`, `AdminUpload`, `StatementImport`, `PartnerCallback`.
- `IndexStatus` (enum) — `NotIndexable`, `Pending`, `Indexed`, `Failed`.
- `IndexedAt` (DateTime?) — when the document last became searchable.

**Notes:**
- Avoid embedding business logic in the entity. Business rules should live in application services.
- Keep PII handling policy-driven (hash, encrypt, mask). Prefer IDs and references.

### 2) DocumentFile (Physical File Reference)
A DocumentFile represents a physical file stored in blob/object storage.

**Key fields (illustrative):**
- `DocumentFileId` (Guid)
- `DocumentId` (Guid)
- `StorageProvider` (string) — `AzureBlob`, `S3`, `GCS`, `MinIO`, etc.
- `StorageContainer` (string?)
- `StorageKey` (string) — object key/path in storage.
- `ContentType` (string)
- `FileName` (string?)
- `FileSizeBytes` (long?)
- `Sha256` (string?) — integrity check.
- `PageIndex` (int?) — for ordering multi-page files.
- `Side` (string?) — `Front`, `Back`, `Other`.
- `CapturedAt` (DateTime?)
- `CapturedBy` (string?) — device/app info.
- `MetadataJson` (string?) — OCR hints, image dimensions, etc.
- `ExtractedTextStatus` (enum, Spec 035) — `Native`, `OcrRequired`, `OcrDone`, `Unsupported`. Tells the ingestion pipeline whether embeddable text is available now or must be deferred to OCR.
- `CreatedAt` (DateTime)

**Notes:**
- Storage references are abstracted to support any blob store.
- Consider pre-signed URL generation through a storage service (not stored in the entity).

### 3) DocumentVersion (Optional)
A DocumentVersion captures immutable snapshots for re-submissions or updates (e.g., new proof after rejection).

**Key fields:**
- `DocumentVersionId` (Guid)
- `DocumentId` (Guid)
- `Version` (int)
- `Status` (string) — `Draft`, `Submitted`, `Verified`, `Rejected`
- `SubmittedAt` (DateTime?)
- `DecisionedAt` (DateTime?)
- `DecisionReason` (string?)

**Notes:**
- A version can reference a set of `DocumentFile` rows if you choose to attach files per version.

### 4) DocumentUsage (Purpose/Workflow Link)
A DocumentUsage links a Document to a *purpose* or workflow without duplicating evidence.

**Key fields (illustrative):**
- `DocumentUsageId` (Guid)
- `DocumentId` (Guid)
- `Purpose` (string) — `IdVerification`, `AddressVerification`, `IncomeVerification`, `DisputeEvidence`, `ContractEvidence`.
- `OwnerPartyId` (Guid)
- `RelatedEntityType` (string) — e.g., `KycCase`, `Order`, `BillingInvoice`, `ComplianceCase`.
- `RelatedEntityId` (Guid)
- `Status` (string) — `Pending`, `Satisfied`, `Rejected`, `Expired`.
- `VerifiedByUserId` (Guid?)
- `VerifiedAt` (DateTime?)
- `Notes` (string?)

**Notes:**
- This keeps “purpose” separate from the evidence itself and supports reuse.

### 5) DocumentVerification (Optional Result Artifact)
A DocumentVerification captures the decisioning record for a usage/purpose.

**Key fields:**
- `DocumentVerificationId` (Guid)
- `DocumentUsageId` (Guid)
- `Decision` (string) — `Approved`, `Rejected`, `ManualReview`.
- `DecisionReasonCode` (string?)
- `DecisionNotes` (string?)
- `VerifierType` (string) — `Human`, `Policy`, `Vendor`, `AI`.
- `VerifierId` (string?)
- `AiRunId` (Guid?) — if AI contributed.
- `CreatedAt` (DateTime)

**Notes:**
- Aligns with AI governance: AI can propose/assist, not directly mutate financial state.

## Relationships (High-Level)

- **Document 1..n DocumentFile** (one document, many files)
- **Document 1..n DocumentUsage** (one document can serve many purposes)
- **DocumentUsage 0..n DocumentVerification** (multiple decisions over time)
- **Document 1..n DocumentVersion** (optional versioning)

## Example Scenarios

### A) ID Verification (Front + Back)
- Document (`DocumentType=NationalId`)
- Two DocumentFiles: `Side=Front`, `Side=Back`
- DocumentUsage: `Purpose=IdVerification`, `RelatedEntityType=KycCase`
- DocumentVerification: `Decision=Approved`, `VerifierType=Vendor`

### B) Address Verification (Utility Bill PDF)
- Document (`DocumentType=UtilityBill`)
- DocumentFile: `ContentType=application/pdf`, `PageIndex=null`
- DocumentUsage: `Purpose=AddressVerification`

### C) Multi-Purpose Evidence
- Document (`DocumentType=BankStatement`)
- DocumentUsage: `Purpose=IncomeVerification`
- DocumentUsage: `Purpose=DisputeEvidence`

## Storage & Security Considerations

- Use a **storage abstraction** service to issue signed read/write URLs.
- Store **only metadata + storage key** in the database.
- Encrypt sensitive fields (e.g., ID numbers) or store hashed/masked values.
- Keep **audit logs** for uploads, updates, and verification decisions.
- Consider **retention policies** and `ExpiresOn` for regulatory compliance.

## Module Placement (Implemented)

Superseding the original Platform-centric suggestion (Spec 035 / ADR-009):

- **`Aonik.Documents`** (new sibling module): the **generic** document capability — `Document`, `DocumentFile`, `DocumentVersion`, `DocumentIngestion`, `DocumentExtraction`; `DocumentService` (create/upload/list/get via `IDocumentReader`/`IDocumentWriter`); `DocumentIndexer` + `DocumentIngestionHandler` (the async extract→chunk→embed→upsert pipeline); `/documents/*` endpoints with customer-accessible policies. References only `SharedKernel`.
- **`Aonik.Platform` / Compliance** (consumer): keeps `DocumentUsage` and `DocumentVerification` (+ `DocumentVerificationService`), storing the document id as a plain `Guid` and resolving detail through `IDocumentReader`. No EF navigation to `Document`.
- **`SharedKernel.Abstractions.Documents`**: the cross-module boundary — `IDocumentReader`/`IDocumentWriter`/`IDocumentSearch`/`IDocumentVectorIndex`/`IDocumentFileStore`/`IDocumentTextExtractor`, the DTOs, `DocumentClassification`/`DocumentIndexStatus`/`ExtractedTextStatus` enums, and the `DocumentUploaded`/`DocumentIndexed`/`DocumentDeleted` integration events.
- **`Aonik.Infrastructure`**: `DocumentFileStore` (blob), `ScopedDocumentVectorIndex` (party-scoped RAG over Qdrant), `DocumentTextExtractor` (+ deferred OCR hook) — all injected at the composition root; Documents never references Infrastructure.
- **`Aonik.Worker`**: hosts the outbox dispatcher that runs the ingestion handler, plus the opt-in `DocumentIngestionBackfillJob`.
- **Migrations** stay in the single `AonikDbContext` stream; `DocumentsDbContext` is runtime-only DI scoping with no migrations (ADR-005/006).

## Notes on Extensions

- Add `DocumentBundle` if you want grouped evidence across multiple parties (e.g., business + directors).
- Add `DocumentPolicy` for validation requirements per `Purpose` and `DocumentType`.
- `DocumentExtraction` (OCR / structured extraction outputs with `AiRunId`) is now a first-class entity in `Aonik.Documents`; a real OCR adapter behind `IDocumentOcrExtractor` is the remaining follow-up.
