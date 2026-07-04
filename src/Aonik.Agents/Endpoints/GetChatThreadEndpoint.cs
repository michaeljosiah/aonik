using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        // H15: require authentication; ChatThreadService enforces owner-only access on top.
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get chat thread with messages";
            s.Description = "Returns a single chat thread with all its messages, ordered by sort order.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(404, "Thread not found");
        });
        Options(x => x.WithTags("AI Agents"));
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
