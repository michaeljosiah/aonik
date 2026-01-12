using Aonik.Api.Contracts.Onboarding;

namespace Aonik.Api.Contracts.Registrations;

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
