using System.Text.Json;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Services;

/// <summary>
/// Generates a ConversationSummary from a completed ChatThread by summarising
/// its messages via an LLM call and extracting structured data (decisions,
/// open loops, recommendation outcomes).
/// <para>
/// After the summary is persisted, a second LLM call extracts learnable user
/// facts (preferences, identity, corrections) and writes them to long-term
/// memory via <see cref="IUserMemorySaveProvider"/>.
/// </para>
/// </summary>
internal sealed class ConversationSummaryGenerator : Contracts.Services.IConversationSummaryService
{
    private readonly AgentsDbContext _dbContext;
    private readonly IChatClient _chatClient;
    private readonly IAiTaskProfileResolver _profileResolver;
    private readonly IUserMemorySaveProvider? _memorySaveProvider;
    private readonly ILogger<ConversationSummaryGenerator> _logger;

    private const string SummaryUseCase = "conversation-summary";
    private const string SummaryPromptName = "conversation_summary";
    private const string MemoryExtractionUseCase = "conversation-memory-extraction";
    private const string MemoryExtractionPromptName = "conversation_memory_extraction";

    public ConversationSummaryGenerator(
        AgentsDbContext dbContext,
        IChatClient chatClient,
        IAiTaskProfileResolver profileResolver,
        ILogger<ConversationSummaryGenerator> logger,
        IUserMemorySaveProvider? memorySaveProvider = null)
    {
        _dbContext = dbContext;
        _chatClient = chatClient;
        _profileResolver = profileResolver;
        _logger = logger;
        _memorySaveProvider = memorySaveProvider;
    }

    private static readonly TimeSpan InactivityThreshold = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public Task GenerateSummaryAsync(Guid chatThreadId, CancellationToken cancellationToken = default)
        => GenerateAsync(chatThreadId, cancellationToken);

