namespace Aonik.Infrastructure.Tests.VectorStore.Fixtures;

using Aonik.SharedKernel.Abstractions.Multitenancy;

/// <summary>
/// Test implementation of ITenantProvider for unit tests.
/// </summary>
internal sealed class TestTenantProvider : ITenantProvider
{
    private Guid currentTenantId = Guid.NewGuid();

    public TestTenantProvider()
    {
    }

    public TestTenantProvider(Guid tenantId)
    {
        currentTenantId = tenantId;
    }

    public Guid GetCurrentTenantId() => currentTenantId;

    public bool TryGetCurrentTenantId(out Guid tenantId)
    {
        tenantId = currentTenantId;
        return true;
    }

    public void SetCurrentTenantId(Guid tenantId)
    {
        currentTenantId = tenantId;
    }
}
