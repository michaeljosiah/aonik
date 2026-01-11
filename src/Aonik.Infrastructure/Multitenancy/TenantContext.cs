using Aonik.Application.Abstractions.Multitenancy;

namespace Aonik.Infrastructure.Multitenancy;

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
    public string? ResolutionSource { get; set; }
    public bool IsResolved => TenantId.HasValue;
}
