using Aonik.Agents.Contracts.Services;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Archives (soft-deletes) a chat thread. The thread is not physically deleted;
/// its status is set to Archived so it no longer appears in list queries.
/// </summary>
internal sealed class ArchiveChatThreadEndpoint
    : Endpoint<ArchiveChatThreadRequest>
{
    private readonly IChatThreadService _threadService;

    public ArchiveChatThreadEndpoint(IChatThreadService threadService)
    {
        _threadService = threadService;
    }

    public override void Configure()
    {
        Delete("/ai/threads/{ThreadId}");
        AllowAnonymous(); // Auth handled by tenant/user providers
    }

    public override async Task HandleAsync(ArchiveChatThreadRequest req, CancellationToken ct)
    {
        var archived = await _threadService.ArchiveThreadAsync(req.ThreadId, ct);

        if (!archived)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

public sealed record ArchiveChatThreadRequest
{
    public Guid ThreadId { get; init; }
}
