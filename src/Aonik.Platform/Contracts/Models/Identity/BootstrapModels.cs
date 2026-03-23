namespace Aonik.Platform.Contracts.Models.Identity;

public record BootstrapOwnerContext(
    string Email,
    string? DisplayName = null);

public record BootstrapTenantResult(
    Guid TenantId,
    string TenantName,
    bool TenantCreated,
    Guid UserId,
    bool UserCreated,
    bool PlatformAdminAssigned,
    bool TenantAdminAssigned,
    string OwnerEmail,
    bool RequiresIdentityLink);
