namespace Aonik.Platform.Contracts.Models.Configuration;

public enum TenantRoutingMode
{
    Claim,          // Read 'aonik_tenant_id' from JWT (production)
    Subdomain,      // Extract from Host (requires forwarded headers setup)
    Header          // X-Tenant-Id (explicitly enabled via configuration)
}
