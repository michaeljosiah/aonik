using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class Tenant : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string DefaultCurrency { get; set; } = string.Empty;
    public string SupportedCountriesJson { get; set; } = string.Empty;
    public string Status { get; set; } = TenantStatus.Active;
}

public static class TenantStatus
{
    public const string Active = "Active";
    public const string Provisioning = "Provisioning";
    public const string Deactivated = "Deactivated";
    public const string Suspended = "Suspended";
}
