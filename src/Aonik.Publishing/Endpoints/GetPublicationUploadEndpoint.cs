using FastEndpoints;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Publishing.Endpoints;

internal sealed class GetPublicationUploadEndpoint(IPublicationIntakeService intake) : PublishingEndpoint<UploadPath, UploadReceipt>
{
    public override void Configure() { Get("/publishing/submissions/{Id:guid}/uploads/{UploadId:guid}"); Policies("UserPolicy"); Tags("Publishing"); }
    public override Task HandleAsync(UploadPath req, CancellationToken ct)
        => GuardedAsync(async () => await Send.OkAsync(await intake.GetAsync(req.Id, req.UploadId, ct), ct));
}
