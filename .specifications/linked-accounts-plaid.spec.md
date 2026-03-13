# Linked Accounts Provider-Agnostic Integration Specification

## Implementation Checklist

- [x] Document provider-agnostic linked-accounts architecture for Payabo mobile
- [x] Define mobile Spending -> Accounts UX states and routing plan
- [x] Define backend integration path with Plaid as the first adapter
- [x] Add Payabo mobile Spending -> Accounts page shell
- [x] Add Payabo mobile account-links repository/state layer
- [x] Wire Spending section navigation to `/spending/accounts`
- [x] Add widget tests for Accounts screen and navigation
- [x] Add live mobile mapping to existing personal-finance accounts API
- [x] Implement provider-agnostic backend session, exchange, list, and summary foundation
- [x] Add mobile connect-flow orchestration for session creation, simulated provider handoff, and exchange
- [x] Add a configurable Plaid mobile launcher path behind the provider-neutral launcher abstraction
- [x] Add backend refresh and disconnect endpoints plus reconnect-targeted session support
- [x] Add mobile refresh, reconnect, disconnect, and OAuth-resume handling foundations
- [x] Replace placeholder mobile app identifiers with the Payabo package/bundle ids needed for Plaid sandbox OAuth setup
- [x] Add a real Plaid backend adapter path for Android link-token creation, public-token exchange, refresh, and item removal
- [x] Add backend Plaid webhook handling and state propagation for action-required/disconnect events
- [x] Add linked-account transaction sync ingestion into `PersonalTransaction`
- [ ] Add future backend recurring sync scheduling/workers

## 1. Purpose

Create the Payabo mobile `Spending -> Accounts` experience as the home for linked financial accounts.

The page should support:

- Connected bank accounts for spend visibility
- Manual fallback accounts for early usage and testing
- Future Plaid integration without coupling the product or API surface to Plaid-specific naming
- A clear path from account connection to transaction import, categorisation, and spending insights

Plaid is the first intended adapter, but the design must remain provider agnostic.

---

## 2. Architectural Guardrails

- Ledger remains the source of financial truth for financially material events.
- Linked accounts and synced transactions are Personal Finance projections, not ledger truth.
- Orders remain separate from personal account linking and spending views.
- AI-derived categorisation or narrative outputs must remain auditable and reference `AiRunId` where applicable.
- Provider credentials and access tokens must never be stored on the client.
- Flutter should receive short-lived session/link data only.

---

## 3. Product Direction

The Accounts page is a Spend companion experience that helps users:

- Connect a bank once
- Keep transactions and balances current
- Improve category, merchant, and budget insights
- Fall back to manual accounts or statement upload when live bank linking is unavailable

The core UX language should talk about:

- `Linked accounts`
- `Secure connection`
- `Reconnect`
- `Sync status`
- `Manual account`

Avoid provider-specific labels in primary navigation or product copy.

---

## 4. Current State

### Payabo Mobile

- `SpendingSection.accounts` already exists in the shared pill navigation.
- The Accounts pill currently shows a placeholder snackbar instead of a page.
- No mobile route exists yet for `/spending/accounts`.
- No dedicated repository/provider exists yet for linked accounts.

### AONIK Backend

- Existing Personal Finance account CRUD is available under `/personal-finance/accounts`.
- Existing model support includes:
  - account name
  - account type / subtype
  - currency
  - institution name
  - external reference
  - last4
  - status / archive fields
- Statement import exists and can serve as a fallback ingestion path.
- No Plaid or generic open-banking provider flow exists yet.

---

## 5. Provider-Agnostic Design Decision

We will not build a Plaid-branded architecture.

Instead:

- Payabo mobile talks to AONIK using provider-neutral linked-account concepts.
- AONIK hosts the provider adapter layer.
- Plaid becomes the first implementation behind that abstraction.

This keeps the system open for:

- Plaid
- Open Banking aggregators
- direct regional adapters
- future enterprise bank-connectors

---

## 6. Mobile UX Scope

## 6.1 Route

Add:

- `/spending/accounts`

## 6.2 Screen States

The screen should support six primary states:

1. `Loading`
2. `Fresh demo state`
3. `No linked accounts yet`
4. `Connected accounts present`
5. `Action required / reconnect`
6. `Load error`

## 6.3 Primary Actions

- `Connect bank account`
- `Upload statement`
- `Add manual account`

## 6.4 Account Card Content

Each account card should be able to display:

- account name
- institution name
- account type / subtype
- masked identifier / last4
- currency
- optional balance label
- status label
- sync or status detail
- last synced / updated label
- linked vs manual source

