-- =============================================================================
-- AONIK Database Wipe Script (Schema-Aware by Table Name)
-- =============================================================================
-- Purpose: Wipe AONIK tables for a fresh install while keeping an explicit
-- table list and drop order.
-- WARNING: This script will DELETE ALL DATA in listed tables.
--
-- How schema resolution works:
-- - For each table name below, the script resolves schema from sys.tables.
-- - If exactly one match exists, it drops [schema].[table].
-- - If no match exists, it skips.
-- - If multiple schemas contain the same table name, it throws so you can
--   disambiguate explicitly.
-- =============================================================================

SET NOCOUNT ON;

DECLARE @Tables TABLE
(
    OrderNo int IDENTITY(1, 1) PRIMARY KEY,
    TableName sysname NOT NULL
);

-- =============================================================================
-- AI DOMAIN (drop child tables first)
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'AiFeedbacks'),
(N'AiTraces'),
(N'AiRuns'),
(N'EvalRuns'),
(N'EvalSuites'),
(N'AiRoutePolicies'),
(N'AiModels'),
(N'AiProviders'),
(N'AiPolicies'),
(N'Insights'),
(N'Signals');

-- =============================================================================
-- AGENTS DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'Proposals'),
(N'AgentRuns'),
(N'Agents'),
(N'OrchestratorPolicies');

-- =============================================================================
-- ORDERS DOMAIN (highly relational - drop child tables first)
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'OrderNotes'),
(N'OrderHistoryEvents'),
(N'OrderFulfilmentRefs'),
(N'OrderFundingRefs'),
(N'OrderPartyRoles'),
(N'OrderItems'),
(N'Orders');

-- =============================================================================
-- CROSS-DOMAIN DEPENDENCIES
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'PartnerFundingAccounts');

-- =============================================================================
-- LEDGER DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'BalanceSnapshots'),
(N'JournalEntryLines'),
(N'JournalEntries'),
(N'LedgerAccounts'),
(N'Ledgers');

-- =============================================================================
-- PAYMENTS DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'Chargebacks'),
(N'Refunds'),
(N'Payouts'),
(N'Payments'),
(N'PaymentIntents');

-- =============================================================================
-- BILLING DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'InvoiceAllocations'),
(N'InvoiceLines'),
(N'Invoices'),
(N'CustomerAccounts'),
(N'DunningPlans');

-- =============================================================================
-- PERSONAL FINANCE DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'BudgetLines'),
(N'Budgets'),
(N'Goals'),
(N'Subscriptions'),
(N'Bills'),
(N'PersonalTransactions'),
(N'CategorisationRules'),
(N'HouseholdMembers'),
(N'Households'),
(N'PersonalProfiles');

-- =============================================================================
-- PARTY DOMAIN (highly relational)
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'PartyRelationships'),
(N'PartyRoleAssignments'),
(N'ExternalAccounts'),
(N'PartyConsents'),
(N'PartyContacts'),
(N'PartyAddresses'),
(N'BusinessProfiles'),
(N'PersonProfiles'),
(N'Parties');

-- =============================================================================
-- CATALOG DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'CatalogBillerServices'),
(N'CatalogBillers'),
(N'CatalogBillerCategories');

-- =============================================================================
-- PARTNERS DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'Transmissions'),
(N'RoutingRules'),
(N'Connectors'),
(N'PayoutSchemas'),
(N'PartnerBranches'),
(N'Partners');

-- =============================================================================
-- PRICING DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'PricingQuotes'),
(N'LimitsPolicies'),
(N'FxSpreadPolicies'),
(N'FxRefreshSchedules'),
(N'FxQuotes'),
(N'FxRateSources'),
(N'FeePolicies');

-- =============================================================================
-- COMPLIANCE DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'DocumentVerifications'),
(N'DocumentUsages'),
(N'DocumentFiles'),
(N'DocumentVersions'),
(N'Documents'),
(N'AuditLogs'),
(N'ScreeningChecks'),
(N'ComplianceCases');

