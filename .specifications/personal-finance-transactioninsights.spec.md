# Personal Finance Transaction Classification & Insights Specification

## Implementation Checklist

- [x] Create feature branch `feature/personal-finance-transaction-insights`
- [x] Add Phase 1 contracts/models for personal accounts and manual transactions
- [x] Implement Phase 1 account and transaction services
- [x] Implement Phase 1 personal finance endpoints (accounts + manual transactions)
- [x] Add/extend EF Core configurations and indexes for Phase 1 entities
- [x] Register new personal finance services in `FinanceModule`
- [x] Add automated tests for Phase 1 services
- [x] Run build/tests and record outcomes
- [x] Add Phase 2 contracts/models for statement import
- [x] Implement statement import entities and EF configurations
- [x] Implement statement import service (upload/parse/list/apply)
- [x] Implement statement import endpoints
- [x] Register statement import service in `FinanceModule`
- [x] Add automated tests for statement import service
- [x] Create EF migration for personal finance phase updates
- [x] Run build/tests and record Phase 2 outcomes
- [x] Implement classification rule management APIs (create/list/update/deactivate)
- [x] Implement classification review queue and accept/override APIs
- [x] Implement deterministic spending insights APIs (summary/category/merchant/account)
- [x] Implement AI narrative insight workflow with `AiRunId` linkage
- [x] Seed and document Personal Finance permissions
- [x] Add API integration tests for new personal finance endpoints
- [x] Wire Payabo frontend to personal finance account/transaction/import endpoints
- [x] Add Payabo classification review and insights pages
- [x] Run full solution tests and final verification for release readiness

## 1. Purpose

Enable Payabo users to:
- Manually log personal transactions.
- Upload bank/credit-card statements.
- Automatically classify transactions into spending categories.
- Review and correct classifications.
- Receive spending insights by category, merchant, and source account.

This capability is implemented in AONIK's Finance module Personal Finance domain and surfaced in Payabo.

---

## 2. Architectural Guardrails (Non-Negotiable)

- Ledger remains the source of financial truth for material money movement.
- Orders remain business intent and are not reused as personal spend records.
- Personal transaction tracking is a Personal Finance projection/model for budgeting and insights.
- AI suggestions are auditable and policy-governed.
- AI model/provider resolution must flow through `AiRoutePolicy` (no hardcoded model/provider).
- Financially material AI outputs must reference `AiRunId`.
- For high-risk changes (for example shared/global rules), use Propose -> Approve -> Apply pattern.

---

## 3. Current State Summary

- Data entities exist:
  - `PersonalTransaction`
  - `PersonalAccount`
  - `CategorisationRule`
- Household APIs exist.
- No Personal Finance APIs for transaction/account CRUD, statement import, or classification review.
- Payabo transaction UI currently shows bill-payment order history, not personal-spend transactions.
- AI insight workflow currently exists only for invoices.

---

## 4. Goals

- Support account-scoped transaction capture (`bank`, `credit_card`, `cash_wallet`, etc.).
- Support manual entry and CSV statement ingestion.
- Automatically classify transactions with confidence scoring.
- Allow user review and correction to improve future classification.
- Provide deterministic and AI-assisted spending insights.
- Keep all classification decisions auditable.

---

## 5. Out of Scope (MVP)

- Direct bank API integrations (Plaid/Open Banking) in MVP.
- OCR/PDF parsing in MVP (CSV-first).
- Real-time balance reconciliation against external institutions.
- Autonomous AI mutation of budgets/goals without explicit user action.

---

## 6. Domain Model Changes

## 6.1 New/Expanded Entities

### PersonalTransaction (extend existing)
- `Id` (Guid)
- `TenantId` (Guid)
- `UserId` (Guid)
- `PersonalAccountId` (Guid?)
- `SourceType` (string) // `manual`, `statement_import`, `system`
- `SourceId` (Guid) // statement row id or manual entry id
- `OccurredAt` (DateTime)
- `Amount` (decimal)
- `Currency` (string)
- `Merchant` (string?)
- `Description` (string?) **new**
- `Category` (string?)
- `Confidence` (decimal)
- `CategorisedBy` (string?) // `rule`, `ai`, `manual`
- `ClassificationMethod` (string?) **new**
- `ClassifierVersion` (string?) **new**
- `AiRunId` (Guid?) **new**
- `ReviewStatus` (string) **new** // `Pending`, `Reviewed`, `AutoAccepted`
- `ReviewedAt` (DateTime?) **new**
- `ReviewedByUserId` (Guid?) **new**
- `ImportFingerprint` (string?) **new**
- `Notes` (string?)
- `TagsJson` (string)

