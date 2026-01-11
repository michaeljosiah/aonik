namespace Aonik.Infrastructure.Authentication.Configuration;

public class AuthOptions
{
    public string Provider { get; set; } = "AzureAd"; // "AzureAd" or "Auth0"
    public TenantRoutingMode TenantRouting { get; set; } = TenantRoutingMode.Claim;
    public AzureAdOptions AzureAd { get; set; } = new();
    public Auth0Options Auth0 { get; set; } = new();
}

public enum TenantRoutingMode
{
    Claim,          // Read 'aonik_tenant_id' from JWT (production)
    Subdomain,      // Extract from Host (requires forwarded headers setup)
    Header          // X-Tenant-Id (explicitly enabled via configuration)
}

public class AzureAdOptions
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool ValidateIssuer { get; set; } = true;
    public int ClockSkewSeconds { get; set; } = 300;
}

public class Auth0Options
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool ValidateIssuer { get; set; } = true;
    public int ClockSkewSeconds { get; set; } = 300;
}
