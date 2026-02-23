namespace Aonik.Platform.Contracts.Models.Authentication;

public record IdpUserRegistration(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone);

public record ExternalIdentityResult(
    string ExternalIssuer,
    string ExternalSubject,
    string? ExternalTenantId);
