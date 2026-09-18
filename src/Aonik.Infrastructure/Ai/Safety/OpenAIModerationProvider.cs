using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Ai.Services.Safety;
using Aonik.SharedKernel.Abstractions.Safety;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Ai.Safety;

/// <summary>
/// The one supported classification route (aonik#323): OpenAI's moderation endpoint, which judges
/// text and images against a fixed taxonomy and returns a score per category. Its categories are
/// mapped onto Spec 096's; a category the taxonomy does not carry — <c>frightening</c> above all —
/// is not scored here, and the gate's other layers (the structural constraints of L1, the guardian
/// review of L5) are what stand for it. That gap is recorded rather than papered over.
///
/// <para>
/// Fails closed by construction: any error here propagates to the gate, which records the check as
/// unavailable and refuses delivery. Nothing is substituted, nothing is guessed.
/// </para>
/// </summary>
internal sealed class OpenAIModerationProvider : ISafetyClassificationProvider
{
    public const string ProviderName = "openai";

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
    private readonly IOptions<OpenAIModerationOptions> _options;
    private readonly ILogger<OpenAIModerationProvider> _logger;

    public OpenAIModerationProvider(HttpClient http, IOptions<OpenAIModerationOptions> options, ILogger<OpenAIModerationProvider> logger)
    {
        _http = http;
        _options = options;
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
        var options = _options.Value;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("The OpenAI moderation route has no API key configured (ContentSafety:OpenAI:ApiKey).");
        }

        object input = string.Equals(modality, SafetyModalities.Image, StringComparison.OrdinalIgnoreCase)
            ? new[] { new { type = "image_url", image_url = new { url = reference } } }
            : reference;

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.BaseUrl.TrimEnd('/') + "/"), "v1/moderations"))
        {
            Content = JsonContent.Create(new { model = modelName, input }),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);

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

/// <summary>Configuration section <c>ContentSafety:OpenAI</c>. No key, no route: the gate refuses.</summary>
public sealed class OpenAIModerationOptions
{
    public const string SectionName = "ContentSafety:OpenAI";

    public string BaseUrl { get; set; } = "https://api.openai.com";

    public string? ApiKey { get; set; }
}
