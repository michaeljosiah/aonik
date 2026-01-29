# Database Schema (DbContext-derived)

This document is derived from the EF Core model snapshot, with simple explanations and examples for each table.

- DbContext: `src/Aonik.Infrastructure/Persistence/AonikDbContext.cs`
- Authoritative schema snapshot: `src/Aonik.Infrastructure/Persistence/Migrations/AonikDbContextModelSnapshot.cs`

## Notes

- Multi-tenancy: most tables are tenant-scoped via `TenantId` and filtered at query time.
- Auditing/soft delete: many tables include `CreatedAt/By`, `UpdatedAt/By`, and soft-delete fields.
- Orders describe why; payments/payouts describe how; the ledger proves what happened.

## Identity

### `Permissions`

- Entity: `Aonik.Domain.Identity.Entities.Permission`
- Purpose: Atomic capabilities the system can authorize (e.g., `invoices:create`, `ledger:post`).
- When to use (example): Add a Permission when you ship a new capability and want it assignable via roles.
- Key columns: `Id`, `Description`, `Key`
- Indexes (partial): (`Key`)

### `RolePermissions`

- Entity: `Aonik.Domain.Identity.Entities.RolePermission`
- Purpose: Join table linking roles to permissions (many-to-many).
- When to use (example): When you decide `BillingAdmin` can issue invoices, insert a RolePermission linking that role to the permission.
- Key columns: `Id`, `PermissionId`, `RoleId`
- Indexes (partial): (`PermissionId`), (`RoleId`, `PermissionId`)

### `Roles`

- Entity: `Aonik.Domain.Identity.Entities.Role`
- Purpose: Named permission bundles used to grant access consistently across many users.
- When to use (example): Create a role like `OpsReviewer` once, then assign it to multiple users.
- Key columns: `Id`, `TenantId`, `Name`
- Indexes (partial): (`TenantId`, `Name`)

### `Tenants`

- Entity: `Aonik.Domain.Identity.Entities.Tenant`
- Purpose: Top-level container for data isolation. Most business data is tenant-scoped and filtered by this tenant.
- When to use (example): Create a Tenant when onboarding a new business/product environment.
- Key columns: `Id`, `Status`, `Name`, `SupportedCountriesJson`, `DefaultCurrency`, `Environment`, `Subdomain`
- Indexes (partial): (`Name`), (`Status`), (`Subdomain`)

### `UserParties`

- Entity: `Aonik.Domain.Identity.Entities.UserParty`
- Purpose: Bridge between identity (`Users`) and business identity (`Parties`). This is how a login maps to a person/business record.
- When to use (example): After onboarding, link the signed-in user to their Person Party so orders/payments can reference the Party.
- Key columns: `Id`, `TenantId`, `UserId`, `PartyId`, `LinkType`
- Indexes (partial): (`TenantId`, `UserId`), (`TenantId`, `UserId`, `PartyId`, `LinkType`)

### `UserRoles`

- Entity: `Aonik.Domain.Identity.Entities.UserRole`
- Purpose: Join table linking users to roles (many-to-many).
- When to use (example): When you invite a teammate, create a UserRole row to grant them `BillingAdmin`.
- Key columns: `Id`, `UserId`, `RoleId`
- Indexes (partial): (`RoleId`), (`UserId`, `RoleId`)

### `Users`

- Entity: `Aonik.Domain.Identity.Entities.User`
- Purpose: Human users who can sign in and act in a tenant. Used for authorization, attribution, and approvals.
- When to use (example): Create a User when inviting an operator to manage billing, review compliance cases, or approve proposals.
- Key columns: `Id`, `TenantId`, `Status`, `ExternalTenantId`, `PreferencesJson`, `Email`, `ExternalIssuer`, `ExternalSubject`, `LastLoginAt`, `Phone`
- Indexes (partial): (`TenantId`, `ExternalIssuer`, `ExternalSubject`)

### `VerificationChallenges`

- Entity: `Aonik.Domain.Identity.Entities.VerificationChallenge`
- Purpose: Short-lived verification workflows (OTP/email/SMS/etc.) for sign-in, signup, or sensitive actions.
- When to use (example): Create a VerificationChallenge when sending an OTP; mark it used/failed when the code is verified/invalid.
- Key columns: `Id`, `TenantId`, `Status`, `ExpiresAt`, `UserId`, `AttemptCount`, `Channel`, `CodeHash`, `Target`
- Indexes (partial): (`TenantId`, `Channel`, `Target`), (`TenantId`, `UserId`, `Channel`)

## Party

### `BusinessProfiles`

- Entity: `Aonik.Domain.Party.Entities.BusinessProfile`
- Purpose: Business details for parties that are organizations (KYB profile data).
- When to use (example): Create a BusinessProfile when onboarding a merchant with registration details.
- Key columns: `Id`, `PartyId`, `IncorporationCountry`, `Industry`, `KybStatus`, `RegistrationNumber`
- Indexes (partial): (`PartyId`)

### `ExternalAccounts`

- Entity: `Aonik.Domain.Party.Entities.ExternalAccount`
- Purpose: External financial accounts linked to a party (bank account, mobile money wallet, card token reference).
- When to use (example): Create an ExternalAccount when a user adds a bank account to fund payments or receive payouts.
- Key columns: `Id`, `TenantId`, `PartyId`, `MetadataJson`, `ExternalAccountType`, `MaskedIdentifier`, `ProviderRef`, `VerificationStatus`

### `Parties`

