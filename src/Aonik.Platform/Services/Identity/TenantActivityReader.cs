using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Platform;

namespace Aonik.Platform.Services.Identity;

internal sealed class TenantActivityReader(PlatformDbContext database) : ITenantActivityReader
{
    public Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => database.Tenants.AsNoTracking().AnyAsync(tenant => tenant.Id == tenantId
            && tenant.Status == TenantStatus.Active, cancellationToken);
}
