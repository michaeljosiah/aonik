using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Fulfilment;

public sealed class GetDeliveryCoverageConfigurationEndpoint(IDeliveryCoverageService coverage)
    : EndpointWithoutRequest<DeliveryCoverageConfigDto>
{
    public override void Configure()
    {
        Get("/commerce/admin/delivery-coverage");
        Policies("AdminReadPolicy");
        Summary(summary => summary.Summary = "Read the tenant's delivery coverage configuration.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var configuration = await coverage.GetConfigurationAsync(ct);
        if (configuration is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(configuration, ct);
    }
}
