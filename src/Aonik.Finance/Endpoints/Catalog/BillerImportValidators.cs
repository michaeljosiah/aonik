using FastEndpoints;
using FluentValidation;
using Aonik.Finance.Contracts.Models.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

// Validators for the partner biller import request DTOs (Spec 040). The connector id is the boundary
// invariant for both calls; the import additionally requires at least one selected biller, each carrying
// a provider biller code — rejected here with 400 rather than flowing into the import service as an
// empty Guid / empty selection.

public sealed class BillerImportPreviewRequestValidator : Validator<BillerImportPreviewRequest>
{
    public BillerImportPreviewRequestValidator()
    {
        RuleFor(x => x.ConnectorId).NotEmpty().WithMessage("Connector id is required.");
    }
}

public sealed class BillerImportRequestValidator : Validator<BillerImportRequest>
{
    public BillerImportRequestValidator()
    {
        RuleFor(x => x.ConnectorId).NotEmpty().WithMessage("Connector id is required.");
        RuleFor(x => x.Entries).NotEmpty().WithMessage("At least one biller must be selected.");
        RuleForEach(x => x.Entries).ChildRules(entry =>
            entry.RuleFor(e => e.BillerCode).NotEmpty()
                .WithMessage("Each selected biller must have a provider biller code."));
    }
}
