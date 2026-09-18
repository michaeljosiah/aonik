using Aonik.SharedKernel.Abstractions.Safety;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Ai.Endpoints.Safety;

// Structural validation of the safety request DTOs; the verdict is the gate's.

internal sealed class ScreenContentRequestValidator : Validator<ScreenContentRequest>
{
    public ScreenContentRequestValidator()
    {
        RuleFor(x => x.SubjectPartyId).NotEmpty();
        RuleFor(x => x.Modality).NotEmpty().Must(m => m?.Trim().ToLowerInvariant() is SafetyModalities.Text or SafetyModalities.Image)
            .WithMessage("Text and image are screened over this route.");
        RuleFor(x => x.Layer).NotEmpty().Must(l => l?.Trim().ToLowerInvariant() is "input" or "output").WithMessage("layer is input or output.");
        RuleFor(x => x.Content).NotEmpty().MaximumLength(40_000_000);
        // An image travels inline, as the bytes themselves: the decision is bound to them, not to a URL somebody else serves.
        RuleFor(x => x.Content).Must(c => c is not null && c.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) && c.Contains(";base64,", StringComparison.Ordinal))
            .When(x => string.Equals(x.Modality?.Trim(), SafetyModalities.Image, StringComparison.OrdinalIgnoreCase))
            .WithMessage("An image is screened as a base64 data URL (data:image/png;base64,...).");
        RuleFor(x => x.Layer).Must(l => l?.Trim().ToLowerInvariant() == "output")
            .When(x => string.Equals(x.Modality?.Trim(), SafetyModalities.Image, StringComparison.OrdinalIgnoreCase))
            .WithMessage("An image is an output; what a child typed is text.");
    }
}

internal sealed class ReviewDecisionRequestValidator : Validator<ReviewDecisionRequest>
{
    public ReviewDecisionRequestValidator()
        => RuleFor(x => x.Decision).NotEmpty().Must(d => d?.Trim().ToLowerInvariant() is "approve" or "decline").WithMessage("decision is approve or decline.");
}