- Entity: `Aonik.Domain.Party.Entities.Party`
- Purpose: Canonical representation of a person or business in the platform (customers, merchants, senders, receivers).
- When to use (example): Create a Party when a new customer is onboarded or when an order needs to reference a real-world counterparty.
- Key columns: `Id`, `TenantId`, `Status`, `CustomerTierCode`, `DisplayName`, `PartyType`

### `PartyAddresses`

- Entity: `Aonik.Domain.Party.Entities.PartyAddress`
- Purpose: Addresses linked to a party (billing, residential, registered).
- When to use (example): Add a PartyAddress when a merchant sets a registered address or a customer adds a billing address.
- Key columns: `Id`, `Type`, `PartyId`, `City`, `Country`, `Line1`, `Line2`, `Line3`, `Postcode`, `State`
- Indexes (partial): (`PartyId`)

### `PartyConsents`

- Entity: `Aonik.Domain.Party.Entities.PartyConsent`
- Purpose: Consent records for a party (what they agreed to, when, and under which policy/version).
- When to use (example): Create a PartyConsent when a user accepts Terms of Service or a data-sharing consent.
- Key columns: `Id`, `PartyId`, `ConsentType`, `GrantedAt`, `RevokedAt`
- Indexes (partial): (`PartyId`)

### `PartyContacts`

- Entity: `Aonik.Domain.Party.Entities.PartyContact`
- Purpose: Contact channels for a party (email, phone, etc.) used for verification and notifications.
- When to use (example): Add a PartyContact when a customer provides a phone number for payout notifications.
- Key columns: `Id`, `Type`, `PartyId`, `IsPrimary`, `Value`
- Indexes (partial): (`PartyId`)

### `PartyRelationships`

- Entity: `Aonik.Domain.Party.Entities.PartyRelationship`
- Purpose: Relationships between two parties (e.g., business owner, parent/child account, employer/employee).
- When to use (example): Link a business Party to an owner Person Party with a PartyRelationship.
- Key columns: `Id`, `TenantId`, `FromPartyId`, `IsActive`, `ToPartyId`, `Notes`, `RelationshipTypeCode`
- Indexes (partial): (`FromPartyId`), (`IsActive`), (`RelationshipTypeCode`), (`ToPartyId`)

### `PartyRoleAssignments`

- Entity: `Aonik.Domain.Party.Entities.PartyRoleAssignment`
- Purpose: Business roles for parties (e.g., Merchant, Customer, Beneficiary), separate from auth roles.
- When to use (example): Assign `Merchant` to a Party to enable billing features and merchant workflows.
- Key columns: `Id`, `TenantId`, `PartyId`, `ContextId`, `ContextType`, `Role`

### `PersonProfiles`

- Entity: `Aonik.Domain.Party.Entities.PersonProfile`
- Purpose: Personal details for parties that are people (KYC profile data).
- When to use (example): Create a PersonProfile after collecting a user's legal name and identity details during KYC.
- Key columns: `Id`, `PartyId`, `Title`, `CountryCode`, `Dob`, `FirstName`, `IdvStatus`, `LastName`, `Nationality`, `Occupation`, `PhotoUrl`
- Indexes (partial): (`PartyId`)

## Ledger

### `BalanceSnapshots`

- Entity: `Aonik.Domain.Ledger.Entities.BalanceSnapshot`
- Purpose: Precomputed balances at a point in time for reporting and performance (does not replace journal entries).
- When to use (example): Write BalanceSnapshots nightly to power dashboards without recalculating from all journal entry lines.
- Key columns: `Id`, `TenantId`, `Currency`, `LedgerAccountId`, `AsOf`, `Balance`

### `JournalEntries`

- Entity: `Aonik.Domain.Ledger.Entities.JournalEntry`
- Purpose: A single accounting event (header) that groups balanced debit/credit lines. This is the proof of financial truth.
- When to use (example): When a payment settles, create one JournalEntry to record the accounting impact (cash movement, fees, liabilities).
- Key columns: `Id`, `TenantId`, `Status`, `LedgerId`, `SourceId`, `SourceType`, `Timestamp`
- Indexes (partial): (`LedgerId`), (`SourceId`), (`Timestamp`)

### `JournalEntryLines`

- Entity: `Aonik.Domain.Ledger.Entities.JournalEntryLine`
- Purpose: Debit/credit lines belonging to a journal entry. The full set must balance.
- When to use (example): Add lines to debit `Cash` and credit `Customer Liability` for a received payment.
- Key columns: `Id`, `TenantId`, `Currency`, `Amount`, `DimensionsJson`, `JournalEntryId`, `LedgerAccountId`, `Direction`, `Narration`
- Indexes (partial): (`JournalEntryId`)

### `LedgerAccounts`

- Entity: `Aonik.Domain.Ledger.Entities.LedgerAccount`
- Purpose: Chart of accounts. Journal entry lines post to these accounts to build balances and financial statements.
- When to use (example): Create LedgerAccounts like `Cash`, `Fees Revenue`, `Customer Receivable` so transactions can be posted correctly.
- Key columns: `Id`, `TenantId`, `Name`, `DimensionsJson`, `LedgerId`, `AccountType`, `Code`
- Indexes (partial): (`Code`), (`LedgerId`), (`Name`)

### `Ledgers`

- Entity: `Aonik.Domain.Ledger.Entities.Ledger`
- Purpose: Accounting boundary/container for a tenant's chart of accounts and journal entries.
- When to use (example): Create a Ledger when provisioning a tenant before creating ledger accounts and posting entries.
- Key columns: `Id`, `TenantId`, `BaseCurrency`

