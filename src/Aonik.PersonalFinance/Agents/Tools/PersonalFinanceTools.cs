using Aonik.PersonalFinance.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Finance;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Platform;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.PersonalFinance.Agents.Tools;

/// <summary>
/// Static composition surface for the AI agent tools that back personal finance
/// ("Simi"). The tool methods themselves live on focused capability classes
/// (<see cref="PersonalFinanceAccountTools"/>, <see cref="PersonalFinanceTransactionTools"/>,
/// <see cref="PersonalFinanceBillTools"/>, <see cref="PersonalFinanceBudgetTools"/>,
/// <see cref="PersonalFinanceDashboardTools"/>, <see cref="PersonalFinanceCommitmentTools"/>,
/// <see cref="PersonalFinanceCompassTools"/>, <see cref="PersonalFinanceInsightTools"/>,
/// <see cref="PersonalFinanceOrderTools"/>), each deriving from
/// <see cref="PersonalFinanceToolGroup"/> and taking only the domain services it
/// needs (#118 / Spec 027 S1). This class only composes them into the
/// <see cref="AITool"/> set via <see cref="AIFunctionFactory.Create"/>.
/// Read-only tools are safe for autonomous use; mutating tools are gated
/// server-side by the <c>IToolApprovalGate</c> (Spec 032).
/// </summary>
internal static class PersonalFinanceTools
{
    // ── Tool Factory ──────────────────────────────────────────────

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all personal finance tools.
    /// Mutating tools (CreateAccount, ArchiveAccount, CreateManualTransaction,
    /// CreateBill, UpdateBill, ArchiveBill, CreateBudget, UpdateBudgetAmount,
    /// DeleteBudget, CreateCommitmentFromTransaction, ConfirmCommitment,
    /// RejectCommitment, OverrideTransactionCategory, CreateCategorisationRule,
    /// ApplyStatementImport, DeleteTransactionAttachment, CancelOrder) are gated
    /// server-side by the <c>IToolApprovalGate</c> (Spec 032), classified by
    /// PersonalFinanceToolApprovalManifest (all Medium/Low — PersonalFinance moves no money).
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        // The six CRUD groups take only their own domain service(s) — no shared
        // base, no DB/agent plumbing they never use (#118 cohesion goal).
        var accounts = new PersonalFinanceAccountTools(
            serviceProvider.GetRequiredService<IPersonalAccountService>());

        var transactions = new PersonalFinanceTransactionTools(
            serviceProvider.GetRequiredService<IPersonalTransactionService>(),
            serviceProvider.GetRequiredService<ITransactionClassificationService>(),
            serviceProvider.GetRequiredService<IStatementImportService>(),
            serviceProvider.GetRequiredService<ITransactionAttachmentService>());

        var bills = new PersonalFinanceBillTools(
            serviceProvider.GetRequiredService<IBillService>());

        var budgets = new PersonalFinanceBudgetTools(
            serviceProvider.GetRequiredService<IBudgetService>(),
            serviceProvider.GetRequiredService<IPersonalFinanceInsightsService>());

        var dashboard = new PersonalFinanceDashboardTools(
            serviceProvider.GetRequiredService<IDashboardService>(),
            serviceProvider.GetRequiredService<IFxRateHistoryReader>());

        var commitments = new PersonalFinanceCommitmentTools(
            serviceProvider.GetRequiredService<ICommitmentService>());

        // The two sub-agent groups share the Spec 025 sub-agent build machinery
        // (PersonalFinanceSubAgentToolGroup) — chat client, service provider,
        // agent config, and tenant/user for the impersonation snapshot.
        var chatClient = serviceProvider.GetRequiredService<IChatClient>();
        var agentConfigurationService = serviceProvider.GetRequiredService<IAgentConfigurationService>();
        var tenantProvider = serviceProvider.GetRequiredService<ITenantProvider>();
        var currentUserProvider = serviceProvider.GetRequiredService<ICurrentUserProvider>();