### PersonalAccount (extend existing)
- `AccountSubtype` (string?) **new** // e.g. `current`, `savings`, `visa`, `mastercard`
- `Last4` (string?) **new**
- `IsArchived` (bool) **new**
- `OpenedAt` (DateTime?) **new**
- `ClosedAt` (DateTime?) **new**

### CategorisationRule (extend existing)
- `MatchType` (string) **new** // `contains`, `regex`, `exact`, `mcc`, `amount_range`
- `CaseSensitive` (bool) **new**
- `MinAmount` (decimal?) **new**
- `MaxAmount` (decimal?) **new**
- `AppliesToAccountId` (Guid?) **new**
- `CreatedFromUserCorrection` (bool) **new**
- `Scope` (string) **new** // `User`, `Household`, `Tenant`
- `ApprovalStatus` (string) **new** // for governed scopes

### StatementImport **new**
- `Id` (Guid)
- `TenantId` (Guid)
- `UserId` (Guid)
- `PersonalAccountId` (Guid)
- `FileName` (string)
- `StorageUri` (string)
- `Format` (string) // `csv`
- `Status` (string) // `Uploaded`, `Parsed`, `Classified`, `Reviewed`, `Applied`, `Failed`
- `RowsTotal` (int)
- `RowsParsed` (int)
- `RowsImported` (int)
- `RowsDuplicate` (int)
- `RowsFailed` (int)
- `FailureReason` (string?)
- `StartedAt` (DateTime?)
- `CompletedAt` (DateTime?)

### StatementImportRow **new**
- `Id` (Guid)
- `TenantId` (Guid)
- `StatementImportId` (Guid)
- `RowNumber` (int)
- `OccurredAtRaw` (string?)
- `AmountRaw` (string?)
- `DescriptionRaw` (string?)
- `MerchantRaw` (string?)
- `CurrencyRaw` (string?)
- `NormalizedOccurredAt` (DateTime?)
- `NormalizedAmount` (decimal?)
- `NormalizedCurrency` (string?)
- `NormalizedDescription` (string?)
- `ParseStatus` (string) // `Parsed`, `Failed`, `Duplicate`
- `ErrorMessage` (string?)
- `Fingerprint` (string?)

### SpendingInsightSnapshot **new** (optional but recommended for performance/audit)
- `Id` (Guid)
- `TenantId` (Guid)
- `UserId` (Guid)
- `PeriodStart` (DateTime)
- `PeriodEnd` (DateTime)
- `Currency` (string)
- `MetricsJson` (string)
- `AiNarrativeInsightId` (Guid?)
- `GeneratedAt` (DateTime)

---

## 7. Persistence & Indexing

- Add EF configurations for all Personal Finance entities, including missing ones.
- Add indexes:
  - `PersonalTransactions`: (`TenantId`, `UserId`, `OccurredAt`)
  - `PersonalTransactions`: (`TenantId`, `UserId`, `Category`, `OccurredAt`)
  - `PersonalTransactions`: (`PersonalAccountId`, `OccurredAt`)
  - `PersonalTransactions`: (`ImportFingerprint`) unique where not null
  - `CategorisationRules`: (`TenantId`, `UserId`, `Priority`, `IsActive`)
  - `StatementImports`: (`TenantId`, `UserId`, `Status`, `CreatedAt`)
- Ensure table naming follows module prefix conventions (`Fin...` runtime mapping).

---

## 8. Service Layer Additions (Finance Module)

## 8.1 Contracts
Add interfaces under `Aonik.Finance.Contracts.Services.PersonalFinance`:
- `IPersonalAccountService`
- `IPersonalTransactionService`
- `IStatementImportService`
- `ITransactionClassificationService`
- `IPersonalFinanceInsightsService`

