using System.Text.Json;

namespace Aonik.Platform.Services.Seeding;

/// <summary>
/// Strongly-typed accessor for the deterministic GUIDs used by
/// <see cref="DemoSeedService"/>. Loaded once per process from the
/// embedded resource <c>platform-demo-ids.json</c> so the demo seed
/// orchestrator isn't littered with <c>Guid.Parse</c> calls.
/// </summary>
internal sealed class PlatformDemoSeedIds
{
    private const string ResourceName = "Aonik.Platform.Persistence.Seed.Data.platform-demo-ids.json";

    private static readonly Lazy<PlatformDemoSeedIds> _instance =
        new(LoadFromEmbeddedResource, LazyThreadSafetyMode.ExecutionAndPublication);

    public static PlatformDemoSeedIds Instance => _instance.Value;

    public required DemoPairIds DemoPair { get; init; }
    public required PersonaIds Personas { get; init; }
    public required PersonaRelationshipIds PersonaRelationships { get; init; }
    public required PersonalFinancePersonaIds PersonalFinancePersonas { get; init; }

    private static PlatformDemoSeedIds LoadFromEmbeddedResource()
    {
        var assembly = typeof(PlatformDemoSeedIds).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' not found. Ensure it is included as <EmbeddedResource> in Aonik.Platform.csproj.");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        return JsonSerializer.Deserialize<PlatformDemoSeedIds>(stream, options)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize '{ResourceName}' into PlatformDemoSeedIds.");
    }

    // ── Domain groupings ────────────────────────────────────────────────

    internal sealed record DemoPairIds(
        Guid DemoPayerPartyId,
        Guid DemoReceiverPartyId,
        Guid DemoRelationshipId);

    internal sealed record PersonaIds(
        Guid TundePartyId,
        Guid AdwoaPartyId,
        Guid PeterPartyId,
        Guid NalediPartyId,
        Guid AishaPartyId,
        Guid KofiPartyId,
        Guid AcmeImportsPartyId,
        Guid SafariFreightPartyId,
        Guid OliviaPartyId,
        Guid LiamPartyId);

    internal sealed record PersonaRelationshipIds(
        Guid TundeAdwoaRelationshipId,
        Guid TundePeterRelationshipId,
        Guid NalediAishaRelationshipId,
        Guid KofiAmaRelationshipId,
        Guid OliviaNalediRelationshipId,
        Guid LiamKwameRelationshipId);

    /// <summary>
    /// Deterministic Guids for the UK personal-finance demo personas (Seamus + Mark Keane).
    /// UserIds are synthetic and not linked to any IdP account — they exist so personal-finance
    /// entities (PersonalTransaction, Bill, Subscription, PersonalAccount) can be scoped to a
    /// stable identity. A future admin feature will let the operator invite a real user and
    /// rewrite all rows here from the synthetic UserId to the real one.
    /// </summary>
    internal sealed record PersonalFinancePersonaIds(
        Guid SeamusKeanePartyId,
        Guid MarkKeanePartyId,
        Guid SeamusKeaneUserId,
        Guid MarkKeaneUserId);
}
