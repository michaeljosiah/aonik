using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureRowVersionConcurrencyTokens : Migration
    {
        /// <summary>
        /// All tables that currently have a RowVersion column of type varbinary(max).
        /// We need to drop the column and re-add it as native SQL Server rowversion,
        /// because ALTER COLUMN to rowversion is not supported.
        /// </summary>
        private static readonly string[] Tables =
        [
            "AnkAgentRuns",
            "AnkAgents",
            "AnkAiFeedbacks",
            "AnkAiModels",
            "AnkAiPolicies",
            "AnkAiProviders",
            "AnkAiRoutePolicies",
            "AnkAiRuns",
            "AnkAiTraces",
            "AnkAuditLogs",
            "AnkAutonumberProfiles",
            "AnkAutonumberReservations",
            "AnkBalanceSnapshots",
            "AnkBills",
            "AnkBudgetLines",
            "AnkBudgets",
            "AnkBusinessProfiles",
            "AnkCatalogBillerCategories",
            "AnkCatalogBillers",
            "AnkCatalogBillerServices",
            "AnkCategorisationRules",
            "AnkChargebacks",
            "AnkComplianceCases",
            "AnkConnectors",
            "AnkContentBlocks",
            "AnkCountries",
            "AnkCountryCurrencies",
            "AnkCurrencies",
            "AnkCustomerAccounts",
            "AnkDocumentFiles",
            "AnkDocuments",
            "AnkDocumentUsages",
            "AnkDocumentVerifications",
            "AnkDocumentVersions",
            "AnkDunningPlans",
            "AnkEvalRuns",
            "AnkEvalSuites",
            "AnkExternalAccounts",
            "AnkFeePolicies",
            "AnkFinancialConnections",
            "AnkFinancialConnectionSessions",
            "AnkFinancialLifeGraphEdges",
            "AnkFinancialLifeGraphNodes",
            "AnkFinancialLinkedAccounts",
            "AnkFinancialWebhookEvents",
            "AnkFxQuotes",
            "AnkFxRateSources",
            "AnkFxRefreshSchedules",
            "AnkFxSpreadPolicies",
            "AnkGoals",
            "AnkHouseholdMembers",
            "AnkHouseholds",
            "AnkInvoiceAllocations",
            "AnkInvoiceLines",
            "AnkInvoices",
            "AnkJobs",
            "AnkJournalEntries",
            "AnkJournalEntryLines",
            "AnkLedgerAccounts",
            "AnkLedgers",
            "AnkLimitsPolicies",
            "AnkNotifications",
            "AnkNotificationTemplateBindings",
            "AnkNotificationTemplates",
            "AnkOrchestratorPolicies",
            "AnkOrderFulfilmentRefs",
            "AnkOrderFundingRefs",
            "AnkOrderHistoryEvents",
            "AnkOrderItems",
            "AnkOrderNotes",
            "AnkOrderPartyRoles",
            "AnkOrders",
            "AnkParties",
            "AnkPartnerBranches",
            "AnkPartnerFundingAccounts",
            "AnkPartners",
            "AnkPartyAddresses",
            "AnkPartyConsents",
            "AnkPartyContacts",
            "AnkPartyRelationships",
            "AnkPartyRoleAssignments",
            "AnkPaymentIntents",
            "AnkPayments",
            "AnkPayouts",
            "AnkPayoutSchemas",
            "AnkPermissions",
            "AnkPersonalAccounts",
            "AnkPersonalProfiles",
            "AnkPersonalTransactions",
            "AnkPersonProfiles",
            "AnkPricingQuotes",
            "AnkPromptSpecs",
            "AnkProposals",
            "AnkReferenceData",
            "AnkRefunds",
            "AnkRolePermissions",
            "AnkRoles",
            "AnkRoutingRules",
            "AnkScreeningChecks",
            "AnkSettings",
            "AnkStatementImportRows",
            "AnkStatementImports",
            "AnkSubscriptions",
            "AnkTenantCountries",
            "AnkTenantCurrencies",
            "AnkTenantFeatures",
            "AnkTenants",
            "AnkToolSpecs",
            "AnkTransmissions",
            "AnkUserParties",
            "AnkUserRoles",
            "AnkUsers",
            "AnkVerificationChallenges",
            "AnkWebhookSubscriptions",
            "AnkWorkItems"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Incidental model fix: AnkProposals.Status was nvarchar(max), should be nvarchar(50)
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkProposals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // Convert every RowVersion column from varbinary(max) to the native
            // SQL Server rowversion type. ALTER COLUMN is not supported for this
            // conversion, so we must DROP and re-ADD the column. All existing values
            // were 0x (empty), so no data is lost. The database engine will
            // auto-populate each row with a unique 8-byte version stamp.
            //
            // Some tables have DEFAULT constraints on RowVersion that must be
            // dropped first, otherwise DROP COLUMN fails with error 5074.
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(
                    $"""
                     DECLARE @constraint NVARCHAR(256);
                     SELECT @constraint = d.name
                     FROM sys.default_constraints d
                     JOIN sys.columns c ON d.parent_column_id = c.column_id
                                        AND d.parent_object_id = c.object_id
                     WHERE c.name = 'RowVersion'
                       AND d.parent_object_id = OBJECT_ID('[dbo].[{table}]');
                     IF @constraint IS NOT NULL
                         EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @constraint + ']');
                     ALTER TABLE [dbo].[{table}] DROP COLUMN [RowVersion];
                     ALTER TABLE [dbo].[{table}] ADD [RowVersion] rowversion NOT NULL;
                     """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert AnkProposals.Status back to nvarchar(max)
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkProposals",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            // Convert back to varbinary(max) with empty default.
            // rowversion columns cannot be altered, so DROP + ADD again.
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(
                    $"""
                     ALTER TABLE [dbo].[{table}] DROP COLUMN [RowVersion];
                     ALTER TABLE [dbo].[{table}] ADD [RowVersion] varbinary(max) NOT NULL DEFAULT 0x;
                     """);
            }
        }
    }
}