## 8.2 Responsibilities
- `IPersonalAccountService`: account CRUD/archive/list.
- `IPersonalTransactionService`: manual transaction CRUD/list/filter/search.
- `IStatementImportService`: upload registration, parse pipeline orchestration, import status, row-level errors.
- `ITransactionClassificationService`: classify single/batch; apply rule-first then AI fallback.
- `IPersonalFinanceInsightsService`: deterministic aggregates + optional AI narrative.

---

## 9. API Specification (FastEndpoints)

All routes under authenticated user policy.

## 9.1 Accounts
- `POST /personal-finance/accounts`
- `GET /personal-finance/accounts`
- `GET /personal-finance/accounts/{id}`
- `PATCH /personal-finance/accounts/{id}`
- `POST /personal-finance/accounts/{id}/archive`

## 9.2 Transactions
- `POST /personal-finance/transactions` (manual)
- `GET /personal-finance/transactions` (filters: date range, accountId, category, min/max amount, search, page/pageSize)
- `GET /personal-finance/transactions/{id}`
- `PATCH /personal-finance/transactions/{id}` (manual edits and category override)
- `POST /personal-finance/transactions/{id}/classify`
- `POST /personal-finance/transactions/classify-batch`

## 9.3 Classification Review
- `GET /personal-finance/classification/review-queue`
- `POST /personal-finance/classification/review/{transactionId}/accept`
- `POST /personal-finance/classification/review/{transactionId}/override`
- `POST /personal-finance/classification/rules` (create from correction)
- `GET /personal-finance/classification/rules`
- `PATCH /personal-finance/classification/rules/{id}`
- `POST /personal-finance/classification/rules/{id}/deactivate`

## 9.4 Statement Imports
- `POST /personal-finance/imports/statements` (multipart CSV + accountId)
- `GET /personal-finance/imports/statements`
- `GET /personal-finance/imports/statements/{id}`
- `GET /personal-finance/imports/statements/{id}/rows`
- `POST /personal-finance/imports/statements/{id}/apply`

## 9.5 Insights
- `GET /personal-finance/insights/spending-summary?period=month&anchorDate=...`
- `GET /personal-finance/insights/category-breakdown?...`
- `GET /personal-finance/insights/merchant-breakdown?...`
- `GET /personal-finance/insights/account-breakdown?...`
- `POST /personal-finance/insights/narrative` (returns Insight record + `AiRunId` reference where applicable)

---

## 10. Classification Pipeline

## 10.1 Deterministic Pass (always first)
1. Normalize merchant/description.
2. Evaluate active rules by priority.
3. If rule match:
   - set `Category`, `Confidence` (high), `CategorisedBy=rule`
   - set `ClassificationMethod=rule_engine`

## 10.2 AI Fallback
Trigger only when deterministic confidence below threshold.
- Build minimal prompt payload with IDs/references and normalized text.
- Resolve model/provider via AI module routing policy.
- Record `AiRun`.
- Persist result with:
  - category suggestion
  - confidence
  - rationale summary (non-PII heavy)
  - `AiRunId`
- Mark review status:
  - `AutoAccepted` if confidence >= tenant threshold
  - `Pending` otherwise

## 10.3 User Feedback Loop
- User overrides classification.
- System offers "create rule from this correction".
- New rule is:
  - auto-applied for user scope (low risk)
  - proposal/approval path for wider scopes.

---

## 11. Statement Import Pipeline

## 11.1 Upload
- Validate MIME, file size, required columns.
- Persist `StatementImport` in `Uploaded`.
- Store file in object storage.

## 11.2 Parse (background job)
- Read CSV.
- Normalize dates, amounts, signs (debit/credit convention).
- Compute fingerprint per row.
- Mark parse errors row-by-row, do not fail full file unless catastrophic.

## 11.3 Classify
- Run classification service on parsed rows.
- Update import counters and status.

## 11.4 Apply
- Convert eligible rows to `PersonalTransaction`.
- Skip duplicates using fingerprint unique rule.
- Complete import with metrics.

---

## 12. AI Governance & Audit Requirements

- Every AI classification/narrative generation has an `AiRun` record.
- No direct hard-coded model in Finance services.
- Keep prompts versioned and immutable by naming/version convention.
- Store `AiRunId` on transaction for AI-derived category.
- For governed/high-risk actions, enforce Propose -> Approve -> Apply.
- Log actor, tenant, source import, and classification provenance for auditability.

