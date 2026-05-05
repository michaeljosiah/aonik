using System.Text.Json;

namespace Aonik.Platform.Services.Seeding;

/// <summary>
/// Canonical demo seed name lists used both for upserts and for the
/// reverse-phase deletion filters. Loaded once per process from the
/// embedded resource <c>platform-demo-names.json</c>.
/// </summary>
internal sealed class PlatformDemoSeedNames
{
    private const string ResourceName = "Aonik.Platform.Persistence.Seed.Data.platform-demo-names.json";

    private static readonly Lazy<PlatformDemoSeedNames> _instance =
        new(LoadFromEmbeddedResource, LazyThreadSafetyMode.ExecutionAndPublication);

    public static PlatformDemoSeedNames Instance => _instance.Value;

    public required string[] WorkflowSlugs { get; init; }
    public required string[] NotificationTypes { get; init; }
    public required string[] AgentNames { get; init; }
    public required string[] PartnerNames { get; init; }
    public required string[] HouseholdNames { get; init; }

    private static PlatformDemoSeedNames LoadFromEmbeddedResource()
    {
        var assembly = typeof(PlatformDemoSeedNames).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' not found. Ensure it is included as <EmbeddedResource> in Aonik.Platform.csproj.");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        return JsonSerializer.Deserialize<PlatformDemoSeedNames>(stream, options)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize '{ResourceName}' into PlatformDemoSeedNames.");
    }
}
