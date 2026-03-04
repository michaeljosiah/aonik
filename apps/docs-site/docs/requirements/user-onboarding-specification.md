# Individual Customer Registration (B2C) — Requirements Specification

## Purpose

Define the platform-neutral requirements for onboarding individual customers (B2C) in AONIK, including identity linkage, verification primitives, and policy-driven activation. This specification translates the provided user story into actionable requirements aligned with the current codebase and architecture.

## Scope

**In scope**
- External identity linkage for authentication providers (Auth0, Azure Entra ID)
- Just-in-time user provisioning on first authenticated request
- Individual customer (Party) creation and linkage to User
- Email/SMS verification as platform primitives
- Policy-driven onboarding eligibility and activation
- Onboarding status snapshot endpoints
- Auditable events and rate limiting
- Messaging abstractions for email and SMS

**Out of scope**
- Front-end UI flows
- Marketing preferences and referrals
- Business customer onboarding
- Manual compliance operations

## Current Codebase Assessment

### Existing capabilities
- **External identity mapping** is modeled in `User` with `ExternalIssuer`, `ExternalSubject`, and `ExternalTenantId` (`src/Aonik.Domain/Identity/Entities/User.cs`).
- **User JIT provisioning** exists via `UserIdentityService.ResolveOrCreateUserAsync` (`src/Aonik.Application/Services/Identity/UserIdentityService.cs`).
- **Party records** exist with `Party`, `PartyContact`, and related entities for storing contact data (`src/Aonik.Domain/Party/Entities/*`).
- **Audit logging** infrastructure exists via `IAuditLogWriter` used in bootstrap workflows (`src/Aonik.Application/Services/Identity/Provisioning/BootstrapService.cs`).

### Gaps relative to the user story
- No explicit **User ↔ Party** (individual customer) linkage model.
- No **verification primitives** (email/SMS start/confirm, token storage, rate limits).
- No **onboarding policy** or status evaluation layer.
- No **communication abstraction** for email/SMS delivery.
- No **product-neutral onboarding endpoints** for individual registration.

## Functional Requirements

### 1) External Identity Resolution
- The API authenticates users via external IdP JWTs (Auth0, Azure Entra ID).
- The system must deterministically map the external identity to an internal `User` record using:
  - `ExternalIssuer` ← `iss`
  - `ExternalSubject` ← `sub` or `oid`
  - `ExternalTenantId` ← `tid` (nullable)
- Composite uniqueness: `(TenantId, ExternalIssuer, ExternalSubject)`.
- On first login, AONIK provisions a `User` if none exists.
- Provisioned user records are created in **Active** status unless policy dictates otherwise.

### 2) Individual Customer (Party) Creation & Linking
- An individual user must be associated with an **Individual Party** record.
- The system must support a deterministic linkage between `User` and `Party`.
- Linkage must be tenant-scoped and auditable.
- Party data for individuals must support:
  - Display name
  - Primary email and phone (via PartyContact)
  - Status (e.g., Pending, Active, Suspended)

### 3) Verification Primitives (Email & SMS)
- The platform provides primitives to start and confirm verification for:
  - Email
  - Phone number (SMS)
- Verification is based on expiring, single-use codes or tokens.
- Verification attempts are rate-limited per user, per channel, per target value.
- Verification attempts are auditable, including:
  - Attempted channel (email/SMS)
  - Target value
  - Success/failure
  - Timestamp and actor

### 4) Onboarding Policy & Eligibility
- Onboarding eligibility is derived from **policy configuration**, not bespoke workflows.
- A policy defines the required gates, such as:
  - Email verified
  - Phone verified
  - Profile completeness (e.g., display name)
- Policies must be evaluated per tenant and per product context.
- Policies must be configurable without schema changes (app configuration or DB-backed policy tables).

### 5) Onboarding Status Snapshot
- The system must expose a product-neutral onboarding status snapshot per user.
- The snapshot should include:
  - Current onboarding status (e.g., Pending, Active, Suspended)
  - Gates required by policy and whether each is satisfied
  - Required next actions (e.g., “verify email”)

### 6) Auditing & Observability
- All onboarding-critical events are written to audit logs, including:
  - User provisioning
  - Party creation and linkage
  - Verification started/confirmed/failed
  - Status transitions (Pending → Active)