## Orders

### `OrderFulfilmentRefs`

- Entity: `Aonik.Domain.Orders.Entities.OrderFulfilmentRef`
- Purpose: References from an order to fulfilment objects (typically Payouts, partner transmissions, etc.).
- When to use (example): Create an OrderFulfilmentRef when a payout is created to deliver money to the beneficiary.
- Key columns: `Id`, `TenantId`, `OrderId`, `PayoutId`

### `OrderFundingRefs`

- Entity: `Aonik.Domain.Orders.Entities.OrderFundingRef`
- Purpose: References from an order to its funding objects (typically PaymentIntents).
- When to use (example): Create an OrderFundingRef after checkout creates a PaymentIntent to fund the order.
- Key columns: `Id`, `TenantId`, `OrderId`, `PaymentIntentId`

### `OrderHistoryEvents`

- Entity: `Aonik.Domain.Orders.Entities.OrderHistoryEvent`
- Purpose: Append-only timeline of state changes and notable events for an order (auditable narrative).
- When to use (example): Add an OrderHistoryEvent when an order moves from `Pending` to `Funded`, or when a partner confirms delivery.
- Key columns: `Id`, `TenantId`, `OrderId`, `ActorId`, `DetailsJson`, `ActorType`, `EventAt`, `EventType`
- Indexes (partial): (`OrderId`)

### `OrderItems`

- Entity: `Aonik.Domain.Orders.Entities.OrderItem`
- Purpose: Components/line items within an order (useful when an order has multiple payable items).
- When to use (example): Add OrderItems when paying multiple bills in one checkout.
- Key columns: `Id`, `TenantId`, `Status`, `CurrencyIn`, `CurrencyOut`, `AmountIn`, `AmountOut`, `FeesTotal`, `OrderId`, `DetailsJson`, `PricingQuoteId`, `ReceiverPartyId`
- Indexes (partial): (`OrderId`), (`PricingQuoteId`), (`ReceiverPartyId`), (`OrderId`, `ItemIndex`)

### `OrderNotes`

- Entity: `Aonik.Domain.Orders.Entities.OrderNote`
- Purpose: Human-entered notes attached to an order for support/ops context (separate from structured events).
- When to use (example): Add an OrderNote when support explains why an order was cancelled or refunded.
- Key columns: `Id`, `TenantId`, `OrderId`, `CreatedByUserId`, `Note`

### `OrderPartyRoles`

- Entity: `Aonik.Domain.Orders.Entities.OrderPartyRole`
- Purpose: Explicit party role assignments inside an order (payer, payee, sender, receiver, beneficiary, merchant).
- When to use (example): Add OrderPartyRoles to state who is the sender and who is the receiver for a remittance order.
- Key columns: `Id`, `TenantId`, `PartyId`, `OrderId`, `DetailsJson`, `Role`
- Indexes (partial): (`OrderId`)

### `Orders`

- Entity: `Aonik.Domain.Orders.Entities.Order`
- Purpose: Business intent hub: why money should move. Orders orchestrate funding and fulfilment without being the payment itself.
- When to use (example): Create an Order when a user initiates a bill payment or remittance; link it to funding (PaymentIntents) and fulfilment (Payouts).
- Key columns: `Id`, `TenantId`, `Status`, `OrderType`, `CurrencyIn`, `CurrencyOut`, `AmountIn`, `AmountOut`, `FeesJson`, `FxQuoteId`, `PayerPartyId`, `ProvenanceJson`
- Indexes (partial): (`IdempotencyKey`), (`OrderType`), (`PayerPartyId`), (`Status`)

## Payments

### `Chargebacks`

- Entity: `Aonik.Domain.Payments.Entities.Chargeback`
- Purpose: Dispute/chargeback records for payments (provider-initiated reversals and dispute lifecycle).
- When to use (example): Create a Chargeback when a card network notifies that a payer disputed a card payment.
- Key columns: `Id`, `TenantId`, `Status`, `Currency`, `Amount`, `PaymentId`, `ProviderReference`

### `PaymentIntents`

- Entity: `Aonik.Domain.Payments.Entities.PaymentIntent`
- Purpose: Intent to collect funds from a payer via a funding method (card, bank, wallet). Created before attempts.
- When to use (example): Create a PaymentIntent at checkout before calling a payment provider.
- Key columns: `Id`, `TenantId`, `Status`, `Currency`, `Amount`, `InvoiceId`, `OrderId`, `PayeePartyId`, `PayerPartyId`, `PurposeId`, `FailureReason`, `PaymentMethodRef`
- Indexes (partial): (`InvoiceId`), (`OrderId`), (`PayerPartyId`), (`Status`)

### `Payments`

- Entity: `Aonik.Domain.Payments.Entities.Payment`
- Purpose: Payment execution records (attempts/results) tied back to a PaymentIntent and provider references.
- When to use (example): Create a Payment when the payment provider returns an authorization/capture result.
- Key columns: `Id`, `TenantId`, `PaymentIntentId`, `OutcomeJson`, `CapturedAt`, `OutcomeStatus`, `Provider`, `ProviderReference`

### `Payouts`

- Entity: `Aonik.Domain.Payments.Entities.Payout`
- Purpose: Outbound fulfilment executions that send money out to a beneficiary (bank/mobile money/etc.).
- When to use (example): Create a Payout after an order is funded to deliver money to the receiver via a connector.
- Key columns: `Id`, `TenantId`, `Status`, `Currency`, `Amount`, `PartnerId`, `DestinationExternalAccountId`

