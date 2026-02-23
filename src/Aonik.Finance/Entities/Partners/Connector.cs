using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

public class Connector : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public string ConnectorType { get; set; } = string.Empty;
    public string? CredentialsRef { get; set; }
    public string ConfigJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
