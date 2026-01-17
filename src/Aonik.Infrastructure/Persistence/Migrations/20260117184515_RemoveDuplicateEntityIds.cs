using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDuplicateEntityIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_TenantId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "WorkItemId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "WebhookSubscriptionId",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "TransmissionId",
                table: "Transmissions");

            migrationBuilder.DropColumn(
                name: "ToolSpecId",
                table: "ToolSpecs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ScreeningCheckId",
                table: "ScreeningChecks");

            migrationBuilder.DropColumn(
                name: "RoutingRuleId",
                table: "RoutingRules");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "RefundId",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "ProposalId",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "PromptSpecId",
                table: "PromptSpecs");

            migrationBuilder.DropColumn(
                name: "PersonalTransactionId",
                table: "PersonalTransactions");

            migrationBuilder.DropColumn(
                name: "PermissionId",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "PayoutSchemaId",
                table: "PayoutSchemas");

            migrationBuilder.DropColumn(
                name: "PayoutId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentIntentId",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "PartyRoleAssignmentId",
                table: "PartyRoleAssignments");

            migrationBuilder.DropColumn(
                name: "PartyContactId",
                table: "PartyContacts");

            migrationBuilder.DropColumn(
                name: "PartyConsentId",
                table: "PartyConsents");

            migrationBuilder.DropColumn(
                name: "PartyAddressId",
                table: "PartyAddresses");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "PartnerBranchId",
                table: "PartnerBranches");

            migrationBuilder.DropColumn(
                name: "PartyId",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderPartyRoleId",
                table: "OrderPartyRoles");

            migrationBuilder.DropColumn(
                name: "OrderNoteId",
                table: "OrderNotes");

            migrationBuilder.DropColumn(
                name: "OrderHistoryEventId",
                table: "OrderHistoryEvents");

            migrationBuilder.DropColumn(
                name: "OrderFundingRefId",
                table: "OrderFundingRefs");

            migrationBuilder.DropColumn(
                name: "OrderFulfilmentRefId",
                table: "OrderFulfilmentRefs");

            migrationBuilder.DropColumn(
                name: "OrchestratorPolicyId",
                table: "OrchestratorPolicies");

            migrationBuilder.DropColumn(
                name: "NotificationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LimitsPolicyId",
                table: "LimitsPolicies");

            migrationBuilder.DropColumn(
                name: "LedgerId",
                table: "Ledgers");

            migrationBuilder.DropColumn(
                name: "LedgerAccountId",
                table: "LedgerAccounts");

            migrationBuilder.DropColumn(
                name: "JournalEntryLineId",
                table: "JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceLineId",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "InvoiceAllocationId",
                table: "InvoiceAllocations");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Households");

            migrationBuilder.DropColumn(
                name: "GoalId",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "FxQuoteId",
                table: "FxQuotes");

            migrationBuilder.DropColumn(
                name: "FeePolicyId",
                table: "FeePolicies");

            migrationBuilder.DropColumn(
                name: "ExternalAccountId",
                table: "ExternalAccounts");

            migrationBuilder.DropColumn(
                name: "EvalSuiteId",
                table: "EvalSuites");

            migrationBuilder.DropColumn(
                name: "EvalRunId",
                table: "EvalRuns");

            migrationBuilder.DropColumn(
                name: "DunningPlanId",
                table: "DunningPlans");

            migrationBuilder.DropColumn(
                name: "CustomerAccountId",
                table: "CustomerAccounts");

            migrationBuilder.DropColumn(
                name: "ConnectorId",
                table: "Connectors");

            migrationBuilder.DropColumn(
                name: "ComplianceCaseId",
                table: "ComplianceCases");

            migrationBuilder.DropColumn(
                name: "ChargebackId",
                table: "Chargebacks");

            migrationBuilder.DropColumn(
                name: "CategorisationRuleId",
                table: "CategorisationRules");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "BudgetLineId",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "BillId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "BalanceSnapshotId",
                table: "BalanceSnapshots");

            migrationBuilder.DropColumn(
                name: "AuditLogId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "AiTraceId",
                table: "AiTraces");

            migrationBuilder.DropColumn(
                name: "AiRunId",
                table: "AiRuns");

            migrationBuilder.DropColumn(
                name: "AiRoutePolicyId",
                table: "AiRoutePolicies");

            migrationBuilder.DropColumn(
                name: "AiProviderId",
                table: "AiProviders");

            migrationBuilder.DropColumn(
                name: "AiPolicyId",
                table: "AiPolicies");

            migrationBuilder.DropColumn(
                name: "AiModelId",
                table: "AiModels");

            migrationBuilder.DropColumn(
                name: "AiFeedbackId",
                table: "AiFeedbacks");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "AgentRunId",
                table: "AgentRuns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkItemId",
                table: "WorkItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "WebhookSubscriptionId",
                table: "WebhookSubscriptions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TransmissionId",
                table: "Transmissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ToolSpecId",
                table: "ToolSpecs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionId",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ScreeningCheckId",
                table: "ScreeningChecks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RoutingRuleId",
                table: "RoutingRules",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "Roles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RefundId",
                table: "Refunds",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProposalId",
                table: "Proposals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PromptSpecId",
                table: "PromptSpecs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalTransactionId",
                table: "PersonalTransactions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PermissionId",
                table: "Permissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PayoutSchemaId",
                table: "PayoutSchemas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PayoutId",
                table: "Payouts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentIntentId",
                table: "PaymentIntents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PartyRoleAssignmentId",
                table: "PartyRoleAssignments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PartyContactId",
                table: "PartyContacts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PartyConsentId",
                table: "PartyConsents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PartyAddressId",
                table: "PartyAddresses",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PartnerId",
                table: "Partners",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PartnerBranchId",
                table: "PartnerBranches",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PartyId",
                table: "Parties",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrderPartyRoleId",
                table: "OrderPartyRoles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrderNoteId",
                table: "OrderNotes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrderHistoryEventId",
                table: "OrderHistoryEvents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrderFundingRefId",
                table: "OrderFundingRefs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrderFulfilmentRefId",
                table: "OrderFulfilmentRefs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrchestratorPolicyId",
                table: "OrchestratorPolicies",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "NotificationId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "LimitsPolicyId",
                table: "LimitsPolicies",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "LedgerId",
                table: "Ledgers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "LedgerAccountId",
                table: "LedgerAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryLineId",
                table: "JournalEntryLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "JournalEntries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "JobId",
                table: "Jobs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceLineId",
                table: "InvoiceLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceAllocationId",
                table: "InvoiceAllocations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdId",
                table: "Households",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "GoalId",
                table: "Goals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FxQuoteId",
                table: "FxQuotes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FeePolicyId",
                table: "FeePolicies",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ExternalAccountId",
                table: "ExternalAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EvalSuiteId",
                table: "EvalSuites",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EvalRunId",
                table: "EvalRuns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DunningPlanId",
                table: "DunningPlans",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerAccountId",
                table: "CustomerAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConnectorId",
                table: "Connectors",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ComplianceCaseId",
                table: "ComplianceCases",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ChargebackId",
                table: "Chargebacks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CategorisationRuleId",
                table: "CategorisationRules",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BudgetId",
                table: "Budgets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BudgetLineId",
                table: "BudgetLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BillId",
                table: "Bills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BalanceSnapshotId",
                table: "BalanceSnapshots",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AuditLogId",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AiTraceId",
                table: "AiTraces",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AiRunId",
                table: "AiRuns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AiRoutePolicyId",
                table: "AiRoutePolicies",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AiProviderId",
                table: "AiProviders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AiPolicyId",
                table: "AiPolicies",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AiModelId",
                table: "AiModels",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AiFeedbackId",
                table: "AiFeedbacks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AgentId",
                table: "Agents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AgentRunId",
                table: "AgentRuns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantId",
                table: "Tenants",
                column: "TenantId",
                unique: true);
        }
    }
}
