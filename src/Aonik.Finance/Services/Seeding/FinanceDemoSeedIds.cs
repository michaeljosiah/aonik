using System.Text.Json;

namespace Aonik.Finance.Services.Seeding;

/// <summary>
/// Strongly-typed accessor for the deterministic GUIDs used by
/// <see cref="FinanceDemoSeedContributor"/>. Loaded once at first access
/// from the embedded resource <c>finance-demo-ids.json</c> so the demo
/// seed file isn't littered with 80+ <c>Guid.Parse</c> calls.
/// </summary>
internal sealed class FinanceDemoSeedIds
{
    private const string ResourceName = "Aonik.Finance.Services.Seeding.Data.finance-demo-ids.json";

    // Lazy ensures the JSON is read+parsed exactly once per process, on
    // first reference, regardless of which contributor instance triggers it.
    private static readonly Lazy<FinanceDemoSeedIds> _instance =
        new(LoadFromEmbeddedResource, LazyThreadSafetyMode.ExecutionAndPublication);

    public static FinanceDemoSeedIds Instance => _instance.Value;

    public required CatalogIds Catalog { get; init; }
    public required PricingIds Pricing { get; init; }
    public required CrossBorderCategoryIds CrossBorderCategories { get; init; }
    public required CrossBorderBillerIds CrossBorderBillers { get; init; }
    public required CrossBorderServiceIds CrossBorderServices { get; init; }
    public required PartnerIds Partners { get; init; }
    public required BranchIds Branches { get; init; }
    public required ConnectorIds Connectors { get; init; }
    public required RoutingRuleIds RoutingRules { get; init; }
    public required HouseholdIds Households { get; init; }
    public required CrossBorderFxQuoteIds CrossBorderFxQuotes { get; init; }
    public required CrossBorderFeePolicyIds CrossBorderFeePolicies { get; init; }
    public required CrossBorderLimitsPolicyIds CrossBorderLimitsPolicies { get; init; }
    public required GlobalCategoryIds GlobalCategories { get; init; }
    public required OrderActivityIds OrderActivity { get; init; }
    public required PartyReferenceIds PartyReferences { get; init; }

    private static FinanceDemoSeedIds LoadFromEmbeddedResource()
    {
        var assembly = typeof(FinanceDemoSeedIds).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' not found. Ensure it is included as <EmbeddedResource> in Aonik.Finance.csproj.");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        return JsonSerializer.Deserialize<FinanceDemoSeedIds>(stream, options)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize '{ResourceName}' into FinanceDemoSeedIds.");
    }

    // ── Domain groupings ────────────────────────────────────────────────
    // Each record mirrors a top-level object in finance-demo-ids.json.

    internal sealed record CatalogIds(
        Guid UtilitiesCategoryId,
        Guid EcgBillerId,
        Guid GhanaWaterBillerId,
        Guid EcgPrepaidServiceId,
        Guid GhanaWaterServiceId,
        Guid EcgPostpaidServiceId,
        Guid GhanaWaterPrepaidServiceId);

    internal sealed record PricingIds(
        Guid DemoFxQuoteId,
        Guid DemoFeePolicyId,
        Guid DemoLimitsPolicyId);

    internal sealed record CrossBorderCategoryIds(
        Guid NigeriaUtilitiesCategoryId,
        Guid KenyaUtilitiesCategoryId,
        Guid SouthAfricaUtilitiesCategoryId);

    internal sealed record CrossBorderBillerIds(
        Guid IkejaElectricBillerId,
        Guid LagosWaterBillerId,
        Guid KenyaPowerBillerId,
        Guid CityPowerBillerId);

    internal sealed record CrossBorderServiceIds(
        Guid IkejaPrepaidServiceId,
        Guid IkejaPostpaidServiceId,
        Guid LagosWaterServiceId,
        Guid LagosWaterPrepaidServiceId,
        Guid KenyaPowerServiceId,
        Guid KenyaPowerPostpaidServiceId,
        Guid CityPowerServiceId,
        Guid CityPowerPostpaidServiceId);

    internal sealed record PartnerIds(
        Guid NigeriaPartnerId,
        Guid GhanaPartnerId,
        Guid KenyaPartnerId,
        Guid SouthAfricaPartnerId);

    internal sealed record BranchIds(
        Guid NigeriaBranchId,
        Guid GhanaBranchId,
        Guid KenyaBranchId,
        Guid SouthAfricaBranchId);

    internal sealed record ConnectorIds(
        Guid NigeriaConnectorId,
        Guid GhanaConnectorId,
        Guid KenyaConnectorId,
        Guid SouthAfricaConnectorId);

    internal sealed record RoutingRuleIds(
        Guid NigeriaRoutingRuleId,
        Guid GhanaRoutingRuleId,
        Guid KenyaRoutingRuleId,
        Guid SouthAfricaRoutingRuleId);

    internal sealed record HouseholdIds(
        Guid FamilyHouseholdId,
        Guid ProfessionalsHouseholdId,
        Guid FamilyHouseholdMemberId,
        Guid ProfessionalsHouseholdMemberId);

    internal sealed record CrossBorderFxQuoteIds(
        Guid NgnKesFxQuoteId,
        Guid NgnZarFxQuoteId,
        Guid UsdGhsFxQuoteId,
        Guid UsdKesFxQuoteId,
        Guid UsdZarFxQuoteId,
        Guid GbpNgnFxQuoteId,
        Guid GbpGhsFxQuoteId,
        Guid GbpKesFxQuoteId,
        Guid GbpZarFxQuoteId);

    internal sealed record CrossBorderFeePolicyIds(
        Guid CrossBorderBand1FeePolicyId,
        Guid CrossBorderBand2FeePolicyId,
        Guid CrossBorderBand3FeePolicyId,
        Guid CrossBorderKesFeePolicyId,
        Guid CrossBorderZarFeePolicyId);

    internal sealed record CrossBorderLimitsPolicyIds(
        Guid KenyaLimitsPolicyId,
        Guid SouthAfricaLimitsPolicyId);

    internal sealed record GlobalCategoryIds(
        Guid GlobalUtilitiesCategoryId,
        Guid GlobalTelecomCategoryId,
        Guid GlobalInternetCategoryId,
        Guid GlobalEducationCategoryId,
        Guid GlobalGovernmentCategoryId,
        Guid GlobalCableCategoryId);

    internal sealed record OrderActivityIds(
        Guid OrderKwameEcg,
        Guid OrderKwameWater,
        Guid OrderTundeIkeja,
        Guid OrderTundeLagosWater,
        Guid OrderAcmePayoutNg,
        Guid OrderAdwoaWaterFailed,
        Guid OrderOliviaToNaledi,
        Guid OrderLiamToKwame,
        Guid OrderKofiAmaTransfer,
        Guid OrderPeterKenyaPower);

    internal sealed record PartyReferenceIds(
        Guid DemoPayerPartyId,
        Guid DemoReceiverPartyId,
        Guid TundePartyId,
        Guid AdwoaPartyId,
        Guid PeterPartyId,
        Guid NalediPartyId,
        Guid KofiPartyId,
        Guid AcmeImportsPartyId,
        Guid OliviaPartyId,
        Guid LiamPartyId);
}
