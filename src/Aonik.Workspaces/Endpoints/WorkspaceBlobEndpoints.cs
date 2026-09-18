using Aonik.Workspaces.Services;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Workspaces.Endpoints;

/// <summary>
/// Single-shot upload of one blob, declared by hash and length (Spec 091 §5; Spec 089 §12).
///
/// <para>
/// The body is the raw bytes. <c>Content-Length</c> is the declared length and is mandatory: quota is claimed
/// against what the caller declares, so "we will find out how big it is" is not an option. The route's hash is the
/// declared hash; the staged bytes are compared against both before anything is promoted.
/// </para>
/// </summary>
internal sealed class UploadBlobEndpoint : WorkspaceEndpoint<EmptyRequest, BlobUploadResponse>
{
    private readonly IWorkspaceTransferService _transfer;

    public UploadBlobEndpoint(IWorkspaceTransferService transfer) => _transfer = transfer;

    public override void Configure()
    {
        Put("/workspaces/{workspaceId:guid}/blobs/{contentHash}");
        Policies(UserPolicy);
        Description(b => b.Accepts<EmptyRequest>("application/octet-stream"));
        Summary(s =>
        {
            s.Summary = "Upload a blob";
            s.Description = "Stores the request body as the blob whose SHA-256 is the route's contentHash. Content-Length is the "
                + "declared length and is required. Requires Write access; a reader consumes no quota. Bytes that do not hash to "
                + "the declaration are discarded, not stored. Blobs above the single-shot limit need the resumable upload.";
            s.Response(200, "Stored, or already present for this tenant; possession recorded for the billing subscriber");
            s.Response(403, "The caller holds Read access only");
            s.Response(404, "No such workspace is available to the caller");
            s.Response(411, "Content-Length is required");
            s.Response(413, "Too large for a single-shot upload");
            s.Response(422, "The bytes do not match the declared hash or length");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var workspaceId = Route<Guid>("workspaceId");
        var contentHash = Route<string>("contentHash")?.ToLowerInvariant() ?? string.Empty;

        if (!IsHash(contentHash))
        {
            ThrowError("contentHash must be a lowercase hex SHA-256.", StatusCodes.Status422UnprocessableEntity);
        }

        if (HttpContext.Request.ContentLength is not { } declaredLength || declaredLength < 0)
        {
            await ProblemAsync(new WorkspaceProblem(
                StatusCodes.Status411LengthRequired, "length-required",
                "Content-Length is required: quota is claimed against the declared length before the bytes are read."));
            return;
        }

        if (await CallerPartyAsync(ct) is not { } callerPartyId)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var stored = await _transfer.UploadAsync(
                workspaceId, callerPartyId, new BlobDeclaration(contentHash, declaredLength), HttpContext.Request.Body, ct);

            await Send.OkAsync(new BlobUploadResponse(stored.ContentHash, stored.SizeBytes, stored.AlreadyPresent), ct);
        });
    }
}

/// <summary>
/// Download one blob a revision of this workspace names (Spec 091 §5).
///
/// <para>
/// Read access to the workspace is necessary but not sufficient: the hash must appear in one of this workspace's
/// revisions. Identical bytes in another workspace are not reachable through this one, so a content hash never
/// becomes a bearer capability (Spec 089 §12), and the answer for "not named here" is the same 404 as for "does not
/// exist".
/// </para>
/// </summary>
internal sealed class DownloadBlobEndpoint : WorkspaceEndpoint<EmptyRequest, object>
{
    private readonly IWorkspaceTransferService _transfer;

    public DownloadBlobEndpoint(IWorkspaceTransferService transfer) => _transfer = transfer;

    public override void Configure()
    {
        Get("/workspaces/{workspaceId:guid}/blobs/{contentHash}");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Download a blob";
            s.Description = "Streams the bytes of a blob named by a revision of this workspace. Requires Read access.";
            s.Response(200, "The bytes, with the manifest's content type when it recorded one");
            s.Response(404, "No such workspace is available to the caller, or no revision of it names this hash");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var workspaceId = Route<Guid>("workspaceId");
        var contentHash = Route<string>("contentHash")?.ToLowerInvariant() ?? string.Empty;

        if (!IsHash(contentHash))
        {
            ThrowError("contentHash must be a lowercase hex SHA-256.", StatusCodes.Status422UnprocessableEntity);
        }

        if (await CallerPartyAsync(ct) is not { } callerPartyId)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var blob = await _transfer.OpenAsync(workspaceId, callerPartyId, contentHash, ct);

            if (blob is null)
            {
                await ProblemAsync(new WorkspaceProblem(
                    StatusCodes.Status404NotFound, "content-not-found", "No revision of this workspace names that content."));
                return;
            }

            await using var content = blob.Content;
            await Send.StreamAsync(
                content,
                fileName: contentHash,
                fileLengthBytes: blob.SizeBytes,
                contentType: blob.ContentType ?? "application/octet-stream",
                cancellation: ct);
        });
    }
}
