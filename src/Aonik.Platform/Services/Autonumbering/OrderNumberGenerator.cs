using Aonik.Platform.Contracts.Models.Autonumbering;
using Aonik.Platform.Contracts.Services.Autonumbering;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

namespace Aonik.Platform.Services.Autonumbering;

internal sealed class OrderNumberGenerator(
    IAutonumberingService autonumbering,
    ITenantProvider tenantProvider,
    IClock clock) : IOrderNumberGenerator
{
    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var profile = await autonumbering.GetProfileAsync("Order", tenantId, cancellationToken);
        if (profile is null) return OrderNumberFormatting.CreateFallback(clock.UtcNow);

        // An authored but disabled, exhausted or invalid profile must not silently switch formats.
        return (await autonumbering.GenerateAsync(new AutonumberGenerateRequest("Order", tenantId), cancellationToken)).Reference;
    }
}
