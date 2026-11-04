using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

internal sealed class ReplaceSubmissionRevisionEndpoint(ISubmissionRevisionService revisions) : PublishingEndpoint<ReplaceSubmissionRevisionRequest, SubmissionReceipt>
{
    public override void Configure()
    {
        Post("/publishing/submissions/{Id:guid}/revisions"); Policies("UserPolicy"); Tags("Publishing");
        Options(builder => builder.WithMetadata(new RequestSizeLimitAttribute(128 * 1024)));
    }
    public override Task HandleAsync(ReplaceSubmissionRevisionRequest req, CancellationToken ct)
        => GuardedAsync(async () => await Send.OkAsync(await revisions.ReplaceAsync(Route<Guid>("Id"), req, ct), ct));
}
