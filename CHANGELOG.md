# Changelog

All notable changes to the AONIK project will be documented in this file.

## [Unreleased]

### Fixed
- **API Tests**: Fixed test database isolation issue
  - Each `CustomWebApplicationFactory` instance now uses a consistent database name across all requests
  - Previously, each DbContext registration created a new unique database, causing resources created in one request to be invisible in subsequent requests
  - Fixed storage file locking issue by using unique storage paths per test factory instance
  - 21 of 22 API integration tests now passing (up from 10)
  - Remaining failure is due to missing Azure Communication Services configuration in test environment (expected)

- **API Tests**: Fixed database context registration in test environment
  - Added `IAonikDbContext` registration to `CustomWebApplicationFactory`
  - Tests now properly resolve database dependencies

### Changed
- **Database Configuration**: Removed InMemory database option for Development environment
  - `UseInMemoryDatabase` configuration setting removed from `appsettings.Development.json`
  - Application now uses SQL Server for all non-test environments
  - InMemory database still used for automated tests
  - Added `dbContext.Database.MigrateAsync()` to Program.cs for automatic migrations on startup in Development
  - Updated DependencyInjection.cs to remove InMemory configuration logic
  - Updated CustomWebApplicationFactory to explicitly use InMemory for tests

### Added
- **Personal Finance**: Added household creation and member invitation endpoints with service support.
- **Party Relationships**: Added relationship type catalog constants and a party endpoint to create an individual related party (e.g., friend) linked to an existing customer.
- **Autonumbering**: Added autonumbering profiles, reservations, and service models with tests for sequencing and reset behavior.
- **Autonumbering**: Added documentation covering performance considerations and reservation table usage guidance.
- **Documentation**: Added a flexible document and file model proposal for multi-purpose evidence and verification workflows.
- **Personal Finance**: Added personal accounts for imported finance sources and account-level transaction grouping.
- **Compliance**: Added document evidence entities, services, and API endpoints with storage-ready metadata fields.
- **Compliance**: Added document listing support plus Admin UI document management pages with create/detail flows.
- **Notifications**: Added notification template entities, bindings, and Scriban-based rendering service for multi-tenant shared templates.
- **Admin UI**: Added an autonumbering settings page with configuration overview and test preview.
- **Ledger**: Added tenant ledger, account, and journal entry API endpoints plus Admin UI pages for creating ledgers, accounts, and transactions.
- **Compliance**: Added related-entity document filtering and ledger-facing document upload workflows in the Admin UI.
- **Admin UI**: Role display in sidebar user profile
  - Added `identityService.getUserInfo()` to fetch user roles from `/identity/userinfo` endpoint
  - Added `formatRoleLabel()` helper to convert role names to Title Case
  - Role fetching with loading state and error handling
  - Displays user's role(s) in bottom-left sidebar profile menu
- **Admin UI**: FX Rate management page and navigation entry
  - Added a dedicated FX Rates settings page with rate sources, spread policies, and refresh cadence overview
- **Payabo Web**: Added a new root-level `Payabo/` React app scaffold with routing, layouts, and asset imports for the Payabo migration.
  - Linked the page in Settings navigation for quick access
- **Payabo Web**: Implemented the Payabo dashboard layout with upcoming bills, bill payment tabs, transactions, budgets, and organizations sections.
- **Pricing**: Added FX management tables for rate sources, spread policies, and refresh schedules.

### Added - 2026-01-17
- Added customer profile endpoints for read/update/email/password/photo flows with profile storage support and IdP account updates.

### Added - 2026-01-12
- Added reference data entities, service, and endpoint for global/tenant lookup values.

### Added - 2025-03-13
- Added onboarding verification flow tests covering service start/confirm paths, rate limiting, policy gates, and API endpoints.

### Changed - 2025-03-12
- Standardized audit event names, passed tenant/actor/correlation IDs explicitly, and masked PII fields before audit logging.
- Added audit log verification coverage for user provisioning and verification workflows.

### Added - 2025-03-11
- Added identity and onboarding endpoints for current-user profile, verification flows, and onboarding snapshots.
- Added customer profile application models and user profile service with audit logging for profile updates.

### Added - 2025-03-10
- Added messaging abstractions with Azure Communication email/SMS senders and configuration bindings.
- Added identity verification service for email/phone challenges with hashing, TTL enforcement, rate limiting, and audit logging.

### Added - 2025-03-09
- Added verification challenge domain model and EF Core configuration with supporting enums for identity verification flows.
- Added correlation IDs to audit logs and captured them from HTTP request context.
- Added audit log emission for JIT user auto-provisioning.

### Added - 2025-03-08
- Added tenant admin and operations authorization policies that accept role or permission checks.
- Added user role service plus tenant-scoped endpoints for role assignment and retrieval.
- Documented policy conventions in the permissions reference.
- Added a dev bootstrap flow to create the first tenant and assign the current user the TenantAdmin role.

### Added - 2025-03-07
- Added `ICurrentUserContext` and `HttpContextCurrentUserContext` for unified current-user data, plus claim-to-role mapping helper.

