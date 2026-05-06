using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.Seeding.Phases;

/// <summary>
/// Seeds the four cross-border partner routes (NG, GH, KE, ZA):
/// partners, branches, connectors, routing rules, and prefund accounts.
/// </summary>
internal sealed class CrossBorderPartnerNetworkSeedPhase
{
    private static readonly FinanceDemoSeedIds SeedIds = FinanceDemoSeedIds.Instance;

    private readonly FinanceDbContext _db;
    private readonly PartnerPrefundSeedHelper _prefund;

    public CrossBorderPartnerNetworkSeedPhase(
        FinanceDbContext db,
        PartnerPrefundSeedHelper prefund)
    {
        _db = db;
        _prefund = prefund;
    }

    private sealed record PartnerRouteSeed(
        string CountryCode,
        Guid PartnerId,
        Guid BranchId,
        Guid ConnectorId,
        Guid RoutingRuleId,
        string PartnerName,
        string City,
        string BranchName,
        int Priority,
        string CurrencyCode,
        decimal OpeningPrefundBalance);

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedContext context,
        Dictionary<string, object> results,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;

        var seeds = new List<PartnerRouteSeed>
        {
            new("NG", SeedIds.Partners.NigeriaPartnerId, SeedIds.Branches.NigeriaBranchId, SeedIds.Connectors.NigeriaConnectorId, SeedIds.RoutingRules.NigeriaRoutingRuleId, "Naija Utility Switch", "Lagos", "Lagos Operations Hub", 10, "NGN", 3500000m),
            new("GH", SeedIds.Partners.GhanaPartnerId, SeedIds.Branches.GhanaBranchId, SeedIds.Connectors.GhanaConnectorId, SeedIds.RoutingRules.GhanaRoutingRuleId, "Gold Coast Bill Hub", "Accra", "Accra Settlement Hub", 20, "GHS", 90000m),
            new("KE", SeedIds.Partners.KenyaPartnerId, SeedIds.Branches.KenyaBranchId, SeedIds.Connectors.KenyaConnectorId, SeedIds.RoutingRules.KenyaRoutingRuleId, "EastPay Kenya", "Nairobi", "Nairobi Operations Hub", 30, "KES", 1800000m),
            new("ZA", SeedIds.Partners.SouthAfricaPartnerId, SeedIds.Branches.SouthAfricaBranchId, SeedIds.Connectors.SouthAfricaConnectorId, SeedIds.RoutingRules.SouthAfricaRoutingRuleId, "Mzansi Bill Connect", "Johannesburg", "Johannesburg Network Hub", 40, "ZAR", 320000m)
        };

        var partnerIdsByCountry = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var connectorIdsByCountry = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in seeds)
        {
            var partner = await _db.Partners
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == seed.PartnerName, cancellationToken);

            var capabilitiesJson = JsonSerializer.Serialize(new[] { "BILLPAY", "PAYOUT", "COLLECTIONS" });
            var operatingHoursJson = JsonSerializer.Serialize(new
            {
                timezone = "Africa/Lagos",
                weekdays = "06:00-22:00",
                weekends = "08:00-20:00"
            });

            if (partner == null)
            {
                partner = new Partner
                {
                    Id = seed.PartnerId,
                    TenantId = tenantId,
                    Name = seed.PartnerName,
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
                partner.Name = seed.PartnerName;
                partner.Status = "Active";
                partner.CapabilitiesJson = capabilitiesJson;
                partner.OperatingHoursJson = operatingHoursJson;
                partner.UpdatedAt = now;
                partner.UpdatedBy = userId;
            }

            var partnerId = partner.Id;

            await _prefund.EnsurePartnerPrefundAccountAsync(
                tenantId,
                partnerId,
                seed.PartnerName,
                seed.CurrencyCode,
                seed.OpeningPrefundBalance,
                now,
                userId,
                cancellationToken);

            var branch = await _db.PartnerBranches
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                             && item.PartnerId == partnerId
                                             && item.Name == seed.BranchName,
                    cancellationToken);

            var metadataJson = JsonSerializer.Serialize(new
            {
                timezone = "Africa/Lagos",
                supportsBillPay = true,
                settlementWindow = "T+0"
            });

            if (branch == null)
            {
                branch = new PartnerBranch
                {
                    Id = seed.BranchId,
                    TenantId = tenantId,
                    PartnerId = partnerId,
                    Name = seed.BranchName,
                    Country = seed.CountryCode,
                    City = seed.City,
                    MetadataJson = metadataJson,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _db.PartnerBranches.Add(branch);
            }
            else
            {
                branch.Name = seed.BranchName;
                branch.Country = seed.CountryCode;
                branch.City = seed.City;
                branch.MetadataJson = metadataJson;
                branch.UpdatedAt = now;
                branch.UpdatedBy = userId;
            }

            var connector = await _db.Connectors
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                             && item.PartnerId == partnerId
                                             && item.ConnectorType == "API",
                    cancellationToken);

            var connectorConfigJson = JsonSerializer.Serialize(new
            {
                endpoint = $"https://api.{seed.CountryCode.ToLowerInvariant()}.demo.aonik/connectors/billpay",
                retryPolicy = "ExponentialBackoff",
                timeoutSeconds = 30
            });

            if (connector == null)
            {
                connector = new Connector
                {
                    Id = seed.ConnectorId,
                    TenantId = tenantId,
                    PartnerId = partnerId,
                    ConnectorType = "API",
                    CredentialsRef = $"kv://demo/partners/{seed.CountryCode.ToLowerInvariant()}/api",
                    ConfigJson = connectorConfigJson,
                    Status = "Active",
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _db.Connectors.Add(connector);
            }
            else
            {
                connector.ConnectorType = "API";
                connector.CredentialsRef = $"kv://demo/partners/{seed.CountryCode.ToLowerInvariant()}/api";
                connector.ConfigJson = connectorConfigJson;
                connector.Status = "Active";
                connector.UpdatedAt = now;
                connector.UpdatedBy = userId;
            }

            var connectorId = connector.Id;

            var routingRule = await _db.RoutingRules
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == seed.RoutingRuleId, cancellationToken);

            var conditionsJson = JsonSerializer.Serialize(new
            {
                quoteContext = "BillPayment",
                capability = "BILLPAY",
                destinationCountry = seed.CountryCode
            });

            if (routingRule == null)
            {
                routingRule = new RoutingRule
                {
                    Id = seed.RoutingRuleId,
                    TenantId = tenantId,
                    ConditionsJson = conditionsJson,
                    TargetPartnerId = partnerId,
                    TargetConnectorId = connectorId,
                    Priority = seed.Priority,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _db.RoutingRules.Add(routingRule);
            }
            else
            {
                routingRule.ConditionsJson = conditionsJson;
                routingRule.TargetPartnerId = partnerId;
                routingRule.TargetConnectorId = connectorId;
                routingRule.Priority = seed.Priority;
                routingRule.IsActive = true;
                routingRule.UpdatedAt = now;
                routingRule.UpdatedBy = userId;
            }

            partnerIdsByCountry[seed.CountryCode] = partnerId;
            connectorIdsByCountry[seed.CountryCode] = connectorId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        results[DemoSeedResultKeys.PartnerIdsByCountry] = partnerIdsByCountry;
        results[DemoSeedResultKeys.ConnectorIdsByCountry] = connectorIdsByCountry;

        return new[] { "Seeded cross-border partner network and routing rules" };
    }
}
