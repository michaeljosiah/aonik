public record BootstrapStatusResponse(
    bool PlatformAdminEmailsConfigured,
    bool IsCurrentUserAllowed,
    int TenantCount);
