namespace Aonik.Platform.Contracts.Api.Onboarding;

public record OnboardingSnapshotResponse(
    Guid UserId,
    Guid? PartyId,
    List<OnboardingGateStatusResponse> Gates,
    List<string> NextActions);

public record OnboardingGateStatusResponse(
    string Gate,
    bool IsSatisfied,
    bool IsRequired,
    List<string> RequiredActions);
