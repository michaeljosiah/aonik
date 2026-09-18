using Aonik.SharedKernel.Abstractions.Workspaces;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Workspaces.Endpoints;

/// <summary>The complete manifest of a revision, or of the head when none is named (Spec 089 §6).</summary>
internal sealed class GetManifestEndpoint : WorkspaceEndpoint<EmptyRequest, ManifestResponse>
{
    private readonly IWorkspaceService _workspaces;
    private readonly IWorkspaceSyncService _sync;

    public GetManifestEndpoint(IWorkspaceService workspaces, IWorkspaceSyncService sync)
    {
        _workspaces = workspaces;
        _sync = sync;
    }

    public override void Configure()
    {
        Get("/workspaces/{workspaceId:guid}/manifest");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Read a manifest";
            s.Description = "Returns the complete manifest of the named revision (?revisionId=) or of the head. "
                + "Requires Read access. A workspace with no commits yet returns a null revision and no entries.";
            s.Response(200, "Manifest returned");
            s.Response(404, "No such workspace is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var workspaceId = Route<Guid>("workspaceId");
        var revisionId = Query<Guid?>("revisionId", isRequired: false);

        if (await CallerPartyAsync(ct) is not { } callerPartyId)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var entries = await _sync.GetManifestAsync(workspaceId, callerPartyId, revisionId, ct);
            var described = revisionId ?? (await _workspaces.GetAsync(workspaceId, ct))?.HeadRevisionId;

            await Send.OkAsync(new ManifestResponse(described, [.. entries.Select(ManifestEntryModel.From)]), ct);
        });
    }
}

/// <summary>Revisions, newest first, including divergent ones awaiting a decision (Spec 089 §7).</summary>
internal sealed class ListRevisionsEndpoint : WorkspaceEndpoint<EmptyRequest, RevisionListResponse>
{
    private readonly IWorkspaceSyncService _sync;

    public ListRevisionsEndpoint(IWorkspaceSyncService sync) => _sync = sync;

    public override void Configure()
    {
        Get("/workspaces/{workspaceId:guid}/revisions");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "List revisions";
            s.Description = "Returns up to ?take= (default 50, at most 500) revisions, newest first. Requires Read access.";
            s.Response(200, "Revisions returned");
            s.Response(404, "No such workspace is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var workspaceId = Route<Guid>("workspaceId");
        var take = Query<int?>("take", isRequired: false) ?? 50;

        if (await CallerPartyAsync(ct) is not { } callerPartyId)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var revisions = await _sync.GetHistoryAsync(workspaceId, callerPartyId, take, ct);
            await Send.OkAsync(new RevisionListResponse([.. revisions.Select(RevisionResponse.From)]), ct);
        });
    }
}

/// <summary>Which hashes the caller must upload before naming them (Spec 091 §6).</summary>
internal sealed class NegotiateEndpoint : WorkspaceEndpoint<NegotiateRequest, NegotiateResponse>
{
    private readonly IWorkspaceSyncService _sync;

    public NegotiateEndpoint(IWorkspaceSyncService sync) => _sync = sync;

    public override void Configure()
    {
        Post("/workspaces/{workspaceId:guid}/negotiate");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Negotiate content";
            s.Description = "Returns the subset of the given content hashes the caller does not possess and cannot reach, "
                + "so an unchanged tree syncs in one round trip and no bytes. Requires Read access.";
            s.Response(200, "Missing hashes returned");
            s.Response(404, "No such workspace is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(NegotiateRequest req, CancellationToken ct)
    {
        var workspaceId = Route<Guid>("workspaceId");

        if (await CallerPartyAsync(ct) is not { } callerPartyId)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var result = await _sync.NegotiateAsync(workspaceId, callerPartyId, req.ContentHashes, ct);
            await Send.OkAsync(new NegotiateResponse(result.Missing), ct);
        });
    }
}

/// <summary>Append a revision from a complete manifest (Spec 089 §6, §6.1, §6.2).</summary>
internal sealed class CommitEndpoint : WorkspaceEndpoint<CommitRequest, CommitResponse>
{
    private readonly IWorkspaceSyncService _sync;

    public CommitEndpoint(IWorkspaceSyncService sync) => _sync = sync;