### `Refunds`

- Entity: `Aonik.Domain.Payments.Entities.Refund`
- Purpose: Refund execution records tied to a payment (money returned to the payer).
- When to use (example): Create a Refund when an order is cancelled after capture and funds must be returned.
- Key columns: `Id`, `TenantId`, `Status`, `Currency`, `Amount`, `PaymentId`, `Reason`

## Billing

### `CustomerAccounts`

- Entity: `Aonik.Domain.Billing.Entities.CustomerAccount`
- Purpose: Billing relationship between a merchant party and a customer party, including preferences and status.
- When to use (example): Create a CustomerAccount when a business starts invoicing a specific customer.
- Key columns: `Id`, `TenantId`, `Status`, `CustomerPartyId`, `MerchantPartyId`, `PreferencesJson`

### `DunningPlans`

- Entity: `Aonik.Domain.Billing.Entities.DunningPlan`
- Purpose: Dunning configuration for a customer account (reminders, escalation rules, schedules).
- When to use (example): Create a DunningPlan to automate reminders for overdue invoices.
- Key columns: `Id`, `TenantId`, `CustomerAccountId`, `IsActive`, `PolicyJson`

### `InvoiceAllocations`

- Entity: `Aonik.Domain.Billing.Entities.InvoiceAllocation`
- Purpose: Allocation records showing how a payment is applied to an invoice (supports partial payments).
- When to use (example): Create an InvoiceAllocation when a 50.00 payment is applied to a 100.00 invoice.
- Key columns: `Id`, `TenantId`, `Amount`, `InvoiceId`, `PaymentId`, `AllocatedAt`

### `InvoiceLines`

- Entity: `Aonik.Domain.Billing.Entities.InvoiceLine`
- Purpose: Invoice line items (description, quantity, unit price, tax).
- When to use (example): Add InvoiceLines like `Internet plan - January` and `Setup fee`.
- Key columns: `Id`, `TenantId`, `InvoiceId`, `MetadataJson`, `Description`, `LineTotal`, `Quantity`, `TaxRate`, `UnitPrice`
- Indexes (partial): (`InvoiceId`)

### `Invoices`

- Entity: `Aonik.Domain.Billing.Entities.Invoice`
- Purpose: Billable documents issued by a merchant to a customer, tracking totals, due dates, and lifecycle state.
- When to use (example): Create an Invoice when generating a bill for a subscription renewal or one-off service.
- Key columns: `Id`, `TenantId`, `Status`, `Currency`, `Total`, `Subtotal`, `DueDate`, `CustomerAccountId`, `OrderId`, `ProvenanceJson`, `DiscountTotal`, `IssueDate`
- Indexes (partial): (`CustomerAccountId`), (`DueDate`), (`OrderId`), (`Status`)

## Catalog

### `CatalogBillerCategories`

- Entity: `Aonik.Domain.Catalog.Entities.CatalogBillerCategory`
- Purpose: Categories used to group billers/services for discovery (e.g., Utilities, Telecom).
- When to use (example): Create a CatalogBillerCategory so the UI can group billers consistently.
- Key columns: `Id`, `TenantId`, `Name`, `IsActive`, `CountryCode`, `Description`, `IconUrl`, `SortOrder`
- Indexes (partial): (`TenantId`, `CountryCode`, `Name`), (`TenantId`, `CountryCode`, `SortOrder`)

### `CatalogBillerServices`

- Entity: `Aonik.Domain.Catalog.Entities.CatalogBillerService`
- Purpose: Specific payable services under a biller (e.g., prepaid top-up vs postpaid bill).
- When to use (example): Add a CatalogBillerService when a biller offers multiple service types.
- Key columns: `Id`, `TenantId`, `Name`, `Type`, `Currency`, `BillerId`, `FieldsJson`, `IsActive`, `ValidationJson`, `MaxAmount`, `MinAmount`, `RequiresValidation`
- Indexes (partial): (`TenantId`, `ServiceCode`), (`TenantId`, `BillerId`, `Name`), (`TenantId`, `BillerId`, `SortOrder`)

### `CatalogBillers`

- Entity: `Aonik.Domain.Catalog.Entities.CatalogBiller`
- Purpose: Directory of billers available for bill payment, with country and routing metadata.
- When to use (example): Add a CatalogBiller when onboarding a new utility/telecom provider into the bill-pay catalog.
- Key columns: `Id`, `TenantId`, `Name`, `CategoryId`, `CorrespondentPartnerId`, `IsActive`, `BannerUrl`, `CountryCode`, `Description`, `IsFeatured`, `LogoUrl`, `SortOrder`
- Indexes (partial): (`TenantId`, `CorrespondentPartnerId`), (`TenantId`, `CountryCode`, `Name`), (`TenantId`, `CountryCode`, `CategoryId`, `SortOrder`)

## Partners

### `Connectors`

- Entity: `Aonik.Domain.Partners.Entities.Connector`
- Purpose: Technical connector definitions for integrating with partners (capabilities and auth references).
- When to use (example): Create a Connector when adding a new integration to a payment processor or payout rail.
- Key columns: `Id`, `TenantId`, `Status`, `PartnerId`, `ConfigJson`, `ConnectorType`, `CredentialsRef`

### `PartnerBranches`

