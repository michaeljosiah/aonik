using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.Remittance;

public record UpdateOperatorBeneficiaryNotesRequest(string Notes);

public sealed class UpdateOperatorBeneficiaryNotesRequestValidator : Validator<UpdateOperatorBeneficiaryNotesRequest>
{
    public UpdateOperatorBeneficiaryNotesRequestValidator()
    {
        RuleFor(request => request.Notes).NotNull().MaximumLength(1024);
    }
}

public sealed class UpdateOperatorBeneficiaryNotesEndpoint(IOperatorPayoutBeneficiaryService service)
    : Endpoint<UpdateOperatorBeneficiaryNotesRequest>
{
    public override void Configure()
    {
        Patch("/remittance/customers/{customerPartyId:guid}/beneficiaries/{beneficiaryPartyId:guid}/notes");
        Policies("AdminWritePolicy");
        this.RequiresPermission("Payment.Create");
        this.RequiresPermission("Customers.Write");
    }

    public override async Task HandleAsync(UpdateOperatorBeneficiaryNotesRequest req, CancellationToken ct)
    {
        await service.UpdateNotesAsync(Route<Guid>("customerPartyId"), Route<Guid>("beneficiaryPartyId"), req.Notes, ct);
        await Send.NoContentAsync(ct);
    }
}
