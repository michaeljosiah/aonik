-- =============================================================================
-- AONIK Database Wipe Script
-- =============================================================================
-- Purpose: Completely wipe the database for a fresh install
-- WARNING: This script will DELETE ALL DATA. Use with caution!
-- 
-- Execute this script in the target database to drop all tables and reset
-- the database to a clean state before running migrations.
-- =============================================================================

-- Disable foreign key constraints temporarily to allow dropping tables
-- with dependencies
EXEC sp_MSforeachtable "ALTER TABLE ? NOCHECK CONSTRAINT all";
GO

-- =============================================================================
-- AI DOMAIN (drop child tables first)
-- =============================================================================
DROP TABLE IF EXISTS [AiFeedbacks];
DROP TABLE IF EXISTS [AiTraces];
DROP TABLE IF EXISTS [AiRuns];
DROP TABLE IF EXISTS [EvalRuns];
DROP TABLE IF EXISTS [EvalSuites];
DROP TABLE IF EXISTS [AiRoutePolicies];
DROP TABLE IF EXISTS [AiModels];
DROP TABLE IF EXISTS [AiProviders];
DROP TABLE IF EXISTS [PromptSpecs];
DROP TABLE IF EXISTS [ToolSpecs];
DROP TABLE IF EXISTS [AiPolicies];
DROP TABLE IF EXISTS [Insights];
DROP TABLE IF EXISTS [Signals];

-- =============================================================================
-- AGENTS DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [Proposals];
DROP TABLE IF EXISTS [AgentRuns];
DROP TABLE IF EXISTS [Agents];
DROP TABLE IF EXISTS [OrchestratorPolicies];

-- =============================================================================
-- ORDERS DOMAIN (highly relational - drop child tables first)
-- =============================================================================
DROP TABLE IF EXISTS [OrderNotes];
DROP TABLE IF EXISTS [OrderHistoryEvents];
DROP TABLE IF EXISTS [OrderFulfilmentRefs];
DROP TABLE IF EXISTS [OrderFundingRefs];
DROP TABLE IF EXISTS [OrderPartyRoles];
DROP TABLE IF EXISTS [OrderItems];
DROP TABLE IF EXISTS [Orders];

-- =============================================================================
-- LEDGER DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [BalanceSnapshots];
DROP TABLE IF EXISTS [JournalEntryLines];
DROP TABLE IF EXISTS [JournalEntries];
DROP TABLE IF EXISTS [LedgerAccounts];
DROP TABLE IF EXISTS [Ledgers];

-- =============================================================================
-- PAYMENTS DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [Chargebacks];
DROP TABLE IF EXISTS [Refunds];
DROP TABLE IF EXISTS [Payouts];
DROP TABLE IF EXISTS [Payments];
DROP TABLE IF EXISTS [PaymentIntents];

-- =============================================================================
-- BILLING DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [InvoiceAllocations];
DROP TABLE IF EXISTS [InvoiceLines];
DROP TABLE IF EXISTS [Invoices];
DROP TABLE IF EXISTS [CustomerAccounts];
DROP TABLE IF EXISTS [DunningPlans];

-- =============================================================================
-- PERSONAL FINANCE DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [BudgetLines];
DROP TABLE IF EXISTS [Budgets];
DROP TABLE IF EXISTS [Goals];
DROP TABLE IF EXISTS [Subscriptions];
DROP TABLE IF EXISTS [Bills];
DROP TABLE IF EXISTS [PersonalTransactions];
DROP TABLE IF EXISTS [CategorisationRules];
DROP TABLE IF EXISTS [HouseholdMembers];
DROP TABLE IF EXISTS [Households];
DROP TABLE IF EXISTS [PersonalProfiles];

