namespace Aonik.Application.Models.Identity;

public record CustomerProfileResponse(
    Guid PartyId,
    Guid UserId,
    Guid TenantId,
    string Email,
    string? FirstName,
    string? LastName,
    string? Title,
    string? Phone,
    string? CountryCode,
    string? PhotoUrl);

public record UpdateCustomerProfileRequest(
    string? FirstName,
    string? LastName,
    string? Title,
    string? Phone,
    string? CountryCode);

public record UpdateCustomerEmailRequest(
    string CurrentEmail,
    string NewEmail,
    string Password);

public record UpdateCustomerPasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record UpdateCustomerPasswordResponse(
    string Status);

public record CustomerPhotoUploadResponse(
    string PhotoUrl);

public record CustomerPhotoDeleteResponse(
    string Status);
