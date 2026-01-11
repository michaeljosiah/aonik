namespace Aonik.Application.Abstractions.Multitenancy;

public interface ITenantContext
{
    Guid? TenantId { get; set; }
    string? ResolutionSource { get; set; }
    bool IsResolved { get; }
}
