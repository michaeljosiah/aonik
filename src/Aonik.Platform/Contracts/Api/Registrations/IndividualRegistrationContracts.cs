using Aonik.Platform.Contracts.Api.Onboarding;

namespace Aonik.Platform.Contracts.Api.Registrations;

public record IndividualRegistrationRequest(
    Guid? TenantId,
    string? RegistrationCountry,
    string? Title,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Password);

public record IndividualRegistrationResponse(
    Guid UserId,
    Guid PartyId,
    OnboardingSnapshotResponse Onboarding);