        var compass = new PersonalFinanceCompassTools(
            serviceProvider.GetRequiredService<IGoalService>(),
            serviceProvider.GetRequiredService<ICompassPlanService>(),
            serviceProvider.GetRequiredService<ICompassGuidanceService>(),
            chatClient, serviceProvider, agentConfigurationService, tenantProvider, currentUserProvider);

        var insights = new PersonalFinanceInsightTools(
            serviceProvider.GetRequiredService<ICustomerInsightSnapshotReader>(),
            chatClient, serviceProvider, agentConfigurationService, tenantProvider, currentUserProvider);

        // Orders read/cancel through the customer-facing order contract and resolve
        // the caller's party via IUserPartyResolver — no dependency on Finance internals.
        var orders = new PersonalFinanceOrderTools(
            serviceProvider.GetRequiredService<ICustomerOrderService>(),
            serviceProvider.GetRequiredService<IUserPartyResolver>(),
            tenantProvider, currentUserProvider);

        // Read-only — safe for autonomous use
        yield return AIFunctionFactory.Create(accounts.ListAccounts, name: "pf_list_accounts");
        yield return AIFunctionFactory.Create(accounts.GetAccount, name: "pf_get_account");
        yield return AIFunctionFactory.Create(transactions.ListTransactions, name: "pf_list_transactions");
        yield return AIFunctionFactory.Create(transactions.GetTransaction, name: "pf_get_transaction");
        yield return AIFunctionFactory.Create(bills.ListBills, name: "pf_list_bills");
        yield return AIFunctionFactory.Create(bills.GetBill, name: "pf_get_bill");
        yield return AIFunctionFactory.Create(bills.GetUpcomingBills, name: "pf_get_upcoming_bills");
        yield return AIFunctionFactory.Create(budgets.ListBudgets, name: "pf_list_budgets");
        yield return AIFunctionFactory.Create(budgets.GetSpendingSummary, name: "pf_get_spending_summary");
        yield return AIFunctionFactory.Create(budgets.GetCategoryBreakdown, name: "pf_get_category_breakdown");
        yield return AIFunctionFactory.Create(budgets.GetMerchantBreakdown, name: "pf_get_merchant_breakdown");
        yield return AIFunctionFactory.Create(budgets.GetAccountBreakdown, name: "pf_get_account_breakdown");
        yield return AIFunctionFactory.Create(budgets.GetMerchantHistory, name: "pf_get_merchant_history");
        yield return AIFunctionFactory.Create(dashboard.GetDashboard, name: "pf_get_dashboard");
        yield return AIFunctionFactory.Create(dashboard.GetFxRateHistory, name: "pf_get_fx_rate_history");

        // Spec 025 §5 — three sub-agent triggers replace the legacy
        // pf_run_spending_intelligence / pf_run_obligation_planning pair.
        // The legacy descriptors stay registered in DI but no longer appear
        // in Simi's tool catalogue (Phase 6 removes them entirely).
        yield return AIFunctionFactory.Create(insights.RunInsights, name: "pf_run_insights");
        yield return AIFunctionFactory.Create(insights.RunForecast, name: "pf_run_forecast");
        yield return AIFunctionFactory.Create(insights.RunClassifyReview, name: "pf_run_classify_review");

        // Spec 021 — AONIK Compass read tools (goals, plans, safe-to-spend,
        // and a read-only planner preview).
        yield return AIFunctionFactory.Create(compass.ListGoals, name: "pf_list_goals");
        yield return AIFunctionFactory.Create(compass.GetGoal, name: "pf_get_goal");
        yield return AIFunctionFactory.Create(compass.GetGoalPlan, name: "pf_get_goal_plan");
        yield return AIFunctionFactory.Create(compass.GetSafeToSpend, name: "pf_get_safe_to_spend");
        yield return AIFunctionFactory.Create(compass.RunCompassPlanner, name: "pf_run_compass_planner");

