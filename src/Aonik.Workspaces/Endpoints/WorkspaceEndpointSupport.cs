using System.Text.RegularExpressions;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.Workspaces.Services;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Workspaces.Endpoints;

/// <summary>
/// What every workspace endpoint does the same way: resolve the acting party, and turn the module's refusals into
/// one problem shape.
///
/// <para>
/// The party comes from the platform's resolver, never from the request. A caller with no party has nothing to
/// own or be granted, so the answer is 403 before any workspace is looked at. Absent and inaccessible workspaces
/// get the same 404, because distinguishing them would let a family enumerate a tenant (Spec 089 §12).
/// </para>
/// </summary>
internal abstract class WorkspaceEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected const string Tag = "Workspaces";

    /// <summary>The policy every customer-facing workspace call requires: a signed-in user of this tenant.</summary>
    protected const string UserPolicy = "UserPolicy";

    protected static readonly Regex HashPattern = new("^[a-f0-9]{64}$", RegexOptions.Compiled);

    /// <summary>The acting party, or null after a 403 has been sent.</summary>
    protected async Task<Guid?> CallerPartyAsync(CancellationToken ct)
    {
        var partyId = await Resolve<ICurrentPartyResolver>().GetCurrentPartyIdAsync(ct);

        if (partyId is null)
        {
            await ProblemAsync(new WorkspaceProblem(
                StatusCodes.Status403Forbidden,
                "no-party",
                "The signed-in user is not linked to a party, so cannot own or access workspaces."));
        }

        return partyId;
    }

    protected Task ProblemAsync(WorkspaceProblem problem)
        => Send.ResultAsync(Results.Json(problem, statusCode: problem.Status));

    /// <summary>Run the body; a refusal the module makes on purpose becomes a problem response rather than a 500.</summary>
    protected async Task GuardedAsync(Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (Exception ex) when (WorkspaceProblems.From(ex) is { } problem)
        {
            await ProblemAsync(problem);
        }
    }

    protected static bool IsHash(string? value) => value is not null && HashPattern.IsMatch(value);
}

internal static class WorkspaceProblems
{
    public static WorkspaceProblem? From(Exception exception) => exception switch
    {
        // One answer for "does not exist" and "not yours": no existence oracle for another family's worlds.
        WorkspaceAccessDeniedException { Effective: WorkspaceAccessLevel.None } => new(
            StatusCodes.Status404NotFound, "workspace-not-found", "No such workspace is available to you."),
        WorkspaceAccessDeniedException denied => new(
            StatusCodes.Status403Forbidden, "insufficient-access",
            $"{denied.Required} access is required; the caller holds {denied.Effective}."),
        CommitIdReusedException reused => new(StatusCodes.Status409Conflict, "commit-id-reused", reused.Message),
        EntitlementExceededException exceeded => new(StatusCodes.Status402PaymentRequired, "allowance-exceeded", exceeded.Message),
        PermissionDeniedException denied => new(StatusCodes.Status403Forbidden, "forbidden", denied.Message),
        UploadHashMismatchException mismatch => new(StatusCodes.Status422UnprocessableEntity, "content-hash-mismatch", mismatch.Message),
        DeclaredLengthExceededException length => new(StatusCodes.Status422UnprocessableEntity, "declared-length-mismatch", length.Message),
        UploadTooLargeForSingleShotException tooLarge => new(StatusCodes.Status413PayloadTooLarge, "too-large", tooLarge.Message),
        ArgumentException invalid => new(StatusCodes.Status422UnprocessableEntity, "invalid-request", invalid.Message),
        InvalidOperationException contention when contention.Message.Contains("contention", StringComparison.OrdinalIgnoreCase) => new(
            StatusCodes.Status409Conflict, "contention", contention.Message),
        _ => null,
    };
}
