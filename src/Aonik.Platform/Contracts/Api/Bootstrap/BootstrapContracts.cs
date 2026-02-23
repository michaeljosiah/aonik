namespace Aonik.Platform.Contracts.Api.Bootstrap;

public record BootstrapStatusResponse(
    bool PlatformAdminEmailsConfigured,
    bool IsCurrentUserAllowed,
    int TenantCount);
