using System.ComponentModel;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Agents.Tools;

/// <summary>
/// Agent tool for semantically searching user memory mid-conversation.
/// Read-only — no approval gate required.
/// <para>
/// When the active memory backend is SQL Server (which does not yet support
/// semantic search), the tool returns an empty result set. The agent will
/// understand this means recall is unavailable and fall back to the User Brief.
/// </para>
/// </summary>
internal sealed class UserMemoryRecallTools
{
    private readonly IUserMemoryRecallProvider _recallProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    private UserMemoryRecallTools(
        IUserMemoryRecallProvider recallProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _recallProvider = recallProvider;
        _currentUserProvider = currentUserProvider;
    }

    [Description(
        "Semantically search the user's memory for entries relevant to a natural language query. " +
        "Use when the User Brief doesn't contain enough context for the current topic, or to " +
        "recall something specific the user has mentioned in a previous conversation. " +
        "Returns memory entries ranked by semantic relevance with confidence scores. " +
        "Returns empty if semantic recall is not available.")]
    public async Task<IReadOnlyList<UserMemoryRecallResult>> RecallUserMemory(
        [Description("Natural language query describing what to recall " +
            "(e.g. 'preferred payment day', 'risk tolerance', 'household size')")]
        string query,
        [Description("Maximum number of results to return (1-10, default 5)")]
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
            return Array.Empty<UserMemoryRecallResult>();

        return await _recallProvider.RecallAsync(
            userId, query, Math.Clamp(limit, 1, 10), 0.5f, cancellationToken);
    }

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var recallProvider = serviceProvider.GetService<IUserMemoryRecallProvider>();
        var currentUserProvider = serviceProvider.GetService<ICurrentUserProvider>();

        // If either dependency is missing, skip — tool won't be available
        if (recallProvider is null || currentUserProvider is null)
            yield break;

        var tools = new UserMemoryRecallTools(recallProvider, currentUserProvider);

        yield return AIFunctionFactory.Create(
            tools.RecallUserMemory,
            name: "user_memory_recall");
    }
}
