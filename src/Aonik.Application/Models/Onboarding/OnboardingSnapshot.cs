namespace Aonik.Application.Models.Onboarding;

public record OnboardingSnapshot(
    Guid UserId,
    Guid? PartyId,
    IReadOnlyList<OnboardingGateStatus> Gates,
    IReadOnlyList<string> NextActions);
