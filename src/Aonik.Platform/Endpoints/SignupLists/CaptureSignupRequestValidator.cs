using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.Platform.Services.SignupLists;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Platform.Endpoints.SignupLists;

internal sealed class CaptureSignupRequestValidator : Validator<CaptureSignupRequest>
{
    public CaptureSignupRequestValidator()
    {
        RuleFor(x => x.ListType).Must(SignupListTypes.IsKnown).WithMessage("Unknown sign-up list.");
        RuleFor(x => x.Capture).NotNull();
        RuleFor(x => x).Custom((request, context) =>
        {
            if (request.Capture is null || !SignupListTypes.IsKnown(request.ListType)) return;
            var result = new SignupCaptureValidator(request.ListType)
                .Validate(SignupCaptureValidator.Normalize(request.Capture));
            foreach (var failure in result.Errors) context.AddFailure(failure);
        });
    }
}