        yield return AIFunctionFactory.Create(commitments.ListCommitments, name: "pf_list_commitments");
        yield return AIFunctionFactory.Create(commitments.GetCommitment, name: "pf_get_commitment");
        yield return AIFunctionFactory.Create(commitments.ListDetectedCommitments, name: "pf_list_detected_commitments");
        yield return AIFunctionFactory.Create(transactions.ListClassificationReviewQueue, name: "pf_list_classification_review_queue");
        yield return AIFunctionFactory.Create(transactions.ListStatementImports, name: "pf_list_statement_imports");
        yield return AIFunctionFactory.Create(transactions.ListStatementImportRows, name: "pf_list_statement_import_rows");
        yield return AIFunctionFactory.Create(transactions.ListTransactionAttachments, name: "pf_list_transaction_attachments");
        yield return AIFunctionFactory.Create(insights.ListSnapshotHistory, name: "pf_list_snapshot_history");
        yield return AIFunctionFactory.Create(insights.CompareSnapshots, name: "pf_compare_snapshots");
        yield return AIFunctionFactory.Create(orders.ListOrders, name: "pf_list_orders");
        yield return AIFunctionFactory.Create(orders.GetOrder, name: "pf_get_order");

        // Mutating — gated server-side by the IToolApprovalGate (PersonalFinanceToolApprovalManifest)
        yield return AIFunctionFactory.Create(accounts.CreateAccount, name: "pf_create_account");
        yield return AIFunctionFactory.Create(accounts.ArchiveAccount, name: "pf_archive_account");
        yield return AIFunctionFactory.Create(transactions.CreateManualTransaction, name: "pf_create_transaction");
        yield return AIFunctionFactory.Create(bills.CreateBill, name: "pf_create_bill");
        yield return AIFunctionFactory.Create(bills.UpdateBill, name: "pf_update_bill");
        yield return AIFunctionFactory.Create(bills.ArchiveBill, name: "pf_archive_bill");
        yield return AIFunctionFactory.Create(budgets.CreateBudget, name: "pf_create_budget");
        yield return AIFunctionFactory.Create(budgets.UpdateBudgetAmount, name: "pf_update_budget_amount");
        yield return AIFunctionFactory.Create(budgets.DeleteBudget, name: "pf_delete_budget");
        yield return AIFunctionFactory.Create(commitments.CreateCommitmentFromTransaction, name: "pf_create_commitment_from_transaction");
        yield return AIFunctionFactory.Create(commitments.ConfirmCommitment, name: "pf_confirm_commitment");
        yield return AIFunctionFactory.Create(commitments.RejectCommitment, name: "pf_reject_commitment");
        yield return AIFunctionFactory.Create(transactions.OverrideTransactionCategory, name: "pf_override_transaction_category");
        yield return AIFunctionFactory.Create(transactions.CreateCategorisationRule, name: "pf_create_categorisation_rule");
        yield return AIFunctionFactory.Create(transactions.ApplyStatementImport, name: "pf_apply_statement_import");
        yield return AIFunctionFactory.Create(transactions.DeleteTransactionAttachment, name: "pf_delete_transaction_attachment");
        yield return AIFunctionFactory.Create(orders.CancelOrder, name: "pf_cancel_order");