## 6.5 Empty-State Messaging

When no accounts exist and the app is not in fresh demo mode, the page should explain that linked accounts unlock:

- better spending coverage
- fresher budget tracking
- cleaner category and merchant insights

When in fresh demo mode, the page should clearly explain that seeded account examples are intentionally hidden.

---

## 7. Mobile Data Layer

Add a new mobile repository abstraction for the Accounts page.

### Proposed Types

- `AccountLinksRepository`
- `LiveAccountLinksRepository`
- `MockAccountLinksRepository`
- `AccountLinksSummary` or list-based equivalent view model
- `AccountLinkStatus` enum

### Initial Live Data Source

Until provider-specific endpoints exist, the live repository should map from:

- `GET /personal-finance/accounts`

This gives Payabo mobile a working live Accounts page immediately.

### Mapping Rules

- Existing Personal Finance accounts with institution metadata should be treated as linked-ready accounts.
- Accounts with no institution or external reference should be treated as manual accounts.
- Archived accounts should render as disconnected/archived.
- Unknown future status strings should degrade gracefully to a neutral status presentation.

---

## 8. Backend Target Architecture

`PersonalAccount` remains the product-facing account used by Payabo spending features.

Do not overload `PersonalAccount` with full provider connection lifecycle data.

Instead add separate provider-aware models such as:

- `FinancialConnection`
- `FinancialLinkedAccount`
- `FinancialSyncRun`
- `FinancialWebhookEvent`
- `FinancialConnectionConsent`

These models should capture:

- provider name
- provider item / connection id
- provider account id
- institution id and institution name
- consent status and consent timestamps
- secret reference / token vault reference
- sync cursor
- last successful sync
- last webhook receipt
- reconnect requirements
- last sync error

`PersonalAccount` should reference these linked-account records rather than storing provider state directly.

---

## 9. Backend API Direction

Existing account CRUD remains:

- `GET /personal-finance/accounts`
- `POST /personal-finance/accounts`
- `PATCH /personal-finance/accounts/{id}`

Future provider-agnostic linked-account endpoints should be added separately, for example:

- `POST /personal-finance/account-links/sessions`
- `POST /personal-finance/account-links/exchanges`
- `GET /personal-finance/account-links`
- `GET /personal-finance/account-links/summary`
- `POST /personal-finance/account-links/{id}/refresh`
- `POST /personal-finance/account-links/{id}/disconnect`
- `POST /personal-finance/account-links/webhooks/{provider}`

### Plaid First Adapter Flow

1. Mobile requests a provider-neutral connection session from AONIK.
2. AONIK creates the provider session/link token with Plaid.
3. Mobile launches the Plaid Link SDK using only the short-lived token.
4. Mobile returns the temporary result to AONIK.
5. AONIK exchanges it for long-lived provider credentials.
6. AONIK stores credentials securely via secret references, not plaintext finance entities.
7. AONIK creates or updates linked financial accounts and maps them into `PersonalAccount`.

---

## 10. Transaction Ingestion Direction

Linked-account sync should not write directly to the ledger.

Instead:

- provider transactions are fetched into provider/raw sync storage
- AONIK normalizes them into `PersonalTransaction`
- `SourceType` should identify provider sync provenance
- dedupe should use provider transaction ids plus normalized fingerprinting

This keeps synced spend aligned with existing Personal Finance insights and classification flows.

---

## 11. Plaid-Specific Constraints Hidden Behind the Adapter

The backend adapter will need to support:

- link token creation
- public token exchange
- item/account sync
- reconnect/update mode
- webhook handling
- consent and error state management

The mobile app should not know about:

- Plaid `access_token`
- Plaid `secret`
- provider-specific credential storage
- raw webhook structures

---

## 12. Mobile Implementation Phases

## Phase 1

- Add `/spending/accounts` route
- Add Accounts screen shell
- Add mock repository and page states
- Wire Spending pill navigation
- Add widget tests

## Phase 2

- Add live repository using `/personal-finance/accounts`
- Show existing manual/live accounts in the new page
- Keep `Connect bank account` CTA in placeholder mode until session endpoints exist

## Phase 3

- Add provider-agnostic backend connection models and APIs
- Integrate Plaid as adapter one
- Add connection / reconnect / disconnect flow

## Phase 4

- Add recurring sync, status surfaces, and richer connected-account health
- Add imported transaction reconciliation into Personal Finance views

---

## 13. Testing Strategy

### Flutter Widget Tests

- Accounts screen renders populated mock state
- Accounts screen renders fresh demo state
- Accounts screen renders onboarding empty state
- Spending section pill navigation opens Accounts page

