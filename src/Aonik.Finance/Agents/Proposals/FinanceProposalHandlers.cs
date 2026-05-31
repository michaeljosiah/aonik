using System.Text.Json;

using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Billing;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Payments;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Finance.Agents.Proposals;

/// <summary>
/// Spec 032 §7.4 — the durable-execution side of the High-tier money tools. When a
/// <c>finance_capture_payment</c> / <c>finance_cancel_payment</c> / <c>finance_create_payment_intent</c>
/// / <c>finance_mark_invoice_paid</c> call is gated, the <c>ApprovalGatedAIFunction</c> decorator never
/// runs the inner domain call — it marshals the model arguments into a durable <c>Proposal</c>. These
/// keyed <see cref="IProposalHandler"/>s are the <strong>only</strong> path that reaches the Finance
/// service, and only after a human approves the proposal (Spec 030 dispatcher).
///
/// <para>
/// Idempotency (Spec 032 §3): each handler does a GET-before-act and treats "already in the target
/// state" as success (the retry path converges). Wrong-state and missing-entity cases return
/// <see cref="ProposalHandlerResult.Applied"/> = <c>false</c> so the dispatcher surfaces HTTP 422
/// rather than a 500. For High proposals a 422/failure is terminal (the proposal lands in
/// <c>Failed</c>; retry is a brand-new proposal), so a money call whose outcome is uncertain can
/// never be re-approved and double-moved.
/// </para>
/// </summary>
internal static class FinanceProposalPayload
{
    private static readonly JsonDocumentOptions DocumentOptions = default;

    /// <summary>Parses the proposal payload into a queryable root element.</summary>
    public static JsonElement Parse(string payloadJson, out JsonDocument document)
    {
        document = JsonDocument.Parse(payloadJson ?? string.Empty, DocumentOptions);
        return document.RootElement;
    }

    /// <summary>
    /// Finds a property by name, case-insensitively. The payload keys are the original tool
    /// parameter names (camelCase), but we tolerate casing drift so a payload authored by a
    /// different serializer policy still binds.
    /// </summary>
    private static bool TryFind(JsonElement root, string name, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty(name, out value))
            {
                return true;
            }

            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    public static bool TryGetGuid(JsonElement root, string name, out Guid value)
    {
        value = Guid.Empty;
        if (!TryFind(root, name, out var element))
        {
            return false;
        }

        // The model emits a GUID as a JSON string; a boxed Guid also serializes to a string.
        if (element.ValueKind == JsonValueKind.String && element.TryGetGuid(out value))
        {
            return true;
        }

        return false;
    }

    public static bool TryGetNullableGuid(JsonElement root, string name, out Guid? value)
    {
        value = null;
        if (!TryFind(root, name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            // Absent or explicit null → a legitimate "no value" for an optional argument.
            return true;
        }

        if (element.ValueKind == JsonValueKind.String && element.TryGetGuid(out var guid))
        {
            value = guid;
            return true;
        }

        return false;
    }

    public static bool TryGetDecimal(JsonElement root, string name, out decimal value)
    {
        value = 0m;
        if (!TryFind(root, name, out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out value))
        {
            return true;
        }

        // A boxed string amount (e.g. "100.50") is tolerated.
        if (element.ValueKind == JsonValueKind.String &&
            decimal.TryParse(element.GetString(), out value))
        {
            return true;
        }

        return false;
    }

    public static bool TryGetString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!TryFind(root, name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}

/// <summary>
/// Executes an approved <c>Finance.CapturePayment</c> proposal — the High-tier capture that moves
/// funds. Never reachable except via approval (Spec 032).
/// </summary>
internal sealed class CapturePaymentProposalHandler : IProposalHandler
{
    public const string ProposalTypeKey = "Finance.CapturePayment";
    private const string ResourceType = "PaymentIntent";

    private readonly IPaymentService _paymentService;

    public CapturePaymentProposalHandler(IPaymentService paymentService) =>
        _paymentService = paymentService;

    public string ProposalType => ProposalTypeKey;

    public async Task<ProposalHandlerResult> HandleAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        Guid paymentIntentId;
        try
        {
            var root = FinanceProposalPayload.Parse(proposal.PayloadJson, out var document);
            using (document)
            {
                if (!FinanceProposalPayload.TryGetGuid(root, "paymentIntentId", out paymentIntentId))
                {
                    return new ProposalHandlerResult(
                        Applied: false,
                        Message: "Proposal payload is missing a valid paymentIntentId.");
                }
            }
        }
        catch (JsonException ex)
        {
            return new ProposalHandlerResult(Applied: false, Message: $"Invalid proposal payload: {ex.Message}");
        }

        var intent = await _paymentService.GetPaymentIntentAsync(paymentIntentId, cancellationToken);
        if (intent is null)
        {
            return new ProposalHandlerResult(
                Applied: false,
                Message: $"Payment intent {paymentIntentId} no longer exists for this tenant.");
        }

        // Idempotent: a second approval of an already-captured intent converges on success.
        if (intent.Status == PaymentStatus.Captured)
        {
            return new ProposalHandlerResult(
                Applied: true,
                AppliedResourceType: ResourceType,
                AppliedResourceId: paymentIntentId,
                Message: "Payment was already captured.");
        }

        // Only an authorized intent can be captured. Any other state is an expected business
        // failure (422), not an unexpected error.
        if (intent.Status != PaymentStatus.Authorized)
        {
            return new ProposalHandlerResult(
                Applied: false,
                Message: $"Payment intent {paymentIntentId} is {intent.Status} and cannot be captured.");
        }

        try
        {
            var result = await _paymentService.CapturePaymentAsync(paymentIntentId, cancellationToken);
            return new ProposalHandlerResult(
                Applied: true,
                AppliedResourceType: ResourceType,
                AppliedResourceId: result.Id);
        }
        catch (InvalidOperationException ex)
        {
            return new ProposalHandlerResult(Applied: false, Message: ex.Message);
        }
    }
}

/// <summary>
/// Executes an approved <c>Finance.CancelPayment</c> proposal — the High-tier cancel of a payment
/// intent. Never reachable except via approval (Spec 032).
/// </summary>
internal sealed class CancelPaymentProposalHandler : IProposalHandler
{
    public const string ProposalTypeKey = "Finance.CancelPayment";
    private const string ResourceType = "PaymentIntent";

