using System.Text.Json;

namespace Aonik.SharedKernel.Seeding;

/// <summary>
/// Strongly-typed accessor for the deterministic GUIDs used by the demo-seed
/// contributors. Loaded once at first access from the embedded resource
/// <c>finance-demo-ids.json</c> so the demo seed files aren't littered with
/// 80+ <c>Guid.Parse</c> calls.
///
/// Lives in SharedKernel (Spec 027 S5, #118/#126) so both the Finance seed
/// contributor and the PersonalFinance seed phases can share the same ids
/// without either module taking a project reference on the other.
/// </summary>
public sealed class FinanceDemoSeedIds
{
    private const string ResourceName = "Aonik.SharedKernel.Seeding.Data.finance-demo-ids.json";

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
    public required PersonalFinancePersonaIds PersonalFinancePersonas { get; init; }

    private static FinanceDemoSeedIds LoadFromEmbeddedResource()
    {
        var assembly = typeof(FinanceDemoSeedIds).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' not found. Ensure it is included as <EmbeddedResource> in Aonik.SharedKernel.csproj.");

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

    public sealed record CatalogIds(
        Guid UtilitiesCategoryId,
        Guid EcgBillerId,
        Guid GhanaWaterBillerId,
        Guid EcgPrepaidServiceId,
        Guid GhanaWaterServiceId,
        Guid EcgPostpaidServiceId,
        Guid GhanaWaterPrepaidServiceId);

    public sealed record PricingIds(
        Guid DemoFxQuoteId,
        Guid DemoFeePolicyId,
        Guid DemoLimitsPolicyId);

    public sealed record CrossBorderCategoryIds(
        Guid NigeriaUtilitiesCategoryId,
        Guid KenyaUtilitiesCategoryId,
        Guid SouthAfricaUtilitiesCategoryId);

    public sealed record CrossBorderBillerIds(
        Guid IkejaElectricBillerId,
        Guid LagosWaterBillerId,
        Guid KenyaPowerBillerId,
        Guid CityPowerBillerId);

    public sealed record CrossBorderServiceIds(
        Guid IkejaPrepaidServiceId,
        Guid IkejaPostpaidServiceId,
        Guid LagosWaterServiceId,
        Guid LagosWaterPrepaidServiceId,
        Guid KenyaPowerServiceId,
        Guid KenyaPowerPostpaidServiceId,
        Guid CityPowerServiceId,
        Guid CityPowerPostpaidServiceId);

    public sealed record PartnerIds(
        Guid NigeriaPartnerId,
        Guid GhanaPartnerId,
        Guid KenyaPartnerId,
        Guid SouthAfricaPartnerId);

    public sealed record BranchIds(
        Guid NigeriaBranchId,
        Guid GhanaBranchId,
        Guid KenyaBranchId,
        Guid SouthAfricaBranchId);

    public sealed record ConnectorIds(
        Guid NigeriaConnectorId,
        Guid GhanaConnectorId,
        Guid KenyaConnectorId,
        Guid SouthAfricaConnectorId);

    public sealed record RoutingRuleIds(
        Guid NigeriaRoutingRuleId,
        Guid GhanaRoutingRuleId,
        Guid KenyaRoutingRuleId,
        Guid SouthAfricaRoutingRuleId);

    public sealed record HouseholdIds(
        Guid FamilyHouseholdId,
        Guid ProfessionalsHouseholdId,
        Guid FamilyHouseholdMemberId,
        Guid ProfessionalsHouseholdMemberId);

    public sealed record CrossBorderFxQuoteIds(
        Guid NgnKesFxQuoteId,
        Guid NgnZarFxQuoteId,
        Guid UsdGhsFxQuoteId,
        Guid UsdKesFxQuoteId,
        Guid UsdZarFxQuoteId,
        Guid GbpNgnFxQuoteId,
        Guid GbpGhsFxQuoteId,
        Guid GbpKesFxQuoteId,
        Guid GbpZarFxQuoteId);

    public sealed record CrossBorderFeePolicyIds(
        Guid CrossBorderBand1FeePolicyId,
        Guid CrossBorderBand2FeePolicyId,
        Guid CrossBorderBand3FeePolicyId,
        Guid CrossBorderKesFeePolicyId,
        Guid CrossBorderZarFeePolicyId);

    public sealed record CrossBorderLimitsPolicyIds(
        Guid KenyaLimitsPolicyId,
        Guid SouthAfricaLimitsPolicyId);

    public sealed record GlobalCategoryIds(
        Guid GlobalUtilitiesCategoryId,
        Guid GlobalTelecomCategoryId,
        Guid GlobalInternetCategoryId,
        Guid GlobalEducationCategoryId,
        Guid GlobalGovernmentCategoryId,
        Guid GlobalCableCategoryId);

    public sealed record OrderActivityIds(
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

    public sealed record PartyReferenceIds(
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

    /// <summary>
    /// Deterministic Guids for the UK personal-finance demo personas (Seamus + Mark Keane).
    /// Mirrors the entries in <c>platform-demo-ids.json#personalFinancePersonas</c> plus
    /// the Finance-side PersonalProfile and PersonalAccount ids.
    /// </summary>
    public sealed record PersonalFinancePersonaIds(
        Guid SeamusKeanePartyId,
        Guid MarkKeanePartyId,
        Guid SeamusKeaneUserId,
        Guid MarkKeaneUserId,
        Guid SeamusKeanePersonalProfileId,
        Guid MarkKeanePersonalProfileId,
        Guid SeamusCurrentAccountId,
        Guid SeamusCreditCardAccountId,
        Guid SeamusSavingsAccountId,
        Guid MarkCurrentAccountId,
        Guid MarkCreditCardAccountId,
        Guid MarkSavingsAccountId);
}
