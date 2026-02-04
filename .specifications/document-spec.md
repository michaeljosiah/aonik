# document-spec

## Objective
Design a comprehensive Admin UI experience for managing compliance documents that mirrors existing Admin UI patterns and is grounded in the document-related entities in the data model.

## Context & Data Model Anchors
Document management is driven by the Compliance domain entities and their relationships:
- **Document**: Core record that defines owner (party), document type, status, issuer, validity windows, tags, and arbitrary attributes metadata.
- **DocumentFile**: Physical file payload(s) tied to a document, with storage provider metadata, MIME type, hashes, file size, capture metadata, and optional page/side indexing.
- **DocumentUsage**: Contextual usage records tying a document to a business purpose and optional related entity (order, compliance case, party role, etc.), with status and verification checkpoints.
- **DocumentVerification**: Decision trail tied to a usage record (decision, reason, notes, verifier type/id, optional AiRunId).
- **DocumentVersion**: Versioning lifecycle and decision timestamps for the document.

This plan must explicitly expose these concepts in the Admin UI so compliance operations can view, validate, and trace document provenance.

## Design Principles (Admin UI Pattern Alignment)
- **Cards + Tables + Tabs**: Follow existing patterns in Tenants list, Media Library, and Customer Detail pages (cards for sectioning, table listing, tabbed detail surface).
- **Action-first workflows**: Surface primary actions (upload, verify, link usage) at the top of detail views.
- **Auditability**: Expose document lifecycle timelines and verification history; show AiRunId where present.
- **Multi-tenancy**: Ensure document data is always scoped to the current tenant context.

## Information Architecture
### Navigation
- **Compliance → Documents** (new primary node).
- Related surfaces:
  - **Documents list** (global compliance inventory).
  - **Document detail** (single document view).
  - **Inline document panel** under Customer Detail → Documents tab (read-only summary with link to full document).

### Core Screens
1. **Documents List (Inventory View)**
2. **Document Detail (Single Record)**
3. **Document Creation & Upload (Wizard/Modal)**
4. **Usage & Verification Panel (Detail sub-section)**

## Screen-Level Plan

### 1) Documents List
**Purpose**: Central inventory for compliance analysts.

**Key elements**
- **Header**: “Documents” with quick stats (total, expiring soon, pending verification).
- **Filters (left-aligned, inline)**:
  - Document Type
  - Status (Draft, Pending, Approved, Rejected, Expired, etc.)
  - Owner Party (search by name or ID)
  - Country Code
  - Issued / Expires date range
  - Tags (multi-select or search)
  - Usage Purpose
- **Search**: Keyword search on reference number, issuer, file name, tag, storage key.
- **Table Columns**:
  - Document Type
  - Owner (Party)
  - Status
  - Issued On
  - Expires On (highlight if within 30 days)
  - Country
  - Files count
  - Last Updated
  - Actions (View, Download latest file)

**Interaction patterns**
- Pagination with existing `DataTablePagination` pattern.
- Row click → Document Detail.
- Batch selection for bulk export or bulk status review (future).

### 2) Document Detail
**Purpose**: A holistic view of a single document and its lifecycle.

**Layout**
- **Header**: Document Type + Status badge + Owner Party reference; quick actions.
- **Tabs**: Overview, Files, Usage, Versions, Verification, Activity (audit log placeholder).

**Overview Tab**
- Summary card with:
  - Issuer Name, Country Code, Reference Number
  - Issued/Expires dates
  - Tags and Attributes JSON (render as key-value preview)
  - Created/Updated timestamps
- Status history strip (if versioning data available).

**Files Tab**
- List of `DocumentFile` entries.
- File metadata: content type, size, page index, side, captured at/by.
- Preview / download actions (open via storage proxy).
- Surface SHA-256 hash for validation.

**Usage Tab**
- Table of `DocumentUsage` entries with:
  - Purpose
  - Related Entity Type + ID
  - Status (Pending/Satisfied/Rejected)
  - Verified At / By
  - Notes
- Inline link to related entity (order, compliance case, party, etc.).

**Verification Tab**
- Timeline of `DocumentVerification` events:
  - Decision, reason, notes
  - Verifier Type (Human/AI/External)
  - Verifier ID
  - AiRunId (link to AI run details if present)

**Versions Tab**
- Version list (version number, status, submitted/decisioned timestamps, decision reason).
- Ability to compare versions (future).

### 3) Document Creation & Upload
**Purpose**: Ensure documents are created with consistent metadata and files attached.

**Flow**
- Step 1: Document metadata (OwnerPartyId, DocumentType, Status, Issued/Expires, Issuer, Country, Reference, Tags, Attributes).
- Step 2: Upload file(s) (storage provider selection, content type detection, file name, size, hash).
- Step 3: Optional usage linking (Purpose, RelatedEntityType, RelatedEntityId, Status, Notes).

**Validation**
- Document type required.
- Storage provider + storage key required per file.

### 4) Inline Documents in Customer Detail
**Purpose**: Provide quick compliance context for a party.

**Experience**
- Show document summary cards (type, status, expiry, usage statuses).
- Link to full document detail.

## Data & API Integration Plan
- **Service layer**: create `documentService` in Admin UI to call compliance endpoints:
  - `POST /compliance/documents`
  - `GET /compliance/documents/{id}`
  - `POST /compliance/documents/{id}/files`
  - `POST /compliance/documents/{id}/usages`
  - `POST /compliance/document-usages/{id}/verifications`
- **Types**: add compliance document types in `src/types` mirroring API contracts.
- **List endpoint**: if not available, add a `GET /compliance/documents` listing endpoint in API (paged) with filters.

## UI Components to Reuse
- `Card`, `Badge`, `Tabs`, `Breadcrumb`, `DataTablePagination`, `Button`, `Input`, `Select`.
- Status badges should follow existing color patterns (success/warning/error).

## Edge Cases & Operational Considerations
- Documents with no files (metadata-only entries) → show empty state with CTA to upload file.
- Expired documents → highlight in list and detail with warning badge.
- Multiple files per document (front/back) → use side + page index metadata.
- AI verification decisions → render AiRunId with link to AI run details.
- Attributes JSON should be displayed safely (read-only with expand option).

## Compliance & Audit Requirements
- All decisions must be traceable to a `DocumentVerification` and/or `AiRunId`.
- UI must display timestamps consistently (UTC with localized display).
- No direct mutation of financial state; compliance decisions remain within document/usage status updates.

## Implementation Sequence
1. **API surface check**: confirm list endpoint availability; add if missing.
2. **Admin UI routing**: add “Compliance → Documents” navigation item.
3. **Document list page** with filters, pagination, and summary metrics.
4. **Document detail page** with tabs (Overview, Files, Usage, Verification, Versions).
5. **Document create/upload flow** (modal or dedicated page).
6. **Customer detail enhancement** (documents summary in tab).
7. **Testing**: UI smoke, state handling, and error states.

## Acceptance Criteria
- Document list supports filtering by status, type, owner, and expiry windows.
- Document detail renders all related entities (files, usages, verifications, versions).
- Admins can create documents, upload files, and add usage/verification entries.
- UI surfaces AI decision metadata where available.
- Pattern consistency with existing Admin UI pages.