    private readonly IPaymentService _paymentService;

    public CancelPaymentProposalHandler(IPaymentService paymentService) =>
        _paymentService = paymentService;

    public string ProposalType => ProposalTypeKey;

    public async Task<ProposalHandlerResult> HandleAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        Guid paymentIntentId;
        try
        {
            var root = FinanceProposalPayload.Parse(proposal.PayloadJson, out var document);
            using (document)
            {
                if (!FinanceProposalPayload.TryGetGuid(root, "paymentIntentId", out paymentIntentId))
                {
                    return new ProposalHandlerResult(
                        Applied: false,
                        Message: "Proposal payload is missing a valid paymentIntentId.");
                }
            }
        }
        catch (JsonException ex)
        {
            return new ProposalHandlerResult(Applied: false, Message: $"Invalid proposal payload: {ex.Message}");
        }

        var intent = await _paymentService.GetPaymentIntentAsync(paymentIntentId, cancellationToken);
        if (intent is null)
        {
            return new ProposalHandlerResult(
                Applied: false,
                Message: $"Payment intent {paymentIntentId} no longer exists for this tenant.");
        }

        // Idempotent: a second approval of an already-cancelled intent converges on success.
        if (intent.Status == PaymentStatus.Cancelled)
        {
            return new ProposalHandlerResult(
                Applied: true,
                AppliedResourceType: ResourceType,
                AppliedResourceId: paymentIntentId,
                Message: "Payment was already cancelled.");
        }

        // A captured intent represents moved funds and cannot be cancelled — expected business
        // failure (422), not an unexpected error.
        if (intent.Status == PaymentStatus.Captured)
        {
            return new ProposalHandlerResult(
                Applied: false,
                Message: $"Payment intent {paymentIntentId} is captured and cannot be cancelled.");
        }

        try
        {
            var result = await _paymentService.CancelPaymentAsync(paymentIntentId, cancellationToken);
            return new ProposalHandlerResult(
                Applied: true,
                AppliedResourceType: ResourceType,
                AppliedResourceId: result.Id);
        }
        catch (InvalidOperationException ex)
        {
            return new ProposalHandlerResult(Applied: false, Message: ex.Message);
        }
    }
}

/// <summary>
/// Executes an approved <c>Finance.CreatePaymentIntent</c> proposal — the High-tier authorisation of
/// funds for an order. Creation is not naturally idempotent, but the approve endpoint guards against
/// re-dispatch (a proposal must be in <c>Proposed</c> to be approved, and a High failure is terminal),
/// so a duplicate intent cannot be created through the approval path.
/// </summary>
internal sealed class CreatePaymentIntentProposalHandler : IProposalHandler
{
    public const string ProposalTypeKey = "Finance.CreatePaymentIntent";
    private const string ResourceType = "PaymentIntent";

