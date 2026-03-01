# Individual Registration Flow

This document explains the backend execution path for `POST /v1/registrations/individual`, including service orchestration and database tables that are read or written.

## Endpoint

- Route: `POST /v1/registrations/individual`
- Endpoint class: `src/Aonik.Platform/Endpoints/Registrations/IndividualRegistrationEndpoint.cs`
- Entry service: `RegistrationService.RegisterIndividualAsync(...)`

The endpoint resolves `TenantId` from:

1. request body (`tenantId`), else
2. header (`X-Tenant-Id`) when header routing is enabled, else
3. subdomain lookup when subdomain routing is enabled.

## End-to-End Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Client as Payabo / Client App
    participant Endpoint as IndividualRegistrationEndpoint
    participant Reg as RegistrationService
    participant Settings as ISettingProvider
    participant Idp as IIdpUserProvisioner
    participant Ext as External IdP (Auth0/AzureAD)
    participant Provision as UserProvisioningService
    participant Identity as UserIdentityService
    participant Profile as UserProfileService
    participant Verify as VerificationService
    participant Onboard as OnboardingPolicyEvaluator
    participant DB as SQL Server (dbo.Ank*)
    participant Comm as Email/SMS Provider

    Client->>Endpoint: POST /v1/registrations/individual
    Endpoint->>Reg: RegisterIndividualAsync(request)

    Reg->>Settings: Get Auth.Provider
    Reg->>Idp: CreateUserAsync(email, password, profile)
    Idp->>Settings: Read IdP settings
    Idp->>Ext: Create external identity user
    Ext-->>Idp: external subject/issuer
    Idp-->>Reg: ExternalIdentityResult

    Reg->>Provision: EnsureUserAndCustomerAsync(identity)
    Provision->>Identity: ResolveOrCreateUserAsync(...)
    Identity->>DB: SELECT dbo.AnkUsers by issuer/subject/tenant
    alt User missing
        Identity->>DB: INSERT dbo.AnkUsers
        Identity->>DB: INSERT dbo.AnkAuditLogs (UserProvisioned)
    else User exists and email changed
        Identity->>DB: UPDATE dbo.AnkUsers (Email)
    end

    Provision->>DB: SELECT dbo.AnkUserParties (Individual link)
    alt No party linked
        Provision->>DB: INSERT dbo.AnkParties
        opt Email provided
            Provision->>DB: INSERT dbo.AnkPartyContacts (Email)
        end
        Provision->>DB: INSERT dbo.AnkAuditLogs (PartyCreated)
    end

    alt No user-party link
        Provision->>DB: INSERT dbo.AnkUserParties
        Provision->>DB: INSERT dbo.AnkAuditLogs (PartyLinked)
    end

    Reg->>Profile: UpdateCustomerProfileForRegistrationAsync(...)
    Profile->>DB: SELECT dbo.AnkUsers, dbo.AnkUserParties, dbo.AnkParties
    Profile->>DB: SELECT/INSERT dbo.AnkPersonProfiles
    Profile->>DB: UPDATE dbo.AnkPersonProfiles, dbo.AnkParties
    opt Phone provided
        Profile->>DB: UPDATE dbo.AnkUsers (Phone)
        Profile->>DB: UPSERT dbo.AnkPartyContacts (Phone)
    end
    Profile->>DB: INSERT dbo.AnkAuditLogs (CustomerProfileUpdated)

    opt Email present
        Reg->>Verify: StartEmailVerificationForRegistrationAsync
        Verify->>DB: INSERT dbo.AnkVerificationChallenges (Email)
        Verify->>Comm: Send email challenge
        Verify->>DB: INSERT dbo.AnkAuditLogs (VerificationStarted)
    end

    opt Phone present
        Reg->>Verify: StartPhoneVerificationForRegistrationAsync
        Verify->>DB: INSERT dbo.AnkVerificationChallenges (SMS)
        Verify->>Comm: Send SMS challenge
        Verify->>DB: INSERT dbo.AnkAuditLogs (VerificationStarted)
    end

    Reg->>Onboard: EvaluateAsync(userId)
    Onboard->>DB: READ users/party/contacts/addresses/challenges
    Onboard-->>Reg: onboarding snapshot
    Reg-->>Endpoint: IndividualRegistrationResult
    Endpoint-->>Client: 200 OK (userId, partyId, onboarding)