- Audit records must include tenant, actor, and correlation identifiers.

### 7) Rate Limiting & Abuse Protection
- Verification start/confirm endpoints must enforce rate limits:
  - Per user
  - Per channel
  - Per contact value
- The system should enforce cooldowns and lockouts after repeated failures.

## API Requirements

### Identity & User Context
- `GET /v1/me`
  - Returns current user info + onboarding status snapshot.

### Verification
- `POST /v1/verifications/email/start`
- `POST /v1/verifications/email/confirm`
- `POST /v1/verifications/phone/start`
- `POST /v1/verifications/phone/confirm`

### Customer Profile
- `PUT /v1/customers/me/profile`
  - Updates display name and contact info for the individual Party.

### Onboarding Status
- `GET /v1/onboarding/me`
  - Returns a policy-evaluated onboarding snapshot.

## Data Model Requirements

### Existing entities used
- `User` (Identity)
- `Party`, `PartyContact` (Party)

### New or extended entities required
- **UserPartyLink** (or equivalent)
  - `UserId`, `PartyId`, `TenantId`, `LinkType` (Individual)
- **VerificationChallenge**
  - `VerificationId`, `TenantId`, `UserId`, `Channel`, `Target`, `CodeHash`, `ExpiresAt`, `AttemptCount`, `Status`
- **OnboardingPolicy** (if DB-backed)
  - `PolicyId`, `TenantId`, `ProductKey`, `RequiredGatesJson`
- **OnboardingSnapshot** (derived view)
  - Not persisted; derived from facts + policy

## Application Services Requirements

### Provisioning
- `UserProvisioningService.EnsureUserAndCustomer(IExternalIdentity identity)`
  - Resolves/creates `User`
  - Resolves/creates Individual `Party`
  - Ensures linkage

### Verification
- `VerificationService.StartEmailVerification(...)`
- `VerificationService.ConfirmEmailVerification(...)`
- `VerificationService.StartPhoneVerification(...)`
- `VerificationService.ConfirmPhoneVerification(...)`

### Onboarding Policy
- `OnboardingPolicyEvaluator.GetSnapshot(userId, policyContext)`
  - Computes gate satisfaction
  - Returns next actions

## Communication Abstractions (Email & SMS)

### Email
- **Interface (Application)**: `IEmailSender`
  - `Task SendEmailAsync(EmailMessage message, CancellationToken ct = default)`
- **Implementation (Infrastructure)**: `AzureCommunicationEmailSender`
  - Uses Microsoft Communication Service SDK
  - Configured via `Communication:EmailConnectionString` and `Communication:EmailSenderAddress`

### SMS
- **Interface (Application)**: `ISmsSender`
  - `Task SendSmsAsync(SmsMessage message, CancellationToken ct = default)`
- **Implementation (Infrastructure)**: `AzureCommunicationSmsSender`
  - Uses Microsoft Communication Service SDK
  - Configured via `Communication:SmsConnectionString` and `Communication:SmsSenderNumber`

### Common requirements
- Support tagging correlation IDs for audit/logging.
- Support structured error reporting (provider error codes and message).
- Provide retry strategy for transient failures.

## Security & Compliance Requirements
- Verification codes must be stored hashed (never plain text).
- Verification codes must expire within a configurable TTL.
- Only the owner of the target contact (email/phone) can verify it.
- All endpoints must enforce tenant scoping and authorization.
- Minimize PII in logs; audit logs must store masked contact values where possible.

## Acceptance Criteria Mapping

| User Story Requirement | Requirement Section |
| --- | --- |
| External identity linkage | External Identity Resolution |
| User + customer provisioning | Individual Customer Creation & Linking |
| Email/SMS verification | Verification Primitives |
| Active only after gates | Onboarding Policy & Eligibility |
| Status snapshot | Onboarding Status Snapshot |
| Auditable actions | Auditing & Observability |
| Policy-driven differences | Onboarding Policy & Eligibility |

## Open Decisions
- Final name and placement of the **User ↔ Party** linkage entity.
- Whether onboarding policies are stored in configuration or database tables.
- Default verification TTL and rate limit thresholds per tenant/product.