-- =============================================================================
-- PARTY DOMAIN (highly relational)
-- =============================================================================
DROP TABLE IF EXISTS [PartyRelationships];
DROP TABLE IF EXISTS [PartyRoleAssignments];
DROP TABLE IF EXISTS [ExternalAccounts];
DROP TABLE IF EXISTS [PartyConsents];
DROP TABLE IF EXISTS [PartyContacts];
DROP TABLE IF EXISTS [PartyAddresses];
DROP TABLE IF EXISTS [BusinessProfiles];
DROP TABLE IF EXISTS [PersonProfiles];
DROP TABLE IF EXISTS [Parties];

-- =============================================================================
-- PARTNERS DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [Transmissions];
DROP TABLE IF EXISTS [RoutingRules];
DROP TABLE IF EXISTS [Connectors];
DROP TABLE IF EXISTS [PayoutSchemas];
DROP TABLE IF EXISTS [PartnerBranches];
DROP TABLE IF EXISTS [Partners];

-- =============================================================================
-- PRICING DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [PricingQuotes];
DROP TABLE IF EXISTS [LimitsPolicies];
DROP TABLE IF EXISTS [FxSpreadPolicies];
DROP TABLE IF EXISTS [FxRefreshSchedules];
DROP TABLE IF EXISTS [FxQuotes];
DROP TABLE IF EXISTS [FxRateSources];
DROP TABLE IF EXISTS [FeePolicies];

-- =============================================================================
-- COMPLIANCE DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [AuditLogs];
DROP TABLE IF EXISTS [ScreeningChecks];
DROP TABLE IF EXISTS [ComplianceCases];

-- =============================================================================
-- IDENTITY & ACCESS DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [VerificationChallenges];
DROP TABLE IF EXISTS [UserParties];
DROP TABLE IF EXISTS [UserRoles];
DROP TABLE IF EXISTS [RolePermissions];
DROP TABLE IF EXISTS [Permissions];
DROP TABLE IF EXISTS [Roles];
DROP TABLE IF EXISTS [Users];
DROP TABLE IF EXISTS [TenantCurrencies];
DROP TABLE IF EXISTS [TenantCountries];
DROP TABLE IF EXISTS [Tenants];

-- =============================================================================
-- AUTONUMBERING DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [AutonumberReservations];
DROP TABLE IF EXISTS [AutonumberProfiles];

-- =============================================================================
-- REFERENCE DATA & SETTINGS
-- =============================================================================
DROP TABLE IF EXISTS [CountryCurrencies];
DROP TABLE IF EXISTS [Settings];
DROP TABLE IF EXISTS [ReferenceDataItems];
DROP TABLE IF EXISTS [Currencies];
DROP TABLE IF EXISTS [Countries];

-- =============================================================================
-- CMS DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [ContentBlockMedia];
DROP TABLE IF EXISTS [ContentBlocks];

-- =============================================================================
-- CATALOG DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [CatalogBillerServices];
DROP TABLE IF EXISTS [CatalogBillers];
DROP TABLE IF EXISTS [CatalogBillerCategories];

-- =============================================================================
-- FEATURES DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [TenantFeatures];

-- =============================================================================
-- OPERATIONS DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [WorkItems];
DROP TABLE IF EXISTS [Jobs];

-- =============================================================================
-- NOTIFICATIONS DOMAIN
-- =============================================================================
DROP TABLE IF EXISTS [WebhookSubscriptions];
DROP TABLE IF EXISTS [Notifications];

-- =============================================================================
-- ENTITY FRAMEWORK MIGRATIONS HISTORY
-- =============================================================================
-- This table tracks applied migrations and must be dropped last
DROP TABLE IF EXISTS [__EFMigrationsHistory];

-- Re-enable foreign key constraints (though all tables are now dropped)
EXEC sp_MSforeachtable "ALTER TABLE ? CHECK CONSTRAINT all";
GO

-- =============================================================================
-- RESET COMPLETE
-- =============================================================================
-- The database is now completely wiped and ready for fresh migrations.
-- Run the following command to recreate the schema:
--   dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
-- =============================================================================