-- =============================================================================
-- IDENTITY & ACCESS DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'VerificationChallenges'),
(N'UserParties'),
(N'UserRoles'),
(N'RolePermissions'),
(N'Permissions'),
(N'Roles'),
(N'Users'),
(N'TenantCurrencies'),
(N'TenantCountries'),
(N'Tenants');

-- =============================================================================
-- AUTONUMBERING DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'AutonumberReservations'),
(N'AutonumberProfiles');

-- =============================================================================
-- REFERENCE DATA & SETTINGS
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'CountryCurrencies'),
(N'Settings'),
(N'ReferenceData'),
(N'Currencies'),
(N'Countries');

-- =============================================================================
-- CMS DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'ContentBlockMedia'),
(N'ContentBlocks');

-- =============================================================================
-- FEATURES DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'TenantFeatures');

-- =============================================================================
-- OPERATIONS DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'AonikBackgroundJobRecords'),
(N'WorkItems'),
(N'Jobs');

-- =============================================================================
-- NOTIFICATIONS DOMAIN
-- =============================================================================
INSERT INTO @Tables (TableName) VALUES
(N'NotificationTemplateBindings'),
(N'NotificationTemplates'),
(N'WebhookSubscriptions'),
(N'Notifications');

-- =============================================================================
-- ENTITY FRAMEWORK MIGRATIONS HISTORY
-- =============================================================================
-- This table tracks applied migrations and should be dropped last.
INSERT INTO @Tables (TableName) VALUES
(N'__EFMigrationsHistory');

DECLARE @CurrentOrder int = 1;
DECLARE @MaxOrder int = (SELECT MAX(OrderNo) FROM @Tables);
DECLARE @TableName sysname;
DECLARE @MatchCount int;
DECLARE @SchemaName sysname;
DECLARE @SchemaList nvarchar(max);
DECLARE @DropSql nvarchar(1000);
DECLARE @ErrorMessage nvarchar(2048);
DECLARE @ResolvedTableName sysname;

WHILE @CurrentOrder <= @MaxOrder
BEGIN
    SELECT @TableName = TableName
    FROM @Tables
    WHERE OrderNo = @CurrentOrder;

    SET @ResolvedTableName = @TableName;

    SELECT
        @MatchCount = COUNT(*),
        @SchemaName = MIN(s.name),
        @SchemaList = STRING_AGG(QUOTENAME(s.name), N', ')
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE t.name = @TableName
      AND t.is_ms_shipped = 0;

    -- Fallback for dbo + prefix naming (current: Ank; legacy: Plt/Fin/Ai/Agt)
    IF @MatchCount = 0
    BEGIN
        SELECT
            @MatchCount = COUNT(*),
            @SchemaName = MIN(s.name),
            @SchemaList = STRING_AGG(QUOTENAME(s.name) + N'.' + QUOTENAME(t.name), N', '),
            @ResolvedTableName = MIN(t.name)
        FROM sys.tables t
        INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE t.name IN (
              N'Ank' + @TableName,
              N'Plt' + @TableName,
              N'Fin' + @TableName,
              N'Ai' + @TableName,
              N'Agt' + @TableName)
          AND t.is_ms_shipped = 0;
    END

    IF @MatchCount = 0
    BEGIN
        PRINT N'Skipping ' + QUOTENAME(@TableName) + N' (not found).';
    END
    ELSE IF @MatchCount > 1
    BEGIN
        SET @ErrorMessage = N'Ambiguous table name ' + QUOTENAME(@TableName)
            + N' found in multiple schemas: ' + COALESCE(@SchemaList, N'(unknown)')
            + N'. Use schema-qualified drops for this table.';
        THROW 50001, @ErrorMessage, 1;
    END
    ELSE
    BEGIN
        SET @DropSql = N'DROP TABLE ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@ResolvedTableName) + N';';
        EXEC sp_executesql @DropSql;
        PRINT N'Dropped ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@ResolvedTableName) + N'.';
    END

    SET @CurrentOrder += 1;
END

PRINT N'Reset complete.';
PRINT N'Run: dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api';
