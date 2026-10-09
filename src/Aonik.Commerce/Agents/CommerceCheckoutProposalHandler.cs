using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Commerce.Agents;

internal sealed class CommerceCheckoutProposalHandler(ICheckoutService checkout) : IProposalHandler
{
    public const string ProposalTypeKey = "Commerce.Checkout";
    public string ProposalType => ProposalTypeKey;

    public async Task<ProposalHandlerResult> HandleAsync(AgentProposalDetail proposal, CancellationToken cancellationToken)
    {
        CheckoutPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CheckoutPayload>(proposal.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return new(false, Message: "The checkout proposal payload is invalid.");
        }
        if (payload is null || payload.CartId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Provider)
            || string.IsNullOrWhiteSpace(payload.PaymentMethodType) || string.IsNullOrWhiteSpace(payload.CartToken)
            || string.IsNullOrWhiteSpace(payload.ExpectedCartVersion))
            return new(false, Message: "Checkout approval requires the cart, guest token, observed version, provider and method.");
        try
        {
            // Execute precisely the approved version. Never refresh it behind the approver's back.
            var result = await checkout.CheckoutAsync(new CheckoutCommand(payload.CartId, payload.Provider, payload.PaymentMethodType, RequireFreshCart: true),
                CartAccessContext.ForGuest(payload.CartToken, payload.ExpectedCartVersion), cancellationToken);
            return new(true, "Order", result.OrderId);
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidStateException
            or NotFoundException or CartWriteConflictException or StorefrontValidationException)
        {
            return new(false, Message: "The approved checkout could not complete. Read the cart and payment state before proposing another action.");
        }
    }

    private sealed record CheckoutPayload(Guid CartId, string? Provider, string? PaymentMethodType,
        string? CartToken, string? ExpectedCartVersion);
}
