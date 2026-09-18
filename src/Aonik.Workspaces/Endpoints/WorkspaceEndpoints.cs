using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.SharedKernel.Abstractions.Workspaces;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Workspaces.Endpoints;

/// <summary>Create a workspace the caller's party owns (Spec 089 §11).</summary>
internal sealed class CreateWorkspaceEndpoint : WorkspaceEndpoint<CreateWorkspaceRequest, WorkspaceResponse>
{
    private readonly IWorkspaceService _workspaces;

    public CreateWorkspaceEndpoint(IWorkspaceService workspaces) => _workspaces = workspaces;

    public override void Configure()
    {
        Post("/workspaces");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Create a workspace";
            s.Description = "Creates a workspace owned by the caller's party. The slot is claimed against the billing subscriber "
                + "(the caller's party unless a subscriber the caller may act for is named) before the workspace exists.";
            s.Response(201, "Workspace created");
            s.Response(402, "The billing subscriber has no workspace allowance left");
            s.Response(403, "The caller has no party, or may not act for the named billing subscriber");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(CreateWorkspaceRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Length > 200)
        {
            ThrowError("A workspace needs a name of at most 200 characters.", StatusCodes.Status422UnprocessableEntity);
        }

        if (req.Kind is not null && !WorkspaceKinds.All.Contains(req.Kind))
        {
            ThrowError($"Unknown workspace kind '{req.Kind}'.", StatusCodes.Status422UnprocessableEntity);
        }

        if (await CallerPartyAsync(ct) is not { } callerPartyId)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var billing = req.BillingSubscriber is { } subscriber
                ? new SubscriberRef(subscriber.Kind, subscriber.Id)
                : null;

            var created = await _workspaces.CreateAsync(
                req.Kind ?? WorkspaceKinds.World, req.Name.Trim(), callerPartyId, billing, ct);

            await Send.CreatedAtAsync<GetWorkspaceEndpoint>(
                new { workspaceId = created.Id }, WorkspaceResponse.From(created), cancellation: ct);
        });
    }
}

/// <summary>The workspaces the caller's party owns. Grants are a separate question (Spec 086).</summary>
internal sealed class ListWorkspacesEndpoint : WorkspaceEndpoint<EmptyRequest, WorkspaceListResponse>
{
    private readonly IWorkspaceService _workspaces;

    public ListWorkspacesEndpoint(IWorkspaceService workspaces) => _workspaces = workspaces;

    public override void Configure()
    {
        Get("/workspaces");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "List my workspaces";
            s.Description = "Returns the active workspaces owned by the caller's party.";
            s.Response(200, "Workspaces returned");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        if (await CallerPartyAsync(ct) is not { } callerPartyId)
        {
            return;
        }

        var owned = await _workspaces.ListForOwnerAsync(callerPartyId, ct);
        await Send.OkAsync(new WorkspaceListResponse([.. owned.Select(WorkspaceResponse.From)]), ct);
    }
}

/// <summary>Open a workspace: its summary, including the head revision a client materialises from.</summary>
internal sealed class GetWorkspaceEndpoint : WorkspaceEndpoint<EmptyRequest, WorkspaceResponse>
{
    private readonly IWorkspaceService _workspaces;
    private readonly IWorkspaceSyncService _sync;

    public GetWorkspaceEndpoint(IWorkspaceService workspaces, IWorkspaceSyncService sync)
    {
        _workspaces = workspaces;
        _sync = sync;
    }

    public override void Configure()
    {
        Get("/workspaces/{workspaceId:guid}");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Get a workspace";
            s.Description = "Returns the workspace summary, including its head revision. Requires Read access; a workspace "
                + "the caller cannot read answers 404 whether or not it exists.";
            s.Response(200, "Workspace returned");
            s.Response(404, "No such workspace is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var workspaceId = Route<Guid>("workspaceId");

        if (await CallerPartyAsync(ct) is not { } callerPartyId)
        {
            return;
        }

        var access = await _sync.ResolveAccessAsync(workspaceId, callerPartyId, ct);
        var summary = access.Allows(WorkspaceAccessLevel.Read) ? await _workspaces.GetAsync(workspaceId, ct) : null;

        if (summary is null || summary.Status != WorkspaceStatuses.Active)
        {
            await ProblemAsync(WorkspaceProblems.From(
                new WorkspaceAccessDeniedException(workspaceId, callerPartyId, WorkspaceAccessLevel.Read, WorkspaceAccessLevel.None))!);
            return;
        }

        await Send.OkAsync(WorkspaceResponse.From(summary), ct);
    }
}
