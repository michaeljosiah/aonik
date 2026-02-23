namespace Aonik.Platform.Contracts.Models.Identity;

public record BootstrapUserContext(
    string ExternalIssuer,
    string ExternalSubject,
    string? ExternalTenantId,
    string? Email);

public record BootstrapTenantResult(
    Guid TenantId,
    string TenantName,
    bool TenantCreated,
    Guid UserId,
    bool UserCreated,
    bool PlatformAdminAssigned);

