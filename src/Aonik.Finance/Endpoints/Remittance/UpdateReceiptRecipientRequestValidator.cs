using Aonik.Finance.Contracts.Services.Remittance;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class UpdateReceiptRecipientRequestValidator : Validator<UpdateReceiptRecipientRequest>
{
    public UpdateReceiptRecipientRequestValidator()
    {
        RuleFor(r => r.ExpectedEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(r => r.NewEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(r => r.ConfirmUpdate).Equal(true).WithMessage("Confirm the saved customer email update.");
    }
}
