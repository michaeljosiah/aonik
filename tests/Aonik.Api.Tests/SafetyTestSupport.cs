using Aonik.Ai.Entities;
using Aonik.Ai.Services.Safety;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// The SQL-lane factory with one classification route registered: a keyword classifier standing in
/// for the OpenAI moderation adapter, so the gate's whole path — route resolution, consented
/// providers, thresholds, recording, guardian review — runs for real over HTTP.
/// </summary>
public sealed class SafetyWebApplicationFactory : CustomWebApplicationFactory
{
    public SafetyWebApplicationFactory(string sqlServerConnectionString) : base(sqlServerConnectionString)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddScoped<ISafetyClassificationProvider, KeywordClassificationProvider>());
    }
}

/// <summary>A SIMULATED classifier: "blood" is graphic violence, everything else is fine. Text only, whole text.</summary>
internal sealed class KeywordClassificationProvider : ISafetyClassificationProvider
{
    public const string Name = "fake-classifier";
    public const string Model = "keyword-moderation";

    public string Provider => Name;

    public IReadOnlySet<string> SupportedModalities { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { SafetyModalities.Text };

    public TemporalCoverage Coverage => TemporalCoverage.Complete;

    public Task<IReadOnlyDictionary<string, double>> ScoreAsync(string modality, string reference, string safetyBand, string modelName, CancellationToken cancellationToken = default)
    {
        var violent = reference.Contains("blood", StringComparison.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, double> scores = new Dictionary<string, double>
        {
            [SafetyCategories.GraphicViolence] = violent ? 0.95 : 0.01,
            [SafetyCategories.Sexual] = 0.0,
            [SafetyCategories.SelfHarm] = 0.0,
            [SafetyCategories.Hate] = 0.0,
        };
        return Task.FromResult(scores);
    }
}

internal static class SafetyTestSeeding
{
    /// <summary>The catalogue rows the safety router needs: a provider, its model, and the route policy for text classification.</summary>
    public static async Task SeedClassificationRouteAsync(CustomWebApplicationFactory factory, Guid tenantId, Guid userId, string providerName = KeywordClassificationProvider.Name, string modelName = KeywordClassificationProvider.Model)
    {
        await using var scope = Impersonate(factory, tenantId, userId);
        // Through the canonical context: its model matches the database, where these rows keep a plain,
        // required RowVersion that the module context believes the server generates.
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();

        var provider = new AiProvider { Id = Guid.NewGuid(), Name = providerName, CapabilitiesJson = "{}", IsActive = true, RowVersion = new byte[8] };
        var model = new AiModel { Id = Guid.NewGuid(), AiProviderId = provider.Id, ModelName = modelName, ContextWindow = 0, CostProfileJson = "{}", LatencyProfileJson = "{}", PolicyTagsJson = "[]", IsActive = true, RowVersion = new byte[8] };
        db.Set<AiProvider>().Add(provider);
        db.Set<AiModel>().Add(model);
        db.Set<AiRoutePolicy>().Add(new AiRoutePolicy { Id = Guid.NewGuid(), TenantId = tenantId, UseCase = SafetyUseCases.ClassifyText, RiskTier = "low", DataSensitivity = "child", PrimaryModelId = model.Id, IsActive = true, RowVersion = new byte[8] });
        await db.SaveChangesAsync();
    }

    /// <summary>A published terms version naming the providers a subject's classification consent covers (Spec 095 §12.3).</summary>
    public static async Task SeedTermsAsync(CustomWebApplicationFactory factory, Guid tenantId, Guid userId, string version, string namedProviders)
    {
        await using var scope = Impersonate(factory, tenantId, userId);
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        platform.ConsentTermsVersions.Add(new ConsentTermsVersion { Id = Guid.NewGuid(), TenantId = tenantId, Version = version, NamedProviders = namedProviders, PublishedAt = DateTime.UtcNow, IsCurrent = true, RowVersion = new byte[8] });
        await platform.SaveChangesAsync();
    }

    public static async Task SetPreReviewAsync(CustomWebApplicationFactory factory, Guid tenantId, Guid userId, Guid guardianPartyId, Guid childPartyId, bool enabled)
    {
        await using var scope = Impersonate(factory, tenantId, userId);
        await scope.ServiceProvider.GetRequiredService<IGuardianPreReviewService>().SetPreReviewAsync(guardianPartyId, childPartyId, enabled);
    }

    private static AsyncServiceScope Impersonate(CustomWebApplicationFactory factory, Guid tenantId, Guid userId)
    {
        var scope = factory.Services.CreateAsyncScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenant.TenantId = tenantId;
        tenant.ResolutionSource = "test";

        var user = scope.ServiceProvider.GetRequiredService<ICurrentUserContext>();
        user.UserId = userId;
        user.TenantId = tenantId;
        user.IsAuthenticated = true;
        user.Roles = ["PersonalUser"];

        return scope;
    }
}
