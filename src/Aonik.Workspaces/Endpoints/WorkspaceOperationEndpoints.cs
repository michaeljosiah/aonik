using Aonik.SharedKernel.Abstractions;
using Aonik.Workspaces.Services;

using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Aonik.Workspaces.Endpoints;

// The engine's operation store over HTTP (aonik#327): begin once by key, complete once, read back.
// The shapes are the engine's record verbatim, as JSON strings the platform keeps and never
// interprets: what the engine wrote is what the engine reads, from any host.

/// <param name="Key">The engine's operation key, lowercase hex SHA-256.</param>
/// <param name="Fingerprint">The engine's fingerprint of the request; a key reused with another is refused.</param>
/// <param name="Context">The engine's trusted context, as JSON.</param>
/// <param name="Resource">The engine's resource, as JSON.</param>
/// <param name="Result">A result carried from the start, as JSON, or null.</param>
public sealed record BeginOperationModel(
    Guid WorkspaceId,
    string Key,
    string Fingerprint,
    string Action,
    string Context,
    string Resource,
    string? Result = null);

public sealed record CompleteOperationModel(string Fingerprint, string Result);

/// <param name="Status"><c>started</c> or <c>completed</c>. A started row that stays started is an uncertain operation.</param>
public sealed record OperationResponse(
    Guid WorkspaceId,
    string Key,
    string Fingerprint,
    string Action,
    string Status,
    string Context,
    string Resource,
    string? Result,
    DateTime StartedAt,
    DateTime? CompletedAt)
{
    public static OperationResponse From(OperationRecord record) => new(
        record.WorkspaceId, record.Key, record.Fingerprint, record.Action, record.Status,
        record.ContextJson, record.ResourceJson, record.ResultJson, record.StartedAt, record.CompletedAt);
}

/// <param name="Inserted">True when this call began the operation; false when it was begun earlier and that row is returned.</param>
public sealed record BeginOperationResponse(bool Inserted, OperationResponse Operation);

internal sealed class BeginOperationEndpoint : WorkspaceEndpoint<BeginOperationModel, BeginOperationResponse>
{
    private readonly IWorkspaceOperationService _operations;

    public BeginOperationEndpoint(IWorkspaceOperationService operations) => _operations = operations;

    public override void Configure()
    {
        Post("/operations");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Begin an engine operation";
            s.Description = "Inserts the operation if absent and returns it; returns the existing row otherwise, whatever its state. "
                + "Two hosts beginning the same key at once get one row between them. The same key with a different "
                + "fingerprint is refused. Requires Write access to the workspace.";
            s.Response(200, "The operation, begun now or earlier");
            s.Response(404, "No such workspace is available to the caller");
            s.Response(409, "The key was begun earlier for a different request");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(BeginOperationModel req, CancellationToken ct)
    {
        if (await CallerPartyAsync(ct) is not { } caller)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var (inserted, operation) = await _operations.BeginAsync(
                new BeginOperationRequest(req.WorkspaceId, req.Key, req.Fingerprint, req.Action.Trim(), req.Context, req.Resource, req.Result), caller, ct);
            await Send.OkAsync(new BeginOperationResponse(inserted, OperationResponse.From(operation)), ct);
        });
    }
}

internal sealed class CompleteOperationEndpoint : WorkspaceEndpoint<CompleteOperationModel, OperationResponse>
{
    private readonly IWorkspaceOperationService _operations;

    public CompleteOperationEndpoint(IWorkspaceOperationService operations) => _operations = operations;

    public override void Configure()
    {
        Post("/operations/{key}/complete");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Complete an engine operation";
            s.Description = "Records the result once. Completing again with the same result is a replay and answers the row; "
                + "with a different result, or a different fingerprint, it is refused.";
            s.Response(200, "Completed, or already completed with this result");
            s.Response(404, "No such operation is available to the caller");
            s.Response(409, "The fingerprint or the result differs from the record");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(CompleteOperationModel req, CancellationToken ct)
    {
        if (await CallerPartyAsync(ct) is not { } caller)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var completed = await _operations.CompleteAsync(Route<string>("key")!, req.Fingerprint, req.Result, caller, ct);
            await Send.OkAsync(OperationResponse.From(completed), ct);
        });
    }
}

internal sealed class GetOperationEndpoint : WorkspaceEndpoint<EmptyRequest, OperationResponse>
{
    private readonly IWorkspaceOperationService _operations;

    public GetOperationEndpoint(IWorkspaceOperationService operations) => _operations = operations;

    public override void Configure()
    {
        Get("/operations/{key}");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Read an engine operation";
            s.Description = "The operation as recorded. A key the caller may not read answers 404 whether or not it exists.";
            s.Response(200, "Operation returned");
            s.Response(404, "No such operation is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        if (await CallerPartyAsync(ct) is not { } caller)
        {
            return;
        }

        var operation = await _operations.ReadAsync(Route<string>("key")!, caller, ct);

        if (operation is null)
        {
            await ProblemAsync(new WorkspaceProblem(StatusCodes.Status404NotFound, "operation-not-found", "No such operation is available to you."));
            return;
        }

        await Send.OkAsync(OperationResponse.From(operation), ct);
    }
}

internal sealed class BeginOperationModelValidator : Validator<BeginOperationModel>
{
    public BeginOperationModelValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Key).Matches("^[a-f0-9]{64}$").WithMessage("key is a lowercase hex SHA-256.");
        RuleFor(x => x.Fingerprint).Matches("^[a-f0-9]{64}$").WithMessage("fingerprint is a lowercase hex SHA-256.");
        RuleFor(x => x.Action).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Context).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.Resource).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.Result).MaximumLength(4_000_000);
    }
}

internal sealed class CompleteOperationModelValidator : Validator<CompleteOperationModel>
{
    public CompleteOperationModelValidator()
    {
        RuleFor(x => x.Fingerprint).Matches("^[a-f0-9]{64}$").WithMessage("fingerprint is a lowercase hex SHA-256.");
        RuleFor(x => x.Result).NotEmpty().MaximumLength(4_000_000);
    }
}
