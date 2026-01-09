namespace Aonik.Application.Abstractions.Authentication;

public interface ITenantResolver
{
    Task<Guid?> ResolveTenantIdAsync(CancellationToken ct = default);
}
