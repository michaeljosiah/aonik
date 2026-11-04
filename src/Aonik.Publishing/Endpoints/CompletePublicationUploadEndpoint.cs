using FastEndpoints;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Publishing.Endpoints;

internal sealed class CompletePublicationUploadEndpoint(IPublicationIntakeService intake) : PublishingEndpointWithoutRequest<UploadReceipt>
{
    public override void Configure() { Post("/publishing/submissions/{Id:guid}/uploads/{UploadId:guid}/complete"); Policies("UserPolicy"); Tags("Publishing"); }
    public override Task HandleAsync(CancellationToken ct)
        => GuardedAsync(async () => await Send.OkAsync(await intake.CompleteAsync(Route<Guid>("Id"), Route<Guid>("UploadId"), ct), ct));
}
