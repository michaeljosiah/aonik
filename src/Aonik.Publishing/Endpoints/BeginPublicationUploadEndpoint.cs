using FastEndpoints;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Publishing.Endpoints;

internal sealed class BeginPublicationUploadEndpoint(IPublicationIntakeService intake) : PublishingEndpointWithoutRequest<UploadReceipt>
{
    public override void Configure() { Post("/publishing/submissions/{Id:guid}/uploads"); Policies("UserPolicy"); Tags("Publishing"); }
    public override Task HandleAsync(CancellationToken ct)
        => GuardedAsync(async () => await Send.OkAsync(await intake.BeginAsync(Route<Guid>("Id"), ct), ct));
}
