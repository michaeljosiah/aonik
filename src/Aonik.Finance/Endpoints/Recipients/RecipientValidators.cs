using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.Recipients;

// Validators for the recipient request DTOs. Every recipient operation is scoped to a customer, so a
// non-empty customer party id (and, for item routes, recipient party id) is the boundary invariant —
// rejected here with 400 rather than flowing into the service as an empty Guid. CreateRecipientEndpoint
// reuses SavePayoutBeneficiaryRequest (validated in PayoutBeneficiaryValidators); the photo endpoint is
// EndpointWithoutRequest and has no request DTO.

public sealed class ListRecipientsRequestValidator : Validator<ListRecipientsRequest>
{
    public ListRecipientsRequestValidator()
    {
        RuleFor(x => x.CustomerPartyId).NotEmpty().WithMessage("Customer party id is required.");
    }
}

public sealed class GetRecipientRequestValidator : Validator<GetRecipientRequest>
{
    public GetRecipientRequestValidator()
    {
        RuleFor(x => x.CustomerPartyId).NotEmpty().WithMessage("Customer party id is required.");
        RuleFor(x => x.RecipientPartyId).NotEmpty().WithMessage("Recipient party id is required.");
    }
}

public sealed class UpdateRecipientRouteRequestValidator : Validator<UpdateRecipientRouteRequest>
{
    public UpdateRecipientRouteRequestValidator()
    {
        RuleFor(x => x.CustomerPartyId).NotEmpty().WithMessage("Customer party id is required.");
        RuleFor(x => x.RecipientPartyId).NotEmpty().WithMessage("Recipient party id is required.");
    }
}

public sealed class RemoveRecipientRequestValidator : Validator<RemoveRecipientRequest>
{
    public RemoveRecipientRequestValidator()
    {
        RuleFor(x => x.CustomerPartyId).NotEmpty().WithMessage("Customer party id is required.");
        RuleFor(x => x.RecipientPartyId).NotEmpty().WithMessage("Recipient party id is required.");
    }
}