- Entity: `Aonik.Domain.Partners.Entities.PartnerBranch`
- Purpose: Partner branch/location metadata used for coverage, routing, and operations.
- When to use (example): Add a PartnerBranch when a cash-out partner has regional branches with different capabilities.
- Key columns: `Id`, `TenantId`, `Name`, `PartnerId`, `MetadataJson`, `City`, `Country`
- Indexes (partial): (`PartnerId`)

### `Partners`

- Entity: `Aonik.Domain.Partners.Entities.Partner`
- Purpose: External partner organizations (processors, correspondents, payout providers) in the network.
- When to use (example): Create a Partner for a new payout provider your routing rules can select.
- Key columns: `Id`, `TenantId`, `Status`, `Name`, `CapabilitiesJson`, `OperatingHoursJson`

### `PayoutSchemas`

- Entity: `Aonik.Domain.Partners.Entities.PayoutSchema`
- Purpose: Schemas/templates describing required payout fields per corridor/connector for validation and mapping.
- When to use (example): Use PayoutSchemas to enforce required fields like bank code/account number for bank payouts.
- Key columns: `Id`, `TenantId`, `Name`, `IsActive`, `SchemaJson`

### `RoutingRules`

- Entity: `Aonik.Domain.Partners.Entities.RoutingRule`
- Purpose: Rules that choose which partner/connector to use based on corridor, amount, service, risk, etc.
- When to use (example): Create a RoutingRule to send NGN mobile money payouts to Provider A but bank payouts to Provider B.
- Key columns: `Id`, `TenantId`, `ConditionsJson`, `IsActive`, `TargetConnectorId`, `TargetPartnerId`, `Priority`

### `Transmissions`

- Entity: `Aonik.Domain.Partners.Entities.Transmission`
- Purpose: Outbound transmission attempts to partners for fulfilment (tracks status, retries, and last error).
- When to use (example): Create a Transmission when sending a payout request to a connector and track retries until success.
- Key columns: `Id`, `TenantId`, `Status`, `PayoutId`, `ConnectorId`, `LastError`, `RetryCount`, `IdempotencyKey`

## Pricing

### `FeePolicies`

- Entity: `Aonik.Domain.Pricing.Entities.FeePolicy`
- Purpose: Fee calculation policies (fixed/percentage + conditions) used during quoting and order pricing.
- When to use (example): Create a FeePolicy like `Standard Remit Fee` with 1% + 2.00 fixed for specific corridors.
- Key columns: `Id`, `TenantId`, `Name`, `ConditionsJson`, `IsActive`, `FixedFee`, `PercentageFee`

### `FxQuotes`

- Entity: `Aonik.Domain.Pricing.Entities.FxQuote`
- Purpose: Short-lived FX quotes (rate + expiry) used to price cross-currency orders.
- When to use (example): Create an FxQuote when showing the user a conversion rate that expires in a short window.
- Key columns: `Id`, `TenantId`, `Rate`, `ExpiresAt`, `MetadataJson`, `BaseCurrency`, `Provider`, `TargetCurrency`

### `LimitsPolicies`

- Entity: `Aonik.Domain.Pricing.Entities.LimitsPolicy`
- Purpose: Limit rules (amount caps, velocity limits, corridor restrictions) used for risk/compliance enforcement.
- When to use (example): Create a LimitsPolicy to cap transfers per day unless the user has a higher verification level.
- Key columns: `Id`, `TenantId`, `Currency`, `IsActive`, `ScopeId`, `MaxAmount`, `Period`, `ScopeType`

### `PricingQuotes`

- Entity: `Aonik.Domain.Pricing.Entities.PricingQuote`
- Purpose: Full pricing results combining FX, fees, and totals for a specific context (a price the user can accept).
- When to use (example): Create a PricingQuote during checkout so the user can accept an all-in price before paying.
- Key columns: `Id`, `TenantId`, `QuoteType`, `FeesTotal`, `ExchangeRate`, `ExpiresAt`, `CustomerId`, `FeeBreakdownJson`, `FxRateId`, `PricingPolicyId`, `CustomerTier`, `DestinationAmount`
- Indexes (partial): (`CustomerId`), (`ExpiresAt`), (`QuoteType`), (`ServiceCode`)

## Compliance

### `AuditLogs`

- Entity: `Aonik.Domain.Compliance.Entities.AuditLog`
- Purpose: Audit trail of important system actions (who did what, when, and on which subject).
- When to use (example): Write an AuditLog entry when an operator approves a high-risk proposal or changes a policy.
- Key columns: `Id`, `TenantId`, `ActorId`, `CorrelationId`, `DetailsJson`, `ResourceId`, `Action`, `ActorType`, `ResourceType`, `Timestamp`

### `ComplianceCases`

- Entity: `Aonik.Domain.Compliance.Entities.ComplianceCase`
- Purpose: Case management records for compliance/risk investigations (holds, reviews, escalations).
- When to use (example): Create a ComplianceCase when screening flags a party and a human must review before fulfilment.
- Key columns: `Id`, `TenantId`, `Status`, `DetailsJson`, `LinkedOrderId`, `LinkedPartyId`, `LinkedPaymentId`, `Summary`, `CaseType`

### `ScreeningChecks`

- Entity: `Aonik.Domain.Compliance.Entities.ScreeningCheck`
- Purpose: Screening results (sanctions/PEP/etc.) tied to parties or transactions for compliance gating.
- When to use (example): Create a ScreeningCheck when onboarding a party or before executing a payout.
- Key columns: `Id`, `TenantId`, `PartyId`, `ResultJson`, `CheckType`, `DecidedAt`, `DecidedBy`, `Decision`, `ResultStatus`

## Notifications

### `Notifications`

