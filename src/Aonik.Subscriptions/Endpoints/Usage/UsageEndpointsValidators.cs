using Aonik.SharedKernel.Abstractions.Subscriptions;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Subscriptions.Endpoints.Usage;

// Structural validation of the usage request DTOs; whether the allowance covers a quantity, and
// whether the caller may act for the subscriber, are the meter's questions.

internal sealed class ReserveUsageRequestValidator : Validator<ReserveUsageRequest>
{
    public ReserveUsageRequestValidator()
    {
        RuleFor(x => x.Subscriber).NotNull();
        RuleFor(x => x.Subscriber.Kind).NotEmpty().Must(kind => kind is SubscriberKinds.Party or SubscriberKinds.Group or SubscriberKinds.Tenant).WithMessage("Unknown subscriber kind.").When(x => x.Subscriber is not null);
        RuleFor(x => x.Subscriber.Id).NotEmpty().When(x => x.Subscriber is not null);
        RuleFor(x => x.MeterCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.HoldForSeconds).InclusiveBetween(1, 86_400).When(x => x.HoldForSeconds is not null);
    }
}

internal sealed class CommitUsageRequestValidator : Validator<CommitUsageRequest>
{
    public CommitUsageRequestValidator()
    {
        RuleFor(x => x.ActualQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SourceType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.SourceId).NotEmpty();
        RuleFor(x => x.ProviderCost).GreaterThanOrEqualTo(0).When(x => x.ProviderCost is not null);
        RuleFor(x => x.ProviderCostCurrency).Length(3).When(x => x.ProviderCostCurrency is not null);
    }
}

internal sealed class AllowanceQueryValidator : Validator<AllowanceQuery>
{
    public AllowanceQueryValidator()
    {
        RuleFor(x => x.SubscriberKind).NotEmpty().Must(kind => kind is SubscriberKinds.Party or SubscriberKinds.Group or SubscriberKinds.Tenant).WithMessage("Unknown subscriber kind.");
        RuleFor(x => x.SubscriberId).NotEmpty();
    }
}
