using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Ai.Services.Safety;
using Aonik.Infrastructure.Settings;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.SharedKernel.Abstractions.Settings;

using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Ai.Safety;

/// <summary>
/// The one supported classification route (aonik#323): OpenAI's moderation endpoint, which judges
/// text and images against a fixed taxonomy and returns a score per category. Its categories are
/// mapped onto Spec 096's; a category the taxonomy does not carry — <c>frightening</c> above all —
/// is not scored here, and the gate's other layers (the structural constraints of L1, the guardian
/// review of L5) are what stand for it. That gap is recorded rather than papered over.
///
/// <para>
/// The key is the tenant's own OpenAI key, read from the Settings module at the moment of the call —
/// <c>Ai.OpenAI.ApiKey</c>, tenant first, by the same rule the rest of the platform's AI resolves it
/// (<see cref="TenantFirstSettingReader"/>), exactly as an operator sets and rotates it from the Admin
/// UI. Nothing is read at startup and nothing is decided there: the route is always registered, and a
/// tenant with no key resolves to a check the gate records as unavailable.
/// </para>
///
/// <para>
/// Fails closed by construction: any error here propagates to the gate, which records the check as
/// unavailable and refuses delivery. Nothing is substituted, nothing is guessed.
/// </para>
/// </summary>
internal sealed class OpenAIModerationProvider : ISafetyClassificationProvider
{
    public const string ProviderName = "openai";

    /// <summary>OpenAI's moderation endpoint. There is no second host to point this at.</summary>
    private static readonly Uri ModerationsEndpoint = new("https://api.openai.com/v1/moderations");

    private static readonly IReadOnlyDictionary<string, string> CategoryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["sexual"] = SafetyCategories.Sexual,
        ["sexual/minors"] = SafetyCategories.Csam,
        ["violence"] = SafetyCategories.GraphicViolence,
        ["violence/graphic"] = SafetyCategories.GraphicViolence,
        ["illicit/violent"] = SafetyCategories.GraphicViolence,
        ["self-harm"] = SafetyCategories.SelfHarm,
        ["self-harm/intent"] = SafetyCategories.SelfHarm,
        ["self-harm/instructions"] = SafetyCategories.SelfHarm,
        ["hate"] = SafetyCategories.Hate,
        ["hate/threatening"] = SafetyCategories.Hate,
        ["harassment"] = SafetyCategories.Hate,
        ["harassment/threatening"] = SafetyCategories.Hate,
    };

    private readonly HttpClient _http;
    private readonly TenantFirstSettingReader _settings;
    private readonly ILogger<OpenAIModerationProvider> _logger;

    public OpenAIModerationProvider(HttpClient http, TenantFirstSettingReader settings, ILogger<OpenAIModerationProvider> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
    }

    public string Provider => ProviderName;

    public IReadOnlySet<string> SupportedModalities { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { SafetyModalities.Text, SafetyModalities.Image };

    /// <summary>The whole text or the whole image is judged in one call; nothing is sampled.</summary>
    public TemporalCoverage Coverage => TemporalCoverage.Complete;

    public async Task<IReadOnlyDictionary<string, double>> ScoreAsync(
        string modality, string reference, string safetyBand, string modelName, CancellationToken cancellationToken = default)
    {
        var apiKey = await _settings.ReadAsync(AiSettingNames.OpenAiApiKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"The OpenAI moderation route has no API key: set '{AiSettingNames.OpenAiApiKey}' in Settings for this tenant.");
        }

        object input = string.Equals(modality, SafetyModalities.Image, StringComparison.OrdinalIgnoreCase)
            ? new[] { new { type = "image_url", image_url = new { url = reference } } }
            : reference;

        using var request = new HttpRequestMessage(HttpMethod.Post, ModerationsEndpoint)
        {
            Content = JsonContent.Create(new { model = modelName, input }),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // The body is not logged: it may echo the input, which is a child's words.
            throw new InvalidOperationException($"The OpenAI moderation endpoint answered {(int)response.StatusCode}.");
        }

        var parsed = await response.Content.ReadFromJsonAsync<ModerationResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The OpenAI moderation endpoint returned no result.");

        var result = parsed.Results.FirstOrDefault()
            ?? throw new InvalidOperationException("The OpenAI moderation endpoint returned no result.");

        // A Spec 096 category takes the highest score of every provider category mapped onto it.
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (providerCategory, score) in result.CategoryScores)
        {
            if (!CategoryMap.TryGetValue(providerCategory, out var category))
            {
                continue;
            }

            scores[category] = Math.Max(scores.TryGetValue(category, out var existing) ? existing : 0d, score);
        }

        _logger.LogDebug("OpenAI moderation ({Model}) scored {Modality}: {Scores}", modelName, modality, string.Join(", ", scores.Select(s => $"{s.Key}={s.Value:0.00}")));

        return scores;
    }

    private sealed record ModerationResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<ModerationResult> Results);

    private sealed record ModerationResult(
        [property: JsonPropertyName("flagged")] bool Flagged,
        [property: JsonPropertyName("category_scores")] Dictionary<string, double> CategoryScores);
}