### Changed - 2025-03-07
- Updated authentication token validation to populate current-user context and resolve roles from claims or the database.

### Added - 2025-02-14
- Added scoped tenant context (`ITenantContext`/`TenantContext`) and tenant context middleware to centralize tenant resolution.

### Changed - 2025-02-14
- Updated tenant validation and tenant provider to consume `ITenantContext` instead of raw `HttpContext.Items`.
- Allowed `X-Tenant-Id` routing in any environment when explicitly configured via `Auth:TenantRouting=Header`.

### Fixed - 2025-01-08

#### Build System
- Fixed NuGet package version conflicts in `Aonik.Infrastructure.csproj`
  - Updated `Microsoft.Extensions.DependencyInjection.Abstractions` from 9.0.0 to 10.0.1 to match .NET 10 dependencies
  - Changed from `Microsoft.AspNetCore.Http.Abstractions` to `Microsoft.AspNetCore.Http` version 2.2.0 to resolve `AddHttpContextAccessor` dependency

#### Entity Framework Configurations
- **LedgerAccountConfiguration**: Fixed property mappings to match actual `LedgerAccount` entity
  - Removed non-existent `Currency` and `CreatedUtc` properties
  - Added correct properties: `Code`, `AccountType`
  
- **PaymentIntentConfiguration**: Updated to match current `PaymentIntent` entity structure
  - Removed `Reference` and `CreatedUtc` properties that don't exist on entity
  - Added correct property configurations for `PurposeType`, `PaymentMethodType`
  
- **InvoiceConfiguration**: Aligned with actual `Invoice` entity properties
  - Replaced `CustomerId` with `CustomerAccountId`
  - Replaced `InvoiceNumber` with proper date-based properties (`IssueDate`, `DueDate`)
  - Changed `TotalAmount` to `Total`, added `Subtotal`, `TaxTotal`, `DiscountTotal`
  - Updated collection mapping from `LineItems` to `Lines`
  
- **JournalEntryConfiguration**: Corrected to match `JournalEntry` entity structure
  - Removed non-existent properties (`AccountId`, `Amount`, `Currency`, `EntryUtc`, `Reference`, `Description`)
  - Added correct properties: `LedgerId`, `Timestamp`, `SourceType`, `SourceId`, `Status`
  - Added relationship mapping for `Lines` collection

#### Test Infrastructure
- Removed outdated domain tests that tested rich domain behavior not present in anemic entity model:
  - `tests/Aonik.Domain.Tests/Billing/InvoiceTests.cs` (deleted)
  - `tests/Aonik.Domain.Tests/Payments/PaymentIntentTests.cs` (deleted)

- Fixed application layer tests to include required dependencies:
  - Added `TestTenantProvider` mock implementation to `BillingServiceTests`, `PaymentServiceTests`, and `LedgerServiceTests`
  - Updated all service instantiations to include `ITenantProvider` parameter
  - Fixed test assertions to reference correct entity properties (`SourceId` instead of `AccountId` in JournalEntry tests)
  - Updated `PaymentServiceTests` to set status directly on anemic entities instead of calling non-existent behavior methods

### Current Status
- ✅ **Build Status**: All projects compile successfully with 0 errors and 0 warnings
- ⚠️ **Test Status**: Some integration and API tests still failing (separate from build errors)
- 📦 **Dependencies**: All NuGet packages resolved correctly for .NET 10

### Known Issues
- Some application and API layer tests fail due to:
  - Services returning incomplete data (e.g., empty `InvoiceNumber`, "N/A" for `Currency`)
  - Tenant context issues in API integration tests
  - These are functional test issues, not build errors

### Notes
This update focused on resolving all compilation errors and making the solution buildable. The codebase follows an **anemic domain model** pattern where:
- Domain entities are simple data containers with no business logic
- All business logic resides in application layer services
- Tests should focus on service behavior rather than entity behavior

---

## Project Structure

```
aonik/
├── src/
│   ├── Aonik.SharedKernel/     # Common primitives and abstractions
│   ├── Aonik.Domain/            # Domain entities (anemic model)
│   ├── Aonik.Application/       # Business logic and services
│   ├── Aonik.Infrastructure/    # EF Core, external services, AI providers
│   ├── Aonik.Api/              # FastEndpoints HTTP API
│   └── Aonik.Worker/           # Background jobs
├── tests/
│   ├── Aonik.Domain.Tests/
│   ├── Aonik.Application.Tests/
│   ├── Aonik.Infrastructure.Tests/
│   └── Aonik.Api.Tests/
├── AGENTS.md                    # Coding guidelines for AI agents
├── CHANGELOG.md                 # This file
└── README.md                    # Project overview
```

---

## Contributing

When contributing to this project:
1. Ensure `dotnet build Aonik.sln` succeeds with 0 errors
2. Run `dotnet test` to verify tests pass
3. Follow the coding standards in `AGENTS.md`
4. Update this CHANGELOG with your changes
5. Update relevant documentation
