namespace Aonik.Infrastructure.Authentication.Configuration;

public class PlatformAdminOptions
{
    public string RoleClaimType { get; set; } = "roles";              // Claim type to check
    public string RoleValue { get; set; } = "Aonik.PlatformAdmin";    // Expected value
    public string? ScopeClaimType { get; set; } = "aonik_platform_admin"; // Alternative scope claim
    
    /// <summary>
    /// Email addresses of users who should automatically be granted platform admin rights.
    /// This is useful for initial setup before identity provider roles are configured.
    /// </summary>
    public string[] AdminEmails { get; set; } = [];
}
