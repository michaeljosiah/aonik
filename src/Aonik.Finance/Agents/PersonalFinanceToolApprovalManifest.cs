using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Finance.Agents;

/// <summary>
/// PersonalFinance ("Simi") tool-approval classification (Spec 032). Declares which of the
/// personal-finance agent's mutating tools are gated and at what tier, so the central
/// <see cref="IToolApprovalGate"/> wraps them before they reach the model — replacing the legacy
/// <c>confirmAction</c> frontend-tool convention these tools previously relied on.
/// <para>
/// PersonalFinance has <strong>no money-moving tools</strong> — payments / transfers / captures live
/// in Finance and are gated there — so every mutation here is <see cref="ToolApprovalTier.Medium"/>
/// (an everyday domain write, confirmed in-session) or <see cref="ToolApprovalTier.Low"/> (a
/// reversible personal-state write that runs audited without blocking). <c>pf_cancel_order</c> only
/// cancels an <em>unsettled</em> order (a status change, no funds move), so it is Medium, not High.
/// Read tools (<c>pf_get_*</c>, <c>pf_list_*</c>, <c>pf_run_*</c>, <c>pf_compare_*</c>) are omitted —
/// the gate passes unclassified, read-looking tools through.
/// </para>
/// </summary>
internal sealed class PersonalFinanceToolApprovalManifest : IToolApprovalManifest
{
    public string Module => "PersonalFinance";

    private static readonly IReadOnlyDictionary<string, ToolClassification> Classifications =
        new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
        {
            // ── Low — reversible personal-state; audited, runs without blocking ──
            ["user_memory_save"] = Low("Save a memory"),
            ["pf_reject_commitment"] = Low("Dismiss a detected commitment"),

            // ── Medium — everyday domain writes (no money movement) ──
            ["pf_create_account"] = Medium("Create a personal account"),
            ["pf_archive_account"] = Medium("Archive a personal account"),
            ["pf_create_transaction"] = Medium("Create a manual transaction"),
            ["pf_create_bill"] = Medium("Create a bill"),
            ["pf_update_bill"] = Medium("Update a bill"),
            ["pf_archive_bill"] = Medium("Archive a bill"),
            ["pf_create_budget"] = Medium("Create a budget"),
            ["pf_update_budget_amount"] = Medium("Update a budget amount"),
            ["pf_delete_budget"] = Medium("Delete a budget"),
            ["pf_create_commitment_from_transaction"] = Medium("Create a commitment"),
            ["pf_confirm_commitment"] = Medium("Confirm a detected commitment"),
            ["pf_override_transaction_category"] = Medium("Recategorise a transaction"),
            ["pf_create_categorisation_rule"] = Medium("Create a categorisation rule"),
            ["pf_apply_statement_import"] = Medium("Apply a statement import"),
            ["pf_delete_transaction_attachment"] = Medium("Delete a transaction attachment"),
            ["pf_cancel_order"] = Medium("Cancel an unsettled order"),
            ["pf_create_account_link_session"] = Medium("Start a bank-link session"),
            ["pf_refresh_linked_account"] = Medium("Refresh a linked account"),
            ["pf_sync_linked_account_transactions"] = Medium("Sync linked-account transactions"),
            ["pf_disconnect_linked_account"] = Medium("Disconnect a linked account"),
        };

    public ToolClassification? Classify(string toolName) =>
        Classifications.TryGetValue(toolName, out var classification) ? classification : null;

    private static ToolClassification Medium(string actionKind) =>
        ToolClassification.Mutating(new ToolApprovalOptions(ToolApprovalTier.Medium, actionKind));

    private static ToolClassification Low(string actionKind) =>
        ToolClassification.Mutating(new ToolApprovalOptions(ToolApprovalTier.Low, actionKind));
}
