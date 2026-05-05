using Aonik.Finance.Contracts.Api.Billing;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.Billing;

// ────────────────────────────────────────────────────────────────────
// Validators for the Billing feature's request DTOs. FastEndpoints
// auto-discovers Validator<T> classes and runs them before HandleAsync
// is invoked, returning a 400 with FluentValidation errors on failure.
// ────────────────────────────────────────────────────────────────────

public sealed class CreateInvoiceValidator : Validator<CreateInvoiceRequest>
{
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.CustomerId).RequiredId();
        RuleFor(x => x.InvoiceNumber).RequiredText(64);
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.DueUtc)
            .GreaterThan(DateTime.UtcNow.AddYears(-10))
            .WithMessage("Due date is unreasonably far in the past.")
            .LessThan(DateTime.UtcNow.AddYears(20))
            .WithMessage("Due date is unreasonably far in the future.");
        RuleFor(x => x.LineItems)
            .NotNull().WithMessage("At least one line item is required.")
            .Must(li => li != null && li.Count > 0)
            .WithMessage("Invoice must contain at least one line item.")
            .Must(li => li == null || li.Count <= 500)
            .WithMessage("Invoice may not contain more than 500 line items.");
        RuleForEach(x => x.LineItems).SetValidator(new CreateInvoiceLineItemValidator());
    }
}

public sealed class CreateInvoiceLineItemValidator : Validator<CreateInvoiceLineItemRequest>
{
    public CreateInvoiceLineItemValidator()
    {
        RuleFor(x => x.Description).RequiredText(512);
        RuleFor(x => x.Quantity).PositiveMoney();
        RuleFor(x => x.UnitPrice).NonNegativeMoney();
    }
}

public sealed class AddInvoiceLineValidator : Validator<AddInvoiceLineRequest>
{
    public AddInvoiceLineValidator()
    {
        RuleFor(x => x.Description).RequiredText(512);
        RuleFor(x => x.Quantity).PositiveMoney();
        RuleFor(x => x.UnitPrice).NonNegativeMoney();
    }
}

public sealed class UpdateLineQuantityValidator : Validator<UpdateLineQuantityRequest>
{
    public UpdateLineQuantityValidator()
    {
        RuleFor(x => x.Quantity).PositiveMoney();
    }
}

public sealed class UpdateLineUnitPriceValidator : Validator<UpdateLineUnitPriceRequest>
{
    public UpdateLineUnitPriceValidator()
    {
        RuleFor(x => x.UnitPrice).NonNegativeMoney();
    }
}

public sealed class ApplyDiscountValidator : Validator<ApplyDiscountRequest>
{
    public ApplyDiscountValidator()
    {
        RuleFor(x => x.DiscountTotal).NonNegativeMoney();
    }
}