    /// <inheritdoc />
    public async Task ProcessStaleSessionsAsync(int batchSize = 10, IReadOnlyList<string>? agentNames = null, CancellationToken cancellationToken = default)
    {
        if (agentNames is null || agentNames.Count == 0)
        {
            _logger.LogDebug("No agent names configured for conversation summarisation, skipping.");
            return;
        }

        var cutoff = DateTime.UtcNow - InactivityThreshold;

        var staleThreadIds = await _dbContext.ChatThreads
            .Where(t => t.Status == ChatThreadStatus.Active
                && t.LastMessageAt != null
                && t.LastMessageAt < cutoff
                && t.MessageCount > 0
                && t.AgentName != null
                && agentNames.Contains(t.AgentName))
            .Where(t => !_dbContext.ConversationSummaries.Any(s => s.ChatThreadId == t.Id))
            .Select(t => t.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (staleThreadIds.Count == 0) return;

        _logger.LogInformation("Found {Count} stale sessions to summarise.", staleThreadIds.Count);

        foreach (var threadId in staleThreadIds)
        {
            try
            {
                await GenerateAsync(threadId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate summary for stale session {ThreadId}.", threadId);
            }
        }
    }

    /// <summary>
    /// Generates a summary for the given chat thread, then extracts and persists
    /// learnable user memories. Idempotent — skips if a summary already exists.
    /// </summary>
    private async Task GenerateAsync(Guid chatThreadId, CancellationToken cancellationToken = default)
    {
        // Idempotency check
        var existing = await _dbContext.ConversationSummaries
            .AnyAsync(s => s.ChatThreadId == chatThreadId, cancellationToken);

        if (existing)
        {
            _logger.LogDebug("ConversationSummary already exists for ChatThread {ChatThreadId}, skipping.", chatThreadId);
            return;
        }

        var thread = await _dbContext.ChatThreads
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == chatThreadId, cancellationToken);

        if (thread is null)
        {
            _logger.LogWarning("ChatThread {ChatThreadId} not found for summary generation.", chatThreadId);
            return;
        }

        if (thread.Messages.Count == 0)
        {
            _logger.LogDebug("ChatThread {ChatThreadId} has no messages, skipping summary.", chatThreadId);
            return;
        }

        // Build conversation transcript for the LLM
        var transcript = BuildTranscript(thread.Messages);

        // ── Step 1: Generate conversation summary ──
        await GenerateSummaryAsync(thread, transcript, chatThreadId, cancellationToken);

        // ── Step 2: Extract and persist user memories ──
        if (_memorySaveProvider is not null && thread.UserId is not null && thread.UserId != Guid.Empty)
        {
            await ExtractAndSaveMemoriesAsync(thread.UserId.Value, transcript, cancellationToken);
        }
    }

    private async Task GenerateSummaryAsync(
        ChatThread thread,
        string transcript,
        Guid chatThreadId,
        CancellationToken cancellationToken)
    {
        var profile = await _profileResolver.ResolveAsync(
            SummaryUseCase, SummaryPromptName, cancellationToken: cancellationToken);

        try
        {
            var messages = new List<ChatMessage>();
            if (!string.IsNullOrEmpty(profile.SystemPrompt))
                messages.Add(new ChatMessage(ChatRole.System, profile.SystemPrompt));
            messages.Add(new ChatMessage(ChatRole.User, transcript));

            var options = profile.ModelId is not null ? new ChatOptions { ModelId = profile.ModelId } : null;

            var response = await _chatClient.GetResponseAsync(
                messages,
                options: options,
                cancellationToken: cancellationToken);

            var responseText = response.Text ?? "{}";
            var parsed = ParseSummaryResponse(responseText);

            var sessionStart = thread.Messages.Min(m => m.CreatedAt);
            var sessionEnd = thread.Messages.Max(m => m.CreatedAt);

            var summary = new ConversationSummary
            {
                TenantId = thread.TenantId,
                UserId = thread.UserId ?? Guid.Empty,
                ChatThreadId = chatThreadId,
                SessionStartedAt = sessionStart,
                SessionEndedAt = sessionEnd,
                SummaryText = parsed.Summary,
                KeyDecisionsJson = parsed.KeyDecisionsJson,
                OpenLoopsJson = parsed.OpenLoopsJson,
                RecommendationOutcomesJson = parsed.RecommendationOutcomesJson,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ConversationSummaries.Add(summary);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Generated ConversationSummary {SummaryId} for ChatThread {ChatThreadId}.",
                summary.Id, chatThreadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate ConversationSummary for ChatThread {ChatThreadId}.", chatThreadId);
        }
    }

    /// <summary>
    /// Makes a second LLM call to extract learnable user facts from the transcript,
    /// then writes each extracted memory via <see cref="IUserMemorySaveProvider"/>.
    /// Failures are logged but do not prevent the summary from being persisted.
    /// </summary>
    private async Task ExtractAndSaveMemoriesAsync(
        Guid userId,
        string transcript,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _profileResolver.ResolveAsync(
                MemoryExtractionUseCase, MemoryExtractionPromptName, cancellationToken: cancellationToken);

            var messages = new List<ChatMessage>();
            if (!string.IsNullOrEmpty(profile.SystemPrompt))
                messages.Add(new ChatMessage(ChatRole.System, profile.SystemPrompt));
            messages.Add(new ChatMessage(ChatRole.User, transcript));

            var options = profile.ModelId is not null ? new ChatOptions { ModelId = profile.ModelId } : null;

            var response = await _chatClient.GetResponseAsync(
                messages,
                options: options,
                cancellationToken: cancellationToken);

            var responseText = response.Text ?? "{}";
            var memories = ParseMemoryExtractionResponse(responseText);

            if (memories.Count == 0)
            {
                _logger.LogDebug("No learnable memories extracted from conversation for User {UserId}.", userId);
                return;
            }

            _logger.LogInformation(
                "Extracted {Count} memory entries from conversation for User {UserId}.",
                memories.Count, userId);

            foreach (var memory in memories)
            {
                try
                {
                    var source = memory.Confidence >= 0.9m ? "UserStated" : "AiInferred";

                    await _memorySaveProvider!.SaveAsync(
                        new UserMemorySaveRequest(
                            UserId: userId,
                            EntryType: memory.EntryType,
                            Key: memory.Key,
                            ValueJson: memory.ValueJson,
                            Confidence: memory.Confidence,
                            Source: source),
                        cancellationToken);

                    _logger.LogDebug(
                        "Saved extracted memory: Key={Key}, EntryType={EntryType}, Confidence={Confidence} for User {UserId}.",
                        memory.Key, memory.EntryType, memory.Confidence, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to save extracted memory Key={Key} for User {UserId}. Continuing with remaining entries.",
                        memory.Key, userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Memory extraction failed for User {UserId}. Summary was still persisted successfully.",
                userId);
        }
    }

    private static string BuildTranscript(IEnumerable<ChatThreadMessage> messages)
    {
        var orderedMessages = messages.OrderBy(m => m.SortOrder);
        return string.Join("\n", orderedMessages.Select(m => $"[{m.Role}]: {m.Content}"));
    }

    private static ParsedSummary ParseSummaryResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";
            var decisions = root.TryGetProperty("keyDecisions", out var d) ? d.GetRawText() : null;
            var loops = root.TryGetProperty("openLoops", out var l) ? l.GetRawText() : null;
            var outcomes = root.TryGetProperty("recommendationOutcomes", out var r) ? r.GetRawText() : null;

            return new ParsedSummary(summary, decisions, loops, outcomes);
        }
        catch
        {
            // If parsing fails, use the raw text as summary
            return new ParsedSummary(
                json.Length > 2000 ? json[..2000] : json,
                null, null, null);
        }
    }

    private static IReadOnlyList<ExtractedMemory> ParseMemoryExtractionResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("memories", out var memoriesElement) ||
                memoriesElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ExtractedMemory>();
            }

            var memories = new List<ExtractedMemory>();
            foreach (var item in memoriesElement.EnumerateArray())
            {
                var key = item.TryGetProperty("key", out var k) ? k.GetString() : null;
                var entryType = item.TryGetProperty("entryType", out var et) ? et.GetString() : null;
                var valueJson = item.TryGetProperty("valueJson", out var v) ? v.GetRawText() : null;
                var confidence = item.TryGetProperty("confidence", out var c) && c.TryGetDecimal(out var conf)
                    ? conf : 0.7m;

                // valueJson might be a raw string (e.g. "London") — need to handle both
                // raw JSON text from GetRawText() and string values
                if (valueJson is null && item.TryGetProperty("valueJson", out var vs) && vs.ValueKind == JsonValueKind.String)
                    valueJson = $"\"{vs.GetString()}\"";

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(valueJson))
                    continue;

                memories.Add(new ExtractedMemory(
                    Key: key,
                    EntryType: entryType ?? "Fact",
                    ValueJson: valueJson,
                    Confidence: Math.Clamp(confidence, 0.1m, 1.0m)));
            }

            return memories;
        }
        catch
        {
            return Array.Empty<ExtractedMemory>();
        }
    }

    private record ParsedSummary(
        string Summary,
        string? KeyDecisionsJson,
        string? OpenLoopsJson,
        string? RecommendationOutcomesJson);

    private record ExtractedMemory(
        string Key,
        string EntryType,
        string ValueJson,
        decimal Confidence);
}
