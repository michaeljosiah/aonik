namespace Aonik.Platform.Contracts.Api.Identity;

public record CurrentUserResponse(
    Guid UserId,
    Guid TenantId,
    string? Email,
    string? Phone,
    string Status,
    Guid? PartyId,
    string? DisplayName);
