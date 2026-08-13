using Aonik.Ai.Entities;
using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services.Safety;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Spec 096 §16.1 — the consent-bounded routing.
///
/// <para>
/// Routing classifiers through <c>AiRoutePolicy</c> was the right correction and it opened a hole:
/// the consent text <em>names</em> the external companies, but the consent reader checks only
/// subject and purpose — so a routing edit or an automatic failover could send a child's content to
/// a company the family had never heard of. It is the most likely way this design breaches consent
/// in production, <strong>precisely because failover is supposed to be invisible</strong>.
/// </para>
/// </summary>
public class SafetyRoutingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private sealed class StubModelResolver : IAiModelResolver
    {
        private readonly string? _modelName;
        public StubModelResolver(string? modelName) => _modelName = modelName;

        public Task<string?> ResolveModelNameAsync(string useCase, CancellationToken ct = default)
            => Task.FromResult(_modelName);

        public Task<string?> ResolveModelNameByIdAsync(Guid modelId, CancellationToken ct = default)
            => Task.FromResult(_modelName);
    }

    private sealed class StubConsentedProviders : IConsentedProviderReader
    {
        private readonly string[] _providers;
        public StubConsentedProviders(params string[] providers) => _providers = providers;

        public Task<IReadOnlySet<string>> GetConsentedProvidersAsync(
            Guid tenantId, Guid subjectPartyId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlySet<string>>(
                _providers.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static AiDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<AiDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static void SeedModel(AiDbContext context, string modelName, string providerName)
    {
        var provider = new AiProvider
        {
            Id = Guid.NewGuid(), Name = providerName, IsActive = true,
            CapabilitiesJson = "{}"
        };
        context.AiProviders.Add(provider);
        context.AiModels.Add(new AiModel
        {
            Id = Guid.NewGuid(), AiProviderId = provider.Id, ModelName = modelName, IsActive = true,
            CostProfileJson = "{}", LatencyProfileJson = "{}", PolicyTagsJson = "{}"
        });
        context.SaveChanges();
    }

    private static SafetyModelRouter CreateRouter(
        AiDbContext context, string? routedModel, params string[] consentedProviders)
        => new(
            context,
            new StubModelResolver(routedModel),
            new StubConsentedProviders(consentedProviders),
            new TestTenantProvider(TenantId),
            NullLogger<SafetyModelRouter>.Instance);

    [Fact]
    public async Task Route_Should_Resolve_WhenTheProviderIsNamedByTheTerms()
    {
        await using var context = CreateDbContext();
        SeedModel(context, "guard-1", "OpenAI");

        var route = await CreateRouter(context, "guard-1", "OpenAI")
            .ResolveAsync(Guid.NewGuid(), SafetyUseCases.ClassifyText);

        route.ModelName.Should().Be("guard-1");
        route.Provider.Should().Be("OpenAI");
    }

    [Fact]
    public async Task Route_Should_Refuse_WhenTheProviderIsNotNamedByTheTerms()
    {
        await using var context = CreateDbContext();
        SeedModel(context, "guard-1", "SomeNewVendor");

        // The finding this test exists for. Routing succeeded; consent did not. Refusing rather than
        // switching is the awkward-but-correct outcome — adding a provider is a terms change.
        var act = async () => await CreateRouter(context, "guard-1", "OpenAI")
            .ResolveAsync(Guid.NewGuid(), SafetyUseCases.ClassifyText);

        await act.Should().ThrowAsync<ProviderNotConsentedException>()
            .Where(e => e.Provider == "SomeNewVendor");
    }

    [Fact]
    public async Task Route_Should_Refuse_WhenTheSubjectHasConsentedToNothing()
    {
        await using var context = CreateDbContext();
        SeedModel(context, "guard-1", "OpenAI");

        // An empty consented set means NO provider may be used — not "no restriction". The
        // permissive reading of an empty collection is the whole failure §16.1 prevents.
        var act = async () => await CreateRouter(context, "guard-1")
            .ResolveAsync(Guid.NewGuid(), SafetyUseCases.ClassifyText);

        await act.Should().ThrowAsync<ProviderNotConsentedException>();
    }

    [Fact]
    public async Task Route_Should_Refuse_WhenNoRoutePolicyResolves()
    {
        await using var context = CreateDbContext();

        var act = async () => await CreateRouter(context, routedModel: null, "OpenAI")
            .ResolveAsync(Guid.NewGuid(), SafetyUseCases.ClassifyText);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No route policy*");
    }

    [Fact]
    public async Task Route_Should_Refuse_WhenTheModelHasNoActiveProvider()
    {
        await using var context = CreateDbContext();

        var act = async () => await CreateRouter(context, "guard-1", "OpenAI")
            .ResolveAsync(Guid.NewGuid(), SafetyUseCases.ClassifyText);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no active provider*");
    }

    [Theory]
    [InlineData(SafetyModalities.Text, "safety-classify-text")]
    [InlineData(SafetyModalities.Image, "safety-classify-image")]
    [InlineData(SafetyModalities.Speech, "safety-classify-speech")]
    [InlineData(SafetyModalities.Video, "safety-classify-video")]
    public void UseCase_Should_BePerModality(string modality, string expected)
    {
        // Named per modality so an operator can route text and image to different providers — and,
        // on a bad day, route one away from an outage without touching the other.
        SafetyUseCases.ForModality(modality).Should().Be(expected);
    }

    // ── Policy as data ───────────────────────────────────────────────────

    private static SafetyPolicyService CreatePolicyService(AiDbContext context)
        => new(context, new TestTenantProvider(TenantId), new TestClock());

    [Fact]
    public async Task PublishPolicy_Should_ChangeThresholds_WithoutADeployment()
    {
        await using var context = CreateDbContext();
        var reader = new SafetyPolicyReader(context, new TestTenantProvider(TenantId));

        var before = await reader.GetAsync(PartySafetyBandNames.Age6To9);
        before.ThresholdFor(SafetyCategories.Frightening).Should().Be(0.40, "the built-in default");

        await CreatePolicyService(context).PublishAsync(
            PartySafetyBandNames.Age6To9,
            new Dictionary<string, double> { [SafetyCategories.Frightening] = 0.15 });

        var after = await reader.GetAsync(PartySafetyBandNames.Age6To9);
        after.ThresholdFor(SafetyCategories.Frightening).Should().Be(0.15);
        after.Version.Should().NotBe(before.Version, "a new version, so old verdicts stay explicable");
    }

    [Fact]
    public async Task PublishPolicy_Should_DeactivateThePriorVersion_RatherThanEditIt()
    {
        await using var context = CreateDbContext();
        var service = CreatePolicyService(context);

        await service.PublishAsync(PartySafetyBandNames.Age6To9,
            new Dictionary<string, double> { [SafetyCategories.Frightening] = 0.4 });
        await service.PublishAsync(PartySafetyBandNames.Age6To9,
            new Dictionary<string, double> { [SafetyCategories.Frightening] = 0.2 });

        var all = await context.SafetyPolicies.ToListAsync();
        all.Should().HaveCount(2, "the old row survives so a verdict judged under it stays explicable");
        all.Count(p => p.IsActive).Should().Be(1);
    }

    [Fact]
    public async Task PublishPolicy_Should_RejectAnUnknownCategory()
    {
        await using var context = CreateDbContext();

        // A typo would otherwise create a threshold that never matches, leaving the real category on
        // the unknown-category default while looking configured.
        var act = async () => await CreatePolicyService(context).PublishAsync(
            PartySafetyBandNames.Age6To9,
            new Dictionary<string, double> { ["frightning"] = 0.2 });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unknown safety category*");
    }

    [Fact]
    public async Task PublishPolicy_Should_RejectAnUnknownBand()
    {
        await using var context = CreateDbContext();

        var act = async () => await CreatePolicyService(context).PublishAsync(
            "toddler", new Dictionary<string, double> { [SafetyCategories.Frightening] = 0.2 });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unknown safety band*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public async Task PublishPolicy_Should_RejectAThresholdOutsideZeroToOne(double threshold)
    {
        await using var context = CreateDbContext();

        var act = async () => await CreatePolicyService(context).PublishAsync(
            PartySafetyBandNames.Age6To9,
            new Dictionary<string, double> { [SafetyCategories.Frightening] = threshold });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
