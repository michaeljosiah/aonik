using Aonik.Platform.Contracts.Models.Onboarding;

namespace Aonik.Platform.Contracts.Services.Onboarding;

public interface IOnboardingPolicyEvaluator
{
    Task<OnboardingSnapshot> EvaluateAsync(Guid userId, CancellationToken cancellationToken = default);
}
