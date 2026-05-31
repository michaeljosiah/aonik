using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Finance.Agents;

/// <summary>
/// Finance module's tool-approval classification (Spec 032, finding C3). Declares which
/// finance agent tools are mutating and at what risk tier, so the central
/// <see cref="IToolApprovalGate"/> wraps them before they reach the model.
/// <para>
/// Routing follows Spec 032 §4.1: a tool that can move money, post to the ledger, or call a
/// partner is <see cref="ToolApprovalTier.High"/>; an everyday domain write is
/// <see cref="ToolApprovalTier.Medium"/>. Read-only tools (the <c>finance_get_*</c>,
/// <c>finance_list_*</c>, and <c>finance_graph_*</c> queries) are intentionally omitted — the
/// gate passes unclassified, read-looking tools through.
/// </para>
/// </summary>
internal sealed class FinanceToolApprovalManifest : IToolApprovalManifest
{
    public string Module => "Finance";

    private static readonly IReadOnlyDictionary<string, ToolClassification> Classifications =
        new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
        {
            // ── High — money movement / ledger posting / partner calls (Spec 032 §4.1) ──
            ["finance_capture_payment"] =
                High("Finance.CapturePayment", "Capture a payment (moves funds)"),
            ["finance_cancel_payment"] =
                High("Finance.CancelPayment", "Cancel a payment intent"),
            ["finance_create_payment_intent"] =
                High("Finance.CreatePaymentIntent", "Create a payment intent (authorises funds)"),
            ["finance_mark_invoice_paid"] =
                High("Finance.MarkInvoicePaid", "Mark an invoice paid (posts to the ledger)"),

            // ── Medium — everyday domain writes (no money movement) ──
            ["finance_create_invoice"] = Medium("Create a draft invoice"),
            ["finance_issue_invoice"] = Medium("Issue an invoice"),
            ["finance_cancel_invoice"] = Medium("Cancel an invoice"),
            ["finance_add_invoice_line"] = Medium("Add a line item to an invoice"),
            ["finance_update_line_quantity"] = Medium("Update an invoice line quantity"),
            ["finance_update_line_unit_price"] = Medium("Update an invoice line unit price"),
            ["finance_apply_invoice_discount"] = Medium("Apply a discount to an invoice"),
            ["finance_create_ledger"] = Medium("Create a ledger"),
            ["finance_create_account"] = Medium("Create a ledger account"),
        };

    public ToolClassification? Classify(string toolName) =>
        Classifications.TryGetValue(toolName, out var classification) ? classification : null;

    private static ToolClassification High(string proposalType, string actionKind) =>
        ToolClassification.Mutating(new ToolApprovalOptions(ToolApprovalTier.High, actionKind, proposalType));

    private static ToolClassification Medium(string actionKind) =>
        ToolClassification.Mutating(new ToolApprovalOptions(ToolApprovalTier.Medium, actionKind));
}
