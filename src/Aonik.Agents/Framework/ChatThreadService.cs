using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Manages persisted chat threads and messages. Thread persistence failures
/// are always logged but never block the primary chat flow.
/// </summary>
internal sealed class ChatThreadService : IChatThreadService
{
    private readonly AgentsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ChatThreadService> _logger;

    public ChatThreadService(
        AgentsDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        ILogger<ChatThreadService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<Guid> CreateThreadAsync(
        string firstMessage,
        string? agentName = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        _currentUserProvider.TryGetCurrentUserId(out var userId);

        // Use the first message as a placeholder title (truncated).
        // The title will be updated later by the title generator.
        var placeholderTitle = firstMessage.Length > 100
            ? firstMessage[..97] + "..."
            : firstMessage;

        var now = DateTime.UtcNow;

        var thread = new ChatThread
        {
            TenantId = tenantId,
            UserId = userId == Guid.Empty ? null : userId,
            Title = placeholderTitle,
            Status = ChatThreadStatus.Active,
            AgentName = agentName,
            LastMessageAt = now,
            MessageCount = 1,
        };

        var message = new ChatThreadMessage
        {
            TenantId = tenantId,
            ChatThreadId = thread.Id,
            Role = "user",
            Content = firstMessage,
            SortOrder = 1,
        };

        thread.Messages.Add(message);

        _dbContext.ChatThreads.Add(thread);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created chat thread {ThreadId} for tenant {TenantId}, user {UserId}",
            thread.Id, tenantId, userId);

        return thread.Id;
    }

    public async Task AppendMessageAsync(
        Guid threadId,
        string role,
        string content,
        string? agentName = null,
        Guid? aiRunId = null,
        string? toolCallsJson = null,
        CancellationToken cancellationToken = default)
    {
        var thread = await _dbContext.ChatThreads
            .FirstOrDefaultAsync(t => t.Id == threadId, cancellationToken);

        if (thread is null)
        {
            _logger.LogWarning("Cannot append message — thread {ThreadId} not found", threadId);
            return;
        }

        var nextSortOrder = thread.MessageCount + 1;

        var message = new ChatThreadMessage
        {
            TenantId = thread.TenantId,
            ChatThreadId = threadId,
            Role = role,
            Content = content,
            AgentName = agentName,
            AiRunId = aiRunId,
            ToolCallsJson = toolCallsJson,
            SortOrder = nextSortOrder,
        };

        _dbContext.ChatThreadMessages.Add(message);

        thread.MessageCount = nextSortOrder;
        thread.LastMessageAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateTitleAsync(
        Guid threadId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var thread = await _dbContext.ChatThreads
            .FirstOrDefaultAsync(t => t.Id == threadId, cancellationToken);

        if (thread is null)
        {
            _logger.LogWarning("Cannot update title — thread {ThreadId} not found", threadId);
            return;
        }

        thread.Title = title.Length > 200 ? title[..197] + "..." : title;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Updated thread {ThreadId} title to: {Title}", threadId, thread.Title);
    }

    public async Task<ChatThreadDetail?> GetThreadAsync(
        Guid threadId,
        CancellationToken cancellationToken = default)
    {
        var thread = await _dbContext.ChatThreads
            .AsNoTracking()
            .Include(t => t.Messages.OrderBy(m => m.SortOrder))
            .FirstOrDefaultAsync(t => t.Id == threadId, cancellationToken);

        if (thread is null)
            return null;

        return MapToDetail(thread);
    }

    public async Task<List<ChatThreadSummary>> ListThreadsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _currentUserProvider.TryGetCurrentUserId(out var userId);

        var query = _dbContext.ChatThreads
            .AsNoTracking()
            .Where(t => t.Status == ChatThreadStatus.Active);

        // Filter by user if available
        if (userId != Guid.Empty)
            query = query.Where(t => t.UserId == userId);

        var threads = await query
            .OrderByDescending(t => t.LastMessageAt ?? t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return threads.Select(MapToSummary).ToList();
    }

    public async Task<bool> ArchiveThreadAsync(
        Guid threadId,
        CancellationToken cancellationToken = default)
    {
        var thread = await _dbContext.ChatThreads
            .FirstOrDefaultAsync(t => t.Id == threadId, cancellationToken);

        if (thread is null)
            return false;

        thread.Status = ChatThreadStatus.Archived;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Archived chat thread {ThreadId}", threadId);
        return true;
    }

    private static ChatThreadSummary MapToSummary(ChatThread thread) => new()
    {
        Id = thread.Id,
        Title = thread.Title,
        Status = thread.Status.ToString(),
        AgentName = thread.AgentName,
        LastMessageAt = thread.LastMessageAt,
        MessageCount = thread.MessageCount,
        CreatedAt = thread.CreatedAt,
    };

    private static ChatThreadDetail MapToDetail(ChatThread thread) => new()
    {
        Id = thread.Id,
        Title = thread.Title,
        Status = thread.Status.ToString(),
        AgentName = thread.AgentName,
        LastMessageAt = thread.LastMessageAt,
        MessageCount = thread.MessageCount,
        CreatedAt = thread.CreatedAt,
        Messages = thread.Messages.Select(m => new ChatThreadMessageDto
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            AgentName = m.AgentName,
            ToolCallsJson = m.ToolCallsJson,
            SortOrder = m.SortOrder,
            CreatedAt = m.CreatedAt,
        }).ToList(),
    };
}
