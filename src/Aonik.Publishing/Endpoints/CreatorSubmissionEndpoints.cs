using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Publishing.Endpoints;

internal abstract class PublishingEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse> where TRequest : notnull
{
    protected async Task GuardedAsync(Func<Task> action)
    {
        try { await action(); }
        catch (PublishingException problem)
        {
            await Send.ResultAsync(Results.Problem(statusCode: problem.Status,
                title: problem.Code, detail: problem.Message,
                extensions: new Dictionary<string, object?> { ["code"] = problem.Code }));
        }
    }
}

internal sealed class CreateSubmissionEndpoint(ICreatorSubmissionService submissions)
    : PublishingEndpoint<CreateSubmissionRequest, CreateSubmissionResult>
{
    public override void Configure()
    {
        Post("/publishing/submissions");
        Policies("UserPolicy");
        Tags("Publishing");
        Options(builder => builder.WithMetadata(new RequestSizeLimitAttribute(128 * 1024)));
        Summary(s =>
        {
            s.Summary = "Create or replay a private creator submission receipt";
            s.Description = "Records unverified declarations only. Ownership, channel and submit authority are enforced by the service. No approval or publication occurs.";
        });
    }

    public override Task HandleAsync(CreateSubmissionRequest req, CancellationToken ct)
        => GuardedAsync(async () =>
        {
            var result = await submissions.CreateAsync(req, ct);
            if (result.Inserted)
                await Send.CreatedAtAsync<GetSubmissionEndpoint>(new { id = result.Submission.Id }, result, cancellation: ct);
            else
                await Send.OkAsync(result, ct);
        });
}

public sealed record GetSubmissionRequest(Guid Id);

internal sealed class GetSubmissionEndpoint(ICreatorSubmissionService submissions)
    : PublishingEndpoint<GetSubmissionRequest, SubmissionReceipt>
{
    public override void Configure()
    {
        Get("/publishing/submissions/{Id:guid}");
        Policies("UserPolicy");
        Tags("Publishing");
        Summary(s => s.Summary = "Read the current creator's private submission receipt");
    }

    public override Task HandleAsync(GetSubmissionRequest req, CancellationToken ct)
        => GuardedAsync(async () =>
        {
            var receipt = await submissions.GetAsync(req.Id, ct);
            if (receipt is null) await Send.NotFoundAsync(ct);
            else await Send.OkAsync(receipt, ct);
        });
}

public sealed class ListSubmissionsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

internal sealed class ListSubmissionsEndpoint(ICreatorSubmissionService submissions)
    : PublishingEndpoint<ListSubmissionsRequest, SubmissionPage>
{
    public override void Configure()
    {
        Get("/publishing/submissions");
        Policies("UserPolicy");
        Tags("Publishing");
        Summary(s => s.Summary = "List only the current creator's private submission receipts");
    }

    public override Task HandleAsync(ListSubmissionsRequest req, CancellationToken ct)
        => GuardedAsync(async () => await Send.OkAsync(await submissions.ListAsync(req.Page, req.PageSize, ct), ct));
}
