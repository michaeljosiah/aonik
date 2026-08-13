using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// The vendor seam for classification. One implementation per provider.
///
/// <para>
/// Deliberately narrow — a reference and a band in, category scores out. It has no access to routing,
/// consent or policy, so a vendor adapter cannot accidentally decide anything it should not.
/// </para>
/// </summary>
public interface ISafetyClassificationProvider
{
    /// <summary>Matches <c>AiProvider.Name</c>, so routing and this registry agree on one identifier.</summary>
    string Provider { get; }

    /// <summary>Modalities this adapter can judge.</summary>
    IReadOnlySet<string> SupportedModalities { get; }

    /// <summary>
    /// Category to confidence, 0–1. Throwing is a legitimate outcome: the gate turns it into
    /// <c>CheckUnavailable</c> and refuses delivery, which is the behaviour we want on a bad day.
    /// </summary>
    Task<IReadOnlyDictionary<string, double>> ScoreAsync(
        string modality,
        string reference,
        string safetyBand,
        string modelName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// An <see cref="IContentClassifier"/> for one modality that resolves its model through
/// <c>AiRoutePolicy</c>, refuses a provider the subject's terms do not name, and records an
/// <c>AiRun</c> for the call (Spec 096 §16, §16.1).
///
/// <para>
/// The order of operations is the design. Route first, then check consent, then call — because
/// checking consent after the call would mean the content had already left, and the check would be
/// an audit note rather than a control.
/// </para>
/// </summary>
internal sealed class RoutedContentClassifier : IContentClassifier
{
    private readonly ISafetyModelRouter _router;
    private readonly IEnumerable<ISafetyClassificationProvider> _providers;
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ILogger<RoutedContentClassifier> _logger;

    public RoutedContentClassifier(
        string modality,
        ISafetyModelRouter router,
        IEnumerable<ISafetyClassificationProvider> providers,
        AiDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        ILogger<RoutedContentClassifier> logger)
    {
        Modality = modality;
        _router = router;
        _providers = providers;
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _logger = logger;
    }

    public string Modality { get; }

    public async Task<ClassificationResult> ClassifyAsync(
        ClassificationRequest request, CancellationToken cancellationToken = default)
    {
        var startedAt = _clock.UtcNow;

        // 1. Central routing. No hard-coded model, no vendor chosen here — the platform rule.
        var route = await _router.ResolveAsync(
            request.SubjectPartyId, SafetyUseCases.ForModality(Modality), cancellationToken);

        // 2. The adapter for the ROUTED provider, not a preferred one. If routing picked a provider
        //    we have no adapter for, that is a misconfiguration and it fails closed rather than
        //    quietly falling back to one we do have — which would defeat the consent check above.
        var provider = _providers.FirstOrDefault(p =>
            string.Equals(p.Provider, route.Provider, StringComparison.OrdinalIgnoreCase)
            && p.SupportedModalities.Contains(Modality));

        if (provider is null)
        {
            throw new InvalidOperationException(
                $"No classification adapter for provider '{route.Provider}' and modality '{Modality}'. "
                + "Refusing rather than substituting a provider the subject's terms may not name.");
        }

        // 3. Only now does the content leave.
        var scores = await provider.ScoreAsync(
            Modality, request.Reference, request.SafetyBand, route.ModelName, cancellationToken);

        var runId = await RecordRunAsync(request, route, startedAt, cancellationToken);

        return new ClassificationResult(scores, runId);
    }

    /// <summary>
    /// Rule 4: every AI action is auditable, recorded as an <c>AiRun</c>. A safety classifier acting
    /// on behalf of a child is the last place to make an exception — and the run id is what makes a
    /// verdict reconstructible when someone asks why their child saw something.
    /// </summary>
    private async Task<Guid> RecordRunAsync(
        ClassificationRequest request,
        SafetyRoute route,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var modelId = await ResolveModelIdAsync(route.ModelName, cancellationToken);

        var run = new AiRun
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantProvider.GetCurrentTenantId(),
            UseCase = SafetyUseCases.ForModality(Modality),
            AiModelId = modelId,
            // A reference, never the content. The AiRun log must not become a second copy of every
            // prompt a child has ever typed.
            InputRefsJson = $$"""{"subject":"{{request.SubjectPartyId}}","band":"{{request.SafetyBand}}"}""",
            Outcome = "Completed",
            LatencyMs = (int)(_clock.UtcNow - startedAt).TotalMilliseconds
        };

        _dbContext.AiRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Safety classification {RunId} for {Modality} via {Provider}/{Model}.",
            run.Id, Modality, route.Provider, route.ModelName);

        return run.Id;
    }

    private async Task<Guid> ResolveModelIdAsync(string modelName, CancellationToken cancellationToken)
    {
        var model = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(
                _dbContext.AiModels.Where(m => m.ModelName == modelName && m.IsActive),
                cancellationToken);

        return model?.Id ?? Guid.Empty;
    }
}
