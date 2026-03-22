using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Lists active chat threads for the current user, ordered by most recent activity.
/// </summary>
internal sealed class ListChatThreadsEndpoint
    : Endpoint<ListChatThreadsRequest, ListChatThreadsResponse>
{
    private readonly IChatThreadService _threadService;

    public ListChatThreadsEndpoint(IChatThreadService threadService)
    {
        _threadService = threadService;
    }

    public override void Configure()
    {
        Get("/ai/threads");
        AllowAnonymous(); // Auth handled by tenant/user providers
    }

    public override async Task HandleAsync(ListChatThreadsRequest req, CancellationToken ct)
    {
        var page = req.Page > 0 ? req.Page : 1;
        var pageSize = req.PageSize is > 0 and <= 100 ? req.PageSize : 20;

        var threads = await _threadService.ListThreadsAsync(page, pageSize, ct);

        await Send.OkAsync(new ListChatThreadsResponse
        {
            Threads = threads,
            Page = page,
            PageSize = pageSize,
        }, ct);
    }
}

public sealed record ListChatThreadsRequest
{
    [QueryParam]
    public int Page { get; init; } = 1;

    [QueryParam]
    public int PageSize { get; init; } = 20;
}

public sealed record ListChatThreadsResponse
{
    public required List<ChatThreadSummary> Threads { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