---

## 13. Permissions

Add and seed new permissions:
- `PersonalFinance.Accounts.Read`
- `PersonalFinance.Accounts.Write`
- `PersonalFinance.Transactions.Read`
- `PersonalFinance.Transactions.Write`
- `PersonalFinance.Imports.Create`
- `PersonalFinance.Imports.Read`
- `PersonalFinance.Classification.Run`
- `PersonalFinance.Classification.Review`
- `PersonalFinance.Insights.Read`

Map Payabo user policy to minimum needed set for self-scope operations.

---

## 14. Payabo Frontend Specification

## 14.1 New API Clients
- `Payabo/src/api/personalFinanceAccounts.ts`
- `Payabo/src/api/personalFinanceTransactions.ts`
- `Payabo/src/api/personalFinanceImports.ts`
- `Payabo/src/api/personalFinanceInsights.ts`

## 14.2 New Pages
- Accounts:
  - `/wallet/accounts`
- Transactions:
  - `/transactions/manual/new`
  - `/transactions` (migrate to personal transaction feed)
  - `/transactions/:id`
- Imports:
  - `/transactions/import`
  - `/transactions/imports/:id`
- Classification:
  - `/transactions/review`
  - `/transactions/rules`
- Insights:
  - `/insights/spending`

## 14.3 UX Requirements
- Manual transaction form includes source account selector.
- Import flow enforces source account selection before upload.
- Review queue supports bulk accept and one-by-one override.
- Insights page supports period toggles (month/quarter/custom).
- Existing bill-payment history remains accessible as separate "Bill Payments" view.

---

## 15. Observability & Operations

- Structured logs:
  - import lifecycle
  - parse failures
  - classification confidence distribution
  - AI fallback rates
- Metrics:
  - `% auto-classified`
  - `% user-corrected`
  - duplicate detection rate
  - avg classification latency
- Background jobs:
  - retry with backoff for parse/classification tasks
  - idempotent apply operation

---

## 16. Testing Strategy

## 16.1 Unit Tests
- Rule matching priority/edge cases.
- Confidence thresholds and review status transitions.
- Dedupe fingerprint generation consistency.
- Insights aggregate calculations.

## 16.2 Integration Tests (InMemory DB)
- Account/transaction CRUD by tenant/user isolation.
- Statement upload -> parse -> classify -> apply full path.
- Duplicate row suppression behavior.
- Permission enforcement on endpoints.

## 16.3 API Tests
- Happy paths and validation failures.
- Unauthorized/forbidden paths.
- Pagination/filter correctness.
- AI fallback path stubbing with deterministic test client.

---

## 17. Delivery Phases

## Phase 1 (MVP Core)
- Account CRUD.
- Manual transaction CRUD.
- Deterministic classification rules.
- Basic insights (category totals, trends).
- Payabo transaction list switched to personal transaction feed.

## Phase 2 (Import)
- CSV statement upload and background parse/apply pipeline.
- Dedupe and row-level error reporting.
- Import status UI.

## Phase 3 (AI + Learning)
- AI fallback classification with `AiRun` linkage.
- Review queue and correction loop.
- Rule learning from corrections.

## Phase 4 (Advanced Insights)
- Narrative insights + anomaly detection.
- Subscription/spend pattern detection.
- Household shared insights and governed rule scopes.

---

## 18. Acceptance Criteria

- User can create source accounts and manually add transactions against an account.
- User can upload CSV statement tied to an account and import completes with counts.
- Each imported/manual transaction has category and confidence (or pending review).
- User can override category and optionally create a reusable rule.
- Insights endpoints return accurate category/merchant/account spend summaries.
- AI-derived classifications include `AiRunId` traceability.
- Bill-payment order history remains separate and unaffected.
- Multi-tenant isolation and permissions are enforced.

---

## 19. Risks & Mitigations

- Risk: noisy statement formats.
  - Mitigation: CSV template contract + pluggable parser profiles.
- Risk: low classification quality early on.
  - Mitigation: rule-first strategy + review queue + correction learning.
- Risk: AI compliance/audit gaps.
  - Mitigation: mandatory `AiRun` persistence and policy-driven routing.
- Risk: confusion between Orders and Personal Transactions.
  - Mitigation: explicit UI separation and domain-level service boundaries.
