using Aonik.Application.Models.Onboarding;

namespace Aonik.Application.Services.Onboarding;

public interface IOnboardingPolicyEvaluator
{
    Task<OnboardingSnapshot> EvaluateAsync(Guid userId, CancellationToken cancellationToken = default);
}
