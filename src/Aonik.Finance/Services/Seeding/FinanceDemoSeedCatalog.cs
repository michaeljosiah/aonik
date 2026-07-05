using System.Text.Json;

using Aonik.SharedKernel.Seeding;

namespace Aonik.Finance.Services.Seeding;

/// <summary>
/// Static descriptive data for Finance demo catalog seeds. Loaded once
/// per process from the embedded resource <c>finance-demo-catalog.json</c>.
/// Pairs with <see cref="FinanceDemoSeedIds"/> — that file owns the GUIDs,
/// this one owns the human-readable fields.
/// </summary>
internal sealed class FinanceDemoSeedCatalog
{
    private const string ResourceName = "Aonik.Finance.Services.Seeding.Data.finance-demo-catalog.json";

    private static readonly Lazy<FinanceDemoSeedCatalog> _instance =
        new(LoadFromEmbeddedResource, LazyThreadSafetyMode.ExecutionAndPublication);

    public static FinanceDemoSeedCatalog Instance => _instance.Value;

    public required IReadOnlyList<GlobalCategoryRecord> GlobalCategories { get; init; }

    private static FinanceDemoSeedCatalog LoadFromEmbeddedResource()
    {
        var assembly = typeof(FinanceDemoSeedCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' not found. Ensure it is included as <EmbeddedResource> in Aonik.Finance.csproj.");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        return JsonSerializer.Deserialize<FinanceDemoSeedCatalog>(stream, options)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize '{ResourceName}' into FinanceDemoSeedCatalog.");
    }

    /// <summary>
    /// One global biller category. The matching GUID is resolved by name
    /// at seed time against <see cref="FinanceDemoSeedIds.GlobalCategoryIds"/>.
    /// </summary>
    internal sealed record GlobalCategoryRecord(
        string Name,
        string CountryCode,
        string Description,
        string IconUrl,
        int SortOrder);
}
