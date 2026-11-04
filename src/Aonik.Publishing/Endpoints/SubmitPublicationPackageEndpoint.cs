using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Publishing.Endpoints;

internal sealed class SubmitPublicationPackageEndpoint(IPublicationIntakeService intake) : PublishingEndpoint<SubmitPackageModel, ValidationPreview>
{
    public override void Configure()
    {
        Post("/publishing/submissions/{Id:guid}/submit"); Policies("UserPolicy"); Tags("Publishing");
        Options(builder => builder.WithMetadata(new RequestSizeLimitAttribute(32 * 1024)));
    }
    public override Task HandleAsync(SubmitPackageModel req, CancellationToken ct)
        => GuardedAsync(async () => await Send.OkAsync(await intake.SubmitAsync(Route<Guid>("Id"), new SubmitPackageRequest(req.RevisionId, req.OperationId), ct), ct));
}
