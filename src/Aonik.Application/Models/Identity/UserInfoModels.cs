namespace Aonik.Application.Models.Identity;

public record UserInfoResponse(
    Guid UserId,
    string Email,
    string? FirstName,
    string? LastName,
    IReadOnlyCollection<string> Roles,
    Guid TenantId,
    Guid PartyId,
    string? PhotoUrl,
    string? PhotoUrlSmall,
    string? PhotoUrlTiny);

public record ForgotPasswordRequest(
    string Email,
    Guid TenantId);

public record ForgotPasswordResponse(string Status);
