using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.Admin.Accounts;

// ────────────────────────────────────────────────────────────────────
// Spec 028 — validators for the manual-categorize / re-categorize
// admin endpoints. FastEndpoints auto-discovers Validator<T> classes and
// runs them before HandleAsync is invoked.
// ────────────────────────────────────────────────────────────────────

internal sealed class SetAccountTransactionCategoryRouteRequestValidator
    : Validator<SetAccountTransactionCategoryEndpoint.RouteRequest>
{
    public SetAccountTransactionCategoryRouteRequestValidator()
    {
        RuleFor(x => x.Id).RequiredId();
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(128).WithMessage("Category must be 128 characters or fewer.");
        RuleFor(x => x.SubCategory)
            .MaximumLength(128).WithMessage("SubCategory must be 128 characters or fewer.")
            .When(x => !string.IsNullOrWhiteSpace(x.SubCategory));
    }
}

internal sealed class RecategorizeAccountTransactionsRouteRequestValidator
    : Validator<RecategorizeAccountTransactionsEndpoint.RouteRequest>
{
    public RecategorizeAccountTransactionsRouteRequestValidator()
    {
        RuleFor(x => x.ConnectionId).RequiredId();
    }
}
