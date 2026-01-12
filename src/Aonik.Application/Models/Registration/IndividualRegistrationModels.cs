using Aonik.Application.Models.Onboarding;

namespace Aonik.Application.Models.Registration;

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
