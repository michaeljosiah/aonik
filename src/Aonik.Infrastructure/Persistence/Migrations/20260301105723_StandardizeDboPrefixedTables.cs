using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeDboPrefixedTables : Migration
    {
        private const string UnifiedPrefix = "Ank";

        private static readonly string[] PlatformTables =
        {
            "Tenants",
            "TenantCountries",
            "TenantCurrencies",
            "Users",
            "Roles",
            "Permissions",
            "UserRoles",
            "RolePermissions",
            "UserParties",
            "VerificationChallenges",
            "Parties",
            "PartyAddresses",
            "PartyContacts",
            "PartyConsents",
            "PersonProfiles",
            "BusinessProfiles",
            "ExternalAccounts",
            "PartyRoleAssignments",
            "PartyRelationships",
            "ScreeningChecks",
            "ComplianceCases",
            "AuditLogs",
            "Documents",
            "DocumentFiles",
            "DocumentUsages",
            "DocumentVerifications",
            "DocumentVersions",
            "Notifications",
            "NotificationTemplates",
            "NotificationTemplateBindings",
            "WebhookSubscriptions",
            "WorkItems",
            "Jobs",
            "Settings",
            "TenantFeatures",
            "ReferenceData",
            "Countries",
            "Currencies",
            "CountryCurrencies",
            "ContentBlocks",
            "ContentBlockMedia",
            "AutonumberProfiles",
            "AutonumberReservations"
        };

        private static readonly string[] FinanceTables =
        {
            "Ledgers",
            "LedgerAccounts",
            "JournalEntries",
            "JournalEntryLines",
            "BalanceSnapshots",
            "PaymentIntents",
            "Payments",
            "Payouts",
            "Refunds",
            "Chargebacks",
            "Invoices",
            "InvoiceLines",
            "InvoiceAllocations",
            "CustomerAccounts",
            "DunningPlans",
            "Orders",
            "OrderItems",
            "OrderPartyRoles",
            "OrderFundingRefs",
            "OrderFulfilmentRefs",
            "OrderHistoryEvents",
            "OrderNotes",
            "FeePolicies",
            "FxQuotes",
            "FxRateSources",
            "FxRefreshSchedules",
            "FxSpreadPolicies",
            "LimitsPolicies",
            "PricingQuotes",
            "Partners",
            "PartnerBranches",
            "PartnerFundingAccounts",
            "Connectors",
            "RoutingRules",
            "PayoutSchemas",
            "Transmissions",
            "CatalogBillerCategories",
            "CatalogBillers",
            "CatalogBillerServices",
            "PersonalProfiles",
            "Households",
            "HouseholdMembers",
            "PersonalAccounts",
            "PersonalTransactions",
            "CategorisationRules",
            "BudgetLines",
            "Bills",
            "Subscriptions",
            "Goals",
            "Budgets"
        };

        private static readonly string[] AiTables =
        {
            "AiProviders",
            "AiModels",
            "AiRoutePolicies",
            "PromptSpecs",
            "ToolSpecs",
            "AiPolicies",
            "AiRuns",
            "AiTraces",
            "AiFeedbacks",
            "EvalSuites",
            "EvalRuns",
            "Insights",
            "Signals"
        };

        private static readonly string[] AgentsTables =
        {
            "Agents",
            "AgentRuns",
            "OrchestratorPolicies",
            "Proposals"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "dbo");

            RenameTablesToPrefixedNames(migrationBuilder, PlatformTables, UnifiedPrefix);
            RenameTablesToPrefixedNames(migrationBuilder, FinanceTables, UnifiedPrefix);
            RenameTablesToPrefixedNames(migrationBuilder, AiTables, UnifiedPrefix);
            RenameTablesToPrefixedNames(migrationBuilder, AgentsTables, UnifiedPrefix);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RenamePrefixedTablesToLegacyNames(migrationBuilder, AgentsTables, UnifiedPrefix);
            RenamePrefixedTablesToLegacyNames(migrationBuilder, AiTables, UnifiedPrefix);
            RenamePrefixedTablesToLegacyNames(migrationBuilder, FinanceTables, UnifiedPrefix);
            RenamePrefixedTablesToLegacyNames(migrationBuilder, PlatformTables, UnifiedPrefix);
        }

        private static void RenameTablesToPrefixedNames(
            MigrationBuilder migrationBuilder,
            IEnumerable<string> tableNames,
            string prefix)
        {
            foreach (var tableName in tableNames)
            {
                var prefixedTableName = prefix + tableName;
                MoveAndRenameToDbo(migrationBuilder, tableName, prefixedTableName);
            }
        }

        private static void RenamePrefixedTablesToLegacyNames(
            MigrationBuilder migrationBuilder,
            IEnumerable<string> tableNames,
            string prefix)
        {
            foreach (var tableName in tableNames)
            {
                var prefixedTableName = prefix + tableName;
                RenameWithinDbo(migrationBuilder, prefixedTableName, tableName);
            }
        }

        private static void MoveAndRenameToDbo(
            MigrationBuilder migrationBuilder,
            string sourceTableName,
            string targetTableName)
        {
            migrationBuilder.Sql($"""
IF OBJECT_ID(N'[dbo].[{targetTableName}]', N'U') IS NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[{sourceTableName}]', N'U') IS NOT NULL
        EXEC sp_rename N'[dbo].[{sourceTableName}]', N'{targetTableName}';
    ELSE IF OBJECT_ID(N'[platform].[{sourceTableName}]', N'U') IS NOT NULL
    BEGIN
        ALTER SCHEMA [dbo] TRANSFER [platform].[{sourceTableName}];
        EXEC sp_rename N'[dbo].[{sourceTableName}]', N'{targetTableName}';
    END
    ELSE IF OBJECT_ID(N'[finance].[{sourceTableName}]', N'U') IS NOT NULL
    BEGIN
        ALTER SCHEMA [dbo] TRANSFER [finance].[{sourceTableName}];
        EXEC sp_rename N'[dbo].[{sourceTableName}]', N'{targetTableName}';
    END
    ELSE IF OBJECT_ID(N'[ai].[{sourceTableName}]', N'U') IS NOT NULL
    BEGIN
        ALTER SCHEMA [dbo] TRANSFER [ai].[{sourceTableName}];
        EXEC sp_rename N'[dbo].[{sourceTableName}]', N'{targetTableName}';
    END
    ELSE IF OBJECT_ID(N'[agents].[{sourceTableName}]', N'U') IS NOT NULL
    BEGIN
        ALTER SCHEMA [dbo] TRANSFER [agents].[{sourceTableName}];
        EXEC sp_rename N'[dbo].[{sourceTableName}]', N'{targetTableName}';
    END
END
""");
        }

        private static void RenameWithinDbo(
            MigrationBuilder migrationBuilder,
            string sourceTableName,
            string targetTableName)
        {
            migrationBuilder.Sql($"""
IF OBJECT_ID(N'[dbo].[{targetTableName}]', N'U') IS NULL
   AND OBJECT_ID(N'[dbo].[{sourceTableName}]', N'U') IS NOT NULL
BEGIN
    EXEC sp_rename N'[dbo].[{sourceTableName}]', N'{targetTableName}';
END
""");
        }
    }
}
