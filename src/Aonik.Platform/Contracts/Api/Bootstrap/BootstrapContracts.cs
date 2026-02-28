namespace Aonik.Platform.Contracts.Api.Bootstrap;

public record BootstrapStatusResponse(
    bool PlatformAdminEmailsConfigured,
    bool IsCurrentUserAllowed,
    int TenantCount,
    bool CanBootstrap,
    string? ResolvedUserEmail = null,
    bool IsAuthenticated = false,
    bool AuthorizationHeaderPresent = false,
    bool BearerTokenLooksJwt = false,
    string? AuthFailureReason = null);