- Entity: `Aonik.Domain.Notifications.Entities.Notification`
- Purpose: Notification records to users/parties (email/SMS/in-app) including delivery status and payload references.
- When to use (example): Create a Notification when an invoice is issued or a payout completes and the user should be informed.
- Key columns: `Id`, `TenantId`, `Status`, `PayloadJson`, `Channel`, `RecipientRef`, `SentAt`, `TemplateKey`

### `WebhookSubscriptions`

- Entity: `Aonik.Domain.Notifications.Entities.WebhookSubscription`
- Purpose: Webhook endpoints registered by external systems that want event callbacks.
- When to use (example): Create a WebhookSubscription so a merchant system receives events like `invoice.paid`.
- Key columns: `Id`, `TenantId`, `EventTypesJson`, `IsActive`, `EndpointUrl`, `SecretRef`, `SubscriberName`

## Operations

### `Jobs`

- Entity: `Aonik.Domain.Operations.Entities.Job`
- Purpose: Batch/scheduled job records tracking long-running operational processes and their status.
- When to use (example): Create a Job record when running nightly reconciliation or daily balance snapshot generation.
- Key columns: `Id`, `TenantId`, `Status`, `LastResultJson`, `JobType`, `LastRunAt`, `ScheduleCron`

### `WorkItems`

- Entity: `Aonik.Domain.Operations.Entities.WorkItem`
- Purpose: Operational tasks/work queue items for humans or automation (triage, reviews, follow-ups).
- When to use (example): Create a WorkItem when a payout fails and ops must investigate.
- Key columns: `Id`, `TenantId`, `Status`, `AssignedToUserId`, `ContextId`, `HistoryJson`, `ContextType`, `Priority`, `SlaDueAt`, `WorkItemType`

## Features

### `TenantFeatures`

- Entity: `Aonik.Domain.Features.Entities.TenantFeature`
- Purpose: Feature flag state per tenant (enables/disables capabilities safely).
- When to use (example): Enable a TenantFeature to roll out a new capability to a pilot tenant first.
- Key columns: `Id`, `TenantId`, `ExpiresAt`, `FeatureName`, `IsEnabled`, `Reason`
- Indexes (partial): (`FeatureName`), (`TenantId`, `FeatureName`)

## Reference Data

### `ReferenceData`

- Entity: `Aonik.Domain.ReferenceData.Entities.ReferenceDataItem`
- Purpose: Curated reference lists (countries, currencies, document types, etc.) used for validation and consistent UX.
- When to use (example): Query ReferenceData for ISO country codes when validating payout addresses or KYC forms.
- Key columns: `Id`, `TenantId`, `Type`, `IsActive`, `Code`, `DisplayName`, `SortOrder`
- Indexes (partial): (`Type`, `Code`), (`Type`, `SortOrder`), (`TenantId`, `Type`, `Code`)

## Settings

### `Settings`

- Entity: `Aonik.Domain.Settings.Entities.Setting`
- Purpose: Key/value runtime configuration scoped to tenant (and sometimes global) used to control behavior.
- When to use (example): Store a Setting like default currency, notification preferences, or integration toggles for a tenant.
- Key columns: `Id`, `TenantId`, `UserId`, `Key`, `Scope`, `Value`
- Indexes (partial): (`Scope`, `Key`, `TenantId`, `UserId`)

## AI

### `AiFeedbacks`

- Entity: `Aonik.Domain.Ai.Entities.AiFeedback`
- Purpose: Human feedback on AI runs (ratings/corrections) for improving prompts and evaluations.
- When to use (example): Create AiFeedback when an operator marks an AI result wrong and provides the correction.
- Key columns: `Id`, `AiRunId`, `Correction`, `GroundTruthRef`, `Rating`

### `AiModels`

- Entity: `Aonik.Domain.Ai.Entities.AiModel`
- Purpose: Models available via providers, with cost/latency profiles and policy tags.
- When to use (example): Add an AiModel when enabling a new model for a use case.
- Key columns: `Id`, `AiProviderId`, `CostProfileJson`, `IsActive`, `LatencyProfileJson`, `PolicyTagsJson`, `ContextWindow`, `ModelName`
- Indexes (partial): (`AiProviderId`)

### `AiPolicies`

- Entity: `Aonik.Domain.Ai.Entities.AiPolicy`
- Purpose: Safety/governance policies for AI runs (allowed data fields, redaction, escalation rules).
- When to use (example): Use an AiPolicy to forbid raw PII in prompts for a specific use case.
- Key columns: `Id`, `Name`, `AllowedDataFieldsJson`, `BannedActionsJson`, `EscalationRulesJson`, `IsActive`, `RedactionRulesJson`

### `AiProviders`

- Entity: `Aonik.Domain.Ai.Entities.AiProvider`
- Purpose: Configured AI providers (vendors) with capability metadata and auth references (not raw secrets).
- When to use (example): Create an AiProvider when adding a new LLM vendor integration.
- Key columns: `Id`, `Name`, `CapabilitiesJson`, `IsActive`, `AuthConfigRef`

### `AiRoutePolicies`

- Entity: `Aonik.Domain.Ai.Entities.AiRoutePolicy`
- Purpose: Routing policies that select which AI model to use based on use case, risk tier, and sensitivity.
- When to use (example): Create an AiRoutePolicy so low-risk tasks use a cheaper model and sensitive tasks use a stricter policy.
- Key columns: `Id`, `TenantId`, `UseCase`, `RiskTier`, `FallbackModelIdsJson`, `IsActive`, `PrimaryModelId`, `CostCeiling`, `DataSensitivity`

