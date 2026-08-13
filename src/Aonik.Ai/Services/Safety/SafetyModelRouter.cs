using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Resolves which model a safety classifier uses, and refuses one the subject's terms do not name
/// (Spec 096 §16, §16.1).
///
/// <para>
/// Two rules meet here, and both had to be learned. <strong>Routing goes through
/// <c>AiRoutePolicy</c></strong>, because selecting classifiers by vendor and local configuration
/// would build a second provider-routing mechanism alongside the platform rule that all model calls
/// resolve through it — and central routing is where a second provider is configured, which is how
/// redundancy is actually delivered rather than asserted. <strong>And the result is intersected with
/// the consented provider list</strong>, because routing through a central policy is exactly what
/// makes an invisible failover possible.
/// </para>
/// </summary>
public interface ISafetyModelRouter
{
    /// <summary>
    /// The model to classify with, or a refusal.
    /// </summary>
    /// <exception cref="ProviderNotConsentedException">
    /// The routed model belongs to a provider the subject's active terms do not name. <strong>Not a
    /// fallback</strong>: adding a provider is a terms change, so the generation fails closed.
    /// </exception>
    Task<SafetyRoute> ResolveAsync(
        Guid subjectPartyId,
        string useCase,
        CancellationToken cancellationToken = default);
}

/// <param name="Provider">The company the content will reach. Checked against the consented set.</param>
public sealed record SafetyRoute(string ModelName, string Provider);

internal sealed class SafetyModelRouter : ISafetyModelRouter
{
    private readonly AiDbContext _dbContext;
    private readonly IAiModelResolver _modelResolver;
    private readonly IConsentedProviderReader _consentedProviders;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<SafetyModelRouter> _logger;

    public SafetyModelRouter(
        AiDbContext dbContext,
        IAiModelResolver modelResolver,
        IConsentedProviderReader consentedProviders,
        ITenantProvider tenantProvider,
        ILogger<SafetyModelRouter> logger)
    {
        _dbContext = dbContext;
        _modelResolver = modelResolver;
        _consentedProviders = consentedProviders;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<SafetyRoute> ResolveAsync(
        Guid subjectPartyId, string useCase, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Central routing, per the platform rule. No hard-coded model, no vendor picked here.
        var modelName = await _modelResolver.ResolveModelNameAsync(useCase, cancellationToken);

        if (string.IsNullOrWhiteSpace(modelName))
        {
            // No route configured means we cannot classify, and unclassified content is not
            // delivered. The gate turns this into CheckUnavailable rather than a pass.
            throw new InvalidOperationException(
                $"No route policy resolves use case '{useCase}'; safety classification cannot proceed.");
        }

        var provider = await _dbContext.AiModels
            .AsNoTracking()
            .Where(m => m.ModelName == modelName && m.IsActive)
            .Join(_dbContext.AiProviders.AsNoTracking(),
                model => model.AiProviderId,
                p => p.Id,
                (_, p) => p.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException(
                $"Model '{modelName}' has no active provider; safety classification cannot proceed.");
        }

        var consented = await _consentedProviders.GetConsentedProvidersAsync(
            tenantId, subjectPartyId, cancellationToken);

        if (!consented.Contains(provider))
        {
            // Refuses rather than switching. A vendor added during an incident cannot be used until
            // terms are published and families re-consent — awkward, and correct: a family who
            // agreed to a named set is being told the truth, and one who agreed to a single provider
            // and got a different one is not.
            _logger.LogError(
                "Routed provider {Provider} for use case {UseCase} is not named by subject {SubjectId}'s "
                + "active terms. Refusing classification rather than failing over.",
                provider, useCase, subjectPartyId);

            throw new ProviderNotConsentedException(subjectPartyId, provider);
        }

        return new SafetyRoute(modelName, provider);
    }
}

/// <summary>
/// Routable use cases for safety classification. Named per modality so an operator can route text
/// and image classification to different providers — and, on a bad day, route one away from an
/// outage without touching the other.
/// </summary>
public static class SafetyUseCases
{
    public const string ClassifyText = "safety-classify-text";
    public const string ClassifyImage = "safety-classify-image";
    public const string ClassifySpeech = "safety-classify-speech";
    public const string ClassifyVideo = "safety-classify-video";

    /// <summary>
    /// Speech-to-text for the transcript leg. Routed like every other model call — transcription sends
    /// a child's audio to a third party, so it cannot be the one path that picks its own vendor and
    /// skips the §16.1 consented-provider check.
    /// </summary>
    public const string TranscribeSpeech = "safety-transcribe-speech";

    public static string ForModality(string modality) => modality switch
    {
        SafetyModalities.Text => ClassifyText,
        SafetyModalities.Image => ClassifyImage,
        SafetyModalities.Speech => ClassifySpeech,
        SafetyModalities.Video => ClassifyVideo,
        _ => throw new ArgumentOutOfRangeException(nameof(modality), modality, "Unknown modality."),
    };
}