```

## Service Responsibilities

- `RegistrationService`
  - Orchestrates provider selection, external user creation, local provisioning, profile update, verification start, onboarding evaluation.
  - Registration-specific behavior:
    - Uses `UpdateCustomerProfileForRegistrationAsync` (permission bypass for initial signup flow).
    - Uses `Start*VerificationForRegistrationAsync` methods.
    - Catches verification delivery exceptions and still returns successful registration.

- `Auth0UserProvisioner` / `AzureAdUserProvisioner`
  - Creates identity in external IdP.
  - No AONIK table writes.

- `UserProvisioningService`
  - Ensures local `User`, `Party`, and `UserParty` link exist.

- `UserIdentityService`
  - Creates or resolves user by `(TenantId, ExternalIssuer, ExternalSubject)`.

- `UserProfileService`
  - Persists first/last/title/country/phone details onto user-party profile structures.

- `VerificationService`
  - Creates verification challenge records and dispatches notifications.

- `OnboardingPolicyEvaluator`
  - Read-only evaluation of onboarding gates (`EmailVerified`, `PhoneVerified`, `ProfileComplete`).

## Table Mutation Map

```mermaid
flowchart LR
    R[RegistrationService]
    P[UserProvisioningService]
    U[UserIdentityService]
    PR[UserProfileService]
    V[VerificationService]
    A[AuditLogWriter]

    T1[(dbo.AnkUsers)]
    T2[(dbo.AnkParties)]
    T3[(dbo.AnkPartyContacts)]
    T4[(dbo.AnkUserParties)]
    T5[(dbo.AnkPersonProfiles)]
    T6[(dbo.AnkVerificationChallenges)]
    T7[(dbo.AnkAuditLogs)]

    R --> P
    P --> U
    U -->|INSERT / UPDATE| T1
    P -->|INSERT| T2
    P -->|INSERT optional| T3
    P -->|INSERT| T4

    R --> PR
    PR -->|UPDATE optional| T1
    PR -->|UPDATE| T2
    PR -->|UPSERT optional| T3
    PR -->|SELECT/INSERT then UPDATE| T5

    R --> V
    V -->|INSERT optional| T6

    U --> A
    P --> A
    PR --> A
    V --> A
    A -->|INSERT| T7
```

## Table-Level Details

| Table | Writes during registration | Conditions |
|---|---|---|
| `dbo.AnkUsers` | `INSERT` (new user), `UPDATE` email, optional `UPDATE` phone | New external identity creates row; existing user email may update; phone updates when provided |
| `dbo.AnkParties` | `INSERT` individual party, `UPDATE` display/profile fields | Insert when no existing individual party link; update during profile step |
| `dbo.AnkPartyContacts` | `INSERT` email contact, optional phone upsert | Email contact on new party create; phone contact when phone provided |
| `dbo.AnkUserParties` | `INSERT` individual link | Insert when no existing `LinkType = "Individual"` link |
| `dbo.AnkPersonProfiles` | `INSERT` if missing, then `UPDATE` profile data | Always ensured by profile step |
| `dbo.AnkVerificationChallenges` | Optional `INSERT` email/sms challenge | Email and/or phone present in registration request |
| `dbo.AnkAuditLogs` | Multiple `INSERT`s | User provisioned, party created, party linked, profile updated, verification started |

## Read-Only Tables During Registration

- `dbo.AnkSettings` (auth provider and IdP credentials)
- `dbo.AnkTenants` (tenant existence/active validation in user creation path)
- `dbo.AnkPartyAddresses` (onboarding profile-complete gate evaluation)
- `dbo.AnkVerificationChallenges` (onboarding verified gates evaluation)

## Important Operational Notes

- There is no single outer transaction across the entire flow; persistence occurs across multiple `SaveChangesAsync` calls in different services.
- If external IdP creation fails, local DB writes do not start.
- Verification send failures do not fail registration (current behavior), but challenge inserts may already exist because the challenge row is written before dispatch.
- No finance-domain tables are touched by individual registration.

## Source Files

- `src/Aonik.Platform/Endpoints/Registrations/IndividualRegistrationEndpoint.cs`
- `src/Aonik.Platform/Services/Registration/RegistrationService.cs`
- `src/Aonik.Infrastructure/Authentication/Provisioning/Auth0UserProvisioner.cs`
- `src/Aonik.Infrastructure/Authentication/Provisioning/AzureAdUserProvisioner.cs`
- `src/Aonik.Platform/Services/Identity/UserProvisioningService.cs`
- `src/Aonik.Platform/Services/Identity/UserIdentityService.cs`
- `src/Aonik.Platform/Services/Identity/UserProfileService.cs`
- `src/Aonik.Platform/Services/Identity/VerificationService.cs`
- `src/Aonik.Platform/Services/Onboarding/OnboardingPolicyEvaluator.cs`
