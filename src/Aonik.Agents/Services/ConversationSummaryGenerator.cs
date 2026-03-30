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
/// </summary>
internal sealed class ConversationSummaryGenerator : Contracts.Services.IConversationSummaryService
{
    private readonly AgentsDbContext _dbContext;
    private readonly IChatClient _chatClient;
    private readonly IAiTaskProfileResolver _profileResolver;
    private readonly ILogger<ConversationSummaryGenerator> _logger;

    private const string UseCase = "conversation-summary";
    private const string PromptName = "conversation_summary";

    public ConversationSummaryGenerator(
        AgentsDbContext dbContext,
        IChatClient chatClient,
        IAiTaskProfileResolver profileResolver,
        ILogger<ConversationSummaryGenerator> logger)
    {
        _dbContext = dbContext;
        _chatClient = chatClient;
        _profileResolver = profileResolver;
        _logger = logger;
    }

    private static readonly TimeSpan InactivityThreshold = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public Task GenerateSummaryAsync(Guid chatThreadId, CancellationToken cancellationToken = default)
        => GenerateAsync(chatThreadId, cancellationToken);

    /// <inheritdoc />
    public async Task ProcessStaleSessionsAsync(int batchSize = 10, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - InactivityThreshold;

        var staleThreadIds = await _dbContext.ChatThreads
            .Where(t => t.Status == ChatThreadStatus.Active
                && t.LastMessageAt != null
                && t.LastMessageAt < cutoff
                && t.MessageCount > 0)
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
    /// Generates a summary for the given chat thread. Idempotent — skips if
    /// a summary already exists for this thread.
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

        var profile = await _profileResolver.ResolveAsync(UseCase, PromptName, cancellationToken: cancellationToken);

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

    private record ParsedSummary(
        string Summary,
        string? KeyDecisionsJson,
        string? OpenLoopsJson,
        string? RecommendationOutcomesJson);
}