### `AiRuns`

- Entity: `Aonik.Domain.Ai.Entities.AiRun`
- Purpose: Audit record of an AI execution: inputs/outputs by reference, tokens, cost, latency, and outcome.
- When to use (example): Create an AiRun whenever the system calls an LLM to classify, summarize, or draft content.
- Key columns: `Id`, `TenantId`, `UseCase`, `UserId`, `AiModelId`, `AiPolicyId`, `InputRefsJson`, `PromptSpecId`, `CostEstimate`, `LatencyMs`, `Outcome`, `OutputRef`

### `AiTraces`

- Entity: `Aonik.Domain.Ai.Entities.AiTrace`
- Purpose: Detailed trace of an AI run (steps/tool calls and optional reasoning reference) for debugging and audit.
- When to use (example): Store an AiTrace when you need to show exactly which tools were called to produce an output.
- Key columns: `Id`, `AiRunId`, `StepsJson`, `ToolCallsJson`, `IntermediateReasoningRef`

### `EvalRuns`

- Entity: `Aonik.Domain.Ai.Entities.EvalRun`
- Purpose: Individual evaluation executions and stored results for an evaluation suite.
- When to use (example): Create an EvalRun when running prompt/model regression tests in CI.
- Key columns: `Id`, `AiModelId`, `EvalSuiteId`, `PromptSpecId`, `ResultsJson`, `PassFail`, `RanAt`

### `EvalSuites`

- Entity: `Aonik.Domain.Ai.Entities.EvalSuite`
- Purpose: Collections of evaluation scenarios/metrics used to test prompts/models systematically.
- When to use (example): Create an EvalSuite for a prompt before publishing changes.
- Key columns: `Id`, `Name`, `IsActive`, `MetricsJson`, `ScenariosJson`, `Domain`

### `Insights`

- Entity: `Aonik.Domain.Ai.Entities.Insight`
- Purpose: Generated insights attached to a subject (order, invoice, user, etc.) for UI surfacing and decision support.
- When to use (example): Create an Insight like `late payment risk` for a customer account to show in operations UI.
- Key columns: `Id`, `SubjectId`, `Summary`, `Title`, `CreatedUtc`, `SubjectType`
- Indexes (partial): (`CreatedUtc`), (`SubjectType`, `SubjectId`)

### `PromptSpecs`

- Entity: `Aonik.Domain.Ai.Entities.PromptSpec`
- Purpose: Versioned prompt definitions (templates + schemas) to keep AI behavior reproducible over time.
- When to use (example): Create a new PromptSpec version when improving a prompt but keeping old runs reproducible.
- Key columns: `Id`, `Name`, `OutputSchemaJson`, `VariablesSchemaJson`, `DeveloperTemplate`, `IsPublished`, `SafetyPolicyRef`, `SystemTemplate`, `Version`

### `Signals`

- Entity: `Aonik.Domain.Ai.Entities.Signal`
- Purpose: Operational/analytic signals (typed messages with severity) for monitoring and triage.
- When to use (example): Write a Signal when repeated payout failures occur for a corridor, triggering ops attention.
- Key columns: `Id`, `Type`, `Message`, `Severity`, `CreatedUtc`
- Indexes (partial): (`CreatedUtc`), (`Severity`), (`Type`)

### `ToolSpecs`

- Entity: `Aonik.Domain.Ai.Entities.ToolSpec`
- Purpose: Versioned tool contracts exposed to agents/LLMs (what tools exist and how to call them safely).
- When to use (example): Add a ToolSpec when exposing a read-only domain tool like `GetInvoiceById` to the AI platform.
- Key columns: `Id`, `Name`, `ContractJson`, `IsActive`, `RateLimitsJson`, `AuthScope`, `Domain`

## Agents

### `AgentRuns`

- Entity: `Aonik.Domain.Agents.Entities.AgentRun`
- Purpose: Execution records for agents (goal, plan/steps, linked AI runs, produced artifacts).
- When to use (example): Create an AgentRun when an agent performs `review overdue invoices and propose dunning actions`.
- Key columns: `Id`, `TenantId`, `Status`, `AgentId`, `ArtifactsProducedJson`, `LinkedAiRunIdsJson`, `PlanJson`, `StepsJson`, `Goal`

### `Agents`

- Entity: `Aonik.Domain.Agents.Entities.Agent`
- Purpose: Configured domain agents (name, domain, risk tier, toolset) that can propose actions but do not directly mutate financial state.
- When to use (example): Create an Agent like `Billing Assistant` that can draft invoice insights and produce proposals.
- Key columns: `Id`, `TenantId`, `Name`, `RiskTier`, `InputSchemaJson`, `InstructionPromptSpecId`, `IsActive`, `OutputSchemaJson`, `PermissionsProfileJson`, `ToolsetIdsJson`, `Domain`

### `OrchestratorPolicies`

- Entity: `Aonik.Domain.Agents.Entities.OrchestratorPolicy`
- Purpose: Policies that decide which agents to use for a given intent type, including preferred and fallback agent sets.
- When to use (example): Create an OrchestratorPolicy so `invoice_help` routes to the Billing agent first, then a generic assistant.
- Key columns: `Id`, `TenantId`, `FallbackAgentsJson`, `IsActive`, `PreferredAgentsJson`, `IntentType`

### `Proposals`

