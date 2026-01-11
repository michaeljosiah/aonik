namespace Aonik.Application.Models.Onboarding;

public record OnboardingGateStatus(
    OnboardingGate Gate,
    bool IsSatisfied,
    bool IsRequired,
    IReadOnlyList<string> RequiredActions);