    private readonly IPaymentService _paymentService;

    public CreatePaymentIntentProposalHandler(IPaymentService paymentService) =>
        _paymentService = paymentService;

    public string ProposalType => ProposalTypeKey;

    public async Task<ProposalHandlerResult> HandleAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        CreatePaymentIntentRequest request;
        try
        {
            var root = FinanceProposalPayload.Parse(proposal.PayloadJson, out var document);
            using (document)
            {
                if (!FinanceProposalPayload.TryGetDecimal(root, "amount", out var amount) ||
                    !FinanceProposalPayload.TryGetString(root, "currency", out var currency) ||
                    !FinanceProposalPayload.TryGetString(root, "reference", out var reference) ||
                    !FinanceProposalPayload.TryGetGuid(root, "orderId", out var orderId) ||
                    !FinanceProposalPayload.TryGetNullableGuid(root, "invoiceId", out var invoiceId))
                {
                    return new ProposalHandlerResult(
                        Applied: false,
                        Message: "Proposal payload is missing required fields (amount, currency, reference, orderId).");
                }

                request = new CreatePaymentIntentRequest(amount, currency, reference, orderId, invoiceId);
            }
        }
        catch (JsonException ex)
        {
            return new ProposalHandlerResult(Applied: false, Message: $"Invalid proposal payload: {ex.Message}");
        }

        try
        {
            var result = await _paymentService.CreatePaymentIntentAsync(request, cancellationToken);
            return new ProposalHandlerResult(
                Applied: true,
                AppliedResourceType: ResourceType,
                AppliedResourceId: result.Id);
        }
        catch (InvalidOperationException ex)
        {
            return new ProposalHandlerResult(Applied: false, Message: ex.Message);
        }
    }
}

/// <summary>
/// Executes an approved <c>Finance.MarkInvoicePaid</c> proposal — the High-tier settlement that posts
/// revenue to the ledger. Never reachable except via approval (Spec 032).
/// </summary>
internal sealed class MarkInvoicePaidProposalHandler : IProposalHandler
{
    public const string ProposalTypeKey = "Finance.MarkInvoicePaid";
    private const string ResourceType = "Invoice";

    private readonly IBillingService _billingService;

    public MarkInvoicePaidProposalHandler(IBillingService billingService) =>
        _billingService = billingService;

    public string ProposalType => ProposalTypeKey;

    public async Task<ProposalHandlerResult> HandleAsync(
        AgentProposalDetail proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        Guid invoiceId;
        try
        {
            var root = FinanceProposalPayload.Parse(proposal.PayloadJson, out var document);
            using (document)
            {
                if (!FinanceProposalPayload.TryGetGuid(root, "invoiceId", out invoiceId))
                {
                    return new ProposalHandlerResult(
                        Applied: false,
                        Message: "Proposal payload is missing a valid invoiceId.");
                }
            }
        }
        catch (JsonException ex)
        {
            return new ProposalHandlerResult(Applied: false, Message: $"Invalid proposal payload: {ex.Message}");
        }

        var invoice = await _billingService.GetInvoiceAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return new ProposalHandlerResult(
                Applied: false,
                Message: $"Invoice {invoiceId} no longer exists for this tenant.");
        }

        // Idempotent: a second approval of an already-paid invoice converges on success.
        if (invoice.Status == InvoiceStatus.Paid)
        {
            return new ProposalHandlerResult(
                Applied: true,
                AppliedResourceType: ResourceType,
                AppliedResourceId: invoiceId,
                Message: "Invoice was already marked paid.");
        }

        // Only an issued invoice can be marked paid. Any other state is an expected business
        // failure (422), not an unexpected error.
        if (invoice.Status != InvoiceStatus.Issued)
        {
            return new ProposalHandlerResult(
                Applied: false,
                Message: $"Invoice {invoiceId} is {invoice.Status} and cannot be marked paid.");
        }

        try
        {
            await _billingService.MarkInvoiceAsPaidAsync(invoiceId, cancellationToken);
            return new ProposalHandlerResult(
                Applied: true,
                AppliedResourceType: ResourceType,
                AppliedResourceId: invoiceId);
        }
        catch (InvalidOperationException ex)
        {
            return new ProposalHandlerResult(Applied: false, Message: ex.Message);
        }
    }
}
