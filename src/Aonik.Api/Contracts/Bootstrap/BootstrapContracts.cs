public record BootstrapStatusResponse(
    bool BootstrapEnabled,
    bool PlatformAdminEmailsConfigured,
    bool IsCurrentUserAllowed,
    int TenantCount);
