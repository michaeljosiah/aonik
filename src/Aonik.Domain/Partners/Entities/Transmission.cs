using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Partners.Entities;

public class Transmission : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PayoutId { get; set; }
    public Guid ConnectorId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