- Entity: `Aonik.Domain.Agents.Entities.Proposal`
- Purpose: Material action proposals produced by agents/AI, including payload, risk tier, and approval state.
- When to use (example): Create a Proposal when an agent suggests a refund or policy change; require approval before applying it.
- Key columns: `Id`, `TenantId`, `Status`, `RiskTier`, `AiRunId`, `ApprovedByUserId`, `PayloadJson`, `ProposedByAgentId`, `ApprovedAt`, `ImpactSummary`, `ProposalType`

## Personal Finance

### `Bills`

- Entity: `Aonik.Domain.PersonalFinance.Entities.Bill`
- Purpose: Bills to pay (one-off or recurring) with due dates and status.
- When to use (example): Create a Bill when a user adds their electricity bill with a due date.
- Key columns: `Id`, `TenantId`, `Status`, `Currency`, `UserId`, `LinkedInvoiceId`, `LinkedOrderId`, `Autopay`, `ExpectedAmount`, `Frequency`, `NextDueDate`, `Payee`

### `BudgetLines`

- Entity: `Aonik.Domain.PersonalFinance.Entities.BudgetLine`
- Purpose: Budget category lines under a budget with planned amounts.
- When to use (example): Add BudgetLines for `Groceries 200` and `Transport 80` for the month.
- Key columns: `Id`, `TenantId`, `Currency`, `BudgetId`, `Category`, `LimitAmount`
- Indexes (partial): (`BudgetId`)

### `Budgets`

- Entity: `Aonik.Domain.PersonalFinance.Entities.Budget`
- Purpose: Budget containers for a period (monthly/weekly) tied to a profile/household.
- When to use (example): Create a Budget at the start of a month for a household.
- Key columns: `Id`, `TenantId`, `Status`, `UserId`, `BudgetCreatedBy`, `PeriodStart`, `PeriodType`

### `CategorisationRules`

- Entity: `Aonik.Domain.PersonalFinance.Entities.CategorisationRule`
- Purpose: Rules to auto-categorize personal transactions based on merchant text, amount ranges, or other heuristics.
- When to use (example): Create a CategorisationRule so any transaction containing `Netflix` is categorized as `Subscriptions`.
- Key columns: `Id`, `TenantId`, `UserId`, `IsActive`, `Category`, `Pattern`, `Priority`

### `Goals`

- Entity: `Aonik.Domain.PersonalFinance.Entities.Goal`
- Purpose: Savings or spending goals with targets and timelines.
- When to use (example): Create a Goal like `Save 1000 by June` and track progress from personal transactions.
- Key columns: `Id`, `TenantId`, `Status`, `Name`, `Currency`, `UserId`, `ProgressAmount`, `TargetAmount`, `TargetDate`

### `HouseholdMembers`

- Entity: `Aonik.Domain.PersonalFinance.Entities.HouseholdMember`
- Purpose: Membership table linking personal profiles to households (who belongs to which household).
- When to use (example): Add a HouseholdMember when inviting another user into a household budget.
- Key columns: `Id`, `UserId`, `HouseholdId`, `PermissionsJson`, `Role`
- Indexes (partial): (`HouseholdId`)

### `Households`

- Entity: `Aonik.Domain.PersonalFinance.Entities.Household`
- Purpose: Groupings for shared personal finance (family/shared budgets) with shared artifacts.
- When to use (example): Create a Household when two users want a shared budget and shared bills list.
- Key columns: `Id`, `TenantId`, `Name`

### `PersonalProfiles`

- Entity: `Aonik.Domain.PersonalFinance.Entities.PersonalProfile`
- Purpose: Personal finance profile used by B2C features (preferences and finance-specific settings).
- When to use (example): Create a PersonalProfile when a user starts using budgeting and personal finance features.
- Key columns: `Id`, `TenantId`, `UserId`, `PartyId`, `HouseholdId`

### `PersonalTransactions`

- Entity: `Aonik.Domain.PersonalFinance.Entities.PersonalTransaction`
- Purpose: Personal finance transaction records (imported or manually entered) used for categorisation and budgeting.
- When to use (example): Create a PersonalTransaction when importing a bank statement line item or recording a cash expense.
- Key columns: `Id`, `TenantId`, `Currency`, `Amount`, `UserId`, `SourceId`, `TagsJson`, `CategorisedBy`, `Category`, `Confidence`, `Merchant`, `Notes`

### `Subscriptions`

- Entity: `Aonik.Domain.PersonalFinance.Entities.Subscription`
- Purpose: Recurring subscription commitments (service, cadence, expected amount) for forecasting and reminders.
- When to use (example): Create a Subscription for `Spotify monthly` so it appears in upcoming bills and spend analysis.
- Key columns: `Id`, `TenantId`, `Status`, `Currency`, `UserId`, `DetectedBy`, `ExpectedAmount`, `Merchant`, `RenewalDate`

## Infrastructure

### `AonikBackgroundJobRecords`

- Entity: `Aonik.Infrastructure.BackgroundJobs.Entities.BackgroundJobRecord`
- Purpose: Internal background job persistence used by the platform runtime (queue/status/retry bookkeeping).
- When to use (example): A background worker creates an AonikBackgroundJobRecord when scheduling work like sending invoice reminders.
- Key columns: `Id`, `TenantId`, `Status`, `ArgumentsJson`, `CorrelationId`, `ErrorDetailsJson`, `RetryCount`, `CompletedAt`, `ErrorMessage`, `JobName`, `LastAttemptAt`, `MaxRetryCount`
- Indexes (partial): (`NextAttemptAt`), (`Priority`), (`Status`), (`TenantId`)