        // Spec 021 — AONIK Compass mutating tools. Gated server-side by the
        // IToolApprovalGate (PersonalFinanceToolApprovalManifest, all Medium —
        // Compass guides, it never moves money: recommendations are Proposals).
        yield return AIFunctionFactory.Create(compass.CreateGoalProgramme, name: "pf_create_goal_programme");
        yield return AIFunctionFactory.Create(compass.UpdateGoalProgramme, name: "pf_update_goal_programme");
        yield return AIFunctionFactory.Create(compass.GenerateGoalPlan, name: "pf_generate_goal_plan");
        yield return AIFunctionFactory.Create(compass.CreateCompassProposal, name: "pf_create_compass_proposal");
    }

    // ── Per-Sub-Agent Read-Only Tool Slices (Spec 025) ───────────
    //
    // These slices feed the three CodeAct-powered analytical sub-agents
    // introduced in `docs/specifications/025.personal-finance-agent-split-and-codeact.html`.
    // Each whitelist is pure read-only: mutations stay on Simi's direct
    // surface, where the server-side approval gate (Spec 032) gates every
    // change (CodeAct's whole-block approval semantics therefore never
    // trigger inside a sub-agent sandbox).
    //
    // Tool definitions and `[Description]` strings remain authored once on the
    // capability classes — the slice methods just filter `CreateAll` by name so
    // the sub-agent whitelists can never drift from Simi's catalogue.

    private static readonly HashSet<string> InsightsSubAgentToolNames = new(StringComparer.Ordinal)
    {
        // Spec 025 §5.1 — explain / audit / rank.
        "pf_get_category_breakdown",
        "pf_get_merchant_breakdown",
        "pf_get_account_breakdown",
        "pf_get_merchant_history",
        "pf_list_transactions",
        "pf_get_transaction",
        "pf_list_commitments",
        "pf_get_commitment",
        "pf_list_detected_commitments",
        "pf_list_snapshot_history",
        "pf_compare_snapshots",
        "pf_get_spending_summary",
        "pf_get_upcoming_bills",
    };

    private static readonly HashSet<string> ForecastSubAgentToolNames = new(StringComparer.Ordinal)
    {
        // Spec 025 §5.2 — projections / what-if / scenarios.
        "pf_get_dashboard",
        "pf_get_spending_summary",
        "pf_get_upcoming_bills",
        "pf_list_commitments",
        "pf_list_budgets",
        "pf_list_snapshot_history",
        "pf_compare_snapshots",
        "pf_get_fx_rate_history",
    };

    private static readonly HashSet<string> ClassifySubAgentToolNames = new(StringComparer.Ordinal)
    {
        // Spec 025 §5.3 — categorisation queue review at scale.
        "pf_list_classification_review_queue",
        "pf_get_transaction",
        "pf_list_transactions",
        "pf_get_merchant_history",
        "pf_get_category_breakdown",
    };

    /// <summary>
    /// Read-only tool slice for the <c>pf-insights</c> sub-agent (Spec 025 §5.1).
    /// Composes data-fetching operations for explain/audit/rank questions over
    /// the user's historical spending and commitments. Never exposes mutating
    /// tools — sub-agents are read-only by design.
    /// </summary>
    public static IEnumerable<AITool> CreateForInsightsSubAgent(IServiceProvider serviceProvider)
        => CreateAll(serviceProvider).Where(tool => InsightsSubAgentToolNames.Contains(tool.Name));

    /// <summary>
    /// Read-only tool slice for the <c>pf-forecast</c> sub-agent (Spec 025 §5.2).
    /// Composes data-fetching operations for forward projections and what-if
    /// scenarios. Never exposes mutating tools.
    /// </summary>
    public static IEnumerable<AITool> CreateForForecastSubAgent(IServiceProvider serviceProvider)
        => CreateAll(serviceProvider).Where(tool => ForecastSubAgentToolNames.Contains(tool.Name));

    /// <summary>
    /// Read-only tool slice for the <c>pf-classify</c> sub-agent (Spec 025 §5.3).
    /// Composes data-fetching operations for walking the classification review
    /// queue and proposing per-item corrections. Never exposes mutating tools —
    /// Simi handles the per-action `pf_override_transaction_category` and
    /// `pf_create_categorisation_rule` calls via the existing `confirmAction`
    /// flow after the sub-agent has surfaced proposals.
    /// </summary>
    public static IEnumerable<AITool> CreateForClassifySubAgent(IServiceProvider serviceProvider)
        => CreateAll(serviceProvider).Where(tool => ClassifySubAgentToolNames.Contains(tool.Name));
}
