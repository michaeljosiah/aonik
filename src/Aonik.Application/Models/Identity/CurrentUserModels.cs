namespace Aonik.Application.Models.Identity;

public record CurrentUserSnapshot(
    Guid UserId,
    Guid TenantId,
    string? Email,
    string? Phone,
    string Status,
    Guid? PartyId,
    string? DisplayName);