    public override void Configure()
    {
        Post("/workspaces/{workspaceId:guid}/commits");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Commit a revision";
            s.Description = "Appends a revision from a complete manifest. Requires Write access. The client-chosen commitId is "
                + "the idempotency key: the same id with the same tree replays the original outcome; the same id with a "
                + "different tree is refused (409 commit-id-reused). A parent that is no longer the head is stored as diverged "
                + "and does not advance the head. Content the caller has not uploaded refuses the whole commit (409 missing-content).";
            s.Response(200, "Committed, diverged or replayed; see outcome");
            s.Response(402, "The billing subscriber's byte allowance is exhausted");
            s.Response(403, "The caller holds Read access only");
            s.Response(404, "No such workspace is available to the caller");
            s.Response(409, "commit-id-reused, missing-content or contention");
            s.Response(422, "Malformed manifest");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(CommitRequest req, CancellationToken ct)
    {
        var workspaceId = Route<Guid>("workspaceId");

        if (await CallerPartyAsync(ct) is not { } callerPartyId)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var request = new CommitRevisionRequest(
                workspaceId,
                req.CommitId,
                req.ParentRevisionId,
                [.. req.Manifest.Select(entry => entry.ToEntry())],
                req.Message);

            var result = await _sync.CommitAsync(request, callerPartyId, ct);

            if (result.MissingHashes.Count > 0)
            {
                // Refused as MISSING rather than forbidden, whether the bytes were never uploaded or belong to
                // someone else: the distinction would confirm the blob exists (Spec 089 §12).
                await ProblemAsync(new WorkspaceProblem(
                    StatusCodes.Status409Conflict,
                    "missing-content",
                    $"The manifest names {result.MissingHashes.Count} hash(es) the caller does not possess. Upload first, commit second.",
                    result.MissingHashes));
                return;
            }

            await Send.OkAsync(
                new CommitResponse(CommitResponse.OutcomeName(result.Outcome), result.RevisionId, result.Sequence, result.HeadRevisionId),
                ct);
        });
    }
}

/// <summary>End a divergent revision the way a person decided (Spec 089 §7.1).</summary>
internal sealed class ResolveRevisionEndpoint : WorkspaceEndpoint<ResolveRevisionRequest, ResolveRevisionResponse>
{
    private readonly IWorkspaceSyncService _sync;

    public ResolveRevisionEndpoint(IWorkspaceSyncService sync) => _sync = sync;

    public override void Configure()
    {
        Post("/workspaces/{workspaceId:guid}/revisions/{revisionId:guid}/resolve");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Resolve a divergent revision";
            s.Description = "accept advances the head through a new revision parented on the current head; reject releases the "
                + "revision's bytes after the retention window; supersede records that a third tree replaced it. Requires Write "
                + "access. Resolving a revision that is not divergent, or already resolved, answers resolved: false.";
            s.Response(200, "Resolution recorded");
            s.Response(403, "The caller holds Read access only");
            s.Response(404, "No such workspace is available to the caller");
            s.Response(422, "Unknown resolution");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(ResolveRevisionRequest req, CancellationToken ct)
    {
        var workspaceId = Route<Guid>("workspaceId");
        var revisionId = Route<Guid>("revisionId");

        var resolution = req.Resolution.Trim().ToLowerInvariant() switch
        {
            "accept" => DivergenceResolution.Accept,
            "reject" => DivergenceResolution.Reject,
            _ => DivergenceResolution.Supersede,
        };

        if (await CallerPartyAsync(ct) is not { } callerPartyId)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            // The route's workspace is checked first so a caller learns nothing about a revision of a workspace
            // they cannot read; the service checks Write on the revision's own workspace again.
            var access = await _sync.ResolveAccessAsync(workspaceId, callerPartyId, ct);

            if (!access.Allows(WorkspaceAccessLevel.Write))
            {
                throw new WorkspaceAccessDeniedException(workspaceId, callerPartyId, WorkspaceAccessLevel.Write, access);
            }

            var resolved = await _sync.ResolveAsync(revisionId, callerPartyId, resolution, ct);
            await Send.OkAsync(new ResolveRevisionResponse(resolved), ct);
        });
    }
}
