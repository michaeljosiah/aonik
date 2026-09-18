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
        RuleFor(x => x.Modality).NotEmpty().Must(m => string.Equals(m?.Trim(), SafetyModalities.Text, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only the text modality is screened over this route.");
        RuleFor(x => x.Layer).NotEmpty().Must(l => l?.Trim().ToLowerInvariant() is "input" or "output").WithMessage("layer is input or output.");
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2_000_000);
    }
}

internal sealed class ReviewDecisionRequestValidator : Validator<ReviewDecisionRequest>
{
    public ReviewDecisionRequestValidator()
        => RuleFor(x => x.Decision).NotEmpty().Must(d => d?.Trim().ToLowerInvariant() is "approve" or "decline").WithMessage("decision is approve or decline.");
}
