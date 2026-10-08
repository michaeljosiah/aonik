using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Fulfilment;

public sealed class UpdateDeliveryCoverageConfigurationEndpoint(IDeliveryCoverageService coverage)
    : Endpoint<DeliveryCoverageConfigDto, DeliveryCoverageConfigDto>
{
    public override void Configure()
    {
        Put("/commerce/admin/delivery-coverage");
        Policies("AdminWritePolicy");
        Summary(summary => summary.Summary = "Replace the tenant's explicitly configured delivery coverage.");
    }

    public override async Task HandleAsync(DeliveryCoverageConfigDto req, CancellationToken ct)
        => await Send.OkAsync(await coverage.UpdateConfigurationAsync(req, ct), ct);
}
