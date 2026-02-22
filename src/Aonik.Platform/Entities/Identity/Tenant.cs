using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Identity;

public class Tenant : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Subdomain { get; set; }                          // For subdomain-based routing
    public string Environment { get; set; } = string.Empty;
    public string DefaultCurrency { get; set; } = string.Empty;
    public string SupportedCountriesJson { get; set; } = string.Empty;
    public string Status { get; set; } = TenantStatus.Active;
    
    // Company Setup fields
    public string? LogoUrl { get; set; }
    public string? Industry { get; set; }
    public string? CompanySize { get; set; }
    public string? Website { get; set; }
    
    // Contact fields
    public string? ContactEmail { get; set; }
    public string? ContactMobile { get; set; }
    
    // Address fields (stored as JSON for flexibility)
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    
    // Setup completion tracking
    public bool IsSetupComplete { get; set; } = false;
    public int SetupStep { get; set; } = 0;
}

public static class TenantStatus
{
    public const string Active = "Active";
    public const string Provisioning = "Provisioning";
    public const string Deactivated = "Deactivated";
    public const string Suspended = "Suspended";
}
