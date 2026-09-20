using System.Security.Cryptography;
using System.Text.Json;
using Aonik.Infrastructure.Ai.Safety;
using Aonik.Infrastructure.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using FluentAssertions;
using Moq;

namespace Aonik.Infrastructure.Tests.Ai;

/// <summary>Explicit opt-in evaluation of the actual transport; never enables a tenant or safety policy.</summary>
public class OpenAISpeechLiveEvaluationTests
{
    [SpeechEvaluationFact]
    public async Task Should_EvaluateExplicitLocalCorpus_WithoutChangingRuntimePolicy()
    {
        string manifestPath = Required("AONIK_SPEECH_EVAL_MANIFEST");
        string reportPath = Required("AONIK_SPEECH_EVAL_REPORT");
        string model = Required("AONIK_SPEECH_EVAL_MODEL");
        var corpus = JsonSerializer.Deserialize<Corpus>(await File.ReadAllTextAsync(manifestPath), new JsonSerializerOptions {PropertyNameCaseInsensitive = true})!;
        corpus.Cases.Should().NotBeEmpty();
        var settings = new Mock<ISettingProvider>();
        settings.Setup(s => s.GetAsync(AiSettingNames.OpenAiApiKey, It.IsAny<CancellationToken>())).ReturnsAsync(Required("AONIK_SPEECH_EVAL_API_KEY"));
        using var http = new HttpClient {Timeout = TimeSpan.FromSeconds(90)};
        var provider = new OpenAISpeechProvider(http, new TenantFirstSettingReader(new Mock<ITenantSettingStore>().Object,
            settings.Object, new Mock<ITenantProvider>().Object));
        var results = new List<object>();
        int failures = 0;
        foreach (var item in corpus.Cases)
        {
            byte[] bytes = await File.ReadAllBytesAsync(Path.GetFullPath(item.Path, Path.GetDirectoryName(Path.GetFullPath(manifestPath))!));
            string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            string reference = $"data:{item.ContentType};base64,{Convert.ToBase64String(bytes)}";
            string? transcript = null;
            IReadOnlyDictionary<string, double>? scores = null;
            string? failure = null;
            try
            {
                transcript = await provider.TranscribeAsync(reference, model);
                scores = await provider.ScoreAsync("speech", reference, item.Band, model);
            }
            catch (Exception error) {failure = error.GetType().Name;}
            bool passed = item.ExpectUnavailable
                ? failure is not null || string.IsNullOrWhiteSpace(transcript)
                : failure is null && !string.IsNullOrWhiteSpace(transcript) && scores is not null
                    && (item.ExpectedTranscript is null || Words(transcript) == Words(item.ExpectedTranscript))
                    && item.MinimumScores.All(pair => scores.TryGetValue(pair.Key, out double score) && score >= pair.Value)
                    && item.MaximumScores.All(pair => scores.TryGetValue(pair.Key, out double score) && score <= pair.Value);
            if (!passed) failures++;
            results.Add(new {item.Id, item.Band, contentHash = hash, passed, failure, transcriptPresent = !string.IsNullOrWhiteSpace(transcript), scores});
        }
        // No keys, raw audio or transcript content in the report. Labels retain their provenance.
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new {model, promptVersion = provider.PromptVersion,
            corpus.LabelProvenance, evaluatedAt = DateTimeOffset.UtcNow, cases = results, failures,
            enablesRuntimePolicy = false}, new JsonSerializerOptions {WriteIndented = true}));
        failures.Should().Be(0, "the explicit corpus expectations must pass; the report records each failure");
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is {Length: > 0} value
        ? value : throw new InvalidOperationException($"Set {name} for the explicit evaluation run.");
    private static string Words(string value) => string.Join(" ", System.Text.RegularExpressions.Regex.Matches(value.ToLowerInvariant(), @"[\p{L}\p{N}]+")
        .Select(match => match.Value));

    public sealed record Corpus(string LabelProvenance, List<Case> Cases);
    public sealed record Case(string Id, string Path, string ContentType, string Band, string? ExpectedTranscript,
        bool ExpectUnavailable, Dictionary<string, double> MinimumScores, Dictionary<string, double> MaximumScores);

    public sealed class SpeechEvaluationFactAttribute : FactAttribute
    {
        public SpeechEvaluationFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("AONIK_SPEECH_EVAL_ENABLED") != "true")
                Skip = "Explicit live corpus evaluation only; requires configured model, local corpus and provider credentials.";
        }
    }
}
