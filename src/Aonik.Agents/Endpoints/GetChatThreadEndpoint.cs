using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Gets a single chat thread with all its messages, ordered by sort order.
/// </summary>
internal sealed class GetChatThreadEndpoint
    : Endpoint<GetChatThreadRequest, ChatThreadDetail>
{
    private readonly IChatThreadService _threadService;

    public GetChatThreadEndpoint(IChatThreadService threadService)
    {
        _threadService = threadService;
    }

    public override void Configure()
    {
        Get("/ai/threads/{ThreadId}");
        AllowAnonymous(); // Auth handled by tenant/user providers
    }

    public override async Task HandleAsync(GetChatThreadRequest req, CancellationToken ct)
    {
        var thread = await _threadService.GetThreadAsync(req.ThreadId, ct);

        if (thread is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(thread, ct);
    }
}

public sealed record GetChatThreadRequest
{
    public Guid ThreadId { get; init; }
}
