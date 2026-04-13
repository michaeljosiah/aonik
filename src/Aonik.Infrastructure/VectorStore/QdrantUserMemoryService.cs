using System.Text.Json;
using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Infrastructure.VectorStore.Contracts;
using Aonik.Infrastructure.VectorStore.Qdrant;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.VectorStore;

/// <summary>
/// Qdrant-backed implementation of <see cref="IUserMemoryService"/>.
/// When this backend is active, Qdrant is the sole memory store — all CRUD,
/// audit chains (supersession), confidence decay, and semantic search happen
/// entirely within Qdrant point payloads.
/// </summary>
internal sealed class QdrantUserMemoryService : IUserMemoryService
{
    private const decimal ConfidenceFloor = 0.3m;
    private const decimal DecayRatePerMonth = 0.1m;

    /// <summary>
    /// Sentinel value for "not superseded" — Qdrant has no native null filter,
    /// so we use empty string to represent current (active) entries.
    /// </summary>
    private const string NotSuperseded = "";

    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly string _collectionName;
    private readonly ILogger<QdrantUserMemoryService> _logger;

    public QdrantUserMemoryService(
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        ITenantProvider tenantProvider,
        IClock clock,
        IOptions<QdrantConfiguration> qdrantOptions,
        ILogger<QdrantUserMemoryService> logger)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _collectionName = qdrantOptions.Value.GetCollectionName("user-memory");
        _logger = logger;
    }

    public async Task<UserMemoryEntryResponse> SetEntryAsync(
        SetUserMemoryEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;
        var newEntryId = Guid.NewGuid();

        // Find the current active entry for this key (if any) to supersede
        var existingFilter = BuildUserKeyFilter(request.UserId, request.Key, currentOnly: true);
        var existing = (await _vectorStore.ScrollAsync(
            _collectionName, existingFilter, limit: 1, cancellationToken)).FirstOrDefault();

        // Supersede the old entry by updating its payload
        if (existing is not null)
        {
            await _vectorStore.SetPayloadAsync(
                _collectionName,
                [existing.Id],
                new Dictionary<string, object> { ["superseded_by"] = newEntryId.ToString() },
                cancellationToken);
        }

        // Generate embedding for semantic search
        var embeddingText = BuildEmbeddingText(request.EntryType, request.Key, request.ValueJson);
        var embedding = await _embeddingService.GetEmbeddingAsync(embeddingText, cancellationToken);

        // Build full payload
        var payload = new Dictionary<string, object>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["user_id"] = request.UserId.ToString(),
            ["entry_type"] = request.EntryType.ToString(),
            ["key"] = request.Key,
            ["value_json"] = request.ValueJson,
            ["confidence"] = (double)request.Confidence,
            ["source"] = request.Source.ToString(),
            ["ai_run_id"] = request.AiRunId?.ToString() ?? "",
            ["superseded_by"] = NotSuperseded,
            ["created_at"] = now.ToString("O"),
            ["last_confirmed_at"] = now.ToString("O")
        };

        await _vectorStore.UpsertVectorAsync(
            _collectionName,
            newEntryId.ToString(),
            embedding,
            payload,
            cancellationToken);

        _logger.LogDebug(
            "Stored user memory entry {EntryId} for user {UserId}, key '{Key}'",
            newEntryId, request.UserId, request.Key);

        return new UserMemoryEntryResponse(
            newEntryId,
            request.UserId,
            request.EntryType,
            request.Key,
            request.ValueJson,
            request.Confidence,
            ComputeEffectiveConfidence(request.Source, request.Confidence, now, now),
            request.Source,
            request.AiRunId,
            null,
            now,
            now);
    }

    public async Task<IReadOnlyList<UserMemoryEntryResponse>> GetCurrentEntriesAsync(
        Guid userId,
        UserMemoryEntryType? entryType = null,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var filter = BuildUserFilter(userId, currentOnly: true, entryType: entryType);

        var points = await _vectorStore.ScrollAsync(
            _collectionName, filter, limit: 500, cancellationToken);

        return points
            .Select(p => PayloadToResponse(p.Id, p.Payload, now))
            .Where(r => r is not null && r.EffectiveConfidence >= ConfidenceFloor)
            .ToList()!;
    }

    public async Task<IReadOnlyList<UserMemoryEntryResponse>> GetEntryHistoryAsync(
        Guid userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var filter = BuildUserKeyFilter(userId, key, currentOnly: false);

        var points = await _vectorStore.ScrollAsync(
            _collectionName, filter, limit: 100, cancellationToken);

        return points
            .Select(p => PayloadToResponse(p.Id, p.Payload, now))
            .Where(r => r is not null)
            .OrderByDescending(r => r!.CreatedAt)
            .ToList()!;
    }

    public async Task ConfirmEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        await _vectorStore.SetPayloadAsync(
            _collectionName,
            [entryId.ToString()],
            new Dictionary<string, object> { ["last_confirmed_at"] = now.ToString("O") },
            cancellationToken);
    }

    public async Task<IReadOnlyList<SemanticMemorySearchResult>> SemanticSearchAsync(
        Guid userId,
        string query,
        int limit = 5,
        float scoreThreshold = 0.6f,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        // Embed the query
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query, cancellationToken);

        // Build user + not-superseded filter
        var filter = BuildUserFilter(userId, currentOnly: true);

        var results = await _vectorStore.SearchAsync(
            _collectionName,
            queryEmbedding,
            limit,
            scoreThreshold,
            filter,
            cancellationToken);

        return results
            .Select(r =>
            {
                var entry = PayloadToResponse(r.Id, r.Payload, now);
                return entry is not null && entry.EffectiveConfidence >= ConfidenceFloor
                    ? new SemanticMemorySearchResult(entry, r.Score)
                    : null;
            })
            .Where(r => r is not null)
            .ToList()!;
    }

    // ── Filter Builders ───────────────────────────────────────────────

    private static Dictionary<string, object> BuildUserFilter(
        Guid userId,
        bool currentOnly,
        UserMemoryEntryType? entryType = null)
    {
        var mustClauses = new List<object>
        {
            MatchClause("user_id", userId.ToString())
        };

        if (currentOnly)
        {
            mustClauses.Add(MatchClause("superseded_by", NotSuperseded));
        }

        if (entryType.HasValue)
        {
            mustClauses.Add(MatchClause("entry_type", entryType.Value.ToString()));
        }

        return new Dictionary<string, object>
        {
            { "must", mustClauses.ToArray() }
        };
    }

    private static Dictionary<string, object> BuildUserKeyFilter(
        Guid userId,
        string key,
        bool currentOnly)
    {
        var mustClauses = new List<object>
        {
            MatchClause("user_id", userId.ToString()),
            MatchClause("key", key)
        };

        if (currentOnly)
        {
            mustClauses.Add(MatchClause("superseded_by", NotSuperseded));
        }

        return new Dictionary<string, object>
        {
            { "must", mustClauses.ToArray() }
        };
    }

    private static Dictionary<string, object> MatchClause(string fieldKey, string value) =>
        new()
        {
            { "key", fieldKey },
            { "match", new Dictionary<string, object> { { "value", value } } }
        };

    // ── Payload Serialization ─────────────────────────────────────────

    private static string BuildEmbeddingText(UserMemoryEntryType entryType, string key, string valueJson)
        => $"{entryType}: {key} = {valueJson}";

    private static UserMemoryEntryResponse? PayloadToResponse(
        string pointId,
        Dictionary<string, object>? payload,
        DateTime now)
    {
        if (payload is null) return null;

        try
        {
            var id = Guid.Parse(pointId);
            var userId = Guid.Parse(GetPayloadString(payload, "user_id"));
            var entryType = Enum.Parse<UserMemoryEntryType>(GetPayloadString(payload, "entry_type"));
            var key = GetPayloadString(payload, "key");
            var valueJson = GetPayloadString(payload, "value_json");
            var confidence = GetPayloadDecimal(payload, "confidence");
            var source = Enum.Parse<UserMemorySource>(GetPayloadString(payload, "source"));
            var aiRunIdStr = GetPayloadString(payload, "ai_run_id");
            var aiRunId = string.IsNullOrEmpty(aiRunIdStr) ? (Guid?)null : Guid.Parse(aiRunIdStr);
            var supersededByStr = GetPayloadString(payload, "superseded_by");
            var supersededById = string.IsNullOrEmpty(supersededByStr) ? (Guid?)null : Guid.Parse(supersededByStr);
            var createdAt = DateTime.Parse(GetPayloadString(payload, "created_at"));
            var lastConfirmedAt = DateTime.Parse(GetPayloadString(payload, "last_confirmed_at"));

            var effectiveConfidence = ComputeEffectiveConfidence(source, confidence, lastConfirmedAt, now);

            return new UserMemoryEntryResponse(
                id, userId, entryType, key, valueJson,
                confidence, effectiveConfidence, source, aiRunId,
                supersededById, createdAt, lastConfirmedAt);
        }
        catch (Exception)
        {
            // Malformed payload — skip entry rather than crash
            return null;
        }
    }

    private static string GetPayloadString(Dictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value)) return "";

        // System.Text.Json deserialization may produce JsonElement
        if (value is JsonElement element)
            return element.GetString() ?? element.ToString();

        return value?.ToString() ?? "";
    }

    private static decimal GetPayloadDecimal(Dictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value)) return 0m;

        if (value is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Number
                ? element.GetDecimal()
                : decimal.TryParse(element.GetString(), out var d) ? d : 0m;
        }

        return value is decimal dec ? dec
            : value is double dbl ? (decimal)dbl
            : decimal.TryParse(value?.ToString(), out var parsed) ? parsed : 0m;
    }

    // ── Confidence Decay ──────────────────────────────────────────────

    private static decimal ComputeEffectiveConfidence(
        UserMemorySource source,
        decimal confidence,
        DateTime lastConfirmedAt,
        DateTime now)
    {
        if (source == UserMemorySource.UserStated)
            return confidence;

        var daysSinceConfirmed = (decimal)(now - lastConfirmedAt).TotalDays;
        var decay = daysSinceConfirmed / 30m * DecayRatePerMonth;
        var effective = confidence - decay;

        return Math.Max(effective, 0m);
    }
}
