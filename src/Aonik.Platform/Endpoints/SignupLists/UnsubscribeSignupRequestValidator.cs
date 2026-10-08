using Aonik.SharedKernel.Validation;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Platform.Endpoints.SignupLists;

internal sealed class UnsubscribeSignupRequestValidator : Validator<UnsubscribeSignupRequest>
{
    public UnsubscribeSignupRequestValidator()
    {
        RuleFor(x => x.ListType).RequiredText(32);
        RuleFor(x => x.SubscriptionId).RequiredId();
        RuleFor(x => x.Body).NotNull();
        When(x => x.Body is not null, () =>
            RuleFor(x => x.Body!.Token).NotEmpty().MaximumLength(1024));
    }
}
