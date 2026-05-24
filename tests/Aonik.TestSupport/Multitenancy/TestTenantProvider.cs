using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.TestSupport.Multitenancy;

/// <summary>
/// In-memory <see cref="ITenantProvider"/> that always reports a fixed
/// tenant id. Convenience overload exists for tests that don't care
/// which tenant they're scoped to — it generates a fresh random Guid.
/// </summary>
public sealed class TestTenantProvider : ITenantProvider
{
    private readonly Guid _tenantId;

    public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;

    /// <summary>Generate a random tenant id; useful when the test
    /// doesn't share a tenant with anyone else.</summary>
    public TestTenantProvider() : this(Guid.NewGuid()) { }

    public Guid TenantId => _tenantId;

    public Guid GetCurrentTenantId() => _tenantId;

    public bool TryGetCurrentTenantId(out Guid tenantId)
    {
        tenantId = _tenantId;
        return true;
    }
}
