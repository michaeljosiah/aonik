using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.SharedKernel.Abstractions.Workspaces;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Workspaces.Endpoints;

/// <summary>Create a workspace the caller's party owns, or one a child of theirs owns (Spec 089 §11; Spec 095 §12).</summary>
internal sealed class CreateWorkspaceEndpoint : WorkspaceEndpoint<CreateWorkspaceRequest, WorkspaceResponse>
{
    private readonly IWorkspaceService _workspaces;
    private readonly IConsentGate _consent;

    public CreateWorkspaceEndpoint(IWorkspaceService workspaces, IConsentGate consent)
    {
        _workspaces = workspaces;
        _consent = consent;
    }

    public override void Configure()
    {
        Post("/workspaces");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Create a workspace";
            s.Description = "Creates a workspace owned by the caller's party, or by a child the caller holds guardian authority over "
                + "when ownerPartyId names one — provided the child's service-core consent stands. The slot is claimed against the "
                + "billing subscriber (the caller's party unless a subscriber the caller may act for is named) before the workspace exists.";
            s.Response(201, "Workspace created");
            s.Response(402, "The billing subscriber has no workspace allowance left");
            s.Response(403, "The caller has no party, the child's service-core consent does not stand, or the caller may not act for the billing subscriber");
            s.Response(404, "The named owner is not a child the caller holds guardian authority over");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(CreateWorkspaceRequest req, CancellationToken ct)
    {
        if (await CallerPartyAsync(ct) is not { } callerPartyId)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var ownerPartyId = req.OwnerPartyId ?? callerPartyId;

            if (ownerPartyId != callerPartyId)
            {
                // Acting for someone else needs guardian authority over them, and a child's world must not
                // exist before the consent that permits it (Spec 095 §12.2; ArkeKidz#8).
                await _consent.EnsureCanActForAsync(callerPartyId, ownerPartyId, ct);
                await _consent.EnsureAsync(ownerPartyId, ConsentPurposes.ServiceCore, ct);
            }

            // A child holds no plan of their own, so the payer defaults to the caller rather than the owner.
            var billing = req.BillingSubscriber is { } subscriber
                ? new SubscriberRef(subscriber.Kind, subscriber.Id)
                : new SubscriberRef(SubscriberKinds.Party, callerPartyId);

            var created = await _workspaces.CreateAsync(
                req.Kind ?? WorkspaceKinds.World, req.Name.Trim(), ownerPartyId, billing, ct);

            await Send.CreatedAtAsync<GetWorkspaceEndpoint>(
                new { workspaceId = created.Id }, WorkspaceResponse.From(created), cancellation: ct);
        });
    }
}

/// <summary>The workspaces the caller's party owns, and those owned by the children they guard. Grants are a separate question (Spec 086).</summary>
internal sealed class ListWorkspacesEndpoint : WorkspaceEndpoint<EmptyRequest, WorkspaceListResponse>
{
    private readonly IWorkspaceService _workspaces;
    private readonly IGuardianshipReader _guardianships;
    private readonly ITenantProvider _tenantProvider;

    public ListWorkspacesEndpoint(IWorkspaceService workspaces, IGuardianshipReader guardianships, ITenantProvider tenantProvider)
    {
        _workspaces = workspaces;
        _guardianships = guardianships;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Get("/workspaces");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "List my workspaces";
            s.Description = "Returns the active workspaces owned by the caller's party and by the children the caller holds guardian authority over.";
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

        var owners = new List<Guid> { callerPartyId };
        owners.AddRange(await _guardianships.GetWardsAsync(_tenantProvider.GetCurrentTenantId(), callerPartyId, ct));

        var workspaces = new List<WorkspaceResponse>();
        foreach (var owner in owners)
        {
            workspaces.AddRange((await _workspaces.ListForOwnerAsync(owner, ct)).Select(WorkspaceResponse.From));
        }

        await Send.OkAsync(new WorkspaceListResponse(workspaces), ct);
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
