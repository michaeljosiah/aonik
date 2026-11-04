using FastEndpoints;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Publishing.Endpoints;

internal sealed class CancelPublicationUploadEndpoint(IPublicationIntakeService intake) : PublishingEndpointWithoutRequest<EmptyResponse>
{
    public override void Configure() { Delete("/publishing/submissions/{Id:guid}/uploads/{UploadId:guid}"); Policies("UserPolicy"); Tags("Publishing"); }
    public override Task HandleAsync(CancellationToken ct)
        => GuardedAsync(async () => { await intake.CancelAsync(Route<Guid>("Id"), Route<Guid>("UploadId"), ct); await Send.NoContentAsync(ct); });
}
