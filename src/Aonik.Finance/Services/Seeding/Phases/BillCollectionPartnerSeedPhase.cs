using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.Seeding.Phases;

/// <summary>
/// Seeds the domestic bill-collection partner ("Gold Coast Bill Hub") and its
/// prefund account. The resulting partner ID is written to
/// <see cref="DemoSeedResultKeys.BillCollectionPartnerId"/> so the
/// <see cref="CatalogSeedPhase"/> can reference it.
/// </summary>
internal sealed class BillCollectionPartnerSeedPhase
{
    private static readonly FinanceDemoSeedIds SeedIds = FinanceDemoSeedIds.Instance;

    private readonly FinanceDbContext _db;
    private readonly PartnerPrefundSeedHelper _prefund;

    public BillCollectionPartnerSeedPhase(
        FinanceDbContext db,
        PartnerPrefundSeedHelper prefund)
    {
        _db = db;
        _prefund = prefund;
    }

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedContext context,
        Dictionary<string, object> results,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;
        const string partnerName = "Gold Coast Bill Hub";

        var partner = await _db.Partners
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == partnerName, cancellationToken);

        var capabilitiesJson = JsonSerializer.Serialize(new[] { "BILLPAY", "COLLECTIONS" });
        var operatingHoursJson = JsonSerializer.Serialize(new
        {
            timezone = "Africa/Accra",
            weekdays = "06:00-22:00",
            weekends = "08:00-20:00"
        });

        if (partner == null)
        {
            partner = new Partner
            {
                Id = SeedIds.Partners.GhanaPartnerId,
                TenantId = tenantId,
                Name = partnerName,
                Status = "Active",
                CapabilitiesJson = capabilitiesJson,
                OperatingHoursJson = operatingHoursJson,
                CreatedAt = now,
                CreatedBy = userId
            };

            _db.Partners.Add(partner);
        }
        else
        {
            partner.Status = "Active";
            partner.CapabilitiesJson = capabilitiesJson;
            partner.OperatingHoursJson = operatingHoursJson;
            partner.UpdatedAt = now;
            partner.UpdatedBy = userId;
        }

        await _prefund.EnsurePartnerPrefundAccountAsync(
            tenantId,
            partner.Id,
            partner.Name,
            "GHS",
            90000m,
            now,
            userId,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        results[DemoSeedResultKeys.BillCollectionPartnerId] = partner.Id;

        return new[] { "Ensured BillCollection GH partner and prefund account" };
    }
}