### Backend Tests (Future)

- create session
- exchange temporary token
- duplicate account prevention
- reconnect/update mode
- disconnect flow
- webhook idempotency
- sync dedupe

---

## 14. Implementation Notes for This Increment

This increment focuses on Payabo mobile foundation work:

- document the provider-agnostic plan
- add the Accounts page
- add a dedicated mobile repository/state layer
- support live listing from the existing personal-finance accounts endpoint

This increment does not yet include:

- live Plaid production credentials
- webhook processing
- recurring sync jobs
- institution/account-specific refresh policies beyond the current provider-neutral foundation

### Native Plaid Launcher Notes

- Payabo mobile now includes a provider-neutral launcher abstraction with a Plaid-backed mobile path.
- The native Plaid launcher is gated by compile-time environment defines so local and test builds can keep using the simulated handoff.
- Current native Plaid scope is Android only. iOS OAuth support is intentionally deferred.
- Payabo Android identifiers are now aligned for Plaid mobile registration:
  - Android application id / namespace: `com.payabo.mobile`
- Current defines:
  - `ACCOUNT_LINK_PROVIDER=Plaid`
  - `ACCOUNT_LINK_USE_NATIVE_LAUNCHER=true|false`
-  - `ACCOUNT_LINK_ANDROID_PACKAGE_NAME=<android package name>`
-  - `ACCOUNT_LINK_REDIRECT_URI=<reserved for future iOS/web support>`
- Android native Link should create provider sessions using the registered Android package name rather than a mobile redirect URI.
- Native Plaid enablement still depends on Android package registration in the Plaid Dashboard and real provider-side mobile configuration.
- Mobile platform baselines for this step are aligned to Plaid package requirements:
  - Android min SDK 21+


### Reconnect / Refresh / Disconnect Notes

- Reconnect reuses the existing provider-neutral session creation endpoint in `update` mode and now targets a specific connection id.
- Refresh uses a provider-neutral endpoint to re-sync an active connection without opening a new Link session.
- Disconnect archives linked personal accounts out of active Spend views while preserving auditable connection history.
- Mobile now exposes linked-account actions for reconnect, refresh, and disconnect, plus an OAuth resume screen for returning provider handoffs.

### Real Plaid Backend Adapter Notes

- AONIK now supports a real Plaid HTTP adapter path behind the provider-neutral gateway abstraction.
- The adapter currently targets the Android-native Link flow and uses `android_package_name` during `/link/token/create`.
- Real Plaid mode is gated by backend configuration under `Finance:PersonalFinance:Plaid`.
- When enabled with Sandbox credentials, AONIK can now:
  - create Plaid link tokens
  - exchange public tokens for access tokens
  - fetch item/account metadata for linked-account projection
  - refresh linked accounts through Plaid account fetches
  - remove Items during disconnect
- AONIK now persists Plaid webhook events and applies webhook-driven state changes for:
  - `PENDING_DISCONNECT`
  - `PENDING_EXPIRATION`
  - `ITEM_LOGIN_REQUIRED` surfaced via `ITEM/ERROR`
  - `USER_PERMISSION_REVOKED`
  - `SYNC_UPDATES_AVAILABLE`
- Webhook processing updates linked connection and account states so Payabo can surface reconnect/disconnect needs without waiting for a manual refresh.
- Access tokens are currently stored as protected values in the existing `SecretReference` field until a dedicated secret-vault integration layer is added.

### Transaction Sync Ingestion Notes

- AONIK now supports on-demand transaction sync for linked accounts via a provider-neutral account-link sync endpoint.
- For Plaid-backed connections, transaction ingestion uses `/transactions/sync` and persists the results into `PersonalTransaction`.
- Synced transactions are stored as Personal Finance projections with:
  - `SourceType = linked_account_sync`
  - deterministic `SourceId` derived from the provider transaction reference
  - linked `PersonalAccountId` resolved from the provider account reference
- Sync upserts provider transactions, removes provider-deleted transactions, and stores the latest Plaid cursor on the financial connection.
- Provider categories are treated as low-confidence provider classifications and remain compatible with later user review or rule-based overrides.

---

## 15. Acceptance Criteria for This Increment

- Payabo mobile exposes a routed `Spending -> Accounts` page
- Accounts pill navigation lands on the new page
- The page supports demo and live-backed account rendering
- The page uses provider-agnostic copy and structure
- The implementation preserves a clean extension path for Plaid as the first adapter
- Existing Personal Finance account API data can be surfaced on mobile without backend schema changes
