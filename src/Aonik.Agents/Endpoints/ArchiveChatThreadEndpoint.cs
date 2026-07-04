using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        // H15: require authentication (write policy excludes ReadOnly); ChatThreadService
        // enforces owner-only access on top.
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Archive a chat thread";
            s.Description = "Soft-deletes a chat thread by setting its status to Archived. The thread is not physically removed.";
            s.Response(204, "Thread archived");
            s.Response(401, "Not authenticated");
            s.Response(404, "Thread not found");
        });
        Options(x => x.WithTags("AI Agents"));
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
