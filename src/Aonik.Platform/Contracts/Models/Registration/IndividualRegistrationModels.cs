using Aonik.Platform.Contracts.Models.Onboarding;

namespace Aonik.Platform.Contracts.Models.Registration;

public record IndividualRegistrationRequest(
    Guid? TenantId,
    string? RegistrationCountry,
    string? Title,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Password);

public record IndividualRegistrationResult(
    Guid UserId,
    Guid PartyId,
    OnboardingSnapshot Onboarding);
